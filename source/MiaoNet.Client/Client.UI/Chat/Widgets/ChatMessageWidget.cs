using Celeste.Mod.MiaoNet.Client.UI.Text;
using Celeste.Mod.UIHelper.Controls;
using Celeste.Mod.UIHelper.Styling;
using Celeste.Mod.UIHelper.State;
using Celeste.Mod.UIHelper.Widgets;
using Celeste.Mod.UIHelper.Core;
using Celeste.Mod.MiaoNet.Client.UI.Settings;
using System.Globalization;
using Microsoft.Xna.Framework;

using MiaoNet.Shared;

namespace Celeste.Mod.MiaoNet.Client.UI.Chat.Widgets;

/// <summary>Builds the visual representation of one chat message.</summary>
public sealed class ChatMessageWidget : StatelessWidget
{
    public required ChatMessageItem Message { get; init; }
    public required MiaoNetSettingsObserver Settings { get; init; }

    public override Widget Build(BuildContext context)
    {
        // Reserved for inherited UI state (theme, scale, etc.).
        _ = context;

        var presentation = Settings.Observe(
            "chat-message-presentation",
            ChatMessagePresentation.FromSettings);

        return new ValueBuilder<ChatMessagePresentation>
        {
            Key = $"chat-message-presentation:{Message.Id}",
            Source = presentation,
            Builder = BuildContent
        };
    }

    private Widget BuildContent(ChatMessagePresentation presentation)
    {
        var textRenderer = MiaoNetTextRenderer.Instance;
        var defaultColor = Message.channel switch
        {
            ChatChannel.Map => Color.Cyan,
            ChatChannel.Channel => Color.LightGray,
            _ => Color.White
        };

        var textStyle = new UiStyle
        {
            FontScale = presentation.Scale,
            LineHeight = presentation.LineHeight,
            TextRenderer = textRenderer,
            Opacity = presentation.TextOpacity * Message.DisplayOpacity
        };

        var items = new List<Widget>
        {
            new TextWidget
            {
                Key = $"chat-message-time:{Message.Id}",
                Text = FormatDateTime(Message.timestamp),
                Style = textStyle with { Foreground = Color.CornflowerBlue }
            }
        };
        if (Message.Sender is { } sender)
        {
            items.Add(new TextWidget
            {
                Key = $"chat-message-sender:{Message.Id}",
                Text = sender.GetDisplayName(true, true),
                Style = textStyle with { Foreground = sender.Info.Color }
            });
        }
        items.Add(new RichTextLineWidget
        {
            Key = $"chat-message-text:{Message.Id}",
            Text = RichText.Parse(Message.rawText, defaultColor),
            Style = textStyle with { Foreground = defaultColor }
        });
        if (Message.RepeatCount > 1)
        {
            items.Add(new TextWidget
            {
                Key = $"chat-message-repeat:{Message.Id}",
                Text = $"X{Message.RepeatCount}",
                Style = textStyle with
                {
                    Foreground = RepeatColor(Message.RepeatCount),
                    Opacity = presentation.TextOpacity * Message.DisplayOpacity
                }
            });
        }

        return new BoxWidget
        {
            Key = $"chat-message-box:{Message.Id}",
            Style = new UiStyle
            {
                Background = Color.Black,
                Opacity = presentation.BackgroundOpacity * Message.DisplayOpacity,
                Padding = new EdgeInsets(8f, presentation.Padding)
            },
            Child = new HBox
            {
                Key = $"chat-message-row:{Message.Id}",
                Spacing = 6f,
                CrossAlignment = CrossAxisAlignment.Center,
                Items = items
            }
        };
    }
    
    private static string FormatDateTime(DateTime dateTime)
        => dateTime.ToLocalTime().ToString("T", CultureInfo.InvariantCulture);

    private static Color RepeatColor(int count)
    {
        var redness = Math.Clamp((count - 2) / 18f, 0f, 1f);
        return Color.Lerp(Color.White, Color.Red, redness);
    }
}
