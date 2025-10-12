using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SmartHome.Backend.Dal;
using SmartHome.Backend.Dal.Entity;
using SmartHome.Backend.Dto.Signatures;
using Microsoft.Extensions.Logging;

namespace SmartHome.Backend.Services;

/// <summary>
/// Heuristic generation of an ApplianceSignature from existing appliance telemetry.
/// Strategy:
/// 1. Load ordered telemetry (power).
/// 2. Compute consecutive deltas (W).
/// 3. Positive deltas => ON candidates, negative => OFF candidates (absolute value).
/// 4. Use percentiles (P20..P90) to define min/max ranges (clamped) and adapt to Hart service edge threshold.
/// 5. Estimate MinOnSeconds using approximate ON durations.
/// 6. RefractorySeconds = min(Max(3, MinOn/4), 30).
/// 7. Persist new version; optionally set as default.
/// Notes: Aligns with NilmHartService which ignores edges < 15 W (EDGE_MIN_W). We clamp generated min deltas accordingly.
/// </summary>
public sealed class ApplianceSignatureGenerationService : IApplianceSignatureGenerationService
{
    private readonly IDbContextFactory<SmartHomeDbContext> _factory;
    private readonly IMapper _mapper;
    private readonly ILogger<ApplianceSignatureGenerationService> _logger;

    // Should match (or be <=) EDGE_MIN_W in NilmHartService for compatibility.
    private const double HART_MIN_EDGE_W = 15.0;
    private const double INTERNAL_DELTA_NOISE_W = 5.0; // ignore tiny fluctuations when building raw delta list
    private const double MIN_RANGE_WIDTH_W = 5.0; // ensure at least this width between min and max
    private const double RANGE_EXPAND_FRACTION = 0.10; // expand computed min/max by 10% to reduce false negatives

    public ApplianceSignatureGenerationService(
        IDbContextFactory<SmartHomeDbContext> factory,
        IMapper mapper,
        ILogger<ApplianceSignatureGenerationService> logger)
    {
        _factory = factory;
        _mapper = mapper;
        _logger = logger;
    }

