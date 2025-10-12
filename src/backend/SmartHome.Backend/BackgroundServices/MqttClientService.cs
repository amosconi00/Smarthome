using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using SmartHome.Backend.Dal;
using SmartHome.Backend.Dal.Entity;
using SmartHome.Backend.Services; // for IPipelineTelemetryService etc.
using SmartHome.Backend.Notifiers; // moved notifiers namespace
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization; // added
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets; // for SocketException
using Microsoft.Extensions.Options; // added for MqttSettings
using SmartHome.Backend.Settings; // added

namespace SmartHome.Backend.BackgroundServices;

public class MqttClientService : BackgroundService
{
    private readonly ILogger<MqttClientService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPipelineTelemetryService _pipeline;
    private readonly IPlugChangeNotifier _plugNotifier;
    private readonly IPowerMeterChangeNotifier _powerMeterNotifier; // new
    private readonly string _broker; // now from settings
    private readonly int _port; // now from settings
    private IMqttClient? _mqttClient;
    private IMqttClientOptions? _options;

    // Cache plug name -> (PlugId, ApplianceId?)
    private volatile Dictionary<string, (int plugId, int? applianceId)> _plugCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _plugCacheLock = new();
    // Cache power meter name -> meterId
    private volatile Dictionary<string, int> _powerMeterCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _powerMeterCacheLock = new();

    private readonly ConcurrentDictionary<string, bool> _seenTopics = new();
    private readonly ConcurrentDictionary<string, bool> _subscribedPlugTopics = new();
    private readonly ConcurrentDictionary<string, bool> _subscribedPowerMeterTopics = new();

    // Connection state handling -------------------------------------------------
    private enum MqttConnectionState { Disconnected = 0, Connecting = 1, Connected = 2, Disconnecting = 3 }
    private int _connectionState = (int)MqttConnectionState.Disconnected;
    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private int _consecutiveFailures = 0;
    private DateTime _lastFailureUtc = DateTime.MinValue;
    private readonly Random _jitter = new();

    private bool TryBeginConnect() =>
        Interlocked.CompareExchange(ref _connectionState,
            (int)MqttConnectionState.Connecting,
            (int)MqttConnectionState.Disconnected) == (int)MqttConnectionState.Disconnected;

    private void MarkDisconnected()
    {
        Interlocked.Exchange(ref _connectionState, (int)MqttConnectionState.Disconnected);
    }

    private void MarkConnected()
    {
        Interlocked.Exchange(ref _connectionState, (int)MqttConnectionState.Connected);
    }

