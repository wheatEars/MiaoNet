using Celeste.Mod.UIHelper.Core;
using Celeste.Mod.UIHelper.Rendering;
using Celeste.Mod.UIHelper.Styling;
using Celeste.Mod.ChatInputBox;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.MiaoNet.Client.UI.Text;

/// <summary>UIHelper adapter backed by the existing MiaoNet PixelFont implementation.</summary>
public sealed class MiaoNetTextRenderer : ITextRenderer
{
    public static MiaoNetTextRenderer Instance { get; } = new();

    private MiaoNetTextRenderer() { }

    public UiSize Measure(string text, TextStyle style)
    {
        var scale = style.Scale ?? 1f;
        var measured = MiaoNetFont.Measure(text);
        var lineHeight = style.LineHeight ?? MiaoNetFont.ENZhsLineHeight * scale;
        return new UiSize(measured.X * scale, Math.Max(measured.Y * scale, lineHeight));
    }

    public void Draw(IUiCanvas canvas, string text, UiOffset position, TextStyle style)
    {
        if (text.Length == 0) return;
        var scale = style.Scale ?? 1f;
        var color = style.Color ?? Color.White;
        var decorations = style.Decorations ?? TextDecoration.None;
        var chatStyle = ChatTextStyle.None;
        if (decorations.HasFlag(TextDecoration.Outline))
            chatStyle |= ChatTextStyle.Outline;
        if (decorations.HasFlag(TextDecoration.Underline))
            chatStyle |= ChatTextStyle.Underscore;
        if (decorations.HasFlag(TextDecoration.Strikethrough))
            chatStyle |= ChatTextStyle.Strikethrough;

        // Delegate drawing and decoration geometry to the canonical MiaoNet
        // renderer. UIHelper's canvas remains responsible for clipping and
        // opacity around this draw call.
        var chatText = new ChatText([new ChatTextSegment(chatStyle, color, text)]);
        MiaoNetFont.Draw(
            chatText, position.ToVector2(), 0f, Vector2.One * scale, 1f);
    }

    public bool CanRender(int character, TextStyle style)
        => MiaoNetFont.CanRender(character);
}
