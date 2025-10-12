namespace SmartHome.Backend.Dto.PowerMeters
{
    public record PowerMeterDto(
        int Id,
        string Name,
        string? Description,
        int Level,
        string Code
    );
}
