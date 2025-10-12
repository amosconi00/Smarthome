using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using SmartHome.Backend.Dal;
using SmartHome.Backend.Dal.Entity;
using SmartHome.Backend.NILM;
using SmartHome.Backend.NILM.Models;
using SmartHome.Backend.NILM.Services;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;

namespace SmartHome.Backend.Services
{
    public class PipelineTelemetryService : IPipelineTelemetryService, IAsyncDisposable
    {
        private readonly ILogger<PipelineTelemetryService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly INilmHartService _hartService; // added
        private readonly INilmModeService _modeService;
        private readonly List<(string name, Seq2PointInferenceService svc)> _seq2PointServices = new();
        private const double THRESHOLD_W = 20.0;
        private const int DEFAULT_WINDOW = 99;
        private readonly int _batchSizeTelemetry = 200;
        private readonly int _batchSizeNilm = 200;
        private readonly TimeSpan _flushInterval = TimeSpan.FromMilliseconds(2500);
        private readonly int _channelCapacity = 5000;
        private readonly Channel<ApplianceTelemetry> _chAppliance;
        private readonly Channel<PowerMeterTelemetry> _chPowerMeter;
        private readonly Channel<NilmDetection> _chNilm;
        private readonly CancellationTokenSource _cts = new();
        private Task? _applianceConsumerTask;
        private Task? _powerMeterConsumerTask;
        private Task? _nilmConsumerTask;

        public PipelineTelemetryService(ILogger<PipelineTelemetryService> logger,
            IServiceScopeFactory scopeFactory,
            INilmHartService hartService,
            INilmModeService modeService)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _hartService = hartService;
            _modeService = modeService;
            var chOpts = new BoundedChannelOptions(_channelCapacity) { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.DropOldest };
            _chAppliance = Channel.CreateBounded<ApplianceTelemetry>(chOpts);
            _chPowerMeter = Channel.CreateBounded<PowerMeterTelemetry>(chOpts);
            _chNilm = Channel.CreateBounded<NilmDetection>(chOpts);
            _applianceConsumerTask = Task.Run(() => ConsumeAppliancesAsync(_cts.Token));
            _powerMeterConsumerTask = Task.Run(() => ConsumePowerMetersAsync(_cts.Token));
            _nilmConsumerTask = Task.Run(() => ConsumeNilmDetectionsAsync(_cts.Token));
            LoadSeq2PointModels();
            _logger.LogInformation("PipelineTelemetryService started with real-time NILM inference (HART + Seq2Point). Models loaded: {Count}", _seq2PointServices.Count);
        }

