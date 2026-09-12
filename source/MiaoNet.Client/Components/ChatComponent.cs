using System.Text;
using Celeste.Mod.ChatInputBox;
using Celeste.Mod.MiaoNet.Client.UI.Chat;
using MiaoNet.Shared;
using UiChatItem = Celeste.Mod.MiaoNet.Client.UI.Chat.ChatItem;

namespace Celeste.Mod.MiaoNet;

/// <summary>Owns chat messages and network conversion; UI is handled by UIComponent.</summary>
public sealed class ChatComponent : MiaoNetComponent, IChatMessageSource
{
    private const float FadeDuration = 0.25f;
    private readonly List<UiChatItem> messages = [];
    private readonly ChatMessageFactory messageFactory;
    private IReadOnlyList<UiChatItem> visibleMessages = Array.Empty<UiChatItem>();
    private ChatChannel? activeChannel;
    private readonly Dictionary<string, FoldEntry> folds = new(StringComparer.Ordinal);
    private static long nextId;
    private bool chatActive;
    private NewMessageShowingMode lastShowingMode = NewMessageShowingMode.ShowAll;

    private sealed class FoldEntry(ChatMessageItem item, DateTime lastTime)
    {
        public ChatMessageItem Item { get; set; } = item;
        public DateTime LastTime { get; set; } = lastTime;
    }

    public IReadOnlyList<UiChatItem> VisibleMessages => visibleMessages;
    public IReadOnlyList<UiChatItem> Messages => messages;
    public event Action? Changed;

    public ChatComponent(MiaoNetContext context) : base(context)
    {
        messageFactory = new ChatMessageFactory(context);
        context.ChatMessageReceived += OnChatMessageReceived;
        context.PlayerJoined += OnPlayerJoined;
        context.PlayerLeft += OnPlayerLeft;
    }

    public override void Update()
    {
        var active = chatActive;
        var deltaTime = Engine.RawDeltaTime;
        var changed = false;
        var mode = MiaoNetModule.Settings.NewMessagesShowing;
        if (mode != lastShowingMode)
        {
            lastShowingMode = mode;
            RebuildVisible();
            changed = true;
        }
        foreach (var item in messages.OfType<ChatMessageItem>())
        {
            var wasExpired = item.ShowTimer <= 0f;
            item.ShowTimer = Math.Max(0f, item.ShowTimer - deltaTime);
            if (active)
            {
                if (item.DisplayOpacity < 1f)
                {
                    item.DisplayOpacity = 1f;
                    changed = true;
                }
                continue;
            }

            if (wasExpired)
            {
                if (item.DisplayOpacity > 0f)
                {
                    item.DisplayOpacity = Math.Max(0f,
                        item.DisplayOpacity - deltaTime / FadeDuration);
                    changed = true;
                }
                continue;
            }

            if (item.ShowTimer <= 0f && item.DisplayOpacity > 0f)
            {
                // The message reached its display deadline this frame; start
                // the fade on the next update so the full duration is visible.
                continue;
            }
        }

        if (changed)
        {
            RebuildVisible();
            Changed?.Invoke();
        }
    }

    public void SetActiveChannel(ChatChannel? channel)
    {
        if (activeChannel == channel) return;
        activeChannel = channel;
        RebuildVisible();
        Changed?.Invoke();
    }

    public void SetChatActive(bool active)
    {
        if (chatActive == active) return;
        if (chatActive && !active)
        {
            // Messages that expired while the chat was open should disappear
            // on close, rather than replaying a fade animation.
            foreach (var item in messages.OfType<ChatMessageItem>())
                if (item.ShowTimer <= 0f)
                    item.DisplayOpacity = 0f;
        }
        chatActive = active;
        RebuildVisible();
        Changed?.Invoke();
    }

    public void AddLocalChat(ChatText message) => Add(CreateItem(DateTime.Now, null, message));

    public void OnSentPrivateMessage(DateTime dateTime, OnlinePlayer other, string text)
    {
        var item = CreateItem(dateTime, null,
            messageFactory.CreateSentPrivateMessage(other, text));
        item.Sender = other;
        Add(item);
    }

    public void ClearChat()
    {
        messages.Clear();
        folds.Clear();
        RebuildVisible();
        Changed?.Invoke();
    }

    public override void OnDisconnected()
    {
        chatActive = false;
        activeChannel = null;
        ClearChat();
    }

    private void OnPlayerJoined(OnlinePlayer player)
    {
        if (MiaoNetModule.Settings.PlayerPresenceMessages)
            AddLocalChat(MiaoNetChatText.CreateAnnouncement(PFormat.Format(
                context.PlayerPresenceMessage.PlayerJoined,
                player.GetDisplayName(false, context.ShowAvatar))));
    }

