namespace SmartHome.Backend.Notifiers;
public interface IApplianceSignatureChangeNotifier { event Action? OnChanged; void NotifyChanged(); }
public sealed class ApplianceSignatureChangeNotifier : IApplianceSignatureChangeNotifier { public event Action? OnChanged; public void NotifyChanged() => OnChanged?.Invoke(); }
