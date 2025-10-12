using SmartHome.Backend.Dto.Nilm;
using SmartHome.Backend.Dal.Entity;

namespace SmartHome.Backend.Services;

public interface INilmService
{
    Task<NilmSeriesDto> GetSeriesCompareAsync(int applianceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
    Task<IReadOnlyList<NilmAlignedPointDto>> GetSeriesDownsampledAsync(int applianceId, DateTime fromUtc, DateTime toUtc, int size, string unit, CancellationToken ct);
    Task<NilmSeriesDto> RecomputeFromMainsAsync(int applianceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
    Task<NilmSeriesDto> RecomputeFromMainsAsync(int applianceId, int signatureId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
    Task<NilmSeriesHartDto> RecomputeHartFromMainsAsync(int applianceId, int signatureId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
    Task<NilmSeriesHartDto> RecomputeHartWhatIfAsync(int applianceId, NilmWhatIfSignatureDto signature, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
}
