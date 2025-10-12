using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartHome.Backend.Dal;
using SmartHome.Backend.Dal.Entity;
using SmartHome.Backend.Notifiers;

namespace SmartHome.Backend.Services;

public sealed class NilmHartService : INilmHartService
{
    private readonly IDbContextFactory<SmartHomeDbContext> _factory;
    private readonly ILogger<NilmHartService> _logger;
    private readonly IApplianceSignatureChangeNotifier _sigNotifier;

    // last mains per meter
    private readonly Dictionary<int, double> _lastPowerByMeter = new();

    private sealed class ApplianceState
    {
        public bool IsOn { get; set; }
        public DateTime LastChangeUtc { get; set; } = DateTime.MinValue;
        public DateTime? OnStartUtc { get; set; }
        public double? LastOnObservedDeltaW { get; set; } // per pairing con OFF
    }

    private readonly Dictionary<int, ApplianceState> _states = new();
    private readonly object _lock = new();

    private record CachedSignature(int ApplianceId, ApplianceSignature Sig, Appliance Meta);
    private volatile ImmutableDictionary<int, CachedSignature> _defaultSignatures =
        ImmutableDictionary<int, CachedSignature>.Empty;

    // --- NUOVO: aggregatore di fronti per meter (merge finestrato) ---
    private sealed class PendingEdge
    {
        public double StartPowerW { get; init; }
        public DateTime StartTsUtc { get; init; }
        public double AccumDeltaW { get; set; }
        public int Sign { get; init; } // +1 / -1
    }
    private readonly Dictionary<int, PendingEdge?> _pendingByMeter = new();

    // Parametri (tarabili)
    private const double EDGE_MIN_W = 40;             // più alto: meno falsi
    private const double RANGE_TOL_FRACTION = 0.15;   // ±15% fuori range
    private const double SCORE_MAX_ACCEPT = 0.60;     // max distanza normalizzata accettabile
    private const double MERGE_WINDOW_SECONDS = 2.0;  // accorpo salti ravvicinati
    private const int MIN_INTER_EVENT_SECONDS = 3;    // anti rimbalzo tra eventi dello stesso device
    private const int OFF_CONFIRM_SECONDS = 0;        // Hart puro: 0

    public NilmHartService(
        IDbContextFactory<SmartHomeDbContext> factory,
        ILogger<NilmHartService> logger,
        IApplianceSignatureChangeNotifier sigNotifier)
    {
        _factory = factory;
        _logger = logger;
        _sigNotifier = sigNotifier;

        _sigNotifier.OnChanged += () => _ = ReloadDefaultSignaturesAsync(CancellationToken.None);
        _ = ReloadDefaultSignaturesAsync(CancellationToken.None);
    }

    public void ResetForBatch()
    {
        lock (_lock)
        {
            _states.Clear();
            _lastPowerByMeter.Clear();
            _pendingByMeter.Clear();
        }
        _logger.LogInformation("NILM HART: stato interno resettato");
    }

