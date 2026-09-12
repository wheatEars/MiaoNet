using Celeste.Mod.UIHelper.Rendering;
using Celeste.Mod.UIHelper.Styling;
using Celeste.Mod.UIHelper.Widgets;

namespace Celeste.Mod.MiaoNet.Client.UI.Text;

public sealed class RichTextLineWidget : RenderObjectWidget
{
    public required RichText Text { get; init; }
    public override Element CreateElement() => new RichTextLineElement(this);
    public override RenderObject CreateRenderObject(UiStyle style) => new RichTextLineRenderObject(style, Text);

    public override void UpdateRenderObject(RenderObject renderObject, UiStyle style)
    {
        base.UpdateRenderObject(renderObject, style);
        ((RichTextLineRenderObject)renderObject).Text = Text;
    }
}
