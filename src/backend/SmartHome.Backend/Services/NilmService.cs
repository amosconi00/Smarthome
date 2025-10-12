using Microsoft.EntityFrameworkCore;
using SmartHome.Backend.Dal;
using SmartHome.Backend.Dto.Nilm;
using SmartHome.Backend.Dal.Entity;
using SmartHome.Backend.NILM.Models;
using SmartHome.Backend.NILM.Services;

namespace SmartHome.Backend.Services;

public class NilmService : INilmService, IAsyncDisposable
{
    private readonly IDbContextFactory<SmartHomeDbContext> _ctxFactory;
    private readonly INilmHartService _hartService;
    private readonly ILogger<NilmService> _logger;

    public NilmService(IDbContextFactory<SmartHomeDbContext> ctxFactory, INilmHartService hartService, ILogger<NilmService> logger)
    {
        _ctxFactory = ctxFactory;
        _hartService = hartService;
        _logger = logger;
    }

    public async Task<NilmSeriesDto> GetSeriesCompareAsync(int applianceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        await using var ctx = await _ctxFactory.CreateDbContextAsync(ct);
        var appliance = await ctx.Appliances.Include(a => a.CategoryAppliance)
            .FirstOrDefaultAsync(a => a.Id == applianceId, ct) ?? throw new KeyNotFoundException("Appliance non trovata");
        var categoryName = appliance.CategoryAppliance?.Name;

        var teleList = await ctx.ApplianceTelemetries.AsNoTracking()
            .Where(t => t.ApplianceId == applianceId && t.Timestamp >= fromUtc && t.Timestamp < toUtc && t.PowerWatts != null)
            .OrderBy(t => t.Timestamp)
            .Select(t => new NilmSampleDto(t.Timestamp, t.PowerWatts))
            .ToListAsync(ct);

        var hartList = await ctx.NilmDetections.AsNoTracking()
            .Where(d => d.ApplianceId == applianceId && d.Source == "HART" && d.Timestamp >= fromUtc && d.Timestamp < toUtc && d.EstimatedPowerW != null)
            .OrderBy(d => d.Timestamp)
            .Select(d => new NilmSampleDto(d.Timestamp, d.EstimatedPowerW))
            .ToListAsync(ct);

        var s2pBase = ctx.NilmDetections.AsNoTracking()
            .Where(d => d.Source == "S2P" && d.Timestamp >= fromUtc && d.Timestamp < toUtc && d.EstimatedPowerW != null);
        if (await s2pBase.AnyAsync(d => d.ApplianceId == applianceId, ct))
            s2pBase = s2pBase.Where(d => d.ApplianceId == applianceId);
        else if (!string.IsNullOrEmpty(categoryName))
            s2pBase = s2pBase.Where(d => d.ModelName == categoryName);
        else s2pBase = s2pBase.Where(d => false);
        var s2pList = await s2pBase.OrderBy(d => d.Timestamp)
            .Select(d => new NilmSampleDto(d.Timestamp, d.EstimatedPowerW))
            .ToListAsync(ct);

        var metrics = ComputeMetrics(teleList, hartList, s2pList, fromUtc, TimeSpan.FromSeconds(1));
        return new NilmSeriesDto(teleList, hartList, s2pList, metrics);
    }

    private static DateTime BucketTs(DateTime ts, DateTime baseTs, TimeSpan bucket)
    {
        var delta = ts - baseTs;
        if (delta.Ticks < 0) delta = TimeSpan.Zero;
        var idx = delta.Ticks / bucket.Ticks;
        return baseTs.AddTicks(idx * bucket.Ticks);
    }

