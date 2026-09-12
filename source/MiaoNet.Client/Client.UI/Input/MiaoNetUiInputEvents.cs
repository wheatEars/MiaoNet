using Celeste.Mod.UIHelper.Events;

namespace Celeste.Mod.MiaoNet.Client.UI.Input;

/// <summary>Names for MiaoNet's configurable UI actions.</summary>
public static class MiaoNetUiInputTypes
{
    public const string ChatToggle = "miaonet.input.chat.toggle";
    public const string ChatCommand = "miaonet.input.chat.command";
    public const string PlayerListToggle = "miaonet.input.player-list.toggle";
    public const string PlayerListScrollUp = "miaonet.input.player-list.scroll-up";
    public const string PlayerListScrollDown = "miaonet.input.player-list.scroll-down";
    public const string CreateFireworks = "miaonet.input.fireworks";
    public const string Emote = "miaonet.input.emote";
    public const string EmoteWheel = "miaonet.input.emote-wheel";

    public static string EmoteAt(int index) => $"{Emote}.{index}";
}

/// <summary>
/// An event for a configured MiaoNet action. Pressed actions may be consumed
/// exactly once by the subscriber that handles them; held actions are emitted
/// once per frame while the binding is held.
/// </summary>
public sealed class MiaoNetActionUiEvent : UiEvent
{
    private readonly Action? consumeAction;
    private int consumed;

    public string ActionName { get; }
    public bool IsRepeat { get; }
    public bool IsConsumed => Volatile.Read(ref consumed) != 0;

    internal MiaoNetActionUiEvent(string actionName, bool isRepeat, Action? consumeAction)
        : base(actionName)
    {
        ActionName = actionName;
        IsRepeat = isRepeat;
        this.consumeAction = consumeAction;
    }

    public void Consume()
    {
        if (Interlocked.Exchange(ref consumed, 1) == 0)
            consumeAction?.Invoke();
        Handled = true;
    }
}
