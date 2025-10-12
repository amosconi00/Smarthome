using System.ComponentModel.DataAnnotations;

namespace SmartHome.Backend.Dal.Entity;

public class BaseTelemetry
{
    [Key]
    public DateTime Timestamp { get; set; } // Time-series aspect
}