using Celeste.Mod.MiaoNet.Client.UI.Text;
using Celeste.Mod.UIHelper.Controls;
using Celeste.Mod.UIHelper.Core;
using Celeste.Mod.UIHelper.Rendering;
using Celeste.Mod.UIHelper.Styling;
using Celeste.Mod.UIHelper.Widgets;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.MiaoNet.Client.UI.Chat.Widgets;

/// <summary>
/// TextField display with syntax highlighting and an anchored completion popup.
/// The popup is laid out by AnchoredOverlay and does not affect input height.
/// </summary>
public sealed class ChatTextFieldDisplayWidget : StatelessWidget
{
    public required ChatTextFieldDisplayController Controller { get; init; }
    public float LineHeight { get; init; } = 24f;
    public float Scale { get; init; } = 1f;
    public float Padding { get; init; } = 8f;
    public float Width { get; init; } = 320f;

    public override Widget Build(BuildContext context)
    {
        _ = context;
        return new ValueBuilder<ChatTextFieldDisplayState>
        {
            Key = "chat-input-display-state",
            Source = Controller,
            Builder = state =>
            {
                var textStyle = new TextStyle
                {
                    Scale = Scale,
                    LineHeight = LineHeight
                };
                var caret = Math.Clamp(state.Editing.Caret, 0, state.Editing.Text.Length);
                var caretX = MiaoNetTextRenderer.Instance.Measure(
                    state.Editing.Text[..caret], textStyle).Width;
                return new AnchoredOverlay
                {
                    Key = "chat-input-overlay",
                    TargetAnchor = UiAlignment.TopLeft,
                    OverlayAnchor = UiAlignment.BottomLeft,
                    Offset = new UiOffset(caretX, -4),
                    Child = new ChatTextFieldTextWidget
                    {
                        Controller = Controller,
                        LineHeight = LineHeight,
                        Scale = Scale,
                        Padding = Padding,
                        Style = new UiStyle
                        {
                            Width = Width,
                            Height = LineHeight + Padding * 2f,
                            TextRenderer = MiaoNetTextRenderer.Instance
                        }
                    },
                    OverlayChild = BuildCompletionPopup()
                };
            }
        };
    }

    private Widget? BuildCompletionPopup()
    {
        var items = Controller.Analysis.Completions;
        if (!Controller.Value.Editing.Focused || items.Count == 0)
            return null;

        var textStyle = new TextStyle
        {
            Scale = Scale,
            LineHeight = LineHeight
        };
        var contentWidth = items.Max(item =>
            MiaoNetTextRenderer.Instance.Measure(item.Label, textStyle).Width);
        var popupWidth = MathF.Min(Width, contentWidth + 16f);

        return new BoxWidget
        {
            Key = "chat-completion-popup",
            Style = new UiStyle
            {
                Width = popupWidth,
                Background = Color.Black * (0xaa / 255f),
                BorderColor = Color.Cyan,
                BorderWidth = 1f,
                Padding = new EdgeInsets(4f)
            },
            Child = new VBox
            {
                CrossAlignment = CrossAxisAlignment.Stretch,
                Items = items.Select((item, index) => (Widget)new BoxWidget
                {
                    Key = $"completion:{index}:{item.Label}",
                    Style = new UiStyle
                    {
                        Height = LineHeight,
                        Background = index == Controller.SelectedCompletionIndex
                            ? Color.Wheat * (0x22 / 255f)
                            : Color.Transparent,
                        Padding = new EdgeInsets(4f, 0f)
                    },
                    Child = new TextWidget
                    {
                        Text = item.Label,
                        Style = new UiStyle
                        {
                            Foreground = index == Controller.SelectedCompletionIndex
                                ? Color.White : Color.LightGray,
                            FontScale = Scale,
                            LineHeight = LineHeight,
                            TextRenderer = MiaoNetTextRenderer.Instance
                        }
                    }
                }).ToArray()
            }
        };
    }
}

