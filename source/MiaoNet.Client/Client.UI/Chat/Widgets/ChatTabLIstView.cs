using Celeste.Mod.UIHelper.Controls;
using Celeste.Mod.UIHelper.Core;
using Celeste.Mod.UIHelper.Styling;
using Celeste.Mod.UIHelper.Widgets;
using Celeste.Mod.MiaoNet.Client.UI.Settings;
using Celeste.Mod.MiaoNet.Client.UI.Text;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.MiaoNet.Client.UI.Chat.Widgets;

public class ChatTabLIstView : StatelessWidget
{
    
    public required ChatController Controller { get; init; }
    public float Scale { get; init; } = 1f;
    public float LineHeight { get; init; } = 24f;

    public override Widget Build(BuildContext context)
    {
        // Reserved for inherited UI state (theme, scale, etc.).
        _ = context;

        var texteRenderer = MiaoNetTextRenderer.Instance;
        var tabNames = Controller.Tabs.ToArray();
        var activeIndex = Controller.ActiveTabIndex + 1;

        return new HBox
        {
            Key = "chat-tabs",
            Spacing = 2,
            CrossAlignment = CrossAxisAlignment.Start,
            Items = tabNames.Select((name, index) => (Widget)new BoxWidget
            {
                Key = $"chat-tab-{name}",
                Style = new UiStyle
                {
                    Padding = new EdgeInsets(8f, 0f),
                    Background = Color.Black,
                    Opacity = (index == activeIndex) ? 0.5f : 0.15f
                },
                Child = new TextWidget
                {
                    Key = $"chat-tab-name-{name}",

                    Text = name,
                    Style = new UiStyle
                    {
                        Foreground = Color.White,
                        FontScale = Scale,
                        LineHeight = LineHeight,
                        TextRenderer = texteRenderer,
                        Opacity = (index == activeIndex) ? 1f : 0.5f
                    }
                }
            }).ToArray()
        };
    }
}
