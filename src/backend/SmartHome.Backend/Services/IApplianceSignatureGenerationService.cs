using SmartHome.Backend.Dto.Signatures;

namespace SmartHome.Backend.Services;

public interface IApplianceSignatureGenerationService
{
    /// <summary>
    /// Generate a new ApplianceSignature from historical telemetry.
    /// If fromUtc/toUtc are null the whole history is considered.
    /// Returns null if appliance or sufficient telemetry not found.
    /// </summary>
    Task<ApplianceSignatureDto?> GenerateAsync(
        int applianceId,
        DateTime? fromUtc,
        DateTime? toUtc,
        bool setAsDefault,
        CancellationToken ct);
}