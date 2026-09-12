using Celeste.Mod.UIHelper.Core;
using Celeste.Mod.UIHelper.Rendering;
using Celeste.Mod.UIHelper.Styling;

namespace Celeste.Mod.MiaoNet.Client.UI.Text;

internal sealed class RichTextLineRenderObject(UiStyle style, RichText text) : RenderObject(style)
{
    
    public RichText Text { get; set; } = text;
    public IReadOnlyList<RichTextSegment> Segments => Text.Segments;

    public override UiSize Measure(BoxConstraints constraints)
    {
        var renderer = Style.TextRenderer ?? SpriteFontTextRenderer.Instance;
        var baseStyle = TextStyle.FromUiStyle(Style);
        var width = 0f;
        var height = baseStyle.LineHeight ?? 0f;
        foreach (var segment in Segments)
        {
            var size = renderer.Measure(segment.Text, baseStyle.Merge(segment.Style));
            width += size.Width;
            height = Math.Max(height, size.Height);
        }
        return DesiredSize = ApplyStyleSize(new(width, height), constraints, true);
    }

    public override void Paint(IUiCanvas canvas)
    {
        var renderer = Style.TextRenderer ?? SpriteFontTextRenderer.Instance;
        var baseStyle = TextStyle.FromUiStyle(Style);
        var padding = Style.Padding ?? EdgeInsets.Zero;
        var position = new UiOffset(Bounds.X + padding.Left, Bounds.Y + padding.Top);
        foreach (var segment in Segments)
        {
            var segmentStyle = baseStyle.Merge(segment.Style);
            if (Style.Opacity is { } segmentOpacity && segmentStyle.Color is { } segmentColor)
                segmentStyle = segmentStyle with { Color = segmentColor * segmentOpacity };
            renderer.Draw(canvas, segment.Text, position, segmentStyle);
            position = new(position.X + renderer.Measure(segment.Text, segmentStyle).Width, position.Y);
        }
    }
}
