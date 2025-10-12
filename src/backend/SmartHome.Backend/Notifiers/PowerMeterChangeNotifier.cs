namespace SmartHome.Backend.Notifiers;
public interface IPowerMeterChangeNotifier { event Action? OnChanged; void NotifyChanged(); }
public class PowerMeterChangeNotifier : IPowerMeterChangeNotifier { public event Action? OnChanged; public void NotifyChanged() => OnChanged?.Invoke(); }
