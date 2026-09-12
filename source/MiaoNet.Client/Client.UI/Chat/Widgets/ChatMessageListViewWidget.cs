using Celeste.Mod.UIHelper.Controls;
using Celeste.Mod.UIHelper.Core;
using Celeste.Mod.UIHelper.Styling;
using Celeste.Mod.UIHelper.Widgets;

namespace Celeste.Mod.MiaoNet.Client.UI.Chat;

/// <summary>
/// Minimal chat view. It owns layout only; messages are supplied by the
/// external data source and are not copied into this widget.
/// </summary>
public sealed class ChatMessageListViewWidget : StatelessWidget
{
    public required IVirtualListDataSource Messages { get; init; }
    public required ScrollController ScrollController { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }
    public float ItemExtent { get; init; } = 24f;
    public float Spacing { get; init; }
    public float Padding { get; init; }

    public override Widget Build(BuildContext context)
        => new BoxWidget
        {
            Style = new UiStyle
            {
                Width = Width,
                Height = Height,
                Padding = new EdgeInsets(Padding)
            },
            Child = new VirtualListView
            {
                Key = "chat-messages",
                DataSource = Messages,
                Controller = ScrollController,
                ItemExtent = ItemExtent,
                Spacing = Spacing,
                StickToEnd = ScrollController.Position == 0f,
                FadeItemsAtEdges = true,
                Style = new UiStyle
                {
                    Width = MathF.Max(0f, Width - 2f * Padding),
                    Height = MathF.Max(0f, Height - 2f * Padding)
                }
            }
        };
}