    private static NilmSeriesMetrics ComputeMetrics(
        IReadOnlyList<NilmSampleDto> telemetry,
        IReadOnlyList<NilmSampleDto> hart,
        IReadOnlyList<NilmSampleDto> s2p,
        DateTime? baseTs = null,
        TimeSpan? bucketSize = null)
    {
        if (telemetry.Count == 0)
            return new NilmSeriesMetrics(null, null, null, null, null, null);
        if (bucketSize is null || bucketSize.Value <= TimeSpan.Zero)
            return ComputeMetricsExact(telemetry, hart, s2p);

        var bucket = bucketSize.Value;
        var baseTime = baseTs ?? telemetry.Min(t => t.TimestampUtc);
        static Dictionary<DateTime, double> Agg(IReadOnlyList<NilmSampleDto> list, DateTime baseTime, TimeSpan b)
            => list.Where(x => x.ValueW.HasValue).GroupBy(x => BucketTs(x.TimestampUtc, baseTime, b)).ToDictionary(g => g.Key, g => g.Average(v => v.ValueW!.Value));
        var teleAgg = Agg(telemetry, baseTime, bucket);
        var hartAgg = Agg(hart, baseTime, bucket);
        var s2pAgg = Agg(s2p, baseTime, bucket);
        IEnumerable<double> Diff(Dictionary<DateTime, double> pred, Dictionary<DateTime, double> truth) { foreach (var kv in pred) if (truth.TryGetValue(kv.Key, out var act)) yield return kv.Value - act; }
        double? hartMae = null, hartRmse = null, s2pMae = null, s2pRmse = null;
        var hartDiffs = Diff(hartAgg, teleAgg).ToList(); if (hartDiffs.Count > 0) { hartMae = hartDiffs.Average(d => Math.Abs(d)); hartRmse = Math.Sqrt(hartDiffs.Average(d => d * d)); }
        var s2pDiffs = Diff(s2pAgg, teleAgg).ToList(); if (s2pDiffs.Count > 0) { s2pMae = s2pDiffs.Average(d => Math.Abs(d)); s2pRmse = Math.Sqrt(s2pDiffs.Average(d => d * d)); }
        // Mirror Hart metrics into Co* fields per compatibilità
        return new NilmSeriesMetrics(hartMae, hartRmse, s2pMae, s2pRmse, hartMae, hartRmse);
    }

    private static NilmSeriesMetrics ComputeMetricsExact(IReadOnlyList<NilmSampleDto> telemetry, IReadOnlyList<NilmSampleDto> hart, IReadOnlyList<NilmSampleDto> s2p)
    {
        double? hartMae = null, hartRmse = null, s2pMae = null, s2pRmse = null;
        var teleMap = telemetry.ToDictionary(x => x.TimestampUtc, x => x.ValueW);
        List<double> Diff(IReadOnlyList<NilmSampleDto> preds) { var l = new List<double>(); foreach (var p in preds) { if (!p.ValueW.HasValue) continue; if (!teleMap.TryGetValue(p.TimestampUtc, out var act) || !act.HasValue) continue; l.Add(p.ValueW.Value - act.Value); } return l; }
        var dHart = Diff(hart); if (dHart.Count > 0) { hartMae = dHart.Average(d => Math.Abs(d)); hartRmse = Math.Sqrt(dHart.Average(d => d * d)); }
        var dS2p = Diff(s2p); if (dS2p.Count > 0) { s2pMae = dS2p.Average(d => Math.Abs(d)); s2pRmse = Math.Sqrt(dS2p.Average(d => d * d)); }
        return new NilmSeriesMetrics(hartMae, hartRmse, s2pMae, s2pRmse, hartMae, hartRmse);
    }

