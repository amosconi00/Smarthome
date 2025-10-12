using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartHome.Backend.Dal;
using SmartHome.Backend.Dal.Entity;
using SmartHome.Backend.Hubs;
using SmartHome.Backend.NILM.Models;
using SmartHome.Backend.NILM.Services;
using SmartHome.Backend.Services;

// added

namespace SmartHome.Backend.BackgroundServices
{
    /// <summary>
    /// Hosted service notturno: processa telemetrie in batch (Hart + Seq2Point) salvando le detection.
    /// Supporta trigger manuale via controller e schedulazione automatica ogni giorno alle 03:00 (ora locale) per il giorno precedente.
    /// </summary>
    public sealed class NilmJobBackgroundService : BackgroundService
    {
        private readonly ILogger<NilmJobBackgroundService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly INilmHartService _hartService; // solo Hart
        private readonly IHubContext<TelemetryHub> _hub;
        private readonly object _sync = new();
        private bool _isRunning;
        private DateTime? _currentStart;
        private DateTime? _currentEnd;
        private DateOnly? _lastAutoDayProcessed; // evita doppia elaborazione automatica stesso giorno
        private readonly INilmModeService _modeService;

        public NilmJobBackgroundService(ILogger<NilmJobBackgroundService> logger,
            IServiceScopeFactory scopeFactory,
            INilmHartService hartService,
            IHubContext<TelemetryHub> hub,
            INilmModeService modeService)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _hartService = hartService;
            _hub = hub;
            _modeService = modeService;
        }

        /// <summary>
        /// Stato corrente (thread-safe snapshot) per controller.
        /// </summary>
        public (bool Running, DateTime? StartUtc, DateTime? EndUtc) GetStatus()
        {
            lock (_sync) return (_isRunning, _currentStart, _currentEnd);
        }

        /// <summary>
        /// Prova ad avviare l'elaborazione su intervallo [startUtc,endUtc). Restituisce false se già in esecuzione.
        /// Bloccato se modalità corrente è Realtime.
        /// </summary>
        public bool TryRun(DateTime startUtc, DateTime endUtc, CancellationToken externalCt = default)
        {
            if (_modeService.CurrentMode == NilmMode.Realtime)
            {
                _logger.LogWarning("NilmJobBackgroundService TryRun rifiutato: modalità corrente {Mode} non consente batch", _modeService.CurrentMode);
                return false;
            }
            if (endUtc <= startUtc) throw new ArgumentException("endUtc deve essere > startUtc");
            lock (_sync)
            {
                if (_isRunning) return false;
                _isRunning = true;
                _currentStart = startUtc;
                _currentEnd = endUtc;
            }
            _ = Task.Run(() => RunIntervalAsync(startUtc, endUtc, externalCt));
            return true;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NilmJobBackgroundService avviato: scheduler automatico attivo (03:00 ora locale)");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Se modalità realtime, dorme e controlla periodicamente
                    if (_modeService.CurrentMode == NilmMode.Realtime)
                    {
                        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                        continue;
                    }

                    // Calcola prossimo target alle 03:00 (ora locale)
                    var nowLocal = DateTime.Now;
                    var today3 = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, 3, 0, 0, DateTimeKind.Local);
                    DateTime target = nowLocal < today3 ? today3 : today3.AddDays(1);
                    var delay = target - nowLocal;
                    _logger.LogDebug("NilmJobBackgroundService scheduler: prossimo run automatico alle {TargetLocal:o} (tra {Delay} hh:mm:ss)", target, delay);
                    await Task.Delay(delay, stoppingToken);

                    // Una volta raggiunto 03:00 aspetta eventualmente job manuale in corso
                    while (true)
                    {
                        lock (_sync)
                        {
                            if (!_isRunning) break;
                        }
                        _logger.LogInformation("NilmJobBackgroundService scheduler: attendo conclusione job manuale prima di auto-run");
                        await Task.Delay(TimeSpan.FromSeconds(320), stoppingToken);
                    }

                    if (_modeService.CurrentMode == NilmMode.Realtime)
                        continue; // cambiata durante attesa

