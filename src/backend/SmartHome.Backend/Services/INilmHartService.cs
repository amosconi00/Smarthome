using SmartHome.Backend.Dal.Entity;

namespace SmartHome.Backend.Services;

/// <summary>
/// Interfaccia servizio NILM stile Hart (edge matching semplice).
/// </summary>
public interface INilmHartService
{
    Task<IReadOnlyList<NilmDetection>> ProcessPowerMeterTelemetryAsync(PowerMeterTelemetry telemetry, CancellationToken ct);
    Task<IReadOnlyList<NilmDetection>> ProcessPowerMeterTelemetryAsync(PowerMeterTelemetry telemetry, ApplianceSignature? overrideSignature, CancellationToken ct);
}
