using Celeste.Mod.UIHelper.Widgets;
using Celeste.Mod.MiaoNet.Client.UI.Chat.Widgets;
using Celeste.Mod.MiaoNet.Client.UI.Settings;

namespace Celeste.Mod.MiaoNet.Client.UI.Chat;

/// <summary>
/// Temporary default item factory. Rich text, timestamps and decorations can
/// be replaced here without making the data source depend on UI layout.
/// </summary>
public sealed class ChatItemWidgetFactory
{
    private readonly Dictionary<Type, Func<ChatItem, Widget>> builders = [];
    private readonly MiaoNetSettingsObserver settings;

    public ChatItemWidgetFactory(MiaoNetSettingsObserver settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Register<ChatMessageItem>(BuildChatMessage);
    }

    private Widget BuildChatMessage(ChatMessageItem item)
        => new ChatMessageWidget
        {
            Key = $"chat-message:{item.Id}",
            Message = item,
            Settings = settings
        };
    
    public void Register<T>(
        Func<T, Widget> builder
    ) where T : ChatItem
    {
        builders[typeof(T)] = item => builder((T)item);
    }

    public Widget Build(ChatItem item)
    {
        if (!builders.TryGetValue(item.GetType(), out var builder))
            throw new NotSupportedException(
                $"No widget builder for {item.GetType().FullName}");
        return builder(item);
    }
}