    private static DateTime NormalizeUtc(DateTime dt)
    {
        return dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(dt, DateTimeKind.Utc), // assume already UTC
            _ => dt
        };
    }

    public async Task<ApplianceSignatureDto?> GenerateAsync(
        int applianceId,
        DateTime? fromUtc,
        DateTime? toUtc,
        bool setAsDefault,
        CancellationToken ct)
    {
        // Normalize incoming optional bounds to UTC to satisfy Npgsql which requires UTC for timestamptz parameters.
        if (fromUtc.HasValue) fromUtc = NormalizeUtc(fromUtc.Value);
        if (toUtc.HasValue) toUtc = NormalizeUtc(toUtc.Value);

        await using var ctx = await _factory.CreateDbContextAsync(ct);

        var appliance = await ctx.Appliances
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == applianceId, ct);
        if (appliance is null)
        {
            _logger.LogWarning("Generate signature: appliance {Id} not found", applianceId);
            return null;
        }

        var q = ctx.ApplianceTelemetries
            .AsNoTracking()
            .Where(t => t.ApplianceId == applianceId);

        if (fromUtc.HasValue) q = q.Where(t => t.Timestamp >= fromUtc.Value);
        if (toUtc.HasValue) q = q.Where(t => t.Timestamp <= toUtc.Value);

        // Load telemetry ordered by time
        var raw = await q
            .OrderBy(t => t.Timestamp)
            .Select(t => new { t.Timestamp, Power = (double?)t.PowerWatts })
            .ToListAsync(ct);

        if (raw.Count < 10)
        {
            _logger.LogWarning("Generate signature: insufficient telemetry points ({Count})", raw.Count);
            return null;
        }

        // Normalize (forward fill nulls) into a mutable list of value tuples
        var telemetry = new List<(DateTime ts, double power)>(raw.Count);
        double last = 0;
        foreach (var r in raw)
        {
            var val = r.Power ?? last;
            last = val;
            telemetry.Add((r.Timestamp, val));
        }

        // Collect positive and negative deltas (absolute for off)
        var onDeltas = new List<double>();
        var offDeltas = new List<double>();
        for (int i = 1; i < telemetry.Count; i++)
        {
            var delta = telemetry[i].power - telemetry[i - 1].power;
            if (delta > INTERNAL_DELTA_NOISE_W) onDeltas.Add(delta);
            else if (delta < -INTERNAL_DELTA_NOISE_W) offDeltas.Add(-delta);
        }

        if (onDeltas.Count == 0 || offDeltas.Count == 0)
        {
            _logger.LogWarning("Generate signature: not enough ON ({On}) or OFF ({Off}) deltas", onDeltas.Count, offDeltas.Count);
            return null;
        }

        onDeltas.Sort();
        offDeltas.Sort();

        static double Percentile(List<double> data, double p)
        {
            if (data.Count == 0) return 0;
            p = Math.Clamp(p, 0, 1);
            var idx = (data.Count - 1) * p;
            var lo = (int)Math.Floor(idx);
            var hi = (int)Math.Ceiling(idx);
            if (lo == hi) return data[lo];
            var frac = idx - lo;
            return data[lo] + (data[hi] - data[lo]) * frac;
        }

        // Raw percentile-based ranges
        double rawOnMin = Percentile(onDeltas, 0.20);
        double rawOnMax = Percentile(onDeltas, 0.90);
        double rawOffMin = Percentile(offDeltas, 0.20);
        double rawOffMax = Percentile(offDeltas, 0.90);

        // Expand and clamp
        (double min, double max) Adjust(double minP, double maxP)
        {
            if (maxP < minP) (minP, maxP) = (maxP, minP);
            // Expand a little to reduce false negatives for Hart matching distance
            var span = Math.Max(MIN_RANGE_WIDTH_W, maxP - minP);
            var expand = span * RANGE_EXPAND_FRACTION;
            var minAdj = Math.Max(1.0, minP - expand);
            var maxAdj = maxP + expand;
            // Ensure minimum >= Hart threshold (since edges below are ignored)
            if (minAdj < HART_MIN_EDGE_W)
            {
                // Shift whole range upward preserving width
                var diff = HART_MIN_EDGE_W - minAdj;
                minAdj += diff;
                maxAdj += diff;
            }
            // Enforce width
            if (maxAdj - minAdj < MIN_RANGE_WIDTH_W)
            {
                maxAdj = minAdj + MIN_RANGE_WIDTH_W;
            }
            // Round to 0.1 W
            minAdj = Math.Round(minAdj, 1);
            maxAdj = Math.Round(maxAdj, 1);
            return (minAdj, maxAdj);
        }

        var (onMin, onMax) = Adjust(rawOnMin, rawOnMax);
        var (offMin, offMax) = Adjust(rawOffMin, rawOffMax);

        // Approximate ON durations
        var onEvents = new List<DateTime>();
        var offEvents = new List<DateTime>();
        for (int i = 1; i < telemetry.Count; i++)
        {
            var delta = telemetry[i].power - telemetry[i - 1].power;
            if (delta > INTERNAL_DELTA_NOISE_W) onEvents.Add(telemetry[i].ts);
            else if (delta < -INTERNAL_DELTA_NOISE_W) offEvents.Add(telemetry[i].ts);
        }

        var durations = new List<double>();
        foreach (var start in onEvents)
        {
            var off = offEvents.FirstOrDefault(o => o > start);
            if (off != default)
            {
                var dur = (off - start).TotalSeconds;
                if (dur > 1 && dur < 24 * 3600) durations.Add(dur);
            }
        }

        double minOnSeconds;
        if (durations.Count >= 3)
        {
            durations.Sort();
            minOnSeconds = Math.Max(5, Math.Round(Percentile(durations, 0.40), 1));
        }
        else
        {
            var span = (telemetry[^1].ts - telemetry[0].ts).TotalSeconds;
            var cycles = Math.Max(1, Math.Min(onEvents.Count, offEvents.Count));
            minOnSeconds = Math.Max(5, Math.Round(span / cycles * 0.10, 1));
        }

        double refractorySeconds = Math.Clamp(Math.Round(minOnSeconds / 4, 1), 3, 30);

        // Determine next version
        var lastVersion = await ctx.ApplianceSignatures
            .Where(s => s.ApplianceId == applianceId)
            .MaxAsync(s => (int?)s.Version, ct) ?? 0;

        await using var tx = await ctx.Database.BeginTransactionAsync(ct);
        if (setAsDefault)
        {
            await ctx.ApplianceSignatures
                .Where(s => s.ApplianceId == applianceId && s.IsDefault)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.IsDefault, false), ct);
        }

        var entity = new ApplianceSignature
        {
            ApplianceId = applianceId,
            Version = lastVersion + 1,
            CreatedAt = DateTime.UtcNow,
            IsDefault = setAsDefault,
            Name = $"Auto {DateTime.UtcNow:yyyyMMdd-HHmm}",
            OnDeltaMinW = onMin,
            OnDeltaMaxW = onMax,
            OffDeltaMinW = offMin,
            OffDeltaMaxW = offMax,
            MinOnSeconds = minOnSeconds,
            RefractorySeconds = refractorySeconds
        };

        ctx.ApplianceSignatures.Add(entity);
        await ctx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation(
            "Generated signature v{Version} for appliance {Id}: ON[{OnMin}-{OnMax}] OFF[{OffMin}-{OffMax}] MinOn={MinOn}s Refractory={Ref}s (rawOn: {RawOnMin}-{RawOnMax} rawOff: {RawOffMin}-{RawOffMax})",
            entity.Version, applianceId, onMin, onMax, offMin, offMax, minOnSeconds, refractorySeconds, Math.Round(rawOnMin,1), Math.Round(rawOnMax,1), Math.Round(rawOffMin,1), Math.Round(rawOffMax,1));

        if (onMin < HART_MIN_EDGE_W || offMin < HART_MIN_EDGE_W)
        {
            _logger.LogWarning("Signature ranges adjusted to respect Hart min edge {MinEdge}W (ON min {OnMin} OFF min {OffMin})", HART_MIN_EDGE_W, onMin, offMin);
        }

        return _mapper.Map<ApplianceSignatureDto>(entity);
    }
}