    private async Task ReloadDefaultSignaturesAsync(CancellationToken ct)
    {
        try
        {
            await using var ctx = await _factory.CreateDbContextAsync(ct);

            var latestPerAppliance = await ctx.ApplianceSignatures
                .Where(s => s.IsDefault)
                .GroupBy(s => s.ApplianceId)
                .Select(g => new { ApplianceId = g.Key, MaxVersion = g.Max(s => s.Version) })
                .ToListAsync(ct);

            var joinQuery =
                from a in ctx.Appliances
                join lm in latestPerAppliance on a.Id equals lm.ApplianceId
                join s in ctx.ApplianceSignatures
                    on new { lm.ApplianceId, lm.MaxVersion } equals new { ApplianceId = s.ApplianceId, MaxVersion = s.Version }
                where a.Enable && a.EnableNilm
                select new { Appliance = a, Signature = s };

            var materialized = await joinQuery.ToListAsync(ct);

            _defaultSignatures = materialized.ToImmutableDictionary(
                x => x.Appliance.Id,
                x => new CachedSignature(x.Appliance.Id, x.Signature, x.Appliance));

            _logger.LogInformation("NILM HART: cache signatures caricata: {Count}", _defaultSignatures.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NILM HART: errore caricando signatures");
        }
    }

    public Task<IReadOnlyList<NilmDetection>> ProcessPowerMeterTelemetryAsync(PowerMeterTelemetry telemetry, CancellationToken ct)
        => ProcessInternalAsync(telemetry, null, ct);

    public Task<IReadOnlyList<NilmDetection>> ProcessPowerMeterTelemetryAsync(PowerMeterTelemetry telemetry, ApplianceSignature? overrideSignature, CancellationToken ct)
        => ProcessInternalAsync(telemetry, overrideSignature, ct);

    private Task<IReadOnlyList<NilmDetection>> ProcessInternalAsync(
        PowerMeterTelemetry telemetry,
        ApplianceSignature? overrideSig,
        CancellationToken ct)
    {
        var detections = new List<NilmDetection>();
        if (telemetry.PowerW is null || double.IsNaN(telemetry.PowerW.Value))
            return Task.FromResult((IReadOnlyList<NilmDetection>)detections);

        double current = telemetry.PowerW.Value;
        double previous;
        DateTime now = telemetry.Timestamp;

        lock (_lock)
        {
            previous = _lastPowerByMeter.TryGetValue(telemetry.PowerMeterId, out var p) ? p : current;
            _lastPowerByMeter[telemetry.PowerMeterId] = current;
        }

        var rawDelta = current - previous;
        var absRaw = Math.Abs(rawDelta);

        // --- MERGE: accumula salti ravvicinati dello stesso segno ---
        lock (_lock)
        {
            _pendingByMeter.TryGetValue(telemetry.PowerMeterId, out var pending);

            bool sameSign = pending != null && Math.Sign(rawDelta) != 0 && Math.Sign(rawDelta) == pending.Sign;
            bool withinWindow = pending != null && (now - pending.StartTsUtc).TotalSeconds <= MERGE_WINDOW_SECONDS;

            if (absRaw >= EDGE_MIN_W)
            {
                if (pending == null || !sameSign || !withinWindow)
                {
                    // chiudi il precedente (se esiste) prima di aprire il nuovo
                    if (pending != null)
                        ResolvePending(telemetry.PowerMeterId, now, current, pending, detections, overrideSig);

                    _pendingByMeter[telemetry.PowerMeterId] = new PendingEdge
                    {
                        StartPowerW = previous,
                        StartTsUtc = now,
                        AccumDeltaW = rawDelta,
                        Sign = Math.Sign(rawDelta)
                    };
                }
                else
                {
                    pending!.AccumDeltaW += rawDelta;
                }
            }
            else
            {
                // se la finestra è scaduta, risolvi il pending
                if (pending != null && (now - pending.StartTsUtc).TotalSeconds > MERGE_WINDOW_SECONDS)
                {
                    ResolvePending(telemetry.PowerMeterId, now, current, pending, detections, overrideSig);
                    _pendingByMeter[telemetry.PowerMeterId] = null;
                }
            }
        }

        return Task.FromResult((IReadOnlyList<NilmDetection>)detections);
    }

    private void ResolvePending(
        int meterId,
        DateTime now,
        double currentPowerW,
        PendingEdge pending,
        List<NilmDetection> detections,
        ApplianceSignature? overrideSig)
    {
        var totalDelta = pending.AccumDeltaW;
        var absDelta = Math.Abs(totalDelta);
        if (absDelta < EDGE_MIN_W) return;

        var snapshot = BuildSnapshot(overrideSig);
        if (snapshot.Count == 0) return;

        bool isOnEdge = totalDelta > 0;

        int? chosenAppliance = null;
        double bestScore = double.MaxValue;

        foreach (var (apId, pack) in snapshot)
        {
            var sig = pack.Sig;
            var meta = pack.Meta;

            if (!_states.TryGetValue(apId, out var st))
            {
                st = new ApplianceState();
                _states[apId] = st;
            }

            // anti-bounce temporale globale per il device
            if ((now - st.LastChangeUtc).TotalSeconds < Math.Max(MIN_INTER_EVENT_SECONDS, sig.RefractorySeconds))
                continue;

            if (isOnEdge)
            {
                if (st.IsOn) continue;

                var (min, max) = (sig.OnDeltaMinW, sig.OnDeltaMaxW);
                if (!InTol(absDelta, min, max)) continue;

                double score = DistScore(absDelta, min, max);
                if (score < bestScore)
                {
                    bestScore = score;
                    chosenAppliance = apId;
                }
            }
            else
            {
                if (!st.IsOn) continue;

                var (min, max) = (sig.OffDeltaMinW, sig.OffDeltaMaxW);
                if (!InTol(absDelta, min, max)) continue;

                // durata minima ON
                var onDur = st.OnStartUtc.HasValue ? (now - st.OnStartUtc.Value).TotalSeconds : 0;
                var metaMin = Math.Max(0, meta.SecondsMinDuration);
                if (onDur < Math.Max(sig.MinOnSeconds, metaMin * 0.5)) continue;

                // pairing: OFF deve "riassorbire" circa l'ON osservato
                if (st.LastOnObservedDeltaW is double onObs)
                {
                    var balanceOk = Math.Abs(absDelta - onObs) <= Math.Max(10, onObs * 0.25);
                    if (!balanceOk) continue;
                }

                double score = DistScore(absDelta, min, max);
                if (score < bestScore)
                {
                    bestScore = score;
                    chosenAppliance = apId;
                }
            }
        }

        if (chosenAppliance.HasValue && bestScore <= SCORE_MAX_ACCEPT)
        {
            var apId = chosenAppliance.Value;
            var pack = snapshot[apId];
            var sig = pack.Sig;
            var meta = pack.Meta;

            var st = _states[apId];

            if (isOnEdge)
            {
                st.IsOn = true;
                st.LastChangeUtc = now;
                st.OnStartUtc = now;
                st.LastOnObservedDeltaW = absDelta;

                var est = ClipToBand(absDelta, sig.OnDeltaMinW, sig.OnDeltaMaxW);

                detections.Add(new NilmDetection
                {
                    Timestamp = now,
                    ApplianceId = apId,
                    Source = "HART",
                    EstimatedPowerW = Math.Round(est, 1),
                    IsOn = true
                });
            }
            else
            {
                st.IsOn = false;
                st.LastChangeUtc = now;
                st.OnStartUtc = null;

                var standby = meta.HasStandby ? (meta.MinStandbyWatts + meta.MaxStandbyWatts) / 2.0 : 0.0;

                detections.Add(new NilmDetection
                {
                    Timestamp = now,
                    ApplianceId = apId,
                    Source = "HART",
                    EstimatedPowerW = standby,
                    IsOn = false
                });

                st.LastOnObservedDeltaW = null; 
            }

            _logger.LogDebug(
                "NILM HART: match {ApplianceId} {Edge} Δ={Delta:+0;-0}W score={Score:0.00}",
                apId, isOnEdge ? "ON" : "OFF", (int)totalDelta, bestScore);
        }
        else
        {
            _logger.LogTrace("NILM HART: fronte non accettato Δ={Delta:+0;-0}W (bestScore={Score:0.00})",
                (int)totalDelta, bestScore);
        }

        _pendingByMeter[meterId] = null;
    }

    private ImmutableDictionary<int, CachedSignature> BuildSnapshot(ApplianceSignature? overrideSig)
    {
        if (overrideSig == null) return _defaultSignatures;

        var b = ImmutableDictionary.CreateBuilder<int, CachedSignature>();

        if (_defaultSignatures.TryGetValue(overrideSig.ApplianceId, out var cached))
            b[overrideSig.ApplianceId] = cached with { Sig = overrideSig };
        else
        {
            try
            {
                using var ctxSync = _factory.CreateDbContext();
                var app = ctxSync.Appliances
                    .AsNoTracking()
                    .FirstOrDefault(a => a.Id == overrideSig.ApplianceId && a.Enable && a.EnableNilm);
                if (app != null) b[overrideSig.ApplianceId] = new CachedSignature(app.Id, overrideSig, app);
            }
            catch { }
        }

        return b.ToImmutable();
    }

    private static bool InTol(double v, double min, double max)
    {
        var lo = min * (1 - RANGE_TOL_FRACTION);
        var hi = max * (1 + RANGE_TOL_FRACTION);
        return v >= lo && v <= hi;
    }

    private static double DistScore(double v, double min, double max)
    {
        double center = (min + max) / 2.0;
        double span = Math.Max(1.0, (max - min));
        return Math.Abs(v - center) / span; // 0 = perfetto
    }

    private static double ClipToBand(double v, double min, double max)
        => Math.Min(Math.Max(v, min), max);
}
