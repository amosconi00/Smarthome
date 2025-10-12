namespace SmartHome.Backend.Settings;

public sealed class MqttSettings
{
    public string? Broker { get; set; }
    public int Port { get; set; } = 1883;
}
