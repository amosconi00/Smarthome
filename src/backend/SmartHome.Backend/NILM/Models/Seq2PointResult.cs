namespace SmartHome.Backend.NILM.Models;

public sealed record Seq2PointResult(
    System.DateTime CenterTimestampUtc,
    double PredictedWatts,
    bool PredictedOn,
    double ThresholdW);

