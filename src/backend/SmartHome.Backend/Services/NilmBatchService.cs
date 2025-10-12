using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartHome.Backend.Dal;
using SmartHome.Backend.Dal.Entity;
using SmartHome.Backend.NILM.Models;
using SmartHome.Backend.NILM.Services;
using System.Text.Json;

namespace SmartHome.Backend.Services;

public interface INilmBatchService
{
    Task<NilmBatchResult> ProcessAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct);
}

public sealed record NilmBatchResult(DateTime StartUtc, DateTime EndUtc, int TelemetriesProcessed, int DetectionsInserted, int ModelsLoaded, double SecondsElapsed, int DeletedExisting);

/// <summary>
/// Servizio batch per elaborazione NILM (Hart + Seq2Point) su un intervallo arbitrario.
/// </summary>
public sealed class NilmBatchService : INilmBatchService
{
    private readonly ILogger<NilmBatchService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly INilmHartService _hartService;

    public NilmBatchService(ILogger<NilmBatchService> logger, IServiceScopeFactory scopeFactory, INilmHartService hartService)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _hartService = hartService;
    }

    public async Task<NilmBatchResult> ProcessAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct)
    {
        if (endUtc <= startUtc) throw new ArgumentException("endUtc deve essere > startUtc");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("NILM Batch: start {Start:o} end {End:o}", startUtc, endUtc);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var ctxFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SmartHomeDbContext>>();
        await using var ctx = await ctxFactory.CreateDbContextAsync(ct);

        // DELETE preesistenti
        int deleted = 0;
        try
        {
            deleted = await ctx.Database.ExecuteSqlRawAsync("DELETE FROM \"NilmDetections\" WHERE \"Timestamp\" >= {0} AND \"Timestamp\" < {1}", startUtc, endUtc, ct);
            _logger.LogInformation("NILM Batch: cancellate {Count} detections esistenti", deleted);
        }
        catch (Exception exDel)
        {
            _logger.LogWarning(exDel, "Errore cancellando detections esistenti");
        }

        // Reset stato CO se possibile
        if (_hartService is NilmHartService concreteHart)
            concreteHart.ResetForBatch();

        // Carica modelli Seq2Point
        var s2pModels = LoadSeq2PointModels();
        _logger.LogInformation("NILM Batch: caricati {Count} modelli Seq2Point", s2pModels.Count);

        var batchSize = 5000;
        var teleCount = 0;
        var detInsertedTotal = 0;
        var nilmBuffer = new List<NilmDetection>(batchSize);

        var query = ctx.PowerMeterTelemetries.AsNoTracking()
            .Where(t => t.Timestamp >= startUtc && t.Timestamp < endUtc && t.PowerW != null)
            .OrderBy(t => t.Timestamp);

        await foreach (var tele in query.AsAsyncEnumerable().WithCancellation(ct))
        {
            teleCount++;
            try
            {
                var detsHart = await _hartService.ProcessPowerMeterTelemetryAsync(tele, ct);
                if (detsHart is { Count: > 0 }) nilmBuffer.AddRange(detsHart);
            }
            catch (Exception exHart) { _logger.LogWarning(exHart, "Errore HART tele {Ts:o}", tele.Timestamp); }

            // Seq2Point
            foreach (var (name, svc, _) in s2pModels)
            {
                try
                {
                    var r = svc.AddSample(tele.Timestamp, tele.PowerW!.Value);
                    if (r is { PredictedOn: true })
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

            if (nilmBuffer.Count >= batchSize)
            {
                detInsertedTotal += await FlushAsync(ctxFactory, nilmBuffer, ct);
                nilmBuffer.Clear();
            }
        }

        if (nilmBuffer.Count > 0)
        {
            detInsertedTotal += await FlushAsync(ctxFactory, nilmBuffer, ct);
            nilmBuffer.Clear();
        }

        foreach (var (_, svc, _) in s2pModels)
        {
            await svc.DisposeAsync();
        }

        sw.Stop();
        var res = new NilmBatchResult(startUtc, endUtc, teleCount, detInsertedTotal, s2pModels.Count, sw.Elapsed.TotalSeconds, deleted);
        _logger.LogInformation("NILM Batch completato: {@Res}", res);
        return res;
    }

    private async Task<int> FlushAsync(IDbContextFactory<SmartHomeDbContext> factory, List<NilmDetection> list, CancellationToken ct)
    {
        if (list.Count == 0) return 0;
        try
        {
            await using var ctx = await factory.CreateDbContextAsync(ct);
            ctx.ChangeTracker.AutoDetectChangesEnabled = false;
            ctx.NilmDetections.AddRange(list);
            await ctx.SaveChangesAsync(ct);
            return list.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore salvataggio detections batch ({Count})", list.Count);
            return 0;
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
