using System.ComponentModel.DataAnnotations;

namespace SmartHome.Backend.Dto.PowerMeters
{
    public class PowerMeterCreateUpdateDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Level { get; set; }

        [Required]
        [MaxLength(100)]
        public string Code { get; set; } = string.Empty;
    }
}