    private TimeSpan GetBackoffDelay()
    {
        var expSeconds = Math.Min(60, Math.Pow(2, Math.Min(6, _consecutiveFailures))); 
        var jitterMs = _jitter.Next(0, 500);
        return TimeSpan.FromSeconds(expSeconds) + TimeSpan.FromMilliseconds(jitterMs);
    }

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_mqttClient?.IsConnected == true)
        {
            if (Volatile.Read(ref _connectionState) != (int)MqttConnectionState.Connected)
                MarkConnected();
            return;
        }

        if (!TryBeginConnect()) return; // someone else is connecting

        await _connectGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_mqttClient?.IsConnected == true)
            {
                MarkConnected();
                return;
            }

            if (_mqttClient == null || _options == null)
            {
                _logger.LogWarning("MQTT client/options not initialized yet.");
                MarkDisconnected();
                return;
            }

            try
            {
                _logger.LogDebug("Attempting MQTT connect to {Broker}:{Port} (attempt {Attempt})", _broker, _port, _consecutiveFailures + 1);
                var res = await _mqttClient.ConnectAsync(_options, ct).ConfigureAwait(false);
                if (res.ResultCode != MqttClientConnectResultCode.Success)
                {
                    _consecutiveFailures++;
                    _lastFailureUtc = DateTime.UtcNow;
                    _logger.LogError("MQTT connection failed with result code: {ResultCode} (attempt {Attempt})", res.ResultCode, _consecutiveFailures);
                    var delay = GetBackoffDelay();
                    MarkDisconnected();
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                    return;
                }

                _logger.LogInformation("Connected to MQTT broker {Broker}:{Port}", _broker, _port);
                _consecutiveFailures = 0;
                MarkConnected();

                await RefreshPlugSubscriptionsAsync(ct).ConfigureAwait(false);
                await RefreshPowerMeterSubscriptionsAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                MarkDisconnected();
                throw;
            }
            catch (SocketException sex)
            {
                _consecutiveFailures++;
                _lastFailureUtc = DateTime.UtcNow;
                _logger.LogWarning(sex, "Socket error during MQTT connect (attempt {Attempt}): {Code}", _consecutiveFailures, sex.SocketErrorCode);
                var delay = GetBackoffDelay();
                MarkDisconnected();
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                _lastFailureUtc = DateTime.UtcNow;
                _logger.LogError(ex, "Unexpected exception during MQTT connect (attempt {Attempt})", _consecutiveFailures);
                var delay = GetBackoffDelay();
                MarkDisconnected();
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            if (_connectGate.CurrentCount == 0)
                _connectGate.Release();
            if (_mqttClient?.IsConnected != true)
                MarkDisconnected();
        }
    }

    public MqttClientService(ILogger<MqttClientService> logger, IServiceScopeFactory scopeFactory, IPipelineTelemetryService pipeline, IPlugChangeNotifier plugNotifier, IPowerMeterChangeNotifier powerMeterNotifier, IOptions<MqttSettings> mqttOptions)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _pipeline = pipeline;
        _plugNotifier = plugNotifier;
        _powerMeterNotifier = powerMeterNotifier;
        var settings = mqttOptions?.Value ?? new MqttSettings();
        _broker = string.IsNullOrWhiteSpace(settings.Broker) ? "localhost" : settings.Broker;
        _port = settings.Port <= 0 ? 1883 : settings.Port;
        _plugNotifier.OnChanged += () => _ = ReloadPlugsAndRefreshSubscriptionsAsync(CancellationToken.None);
        _powerMeterNotifier.OnChanged += () => _ = ReloadPowerMetersAndRefreshSubscriptionsAsync(CancellationToken.None);
    }

    private async Task ReloadPlugsAndRefreshSubscriptionsAsync(CancellationToken ct)
    {
        try
        {
            await LoadPlugCacheAsync(ct);
            await RefreshPlugSubscriptionsAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed reloading plug cache");
        }
    }

    private async Task ReloadPowerMetersAndRefreshSubscriptionsAsync(CancellationToken ct)
    {
        try
        {
            await LoadPowerMeterCacheAsync(ct);
            await RefreshPowerMeterSubscriptionsAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed reloading power meter cache");
        }
    }

    private async Task LoadPlugCacheAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var plugService = scope.ServiceProvider.GetRequiredService<IPlugService>();
        var plugs = await plugService.GetAllAsync(ct);
        var dict = new Dictionary<string, (int plugId, int? applianceId)>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in plugs)
        {
            if (!string.IsNullOrWhiteSpace(p.Name))
            {
                var key = p.Name.Trim().Replace(" ", "_");
                dict[key] = (p.Id, p.ApplianceId);
            }
        }
        lock (_plugCacheLock)
        {
            _plugCache = dict;
        }
        _logger.LogInformation("Plug cache loaded: {Count} entries", dict.Count);
    }

    private async Task LoadPowerMeterCacheAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPowerMeterService>();
        var meters = await service.GetAllAsync(ct);
        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in meters)
        {
            if (!string.IsNullOrWhiteSpace(m.Name))
            {
                var key = m.Name.Trim().Replace(" ", "_");
                dict[key] = m.Id;
            }
        }
        lock (_powerMeterCacheLock)
        {
            _powerMeterCache = dict;
        }
        _logger.LogInformation("Power meter cache loaded: {Count} entries", dict.Count);
    }

    public Task<IList<string>> GetTopicsAsync() => Task.FromResult<IList<string>>(new List<string>());

    private async Task SubscribeTopicAsync(string topic, int qos = 0)
    {
        if (_mqttClient != null && _mqttClient.IsConnected)
        {
            await _mqttClient.SubscribeAsync(new MQTTnet.Client.Subscribing.MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => { f.WithTopic(topic).WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)qos); })
                .Build());
            _logger.LogInformation("Subscribed to topic: {Topic}", topic);
        }
        else
        {
            _logger.LogWarning("MQTT client is not connected. Cannot subscribe to topic: {Topic}", topic);
        }
    }

    private async Task UnsubscribeTopicAsync(string topic)
    {
        if (_mqttClient != null && _mqttClient.IsConnected)
        {
            try
            {
                await _mqttClient.UnsubscribeAsync(topic);
                _logger.LogInformation("Unsubscribed from topic: {Topic}", topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unsubscribe from topic: {Topic}", topic);
            }
        }
    }

    private static string ToTopicSafe(string? name) => string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim().Replace(' ', '_');
    private static string BuildPlugTopic(string plugName) => $"smarthome001/meters/plugs/{ToTopicSafe(plugName)}";
    private static string BuildPowerMeterTopic(string meterName) => $"smarthome001/meters/power_meters/{ToTopicSafe(meterName)}";

    private Task<HashSet<string>> GetDesiredPlugTopicsAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, (int plugId, int? applianceId)> snapshot;
        lock (_plugCacheLock) snapshot = _plugCache;
        var topics = snapshot.Keys.Select(BuildPlugTopic).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(topics);
    }

    private Task<HashSet<string>> GetDesiredPowerMeterTopicsAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, int> snapshot;
        lock (_powerMeterCacheLock) snapshot = _powerMeterCache;
        var topics = snapshot.Keys.Select(BuildPowerMeterTopic).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(topics);
    }

    private async Task RefreshPlugSubscriptionsAsync(CancellationToken cancellationToken)
    {
        var desired = await GetDesiredPlugTopicsAsync(cancellationToken);
        var current = _subscribedPlugTopics.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var toRemove in current.Except(desired)) { await UnsubscribeTopicAsync(toRemove); _subscribedPlugTopics.TryRemove(toRemove, out _); }
        foreach (var toAdd in desired.Except(current)) { await SubscribeTopicAsync(toAdd); _subscribedPlugTopics[toAdd] = true; }
    }

    private async Task RefreshPowerMeterSubscriptionsAsync(CancellationToken cancellationToken)
    {
        var desired = await GetDesiredPowerMeterTopicsAsync(cancellationToken);
        var current = _subscribedPowerMeterTopics.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var toRemove in current.Except(desired)) { await UnsubscribeTopicAsync(toRemove); _subscribedPowerMeterTopics.TryRemove(toRemove, out _); }
        foreach (var toAdd in desired.Except(current)) { await SubscribeTopicAsync(toAdd); _subscribedPowerMeterTopics[toAdd] = true; }
    }

    private static double? TryGetNumber(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var d1)) return d1;
        if (element.ValueKind == JsonValueKind.String && double.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d2)) return d2;
        return null;
    }

    private static bool TryExtractTimestamp(string payload, out DateTime timestampUtc)
    {
        timestampUtc = default;
        try
        {
            if (!payload.TrimStart().StartsWith("{")) return false;
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            foreach (var key in new[] { "timestamp", "ts", "time", "t", "date", "datetime" })
            {
                if (!root.TryGetProperty(key, out var el)) continue;
                if (el.ValueKind == JsonValueKind.Number)
                {
                    if (el.TryGetInt64(out var l))
                    {
                        if (l > 1_000_000_000_000) { timestampUtc = DateTimeOffset.FromUnixTimeMilliseconds(l).UtcDateTime; return true; }
                        if (l > 1_000_000_000) { timestampUtc = DateTimeOffset.FromUnixTimeSeconds(l).UtcDateTime; return true; }
                    }
                    if (el.TryGetDouble(out var d))
                    {
                        if (d > 1_000_000_000_000) { timestampUtc = DateTimeOffset.FromUnixTimeMilliseconds((long)d).UtcDateTime; return true; }
                        if (d > 1_000_000_000) { timestampUtc = DateTimeOffset.FromUnixTimeSeconds((long)d).UtcDateTime; return true; }
                    }
                }
                else if (el.ValueKind == JsonValueKind.String)
                {
                    var s = el.GetString(); if (string.IsNullOrWhiteSpace(s)) continue;
                    if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto)) { timestampUtc = dto.UtcDateTime; return true; }
                    if (long.TryParse(s, out var l2))
                    {
                        if (l2 > 1_000_000_000_000) { timestampUtc = DateTimeOffset.FromUnixTimeMilliseconds(l2).UtcDateTime; return true; }
                        if (l2 > 1_000_000_000) { timestampUtc = DateTimeOffset.FromUnixTimeSeconds(l2).UtcDateTime; return true; }
                    }
                }
            }
        }
        catch { }
        return false;
    }

    private async Task HandlePlugTelemetryAsync(string plugTopicName, string payload, CancellationToken ct)
    {
        var safeName = plugTopicName;
        Dictionary<string, (int plugId, int? applianceId)> snapshot; lock (_plugCacheLock) snapshot = _plugCache;
        if (!snapshot.TryGetValue(safeName, out var entry)) { _logger.LogWarning("Plug not found for topic name (cache miss): {Name}", safeName); return; }
        if (entry.applianceId is null) { _logger.LogInformation("Plug {Name} has no related appliance. Skipping ApplianceTelemetry save.", safeName); return; }

        double powerW = 0; DateTime timestampUtc; var hasTimestamp = TryExtractTimestamp(payload, out timestampUtc);
        try
        {
            if (payload.TrimStart().StartsWith("{"))
            {
                using var doc = JsonDocument.Parse(payload); var root = doc.RootElement; foreach (var key in new[] { "apower", "power", "powerW", "power_w", "watts", "w" }) if (root.TryGetProperty(key, out var el)) { var d = TryGetNumber(el); if (d.HasValue) { powerW = d.Value; break; } }
            }
            else if (double.TryParse(payload, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) powerW = d;
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to parse plug payload for {Name}", safeName); }
        if (!hasTimestamp) { _logger.LogWarning("Telemetry plug {Name} skipped: timestamp non presente nel payload", safeName); return; }
        var telemetry = new ApplianceTelemetry { Timestamp = timestampUtc, ApplianceId = entry.applianceId.Value, PowerWatts = powerW };
        await _pipeline.ProcessApplianceTelemetry(telemetry, ct);
    }

    private async Task HandlePowerMeterTelemetryAsync(string meterTopicName, string payload, CancellationToken ct)
    {
        var safeName = meterTopicName;
        Dictionary<string, int> snapshot; lock (_powerMeterCacheLock) snapshot = _powerMeterCache;
        if (!snapshot.TryGetValue(safeName, out var meterId)) { _logger.LogWarning("Power meter not found for topic name (cache miss): {Name}", safeName); return; }

        double? voltage = null, current = null, power = null, energy = null; DateTime timestampUtc; var hasTimestamp = TryExtractTimestamp(payload, out timestampUtc);
        try
        {
            if (payload.TrimStart().StartsWith("{"))
            {
                using var doc = JsonDocument.Parse(payload); var root = doc.RootElement;
                (string[] keys, Action<double?> setter)[] map = new[]
                {
                    (new[] { "voltage", "voltageV", "v" }, new Action<double?>(v => voltage = v)),
                    (new[] { "current", "currentA", "i", "a" }, new Action<double?>(v => current = v)),
                    (new[] { "apower", "power", "powerW", "w" }, new Action<double?>(v => power = v)),
                    (new[] { "aenergy", "energy", "energyWh", "wh", "kwh" }, new Action<double?>(v => energy = v))
                };
                foreach (var (keys, setter) in map)
                {
                    foreach (var key in keys)
                    {
                        if (root.TryGetProperty(key, out var el)) { var d = TryGetNumber(el); if (d.HasValue) { setter(d); break; } }
                    }
                }
            }
            else if (double.TryParse(payload, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) power = d;
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to parse power meter payload for {Name}", safeName); }
        if (!hasTimestamp) { _logger.LogWarning("Telemetry power meter {Name} skipped: timestamp non presente nel payload", safeName); return; }
        var telemetry = new PowerMeterTelemetry { Timestamp = timestampUtc, PowerMeterId = meterId, VoltageV = voltage, CurrentA = current, PowerW = power, EnergyWh = energy };
        await _pipeline.ProcessPowerMeterTelemetry(telemetry, ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await LoadPlugCacheAsync(stoppingToken);
        await LoadPowerMeterCacheAsync(stoppingToken);

        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();
        _options = new MqttClientOptionsBuilder().WithTcpServer(_broker, _port).Build();

        _mqttClient.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(async e =>
        {
            try
            {
                if (!string.IsNullOrEmpty(e.ApplicationMessage.Topic)) _seenTopics.TryAdd(e.ApplicationMessage.Topic, true);
                var payloadString = System.Text.Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                var topic = e.ApplicationMessage.Topic ?? string.Empty;
                _logger.LogInformation("Received message on topic {Topic}: {Payload}", topic, payloadString);
                if (topic.StartsWith("smarthome001/meters/plugs/", StringComparison.OrdinalIgnoreCase))
                { var name = topic.Substring("smarthome001/meters/plugs/".Length); await HandlePlugTelemetryAsync(name, payloadString, stoppingToken); }
                else if (topic.StartsWith("smarthome001/meters/power_meters/", StringComparison.OrdinalIgnoreCase))
                { var name = topic.Substring("smarthome001/meters/power_meters/".Length); await HandlePowerMeterTelemetryAsync(name, payloadString, stoppingToken); }
            }
            catch (Exception ex) { _logger.LogError(ex, "Error processing MQTT message"); }
        });

        _mqttClient.DisconnectedHandler = new MQTTnet.Client.Disconnecting.MqttClientDisconnectedHandlerDelegate(async e =>
        {
            _logger.LogWarning("MQTT disconnected (WasConnected={WasConnected})", e.ClientWasConnected);
            MarkDisconnected();
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        });

        while (!stoppingToken.IsCancellationRequested)
        {
            await EnsureConnectedAsync(stoppingToken);
            var delay = (_mqttClient?.IsConnected == true) ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(2);
            try { await Task.Delay(delay, stoppingToken); } catch (OperationCanceledException) { }
        }
    }
}
