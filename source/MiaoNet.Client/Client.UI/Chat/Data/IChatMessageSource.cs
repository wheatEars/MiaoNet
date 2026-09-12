namespace Celeste.Mod.MiaoNet.Client.UI.Chat;

/// <summary>
/// Boundary between chat business state and the UI layer.
/// Changed must be raised on the game/UI thread.
/// </summary>
public interface IChatMessageSource
{
    IReadOnlyList<ChatItem> VisibleMessages { get; }

    event Action? Changed;
}