                    var startUtc = DateTime.UtcNow.Date.AddDays(-1); // giorno precedente UTC
                    var endUtc = startUtc.AddDays(1);
                    var day = DateOnly.FromDateTime(startUtc);
                    bool skip;
                    lock (_sync)
                    {
                        skip = _lastAutoDayProcessed == day; // già fatto
                        if (!skip)
                        {
                            _isRunning = true;
                            _currentStart = startUtc;
                            _currentEnd = endUtc;
                        }
                    }
                    if (skip)
                    {
                        _logger.LogInformation("NilmJobBackgroundService scheduler: giorno {Day} già processato automaticamente, skip", day);
                        continue;
                    }

                    _logger.LogInformation("NilmJobBackgroundService scheduler: avvio elaborazione automatica giorno {Day}", day);
                    await RunIntervalAsync(startUtc, endUtc, stoppingToken);
                    lock (_sync)
                    {
                        _lastAutoDayProcessed = day;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "NilmJobBackgroundService scheduler: errore loop principale");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // backoff semplice
                }
            }
        }

        private async Task RunIntervalAsync(DateTime startUtc, DateTime endUtc, CancellationToken externalCt)
        {
            var swTotal = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await _hub.Clients.All.SendAsync("NilmJobStatus", new
                {
                    status = "started",
                    startUtc,
                    endUtc
                });
                _logger.LogInformation("NilmJobBackgroundService start elaborazione {Start:o} - {End:o}", startUtc, endUtc);
                var result = await ProcessCoreAsync(startUtc, endUtc, externalCt);
                _logger.LogInformation("NilmJobBackgroundService completato: {Tele} telemetrie, {Det} detections, durata {Sec}s", result.totalTelemetries, result.savedDetections, result.elapsedSeconds);
                await _hub.Clients.All.SendAsync("NilmJobStatus", new
                {
                    status = "completed",
                    startUtc,
                    endUtc,
                    telemetries = result.totalTelemetries,
                    detections = result.savedDetections,
                    seconds = result.elapsedSeconds
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NilmJobBackgroundService errore interval {Start:o}-{End:o}", startUtc, endUtc);
                await _hub.Clients.All.SendAsync("NilmJobStatus", new
                {
                    status = "error",
                    startUtc,
                    endUtc,
                    message = ex.Message
                });
            }
            finally
            {
                lock (_sync)
                {
                    _isRunning = false;
                    _currentStart = null;
                    _currentEnd = null;
                }
                swTotal.Stop();
            }
        }

        private async Task<(int totalTelemetries, int savedDetections, double elapsedSeconds)> ProcessCoreAsync(DateTime start, DateTime end, CancellationToken ct)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await using var scope = _scopeFactory.CreateAsyncScope();
            var ctxFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SmartHomeDbContext>>();
            await using var ctx = await ctxFactory.CreateDbContextAsync(ct);

            // DELETE preesistenti nell'intervallo
            int deleted = 0;
            try
            {
                deleted = await ctx.Database.ExecuteSqlRawAsync(
                    "DELETE FROM public.nilm_detections WHERE \"Timestamp\" >= {0} AND \"Timestamp\" < {1}",
                    start, end);
                _logger.LogInformation("NilmJobBackgroundService: cancellate {Count} detections preesistenti ({Start:o} - {End:o})", deleted, start, end);
            }
            catch (Exception exDel)
            {
                _logger.LogWarning(exDel, "NilmJobBackgroundService: errore cancellando detections preesistenti");
            }

            var s2pModels = LoadSeq2PointModels();
            _logger.LogInformation("NilmJobBackgroundService modelli Seq2Point caricati: {Count}", s2pModels.Count);

            if (_hartService is NilmHartService concreteHart)
                concreteHart.ResetForBatch();

            var batchSize = 2000; // flush dimensione batch
            var totalTelemetries = 0;
            var savedDetections = 0;
            var nilmBuffer = new List<NilmDetection>(capacity: 5000);

            var query = ctx.PowerMeterTelemetries.AsNoTracking()
                .Where(t => t.Timestamp >= start && t.Timestamp < end && t.PowerW != null)
                .OrderBy(t => t.Timestamp);

            await foreach (var tele in query.AsAsyncEnumerable().WithCancellation(ct))
            {
                totalTelemetries++;
                // Hart
                try
                {
                    var detsHart = await _hartService.ProcessPowerMeterTelemetryAsync(tele, ct);
                    if (detsHart is { Count: > 0 }) nilmBuffer.AddRange(detsHart);
                }
                catch (Exception exHart)
                {
                    _logger.LogWarning(exHart, "Errore HART tele {Ts:o}", tele.Timestamp);
                }

                // Seq2Point
                foreach (var (name, svc, threshold) in s2pModels)
                {
                    try
                    {
                        var r = svc.AddSample(tele.Timestamp, tele.PowerW!.Value);
                        if (r != null && r.PredictedOn)
                        {
                            nilmBuffer.Add(new NilmDetection
                            {
                                Timestamp = r.CenterTimestampUtc,
                                Source = "S2P",
                                ModelName = name,
                                EstimatedPowerW = r.PredictedWatts,
                                IsOn = r.PredictedOn
                            });
                        }
                    }
                    catch (Exception exS2p)
                    {
                        _logger.LogWarning(exS2p, "Errore Seq2Point modello {Model} tele {Ts:o}", name, tele.Timestamp);
                    }
                }

                if (batchSize > 0 && nilmBuffer.Count >= batchSize)
                {
                    var toInsert = nilmBuffer.Count;
                    await BulkInsertDetectionsAsync(ctxFactory, nilmBuffer, ct);
                    savedDetections += toInsert;
                    nilmBuffer.Clear();
                }
            }

            // Flush finale
            if (nilmBuffer.Count > 0)
            {
                var toInsert = nilmBuffer.Count;
                await BulkInsertDetectionsAsync(ctxFactory, nilmBuffer, ct);
                savedDetections += toInsert;
                nilmBuffer.Clear();
            }

            foreach (var (_, svc, _) in s2pModels)
                await svc.DisposeAsync();

            sw.Stop();
            return (totalTelemetries, savedDetections, sw.Elapsed.TotalSeconds);
        }

        private async Task BulkInsertDetectionsAsync(IDbContextFactory<SmartHomeDbContext> factory, List<NilmDetection> list, CancellationToken ct)
        {
            if (list.Count == 0) return;
            try
            {
                await using var ctx = await factory.CreateDbContextAsync(ct);
                ctx.ChangeTracker.AutoDetectChangesEnabled = false;
                ctx.NilmDetections.AddRange(list);
                await ctx.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore salvataggio detections batch ({Count})", list.Count);
            }
        }

        private List<(string name, Seq2PointInferenceService svc, double threshold)> LoadSeq2PointModels()
        {
            var result = new List<(string, Seq2PointInferenceService, double)>();
            try
            {
                var modelsRoot = Path.Combine(AppContext.BaseDirectory, "NILM", "TrainedModels");
                if (!Directory.Exists(modelsRoot)) return result;
                var onnxFiles = Directory.GetFiles(modelsRoot, "seq2point_*.onnx");
                foreach (var onnx in onnxFiles)
                {
                    var suffix = Path.GetFileNameWithoutExtension(onnx).Substring("seq2point_".Length);
                    var statsFile = Path.Combine(modelsRoot, $"normalization_stats_{suffix}.json");
                    if (!File.Exists(statsFile)) continue;
                    try
                    {
                        var stats = NormalizationStats.Load(statsFile);
                        var trainingInfoFile = Path.Combine(modelsRoot, $"training_info_{suffix}.json");
                        int window = 99; double threshold = 20;
                        if (File.Exists(trainingInfoFile))
                        {
                            using var fs = File.OpenRead(trainingInfoFile);
                            using var doc = JsonDocument.Parse(fs);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("window", out var w) && w.TryGetInt32(out var wv) && wv > 0) window = wv;
                            if (root.TryGetProperty("threshold_w", out var th) || root.TryGetProperty("threshold", out th) || root.TryGetProperty("thresholdW", out th))
                            {
                                if (th.ValueKind == JsonValueKind.Number)
                                {
                                    var tv = th.GetDouble();
                                    if (tv > 0) threshold = tv;
                                }
                            }
                        }
                        var svc = new Seq2PointInferenceService(onnx, stats, window: window, thresholdW: threshold, tryGpu: false);
                        result.Add((suffix, svc, threshold));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Errore caricando modello Seq2Point {File}", onnx);
                    }
                }
            }
            catch (Exception exRoot)
            {
                _logger.LogError(exRoot, "Errore enumerando modelli Seq2Point");
            }
            return result;
        }
    }
}