    public async Task<IReadOnlyList<NilmAlignedPointDto>> GetSeriesDownsampledAsync(int applianceId, DateTime fromUtc, DateTime toUtc, int size, string unit, CancellationToken ct)
    {
        if (size <= 0) size = 1;
        unit = (unit ?? "second").Trim().ToLowerInvariant();
        var supported = new HashSet<string> { "second", "seconds", "s", "minute", "minutes", "m", "hour", "hours", "h", "day", "days", "d", "week", "weeks", "w", "month", "months", "mo", "year", "years", "y" };
        if (!supported.Contains(unit)) throw new ArgumentException("Unità non supportata", nameof(unit));
        TimeSpan bucketSize = unit switch
        {
            "second" or "seconds" or "s" => TimeSpan.FromSeconds(size),
            "minute" or "minutes" or "m" => TimeSpan.FromMinutes(size),
            "hour" or "hours" or "h" => TimeSpan.FromHours(size),
            "day" or "days" or "d" => TimeSpan.FromDays(size),
            "week" or "weeks" or "w" => TimeSpan.FromDays(7 * size),
            "month" or "months" or "mo" => TimeSpan.FromDays(30 * size),
            "year" or "years" or "y" => TimeSpan.FromDays(365 * size),
            _ => TimeSpan.FromSeconds(size)
        };
        await using var ctx = await _ctxFactory.CreateDbContextAsync(ct);
        var appliance = await ctx.Appliances.Include(a => a.CategoryAppliance).FirstOrDefaultAsync(a => a.Id == applianceId, ct) ?? throw new KeyNotFoundException("Appliance non trovata");
        var categoryName = appliance.CategoryAppliance?.Name;
        var tele = await ctx.ApplianceTelemetries.AsNoTracking().Where(t => t.ApplianceId == applianceId && t.Timestamp >= fromUtc && t.Timestamp < toUtc && t.PowerWatts != null).Select(t => new { t.Timestamp, t.PowerWatts }).ToListAsync(ct);
        var hart = await ctx.NilmDetections.AsNoTracking().Where(d => d.ApplianceId == applianceId && d.Source == "HART" && d.Timestamp >= fromUtc && d.Timestamp < toUtc && d.EstimatedPowerW != null).Select(d => new { d.Timestamp, d.EstimatedPowerW }).ToListAsync(ct);
        var s2pBase = ctx.NilmDetections.AsNoTracking().Where(d => d.Source == "S2P" && d.Timestamp >= fromUtc && d.Timestamp < toUtc && d.EstimatedPowerW != null);
        if (await s2pBase.AnyAsync(d => d.ApplianceId == applianceId, ct)) s2pBase = s2pBase.Where(d => d.ApplianceId == applianceId); else if (!string.IsNullOrEmpty(categoryName)) s2pBase = s2pBase.Where(d => d.ModelName == categoryName); else s2pBase = s2pBase.Where(d => false);
        var s2p = await s2pBase.Select(d => new { d.Timestamp, d.EstimatedPowerW }).ToListAsync(ct);
        DateTime Truncate(DateTime ts) { var delta = ts - fromUtc; if (delta.Ticks < 0) delta = TimeSpan.Zero; var idx = (long)(delta.Ticks / bucketSize.Ticks); return fromUtc.AddTicks(idx * bucketSize.Ticks); }
        var teleAgg = tele.GroupBy(x => Truncate(x.Timestamp)).ToDictionary(g => g.Key, g => g.Average(v => v.PowerWatts));
        var hartAgg = hart.GroupBy(x => Truncate(x.Timestamp)).ToDictionary(g => g.Key, g => g.Average(v => v.EstimatedPowerW));
        var s2pAgg = s2p.GroupBy(x => Truncate(x.Timestamp)).ToDictionary(g => g.Key, g => g.Average(v => v.EstimatedPowerW));
        var allTs = teleAgg.Keys.Union(hartAgg.Keys).Union(s2pAgg.Keys).OrderBy(ts => ts);
        return allTs.Select(ts => new NilmAlignedPointDto(ts, teleAgg.GetValueOrDefault(ts), hartAgg.GetValueOrDefault(ts), s2pAgg.GetValueOrDefault(ts))).ToList();
    }

