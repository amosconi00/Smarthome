namespace SmartHome.Backend.Dto.Nilm;

public record NilmSampleDto(DateTime TimestampUtc, double? ValueW);

public record NilmSeriesMetrics(double? CoMae, double? CoRmse, double? S2pMae, double? S2pRmse, double? HartMae, double? HartRmse);

public record NilmSeriesDto(
    IReadOnlyList<NilmSampleDto> ApplianceTelemetry,
    IReadOnlyList<NilmSampleDto> CoDetections,
    IReadOnlyList<NilmSampleDto> S2pDetections,
    NilmSeriesMetrics Metrics
);

// DTO ridotto per risposta solo Hart (senza S2P)
public record NilmSeriesHartDto(
    IReadOnlyList<NilmSampleDto> ApplianceTelemetry,
    IReadOnlyList<NilmSampleDto> CoDetections,
    NilmSeriesMetrics Metrics
);
