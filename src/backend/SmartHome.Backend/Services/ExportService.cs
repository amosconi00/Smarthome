using Microsoft.EntityFrameworkCore;
using SmartHome.Backend.Dal;
using System.Globalization;
using System.Text;

namespace SmartHome.Backend.Services;

public class ExportService : IExportService
{
    private readonly IDbContextFactory<SmartHomeDbContext> _contextFactory;

    public ExportService(IDbContextFactory<SmartHomeDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<(string fileName, byte[] content)> ExportPowerMeterAsync(int powerMeterId, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var baseQuery = ctx.PowerMeterTelemetries.AsNoTracking();
        var cc = baseQuery.Count();
        if (from is null || to is null)
        {
            var minTs = await baseQuery.MinAsync(t => (DateTime?)t.Timestamp, cancellationToken) ?? DateTime.MinValue;
            var maxTs = await baseQuery.MaxAsync(t => (DateTime?)t.Timestamp, cancellationToken) ?? minTs;
            from ??= minTs;
            to ??= maxTs;
        }

        var start = from!.Value.ToUniversalTime();
        var end = to!.Value.ToUniversalTime();
        if (end < start) throw new ArgumentException("'to' must be >= 'from'");

        var query = baseQuery
            .Where(t => t.Timestamp >= start && t.Timestamp <= end)
            .OrderBy(t => t.Timestamp)
            .Select(t => new { t.Timestamp, Power = t.PowerW ?? 0d })
            .AsAsyncEnumerable();


        await using var ms = new MemoryStream(capacity: 1024 * 256);
        await using (var writer = new StreamWriter(ms, new UTF8Encoding(false), 4096, leaveOpen: true))
        {
            await writer.WriteLineAsync("timestamp_utc,power_w");
            long rows = 0;
            await foreach (var row in query.WithCancellation(cancellationToken))
            {
                rows++;
                await writer.WriteLineAsync(string.Concat(row.Timestamp.ToString("O", CultureInfo.InvariantCulture), ",", row.Power.ToString(CultureInfo.InvariantCulture)));
            }
            if (rows == 0)
            {
                // Nessun dato: aggiungo commento (riga vuota informativa)
                await writer.WriteLineAsync("# nessun dato nel range richiesto");
            }
        }

        var fileName = $"powermeter_{powerMeterId}_{start:yyyyMMddHHmmss}_{end:yyyyMMddHHmmss}.csv";
        return (fileName, ms.ToArray());
    }

    public async Task<(string fileName, byte[] content)> ExportApplianceAsync(int applianceId, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var baseQuery = ctx.ApplianceTelemetries.AsNoTracking().Where(t => t.ApplianceId == applianceId);


        if (from is null || to is null)
        {
            var minTs = await baseQuery.MinAsync(t => (DateTime?)t.Timestamp, cancellationToken) ?? DateTime.MinValue;
            var maxTs = await baseQuery.MaxAsync(t => (DateTime?)t.Timestamp, cancellationToken) ?? minTs;
            from ??= minTs;
            to ??= maxTs;
        }

        var start = from!.Value.ToUniversalTime();
        var end = to!.Value.ToUniversalTime();
        if (end < start) throw new ArgumentException("'to' must be >= 'from'");

        // Recupero nome categoria appliance (o null se non presente)
        var categoryName = await ctx.Appliances.AsNoTracking()
            .Where(a => a.Id == applianceId)
            .Select(a => a.CategoryAppliance != null ? a.CategoryAppliance.Name : null)
            .FirstOrDefaultAsync(cancellationToken);

        var query = baseQuery
            .Where(t => t.Timestamp >= start && t.Timestamp <= end)
            .OrderBy(t => t.Timestamp)
            .Select(t => new { t.Timestamp, Power = t.PowerWatts ?? 0d })
            .AsAsyncEnumerable();

        await using var ms = new MemoryStream(capacity: 1024 * 64);
        await using (var writer = new StreamWriter(ms, new UTF8Encoding(false), 4096, leaveOpen: true))
        {
            await writer.WriteLineAsync("timestamp_utc,power_w");
            long rows = 0;
            await foreach (var row in query.WithCancellation(cancellationToken))
            {
                rows++;
                await writer.WriteLineAsync(string.Concat(row.Timestamp.ToString("O", CultureInfo.InvariantCulture), ",", row.Power.ToString(CultureInfo.InvariantCulture)));
            }
            if (rows == 0)
            {
                await writer.WriteLineAsync("# nessun dato nel range richiesto");
            }
        }

        // Nome file basato sul category name
        var fileName = $"{categoryName ?? "unknow"}_{start:yyyyMMddHHmmss}_{end:yyyyMMddHHmmss}.csv";
        return (fileName, ms.ToArray());
    }
}
