namespace SmartHome.Backend.Dto.Realtime;

public record RealtimeAppliancePointDto(
    int ApplianceId,
    string Name,
    double? TelemetryW,
    double? CoEstimatedW,
    double? S2pEstimatedW
);

public record RealtimeTelemetrySnapshotDto(
    DateTime TimestampUtc,
    double? MainsW,
    IReadOnlyList<RealtimeAppliancePointDto> Appliances
);
