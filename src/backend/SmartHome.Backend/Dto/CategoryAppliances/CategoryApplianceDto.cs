namespace SmartHome.Backend.Dto.CategoryAppliances
{
    public record CategoryApplianceSignatureDto(
        int CategoryApplianceId,
        double OnDeltaMinW,
        double OnDeltaMaxW,
        double OffDeltaMinW,
        double OffDeltaMaxW,
        double MinOnSeconds,
        double RefractorySeconds
    );

    public record CategoryApplianceDto(
        int Id,
        string Name,
        string? Description,
        CategoryApplianceSignatureDto? Signature
    );
}
