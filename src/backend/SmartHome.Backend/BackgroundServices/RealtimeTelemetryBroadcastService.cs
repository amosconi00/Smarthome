using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using SmartHome.Backend.Dal;
using SmartHome.Backend.Hubs;
using SmartHome.Backend.Dto.Realtime;

namespace SmartHome.Backend.BackgroundServices;

public class RealtimeTelemetryBroadcastService : BackgroundService
{
    private readonly IDbContextFactory<SmartHomeDbContext> _ctxFactory;
    private readonly IHubContext<TelemetryHub> _hub;
    private readonly ILogger<RealtimeTelemetryBroadcastService> _logger;
    private readonly TimeSpan _period = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ApplianceTelemetryFreshness = TimeSpan.FromSeconds(30);

    public RealtimeTelemetryBroadcastService(IDbContextFactory<SmartHomeDbContext> ctxFactory,
        IHubContext<TelemetryHub> hub,
        ILogger<RealtimeTelemetryBroadcastService> logger)
    {
        _ctxFactory = ctxFactory;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await BroadcastAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore broadcast realtime telemetry");
            }
            await Task.Delay(_period, stoppingToken);
        }
    }

    private async Task BroadcastAsync(CancellationToken ct)
    {
        await using var ctx = await _ctxFactory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;
        var freshnessThreshold = now - ApplianceTelemetryFreshness;

        var mains = await ctx.PowerMeterTelemetries.AsNoTracking()
            .OrderByDescending(t => t.Timestamp)
            .Select(t => t.PowerW)
            .FirstOrDefaultAsync(ct);

        var appliances = await ctx.Appliances.AsNoTracking()
            .Select(a => new { a.Id, a.Name, a.EnableNilm })
            .ToListAsync(ct);

        var latestTeleAgg = from t in ctx.ApplianceTelemetries.AsNoTracking()
                            group t by t.ApplianceId into g
                            select new { ApplianceId = g.Key, MaxTimestamp = g.Max(x => x.Timestamp) };

        var telemetry = await (from t in ctx.ApplianceTelemetries.AsNoTracking()
                               join lt in latestTeleAgg
                                   on new { t.ApplianceId, t.Timestamp } equals new { lt.ApplianceId, Timestamp = lt.MaxTimestamp }
                               select new { t.ApplianceId, t.PowerWatts, t.Timestamp })
                              .ToListAsync(ct);
        var teleDict = telemetry.ToDictionary(x => x.ApplianceId);

        var list = new List<RealtimeAppliancePointDto>(appliances.Count);
        foreach (var a in appliances)
        {
            double? teleW = null;
            if (teleDict.TryGetValue(a.Id, out var tele) && tele.Timestamp >= freshnessThreshold)
            {
                teleW = tele.PowerWatts;
            }
            list.Add(new RealtimeAppliancePointDto(a.Id, a.Name, teleW, 0, 0));
        }

        var snapshot = new RealtimeTelemetrySnapshotDto(now, mains, list);
        await _hub.Clients.All.SendAsync("telemetry", snapshot, ct);
    }
}