        private void LoadSeq2PointModels()
        {
            try
            {
                var modelsRoot = Path.Combine(AppContext.BaseDirectory, "NILM", "TrainedModels");
                if (!Directory.Exists(modelsRoot))
                {
                    _logger.LogWarning("Seq2Point models directory not found: {Dir}", modelsRoot);
                    return;
                }

                var onnxFiles = Directory.GetFiles(modelsRoot, "seq2point_*.onnx", SearchOption.TopDirectoryOnly);
                foreach (var onnx in onnxFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(onnx); // seq2point_<name>
                    var suffix = fileName.Substring("seq2point_".Length);
                    var statsFile = Path.Combine(modelsRoot, $"normalization_stats_{suffix}.json");
                    var trainingInfoFile = Path.Combine(modelsRoot, $"training_info_{suffix}.json");

                    if (!File.Exists(statsFile))
                    {
                        _logger.LogWarning("Normalization stats not found for model {Model} (expected {Stats})", onnx, statsFile);
                        continue;
                    }

                    int window = DEFAULT_WINDOW;
                    double thresholdModel = THRESHOLD_W; // fallback threshold
                    if (File.Exists(trainingInfoFile))
                    {
                        try
                        {
                            using var fs = File.OpenRead(trainingInfoFile);
                            using var doc = JsonDocument.Parse(fs);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("window", out var winProp) && winProp.TryGetInt32(out var parsedWin) && parsedWin > 0)
                            {
                                window = parsedWin;
                            }
                            else
                            {
                                _logger.LogWarning("Field 'window' invalid in {File}, using fallback {Window}", trainingInfoFile, window);
                            }

                            JsonElement thrProp;
                            if (root.TryGetProperty("threshold_w", out thrProp) || root.TryGetProperty("threshold", out thrProp) || root.TryGetProperty("thresholdW", out thrProp))
                            {
                                try
                                {
                                    double val = thrProp.ValueKind == JsonValueKind.Number ? thrProp.GetDouble() : double.NaN;
                                    if (!double.IsNaN(val) && val > 0)
                                    {
                                        thresholdModel = val;
                                    }
                                    else
                                    {
                                        _logger.LogWarning("Threshold value invalid in {File}, using fallback {Threshold}", trainingInfoFile, thresholdModel);
                                    }
                                }
                                catch (Exception exThr)
                                {
                                    _logger.LogWarning(exThr, "Error reading threshold in {File}, using fallback {Threshold}", trainingInfoFile, thresholdModel);
                                }
                            }
                            else
                            {
                                _logger.LogDebug("Threshold field not present in {File}, using fallback {Threshold}", trainingInfoFile, thresholdModel);
                            }
                        }
                        catch (Exception exTi)
                        {
                            _logger.LogWarning(exTi, "Cannot read training info {File}, using fallback window={Window} threshold={Threshold}", trainingInfoFile, window, thresholdModel);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Training info file missing for model {Name} (expected {File}) - using fallback window={Window} threshold={Threshold}", suffix, trainingInfoFile, window, thresholdModel);
                    }

                    try
                    {
                        var stats = NormalizationStats.Load(statsFile);
                        var svc = new Seq2PointInferenceService(onnx, stats, window: window, thresholdW: thresholdModel, tryGpu: false);
                        _seq2PointServices.Add((suffix, svc));
                        _logger.LogInformation("Loaded Seq2Point model {Name} (window={Window}, threshold={Threshold})", suffix, window, thresholdModel);
                    }
                    catch (Exception exModel)
                    {
                        _logger.LogError(exModel, "Error loading model {Model}", onnx);
                    }
                }

                if (_seq2PointServices.Count == 0)
                {
                    _logger.LogWarning("No Seq2Point model loaded");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while loading Seq2Point models");
            }
        }

        private async Task InsertBulkAsync(List<ApplianceTelemetry>? appliances, List<PowerMeterTelemetry>? powerMeters, List<NilmDetection>? nilms, CancellationToken ct)
        {
            if ((appliances == null || appliances.Count == 0) && (powerMeters == null || powerMeters.Count == 0) && (nilms == null || nilms.Count == 0))
                return;
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var ctxFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SmartHomeDbContext>>();
                await using var ctx = await ctxFactory.CreateDbContextAsync(ct);
                ctx.ChangeTracker.AutoDetectChangesEnabled = false;

                if (appliances != null && appliances.Count > 0)
                    ctx.ApplianceTelemetries.AddRange(appliances);
                if (powerMeters != null && powerMeters.Count > 0)
                    ctx.PowerMeterTelemetries.AddRange(powerMeters);
                if (nilms != null && nilms.Count > 0)
                    ctx.NilmDetections.AddRange(nilms);

                await ctx.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk insert (telemetries/detections)");
            }
        }

