using System;
using System.Threading;
using System.Threading.Tasks;

namespace SmartHome.Backend.Services;

public interface IExportService
{
    /// <summary>
    /// Esporta tutte (o intervallo) le telemetrie del power meter in CSV: timestamp_utc,power_w
    /// Se from/to null prende tutto l'intervallo disponibile.
    /// </summary>
    Task<(string fileName, byte[] content)> ExportPowerMeterAsync(int powerMeterId, DateTime? from, DateTime? to, CancellationToken cancellationToken);

    /// <summary>
    /// Esporta tutte (o intervallo) le telemetrie dell'appliance in CSV: timestamp_utc,power_w
    /// Se from/to null prende tutto l'intervallo disponibile.
    /// </summary>
    Task<(string fileName, byte[] content)> ExportApplianceAsync(int applianceId, DateTime? from, DateTime? to, CancellationToken cancellationToken);
}
