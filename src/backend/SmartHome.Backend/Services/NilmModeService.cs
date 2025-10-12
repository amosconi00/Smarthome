using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace SmartHome.Backend.Services;

public enum NilmMode
{
    Realtime = 0,
    NightlyBatch = 1
}

public sealed class NilmSettings
{
    public NilmMode Mode { get; set; } = NilmMode.Realtime;
}

public interface INilmModeService
{
    NilmMode CurrentMode { get; }
    bool SetMode(NilmMode mode);
}

/// <summary>
/// Gestisce la modalità NILM corrente (Realtime vs NightlyBatch). Thread-safe, runtime only (non persiste su file).
/// </summary>
public sealed class NilmModeService : INilmModeService
{
    private readonly object _sync = new();
    private NilmMode _mode;
    private readonly ILogger<NilmModeService> _logger;

    public NilmModeService(IOptions<NilmSettings> opt, ILogger<NilmModeService> logger)
    {
        _mode = opt.Value.Mode;
        _logger = logger;
        _logger.LogInformation("NILM mode iniziale: {Mode}", _mode);
    }

    public NilmMode CurrentMode { get { lock (_sync) return _mode; } }

    public bool SetMode(NilmMode mode)
    {
        lock (_sync)
        {
            if (_mode == mode) return false;
            _mode = mode;
            _logger.LogInformation("NILM mode cambiata a {Mode}", _mode);
            return true;
        }
    }
}