        private async Task ConsumeAppliancesAsync(CancellationToken ct)
        {
            var buffer = new List<ApplianceTelemetry>(_batchSizeTelemetry);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await foreach (var item in _chAppliance.Reader.ReadAllAsync(ct))
                {
                    buffer.Add(item);
                    if (buffer.Count >= _batchSizeTelemetry || sw.Elapsed >= _flushInterval)
                    {
                        await InsertBulkAsync(buffer, null, null, ct);
                        buffer.Clear();
                        sw.Restart();
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Appliance consumer error");
            }
            finally
            {
                if (buffer.Count > 0)
                    await InsertBulkAsync(buffer, null, null, CancellationToken.None);
            }
        }

        private async Task ConsumePowerMetersAsync(CancellationToken ct)
        {
            var buffer = new List<PowerMeterTelemetry>(_batchSizeTelemetry);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await foreach (var sample in _chPowerMeter.Reader.ReadAllAsync(ct))
                {
                    buffer.Add(sample);
                    if (buffer.Count >= _batchSizeTelemetry || sw.Elapsed >= _flushInterval)
                    {
                        await InsertBulkAsync(null, buffer, null, ct);
                        buffer.Clear();
                        sw.Restart();
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Power meter consumer error");
            }
            finally
            {
                if (buffer.Count > 0)
                    await InsertBulkAsync(null, buffer, null, CancellationToken.None);
            }
        }

        private async Task ConsumeNilmDetectionsAsync(CancellationToken ct)
        {
            var buffer = new List<NilmDetection>(_batchSizeNilm);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await foreach (var det in _chNilm.Reader.ReadAllAsync(ct))
                {
                    buffer.Add(det);
                    if (buffer.Count >= _batchSizeNilm || sw.Elapsed >= _flushInterval)
                    {
                        await InsertBulkAsync(null, null, buffer, ct);
                        buffer.Clear();
                        sw.Restart();
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NILM detections consumer error");
            }
            finally
            {
                if (buffer.Count > 0)
                    await InsertBulkAsync(null, null, buffer, CancellationToken.None);
            }
        }

        public Task ProcessApplianceTelemetry(ApplianceTelemetry telemetry, CancellationToken ct)
        {
            if (telemetry.PowerWatts == 0) return Task.CompletedTask;

            var tooOldTelemetry = CheckTelemetryTooold(telemetry.Timestamp);
            if (tooOldTelemetry)
            {
                _logger.LogWarning("Appliance telemetry with old timestamp dropped: {Ts:o}", telemetry.Timestamp);
                return Task.CompletedTask;
            }

            if (!_chAppliance.Writer.TryWrite(telemetry))
                _logger.LogWarning("Appliance channel full, telemetry dropped");
            return Task.CompletedTask;
        }

        private static bool CheckTelemetryTooold(DateTime timestamp)
        {
            var tooOldTelemetry = timestamp < DateTime.UtcNow.AddMinutes(-10);
            return tooOldTelemetry;
        }

        public async Task ProcessPowerMeterTelemetry(PowerMeterTelemetry telemetry, CancellationToken ct)
        {
            if (telemetry.PowerW == 0) return;

            var tooOldTelemetry = CheckTelemetryTooold(telemetry.Timestamp);
            if (tooOldTelemetry)
            {
                _logger.LogWarning("Power meter telemetry with old timestamp dropped: {Ts:o}", telemetry.Timestamp);
                return;
            }

            // Real-time NILM inference solo se modalità attuale è Realtime
            if (_modeService.CurrentMode == NilmMode.Realtime)
            {
                // Real-time NILM inference (Hart)
                try
                {
                    var detsHart = await _hartService.ProcessPowerMeterTelemetryAsync(telemetry, ct);
                    if (detsHart is { Count: > 0 })
                    {
                        foreach (var det in detsHart)
                        {
                            if (!_chNilm.Writer.TryWrite(det))
                                _logger.LogWarning("NILM channel full, Hart detection dropped");
                        }
                    }
                }
                catch (Exception exHart)
                {
                    _logger.LogWarning(exHart, "Hart inference error sample {Ts:o}", telemetry.Timestamp);
                }

                // Real-time NILM inference (Seq2Point)
                if (_seq2PointServices.Count > 0 && telemetry.PowerW.HasValue)
                {
                    foreach (var (name, svc) in _seq2PointServices)
                    {
                        try
                        {
                            var r = svc.AddSample(telemetry.Timestamp, telemetry.PowerW.Value);
                            if (r is { PredictedOn: true })
                            {
                                var det = new NilmDetection
                                {
                                    Timestamp = r.CenterTimestampUtc,
                                    Source = "S2P",
                                    ModelName = name,
                                    EstimatedPowerW = r.PredictedWatts,
                                    IsOn = r.PredictedOn
                                };
                                if (!_chNilm.Writer.TryWrite(det))
                                    _logger.LogWarning("NILM channel full, S2P detection dropped");
                            }
                        }
                        catch (Exception exS2p)
                        {
                            _logger.LogWarning(exS2p, "Seq2Point inference error model {Model} sample {Ts:o}", name, telemetry.Timestamp);
                        }
                    }
                }
            }

            if (!_chPowerMeter.Writer.TryWrite(telemetry))
                _logger.LogWarning("Power meter channel full, telemetry dropped");
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _cts.Cancel();
                _chAppliance.Writer.TryComplete();
                _chPowerMeter.Writer.TryComplete();
                _chNilm.Writer.TryComplete();

                var tasks = new List<Task>();
                if (_applianceConsumerTask != null) tasks.Add(_applianceConsumerTask);
                if (_powerMeterConsumerTask != null) tasks.Add(_powerMeterConsumerTask);
                if (_nilmConsumerTask != null) tasks.Add(_nilmConsumerTask);
                if (tasks.Count > 0)
                {
                    try { await Task.WhenAll(tasks); } catch { /* swallow */ }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during pipeline dispose");
            }

            foreach (var (_, svc) in _seq2PointServices)
            {
                try { await svc.DisposeAsync(); } catch { /* ignore */ }
            }
            _cts.Dispose();
        }
    }

    public interface IPipelineTelemetryService
    {
        Task ProcessPowerMeterTelemetry(PowerMeterTelemetry telemetry, CancellationToken ct);
        Task ProcessApplianceTelemetry(ApplianceTelemetry telemetry, CancellationToken ct);
    }
}