internal sealed class ChatTextFieldTextWidget : RenderObjectWidget
{
    public required ChatTextFieldDisplayController Controller { get; init; }
    public float LineHeight { get; init; }
    public float Scale { get; init; }
    public float Padding { get; init; }

    public override RenderObject CreateRenderObject(UiStyle style)
        => new ChatTextFieldTextRenderObject(style, Controller, LineHeight, Scale, Padding);

    public override void UpdateRenderObject(RenderObject renderObject, UiStyle style)
    {
        base.UpdateRenderObject(renderObject, style);
        ((ChatTextFieldTextRenderObject)renderObject).Update(Controller, LineHeight, Scale, Padding);
    }
}

internal sealed class ChatTextFieldTextRenderObject(
    UiStyle style,
    ChatTextFieldDisplayController controller,
    float lineHeight,
    float scale,
    float padding) : RenderObject(style)
{
    private ChatTextFieldDisplayController controller = controller;
    private float lineHeight = lineHeight;
    private float scale = scale;
    private float padding = padding;

    public void Update(ChatTextFieldDisplayController next, float nextLineHeight, float nextScale, float nextPadding)
    {
        controller = next;
        lineHeight = nextLineHeight;
        scale = nextScale;
        padding = nextPadding;
    }

    public override UiSize Measure(BoxConstraints constraints)
        => DesiredSize = constraints.Constrain(new(
            Style.Width ?? constraints.MaxWidth,
            Style.Height ?? lineHeight + padding * 2f));

    public override void Paint(IUiCanvas canvas)
    {
        var renderer = MiaoNetTextRenderer.Instance;
        var state = controller.Value;
        var editing = state.Editing;
        var text = editing.Text;
        var caret = Math.Clamp(editing.Caret, 0, text.Length);
        var before = text[..caret];
        var after = text[caret..];
        var origin = new UiOffset(Bounds.X + padding, Bounds.Y + padding);
        var textStyle = new TextStyle
        {
            Color = Color.White,
            Scale = scale,
            LineHeight = lineHeight
        };

        var x = DrawHighlighted(canvas, renderer, before, 0, origin.Y, origin.X, textStyle);
        if (editing.Composition is { } composition)
        {
            var imeStyle = textStyle with { Color = Color.Gray };
            renderer.Draw(canvas, composition, new(x, origin.Y), imeStyle);
            x += renderer.Measure(composition, imeStyle).Width;
        }
        DrawHighlighted(canvas, renderer, after, caret, origin.Y, x, textStyle);

        if (editing.Focused)
            canvas.FillRect(new(
                origin.X + renderer.Measure(before, textStyle).Width,
                origin.Y, 2f, lineHeight), Color.White);
    }

    private float DrawHighlighted(
        IUiCanvas canvas,
        ITextRenderer renderer,
        string text,
        int sourceStart,
        float y,
        float x,
        TextStyle style)
    {
        var cursor = sourceStart;
        var endLimit = sourceStart + text.Length;
        foreach (var token in controller.Analysis.Tokens.OrderBy(token => token.Start))
        {
            var start = Math.Clamp(token.Start, cursor, endLimit);
            var end = Math.Clamp(token.End, start, endLimit);
            x = DrawRun(cursor, start, Color.White, x);
            x = DrawRun(start, end, TokenColor(token.Kind), x);
            cursor = Math.Max(cursor, end);
        }
        return DrawRun(cursor, endLimit, Color.White, x);

        float DrawRun(int start, int end, Color color, float drawX)
        {
            if (end <= start) return drawX;
            var run = text[(start - sourceStart)..(end - sourceStart)];
            var runStyle = style with { Color = color };
            renderer.Draw(canvas, run, new(drawX, y), runStyle);
            return drawX + renderer.Measure(run, runStyle).Width;
        }
    }

    private static Color TokenColor(string kind) => kind switch
    {
        "command" => Color.Yellow,
        "mention" => Color.Gold,
        "emoji" => Color.Cyan,
        _ => Color.White
    };
}
