using System.ComponentModel.DataAnnotations;

namespace SmartHome.Backend.Dto.Appliances
{
    public class ApplianceCreateUpdateDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public string? Model { get; set; }
        public string? SerialNumber { get; set; }
        [Range(0, double.MaxValue)]
        public double OffThresholdWatts { get; set; }
        [Range(0, double.MaxValue)]
        public double OnThresholdWatts { get; set; }
        [Range(0, double.MaxValue)]
        public double SecondsMinDuration { get; set; }
        public bool HasStandby { get; set; }
        [Range(0, double.MaxValue)]
        public double MinStandbyWatts { get; set; }
        [Range(0, double.MaxValue)]
        public double MaxStandbyWatts { get; set; }
        public bool Enable { get; set; }
        public bool EnableNilm { get; set; }
        public int? CategoryApplianceId { get; set; }
    }
}
