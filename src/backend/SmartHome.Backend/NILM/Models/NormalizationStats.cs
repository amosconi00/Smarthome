using System.Text.Json.Serialization;

namespace SmartHome.Backend.NILM.Models;

public sealed record NormalizationStats(
    [property: JsonPropertyName("mains_mean")] double MainsMean,
    [property: JsonPropertyName("mains_std")] double MainsStd,
    [property: JsonPropertyName("target_mean")] double TargetMean,
    [property: JsonPropertyName("target_std")] double TargetStd)
{
    public static NormalizationStats Load(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Stats JSON non trovato", path);
        var json = File.ReadAllText(path);
        var stats = System.Text.Json.JsonSerializer.Deserialize<NormalizationStats>(json)
                    ?? throw new InvalidOperationException("Formato stats JSON non valido");
        // Evita divisioni per zero
        return stats with
        {
            MainsStd = stats.MainsStd == 0 ? 1.0 : stats.MainsStd,
            TargetStd = stats.TargetStd == 0 ? 1.0 : stats.TargetStd
        };
    }
}

