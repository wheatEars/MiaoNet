using Celeste.Mod.UIHelper.State;

namespace Celeste.Mod.MiaoNet.Client.UI.Settings;

/// <summary>Bridges MiaoNet settings notifications to UIHelper observables.</summary>
public sealed class MiaoNetSettingsObserver : IDisposable
{
    private readonly MiaoNetModuleSettings settings;
    private readonly Dictionary<string, IBinding> bindings = new(StringComparer.Ordinal);

    public MiaoNetSettingsObserver(MiaoNetModuleSettings settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        settings.SettingsChanged += OnSettingsChanged;
    }

    public IObservableValue<T> Observe<T>(string key, Func<MiaoNetModuleSettings, T> selector, SettingsCategory category = SettingsCategory.VisualsUI)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(selector);
        if (bindings.TryGetValue(key, out var existing))
        {
            if (existing is Binding<T> typed) return typed.Value;
            throw new InvalidOperationException($"Setting key '{key}' was registered with another type.");
        }
        var binding = new Binding<T>(selector, category, selector(settings));
        bindings.Add(key, binding);
        return binding.Value;
    }

    private void OnSettingsChanged(MiaoNetModuleSettings _, SettingsCategory changedCategory)
    {
        foreach (var binding in bindings.Values)
            binding.Refresh(settings, changedCategory);
    }

    public void Dispose()
    {
        settings.SettingsChanged -= OnSettingsChanged;
        bindings.Clear();
    }

    private interface IBinding { void Refresh(MiaoNetModuleSettings settings, SettingsCategory category); }

    private sealed class Binding<T> : IBinding
    {
        private readonly Func<MiaoNetModuleSettings, T> selector;
        private readonly SettingsCategory category;

        public Binding(Func<MiaoNetModuleSettings, T> selector, SettingsCategory category, T initial)
        {
            this.selector = selector;
            this.category = category;
            Value = new ObservableValue<T>(initial, $"miaonet/settings/{typeof(T).Name}");
        }

        public ObservableValue<T> Value { get; }

        public void Refresh(MiaoNetModuleSettings settings, SettingsCategory changedCategory)
        {
            if (changedCategory == category)
                Value.Value = selector(settings);
        }
    }
}
