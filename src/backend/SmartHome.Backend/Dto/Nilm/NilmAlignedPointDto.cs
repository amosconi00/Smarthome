namespace SmartHome.Backend.Dto.Nilm;

public record NilmAlignedPointDto(DateTime TimestampUtc, double? ApplianceAvgW, double? CoAvgW, double? S2pAvgW);
