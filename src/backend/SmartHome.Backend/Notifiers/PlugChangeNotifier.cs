namespace SmartHome.Backend.Notifiers;
public interface IPlugChangeNotifier { event Action? OnChanged; void NotifyChanged(); }
public class PlugChangeNotifier : IPlugChangeNotifier { public event Action? OnChanged; public void NotifyChanged() => OnChanged?.Invoke(); }
