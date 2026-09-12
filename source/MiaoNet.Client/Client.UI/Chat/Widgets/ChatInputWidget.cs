using Celeste.Mod.MiaoNet.Client.UI.Text;
using Celeste.Mod.UIHelper.Controls;
using Celeste.Mod.UIHelper.Core;
using Celeste.Mod.UIHelper.Styling;
using Celeste.Mod.UIHelper.Widgets;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.MiaoNet.Client.UI.Chat.Widgets;

/// <summary>Chat input styled after the legacy ChatInputBox.</summary>
public sealed class ChatInputWidget : StatelessWidget
{
    public required TextEditingController Editor { get; init; }
    public required ChatTextFieldDisplayController DisplayController { get; init; }
    public Action<string>? OnSubmitted { get; init; }
    public float Width { get; init; }
    public float Scale { get; init; } = 1f;
    public float LineHeight { get; init; } = 24f;
    public float Padding { get; init; } = 8f;

    public override Widget Build(BuildContext context)
    {
        _ = context;
        return new TextField
        {
            Key = "miaonet-chat-input",
            Controller = Editor,
            OnSubmitted = OnSubmitted,
            Display = new ChatTextFieldDisplayWidget
            {
                Controller = DisplayController,
                LineHeight = LineHeight,
                Scale = Scale,
                Padding = Padding,
                Width = Math.Max(0, Width - Padding * 2f)
            },
            Style = new UiStyle
            {
                Width = Width,
                Height = LineHeight + Padding * 2f,
                Background = Color.Black * (0x7f / 255f),
                Foreground = Color.White,
                FontScale = Scale,
                LineHeight = LineHeight,
                TextRenderer = MiaoNetTextRenderer.Instance
            }
        };
    }
}
