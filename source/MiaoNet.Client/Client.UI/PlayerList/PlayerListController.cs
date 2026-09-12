using Celeste.Mod.MiaoNet.Client.UI.Input;
using Celeste.Mod.UIHelper.Controls;

namespace Celeste.Mod.MiaoNet.Client.UI.PlayerList;

/// <summary>Owns player-list visibility and input-to-scroll behavior.</summary>
public sealed class PlayerListController : IDisposable
{
    private readonly List<IDisposable> subscriptions = [];
    private readonly Func<global::Celeste.Mod.MiaoNet.ButtonMode> buttonMode;
    private bool holdSeen;
    private bool keepAtTop;
    private bool disposed;

    public bool IsOpen { get; private set; }
    public ScrollController Scroll { get; } = new();
    public event Action? Opened;
    public event Action? Closed;

    public PlayerListController(
        MiaoNetUiInputAdapter input,
        Func<global::Celeste.Mod.MiaoNet.ButtonMode> buttonMode)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(buttonMode);
        this.buttonMode = buttonMode;

        subscriptions.Add(input.Events.Subscribe<MiaoNetActionUiEvent>(
            MiaoNetUiInputTypes.PlayerListToggle, OnToggle));
        subscriptions.Add(input.Events.Subscribe<MiaoNetActionUiEvent>(
            MiaoNetUiInputTypes.PlayerListScrollUp,
            action => OnScroll(action, 1f)));
        subscriptions.Add(input.Events.Subscribe<MiaoNetActionUiEvent>(
            MiaoNetUiInputTypes.PlayerListScrollDown,
            action => OnScroll(action, -1f)));
    }

    public void BeginFrame() => holdSeen = false;

    public void EndFrame()
    {
        if (buttonMode() == global::Celeste.Mod.MiaoNet.ButtonMode.Hold && IsOpen && !holdSeen)
            Close();

        // UIHelper's shared scroll controller uses position 0 for the bottom
        // (which is correct for chat). PlayerList starts at the top instead.
        if (IsOpen && keepAtTop && Scroll.Maximum > 0f)
        {
            Scroll.ScrollBy(float.MaxValue);
            Scroll.Update(1f);
            keepAtTop = false;
        }
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        keepAtTop = true;
        Opened?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Scroll.Reset();
        keepAtTop = false;
        Closed?.Invoke();
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var subscription in subscriptions)
            subscription.Dispose();
        subscriptions.Clear();
    }

    private void OnToggle(MiaoNetActionUiEvent action)
    {
        if (action.IsConsumed) return;
        action.Consume();
        if (buttonMode() == global::Celeste.Mod.MiaoNet.ButtonMode.Hold)
        {
            holdSeen = true;
            Open();
        }
        else
        {
            Toggle();
        }
    }

    private void OnScroll(MiaoNetActionUiEvent action, float direction)
    {
        if (action.IsConsumed || !IsOpen) return;
        action.Consume();
        keepAtTop = false;
        Scroll.ScrollBy(direction * 40f);
    }
}
