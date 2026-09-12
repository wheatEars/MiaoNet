using MiaoNet.Shared;

namespace Celeste.Mod.MiaoNet.Client.UI.Chat;

public class ChatMessageItem : ChatItem
{
    public DateTime timestamp;
    public OnlinePlayer? Sender;
    public ChatChannel? channel;
    public string rawText;
    public int RepeatCount { get; set; } = 1;
    public float ShowTimer { get; set; }
    public float DisplayOpacity { get; set; } = 1f;

    public ChatMessageItem(long id, ChatChannel? channel, string rawText) : base(id)
    {
        this.channel = channel;
        this.rawText = rawText;
        timestamp = DateTime.Now;
    }
}