    private void OnPlayerLeft(OnlinePlayer player)
    {
        if (MiaoNetModule.Settings.PlayerPresenceMessages)
            AddLocalChat(MiaoNetChatText.CreateAnnouncement(PFormat.Format(
                context.PlayerPresenceMessage.PlayerLeft,
                player.GetDisplayName(false, context.ShowAvatar))));
    }

    private void OnChatMessageReceived(OnlinePlayer? player, PacketChatMessage packet)
    {
        if (MiaoNetModule.Settings.LiveMode && packet.Type is not ChatMessageType.Server and not ChatMessageType.ServerChat)
            return;
        var received = messageFactory.CreateReceived(player, packet);
        if (received.Text is null) return;
        var channel = packet.Type switch
        {
            ChatMessageType.ChannelChat => ChatChannel.Channel,
            ChatMessageType.MapChat => ChatChannel.Map,
            ChatMessageType.Chat => ChatChannel.Global,
            _ => (ChatChannel?)null
        };
        string? foldKey = null;
        ChatText? foldedText = null;
        if (MiaoNetModule.Settings.MessageFolding)
            (foldKey, foldedText) = messageFactory.CreateFoldInfo(player, packet, received.Content);
        Add(CreateItem(packet.DateTime, channel, received.Text), foldKey, foldedText);
        if (received.MentionsSelf) Audio.Play(MiaoNetSFX.ChatMention);
    }

    private void Add(ChatMessageItem item, string? foldKey = null, ChatText? foldedText = null)
    {
        if (foldKey is not null && foldedText is not null &&
            folds.TryGetValue(foldKey, out var entry) &&
            (item.timestamp - entry.LastTime).TotalSeconds <= MiaoNetModule.Settings.FoldWindowSeconds)
        {
            var currentTimestamp = item.timestamp;
            messages.Remove(entry.Item);
            item = CreateItem(entry.Item.timestamp, item.channel, foldedText);
            item.RepeatCount = entry.Item.RepeatCount + 1;
            item.Sender = entry.Item.Sender;
            entry.Item = item;
            entry.LastTime = currentTimestamp;
        }
        messages.Add(item);
        if (foldKey is not null && (foldedText is null || !folds.ContainsKey(foldKey) ||
            !ReferenceEquals(folds[foldKey].Item, item)))
            folds[foldKey] = new FoldEntry(item, item.timestamp);
        RebuildVisible();
        Changed?.Invoke();
    }

    private ChatMessageItem CreateItem(DateTime timestamp, ChatChannel? channel, ChatText text)
        => new(NewId(), channel, ToMarkup(text))
        {
            timestamp = timestamp,
            ShowTimer = Math.Max(0f, MiaoNetModule.Settings.ChatDisplayDuration)
        };

    private void RebuildVisible()
    {
        if (!chatActive && MiaoNetModule.Settings.NewMessagesShowing == NewMessageShowingMode.HideAll)
        {
            visibleMessages = Array.Empty<UiChatItem>();
            return;
        }

        var filterByChannel = chatActive ||
            MiaoNetModule.Settings.NewMessagesShowing == NewMessageShowingMode.WithTab;
        var source = messages.Where(item =>
            (chatActive || item is not ChatMessageItem message || message.DisplayOpacity > 0f) &&
            (!filterByChannel || activeChannel is null ||
             item is not ChatMessageItem channelMessage ||
             channelMessage.channel is null || channelMessage.channel == activeChannel));
        visibleMessages = source.ToArray();
    }

    private static string ToMarkup(ChatText text)
    {
        var result = new StringBuilder();
        foreach (var segment in text.Segments)
        {
            result.Append("\\r\\#")
                .Append(segment.Color.R.ToString("X2", System.Globalization.CultureInfo.InvariantCulture))
                .Append(segment.Color.G.ToString("X2", System.Globalization.CultureInfo.InvariantCulture))
                .Append(segment.Color.B.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            if (segment.Style.HasFlag(ChatTextStyle.Underscore)) result.Append("\\u");
            if (segment.Style.HasFlag(ChatTextStyle.Strikethrough)) result.Append("\\s");
            if (segment.Style.HasFlag(ChatTextStyle.Outline)) result.Append("\\o");
            result.Append(segment.Text.Replace("\\", "\\\\", StringComparison.Ordinal));
        }
        return result.ToString();
    }

    private static long NewId() => Interlocked.Increment(ref nextId);
}