    public async Task<NilmSeriesDto> RecomputeFromMainsAsync(int applianceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        await using var ctx = await _ctxFactory.CreateDbContextAsync(ct);
        var appliance = await ctx.Appliances.Include(a => a.CategoryAppliance).FirstOrDefaultAsync(a => a.Id == applianceId, ct) ?? throw new KeyNotFoundException("Appliance non trovata");
        var powerMeterId = await ctx.PowerMeters.Select(pm => (int?)pm.Id).FirstOrDefaultAsync(ct) ?? throw new InvalidOperationException("Nessun PowerMeter disponibile");
        var mains = await ctx.PowerMeterTelemetries.AsNoTracking().Where(t => t.PowerMeterId == powerMeterId && t.Timestamp >= fromUtc && t.Timestamp < toUtc && t.PowerW != null).OrderBy(t => t.Timestamp).Select(t => new { t.Timestamp, t.PowerW }).ToListAsync(ct);
        if (_hartService is NilmHartService concreteHart) concreteHart.ResetForBatch();
        var hartList = new List<NilmSampleDto>();
        foreach (var m in mains)
        {
            try
            {
                var dets = await _hartService.ProcessPowerMeterTelemetryAsync(
                    new PowerMeterTelemetry { PowerMeterId = powerMeterId, Timestamp = m.Timestamp, PowerW = m.PowerW }, ct);
                var det = dets.FirstOrDefault(d => d.ApplianceId == applianceId && d.EstimatedPowerW.HasValue);
                if (det != null)
                    hartList.Add(new NilmSampleDto(det.Timestamp, det.EstimatedPowerW));
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Errore HART sample {Ts}", m.Timestamp); }
        }

        // Seq2Point
        var categoryName = appliance.CategoryAppliance?.Name; var modelSuffix = categoryName?.Replace(' ', '_').ToLowerInvariant(); var modelsRoot = Path.Combine(AppContext.BaseDirectory, "NILM", "TrainedModels"); string? onnx = Directory.GetFiles(modelsRoot, "seq2point_*.onnx").FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).EndsWith(modelSuffix ?? string.Empty)); var s2pList = new List<NilmSampleDto>();
        if (onnx != null) { try { var suffix = Path.GetFileNameWithoutExtension(onnx)["seq2point_".Length..]; var statsPath = Path.Combine(modelsRoot, $"normalization_stats_{suffix}.json"); var trainInfoPath = Path.Combine(modelsRoot, $"training_info_{suffix}.json"); int window = 99; double threshold = 20; if (File.Exists(trainInfoPath)) { using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(trainInfoPath)); var root = doc.RootElement; if (root.TryGetProperty("window", out var wProp) && wProp.TryGetInt32(out var w) && w > 0) window = w; if (root.TryGetProperty("threshold_w", out var thrProp) && thrProp.ValueKind == System.Text.Json.JsonValueKind.Number) threshold = thrProp.GetDouble(); } var stats = NormalizationStats.Load(statsPath); await using var s2p = new Seq2PointInferenceService(onnx, stats, window: window, thresholdW: threshold, tryGpu: false); foreach (var sample in mains) { var r = s2p.AddSample(sample.Timestamp, sample.PowerW!.Value); if (r != null && r.PredictedOn) s2pList.Add(new NilmSampleDto(r.CenterTimestampUtc, r.PredictedWatts)); } } catch (Exception ex) { _logger.LogError(ex, "Errore ricalcolo Seq2Point"); } }
        var teleList = await ctx.ApplianceTelemetries.AsNoTracking().Where(t => t.ApplianceId == applianceId && t.Timestamp >= fromUtc && t.Timestamp < toUtc && t.PowerWatts != null).OrderBy(t => t.Timestamp).Select(t => new NilmSampleDto(t.Timestamp, t.PowerWatts)).ToListAsync(ct);
        var metrics = ComputeMetrics(teleList, hartList, s2pList, fromUtc, TimeSpan.FromSeconds(1));
        return new NilmSeriesDto(teleList, hartList, s2pList, metrics);
    }

    public async Task<NilmSeriesDto> RecomputeFromMainsAsync(int applianceId, int signatureId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        // signatureId ignorato per Hart-only (potrebbe essere usato per filtri futuri)
        return await RecomputeFromMainsAsync(applianceId, fromUtc, toUtc, ct);
    }

    public async Task<NilmSeriesHartDto> RecomputeHartFromMainsAsync(int applianceId, int signatureId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        await using var ctx = await _ctxFactory.CreateDbContextAsync(ct);
        var appliance = await ctx.Appliances.Include(a => a.CategoryAppliance).FirstOrDefaultAsync(a => a.Id == applianceId, ct) ?? throw new KeyNotFoundException("Appliance non trovata");
        var powerMeterId = await ctx.PowerMeters.Select(pm => (int?)pm.Id).FirstOrDefaultAsync(ct) ?? throw new InvalidOperationException("Nessun PowerMeter disponibile");

        // Carico signature specifica se fornita
        ApplianceSignature? overrideSignature = await ctx.ApplianceSignatures.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == signatureId && s.ApplianceId == applianceId, ct);
        if (overrideSignature == null) throw new KeyNotFoundException("Signature non trovata");

        var mains = await ctx.PowerMeterTelemetries.AsNoTracking()
            .Where(t => t.PowerMeterId == powerMeterId && t.Timestamp >= fromUtc && t.Timestamp < toUtc && t.PowerW != null)
            .OrderBy(t => t.Timestamp)
            .Select(t => new { t.Timestamp, t.PowerW }).ToListAsync(ct);

        if (_hartService is NilmHartService concreteHart) concreteHart.ResetForBatch();
        var hartList = new List<NilmSampleDto>();
        foreach (var m in mains)
        {
            try
            {
                var dets = await _hartService.ProcessPowerMeterTelemetryAsync(
                    new PowerMeterTelemetry { PowerMeterId = powerMeterId, Timestamp = m.Timestamp, PowerW = m.PowerW }, overrideSignature, ct);
                var det = dets.FirstOrDefault(d => d.ApplianceId == applianceId && d.EstimatedPowerW.HasValue);
                if (det != null)
                    hartList.Add(new NilmSampleDto(det.Timestamp, det.EstimatedPowerW));
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Errore HART sample {Ts}", m.Timestamp); }
        }

        var teleList = await ctx.ApplianceTelemetries.AsNoTracking()
            .Where(t => t.ApplianceId == applianceId && t.Timestamp >= fromUtc && t.Timestamp < toUtc && t.PowerWatts != null)
            .OrderBy(t => t.Timestamp)
            .Select(t => new NilmSampleDto(t.Timestamp, t.PowerWatts)).ToListAsync(ct);

        // Calcolo metriche usando solo Hart (passo lista vuota per S2P)
        var metrics = ComputeMetrics(teleList, hartList, new List<NilmSampleDto>(), fromUtc, TimeSpan.FromSeconds(1));
        return new NilmSeriesHartDto(teleList, hartList, metrics);
    }

    public async Task<NilmSeriesHartDto> RecomputeHartWhatIfAsync(int applianceId, NilmWhatIfSignatureDto signature, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        await using var ctx = await _ctxFactory.CreateDbContextAsync(ct);
        var appliance = await ctx.Appliances.Include(a => a.CategoryAppliance).FirstOrDefaultAsync(a => a.Id == applianceId, ct) ?? throw new KeyNotFoundException("Appliance non trovata");
        var powerMeterId = await ctx.PowerMeters.Select(pm => (int?)pm.Id).FirstOrDefaultAsync(ct) ?? throw new InvalidOperationException("Nessun PowerMeter disponibile");

        // Costruisco una ApplianceSignature volatile (non salvata)
        var overrideSignature = new ApplianceSignature
        {
            ApplianceId = appliance.Id,
            Appliance = appliance,
            Name = "whatif",
            Version = 0,
            CreatedAt = DateTime.UtcNow,
            IsDefault = false,
            OnDeltaMinW = signature.OnDeltaMinW,
            OnDeltaMaxW = signature.OnDeltaMaxW,
            OffDeltaMinW = signature.OffDeltaMinW,
            OffDeltaMaxW = signature.OffDeltaMaxW,
            MinOnSeconds = signature.MinOnSeconds,
            RefractorySeconds = signature.RefractorySeconds
        };

        var mains = await ctx.PowerMeterTelemetries.AsNoTracking()
            .Where(t => t.PowerMeterId == powerMeterId && t.Timestamp >= fromUtc && t.Timestamp < toUtc && t.PowerW != null)
            .OrderBy(t => t.Timestamp)
            .Select(t => new { t.Timestamp, t.PowerW }).ToListAsync(ct);

        if (_hartService is NilmHartService concreteHart) concreteHart.ResetForBatch();
        var hartList = new List<NilmSampleDto>();
        foreach (var m in mains)
        {
            try
            {
                var dets = await _hartService.ProcessPowerMeterTelemetryAsync(
                    new PowerMeterTelemetry { PowerMeterId = powerMeterId, Timestamp = m.Timestamp, PowerW = m.PowerW }, overrideSignature, ct);
                var det = dets.FirstOrDefault(d => d.ApplianceId == applianceId && d.EstimatedPowerW.HasValue);
                if (det != null)
                    hartList.Add(new NilmSampleDto(det.Timestamp, det.EstimatedPowerW));
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Errore HART what-if sample {Ts}", m.Timestamp); }
        }

        var teleList = await ctx.ApplianceTelemetries.AsNoTracking()
            .Where(t => t.ApplianceId == applianceId && t.Timestamp >= fromUtc && t.Timestamp < toUtc && t.PowerWatts != null)
            .OrderBy(t => t.Timestamp)
            .Select(t => new NilmSampleDto(t.Timestamp, t.PowerWatts)).ToListAsync(ct);

        var metrics = ComputeMetrics(teleList, hartList, new List<NilmSampleDto>(), fromUtc, TimeSpan.FromSeconds(1));
        return new NilmSeriesHartDto(teleList, hartList, metrics);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
