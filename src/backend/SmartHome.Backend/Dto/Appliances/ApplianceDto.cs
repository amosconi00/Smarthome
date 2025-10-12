using SmartHome.Backend.Dto.Signatures;

namespace SmartHome.Backend.Dto.Appliances
{
   public record ApplianceDto
    (
        int Id,
        string Name,
        string? Brand,
        string? Model,
        string? SerialNumber,
        double OffThresholdWatts,
        double OnThresholdWatts,
        double SecondsMinDuration,
        bool HasStandby,
        double MinStandbyWatts,
        double MaxStandbyWatts,
        bool Enable,
        bool EnableNilm,
        int? CategoryApplianceId,
        int? SignatureVersion,
        IReadOnlyCollection<ApplianceSignatureDto> Signatures
    );

    // Lightweight list item DTO (no signatures collection)
    public record ApplianceListItemDto
    (
        int Id,
        string Name,
        string? Brand,
        string? Model,
        bool Enable,
        bool EnableNilm,
        int? CategoryApplianceId,
        int? SignatureVersion
    );
}
