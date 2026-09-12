using Celeste.Mod.MiaoNet.Client.UI.Input;
using Celeste.Mod.UIHelper.Events;
using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.MiaoNet.Client.UI.Chat;

/// <summary>
/// Coordinates chat state and maps the shared UI input bus to chat actions.
/// Rendering widgets subscribe to the state/events; they do not read keys.
/// </summary>
public sealed class ChatController : IDisposable
{
    private readonly List<string> tabs = [];
    private readonly List<IDisposable> subscriptions = [];
    private bool disposed;
    private int activeTabIndex = -1;

    public bool IsOpen { get; private set; }
    public IReadOnlyList<string> Tabs => tabs;
    public int ActiveTabIndex => activeTabIndex;
    public string? ActiveTabName => activeTabIndex < 0 ? null : tabs[activeTabIndex];

    public event Action? ToggleRequested;
    public event Action? Opened;
    public event Action? Closed;
    public event Action? CommandRequested;
    public event Action<int>? ActiveTabChanged;

    /// <summary>Optional owner-level guard for opening chat.</summary>
    public Func<bool>? CanOpen { get; set; }

    public ChatController(MiaoNetUiInputAdapter input)
    {
        ArgumentNullException.ThrowIfNull(input);
        subscriptions.Add(input.Events.Subscribe<MiaoNetActionUiEvent>(
            MiaoNetUiInputTypes.ChatToggle, OnChatToggle));
        subscriptions.Add(input.Events.Subscribe<MiaoNetActionUiEvent>(
            MiaoNetUiInputTypes.ChatCommand, OnChatCommand));
        subscriptions.Add(input.Events.Subscribe<KeyUiEvent>(
            UiEventTypes.KeyDown, OnKeyDown));
    }

    public void AddTab(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (tabs.Contains(name, StringComparer.Ordinal)) return;
        tabs.Add(name);
        ActiveTabChanged?.Invoke(activeTabIndex);
    }

    public void RemoveTab(string name)
    {
        var index = tabs.FindIndex(tab => string.Equals(tab, name, StringComparison.Ordinal));
        if (index < 0) return;
        tabs.RemoveAt(index);
        if (activeTabIndex == index)
            activeTabIndex = tabs.Count == 0 ? -1 : Math.Min(activeTabIndex, tabs.Count - 1);
        else if (index < activeTabIndex)
            activeTabIndex--;
        ActiveTabChanged?.Invoke(activeTabIndex);
    }

    public void ClearTabs()
    {
        if (tabs.Count == 0 && activeTabIndex == -1) return;
        tabs.Clear();
        activeTabIndex = -1;
        ActiveTabChanged?.Invoke(activeTabIndex);
    }

    public void SetActiveTab(string? name)
    {
        var next = name is null ? -1 : tabs.FindIndex(tab =>
            string.Equals(tab, name, StringComparison.Ordinal));
        if (next < 0 && name is not null) return;
        if (next == activeTabIndex) return;
        activeTabIndex = next;
        ActiveTabChanged?.Invoke(activeTabIndex);
    }

    public void CycleTab(int offset)
    {
        var count = tabs.Count + 1;
        if (count == 1) return;
        var slot = activeTabIndex + 1;
        slot = ((slot + offset) % count + count) % count;
        SetActiveTab(slot == 0 ? null : tabs[slot - 1]);
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        Opened?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Closed?.Invoke();
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
        ToggleRequested?.Invoke();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var subscription in subscriptions) subscription.Dispose();
        subscriptions.Clear();
    }

    private void OnChatToggle(MiaoNetActionUiEvent action)
    {
        if (action.IsConsumed) return;
        action.Consume();
        // The same key is commonly used to open chat and to type a character
        // (T by default). Once the editor is open, leave the action consumed
        // but do not close the editor; the text-input event will insert it.
        if (!IsOpen && CanOpen?.Invoke() != false)
            Toggle();
    }

    private void OnChatCommand(MiaoNetActionUiEvent action)
    {
        if (action.IsConsumed) return;
        action.Consume();
        if (CanOpen?.Invoke() == false)
            return;
        Open();
        CommandRequested?.Invoke();
    }

    private void OnKeyDown(KeyUiEvent key)
    {
        if (!IsOpen) return;
        if (key.Key == Keys.Escape)
        {
            Close();
            key.Handled = true;
            return;
        }
        if (!key.Shift) return;
        switch (key.Key)
        {
            case Keys.Left:
                CycleTab(-1);
                key.Handled = true;
                break;
            case Keys.Right:
                CycleTab(1);
                key.Handled = true;
                break;
        }
    }
}
