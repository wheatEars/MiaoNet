using Celeste.Mod.UIHelper.Events;
using Celeste.Mod.UIHelper.Runtime;

namespace Celeste.Mod.MiaoNet.Client.UI.Input;

/// <summary>
/// Converts both raw keyboard/text input and MiaoNet's configurable virtual
/// buttons into one UIHelper event stream. The adapter deliberately accepts
/// delegates instead of ButtonBinding, keeping this layer independent from the
/// settings serialization type.
/// </summary>
public sealed class MiaoNetUiInputAdapter : IDisposable
{
    private sealed record ActionBinding(
        string EventType,
        Func<bool> IsActive,
        bool Repeat,
        Action? Consume
    );

    private readonly UiRuntime runtime;
    private readonly CelesteInputAdapter keyboard;
    private readonly List<ActionBinding> bindings = [];
    private bool disposed;

    public UiEventBus Events => runtime.Events;

    public MiaoNetUiInputAdapter(UiRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        keyboard = new CelesteInputAdapter(runtime);
    }

    /// <summary>Registers a one-frame action such as a toggle or emote key.</summary>
    public IDisposable RegisterPressed(
        string eventType,
        Func<bool> isPressed,
        Action? consume = null
    ) => Register(eventType, isPressed, repeat: false, consume);

    /// <summary>Registers a held action such as player-list scrolling.</summary>
    public IDisposable RegisterHeld(string eventType, Func<bool> isHeld)
        => Register(eventType, isHeld, repeat: true, consume: null);

    public void EnableTextInput()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        keyboard.EnableTextInput();
    }

    public void DisableTextInput()
    {
        if (!disposed)
            keyboard.DisableTextInput();
    }

    /// <summary>
    /// Call once after MInput.Update. Raw keyboard events are emitted first,
    /// followed by configured MiaoNet action events.
    /// </summary>
    public void Poll(float deltaTime)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        keyboard.Poll(deltaTime);

        foreach (var binding in bindings.ToArray())
        {
            if (!binding.IsActive())
                continue;

            runtime.Broadcast(new MiaoNetActionUiEvent(
                binding.EventType,
                binding.Repeat,
                binding.Consume
            ));
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        bindings.Clear();
        keyboard.Dispose();
    }

    private IDisposable Register(
        string eventType,
        Func<bool> isActive,
        bool repeat,
        Action? consume
    )
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(eventType);
        ArgumentNullException.ThrowIfNull(isActive);

        var binding = new ActionBinding(eventType, isActive, repeat, consume);
        bindings.Add(binding);
        return new Registration(() => bindings.Remove(binding));
    }

    private sealed class Registration(Action unregister) : IDisposable
    {
        private Action? action = unregister;

        public void Dispose()
            => Interlocked.Exchange(ref action, null)?.Invoke();
    }
}
