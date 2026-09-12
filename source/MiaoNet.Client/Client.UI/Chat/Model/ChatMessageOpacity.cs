namespace Celeste.Mod.MiaoNet.Client.UI.Chat;

/// <summary>Observable presentation values shared by all chat message widgets.</summary>
public readonly record struct ChatMessageOpacity(float Background, float Text)
{
    public static ChatMessageOpacity Clamp(ChatMessageOpacity value)
        => new(Math.Clamp(value.Background, 0f, 1f), Math.Clamp(value.Text, 0f, 1f));
}

public readonly record struct ChatMessagePresentation(
    float Scale,
    float LineHeight,
    float Padding,
    float BackgroundOpacity,
    float TextOpacity
)
{
    public static ChatMessagePresentation FromSettings(MiaoNetModuleSettings settings)
    {
        var scale = settings.ChatUIScaleValue;
        return new(
            scale,
            MiaoNetFont.ENZhsLineHeight * scale,
            Math.Max(0f, settings.ChatMessagePadding),
            Math.Clamp(settings.ChatBackgroundOpacityValue, 0f, 1f),
            Math.Clamp(settings.ChatTextOpacityValue, 0f, 1f));
    }
}
