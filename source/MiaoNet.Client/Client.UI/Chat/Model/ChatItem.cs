using Microsoft.Xna.Framework;

namespace Celeste.Mod.MiaoNet.Client.UI.Chat;

/// <summary>
/// UI-facing snapshot of one chat message. The chat/business layer owns the
/// original model and publishes snapshots through <see cref="IChatMessageSource"/>.
/// </summary>
public abstract class ChatItem
{
    public long Id { get; }

    public ChatItem(long id)
    {
        Id = id;
    }
}
