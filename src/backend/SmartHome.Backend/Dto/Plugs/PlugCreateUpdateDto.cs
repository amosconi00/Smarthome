using System.ComponentModel.DataAnnotations;

namespace SmartHome.Backend.Dto.Plugs
{
    public class PlugCreateUpdateDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        public string? Manufacturer { get; set; }
        public string? Model { get; set; }
        public string? MacAddress { get; set; }
        public string? IpAddress { get; set; }
        public string? FirmwareVersion { get; set; }
        public bool IsOnline { get; set; }
        public int? ApplianceId { get; set; }
    }
}
