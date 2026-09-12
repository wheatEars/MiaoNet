using System.Collections.Immutable;

namespace Celeste.Mod.MiaoNet.Client.UI.Text;

public class RichText
{
    public ImmutableArray<RichTextSegment> Segments;
    
    public RichText(ImmutableArray<RichTextSegment> segments)
    {
        this.Segments = segments;
    }
    
    public static RichText Parse(string rawText, Color defaultColor) => new (RichTextMarkup.Parse(rawText, defaultColor));
}