namespace SmartHome.Backend.Dto.Plugs
{
    public record PlugDto(
        int Id,
        string Name,
        string? Manufacturer,
        string? Model,
        string? MacAddress,
        string? IpAddress,
        string? FirmwareVersion,
        bool IsOnline,
        int? ApplianceId
    );
}
