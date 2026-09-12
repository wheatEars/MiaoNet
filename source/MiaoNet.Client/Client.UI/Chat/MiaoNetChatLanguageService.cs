using Celeste.Mod.ChatInputBox;
using Celeste.Mod.UIHelper.Controls;
using Celeste.Mod.UIHelper.Rendering;

namespace Celeste.Mod.MiaoNet.Client.UI.Chat;

/// <summary>Adapts the legacy MiaoNet completion provider to UIHelper's language API.</summary>
public sealed class MiaoNetChatLanguageService : ITextLanguageService
{
    private readonly ChatCompletionProvider provider;
    private readonly int maxCompletions;

    public MiaoNetChatLanguageService(ChatCompletionProvider provider, int maxCompletions = 8)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        this.maxCompletions = Math.Max(1, maxCompletions);
    }

    public TextAnalysis Analyze(TextEditingState state)
    {
        var input = state.Text[..Math.Clamp(state.Caret, 0, state.Text.Length)];
        var completions = provider.GetCompletions(input)?
            .Select(item => new TextCompletionItem
            {
                Label = item.Display,
                FilterText = item.Content,
                Edit = new TextEdit(
                    Math.Max(0, state.Caret - item.Remove),
                    Math.Min(item.Remove, state.Caret),
                    item.Content),
                Data = item
            })
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .Take(maxCompletions)
            .ToArray() ?? Array.Empty<TextCompletionItem>();

        return new TextAnalysis
        {
            Version = state.Caret ^ state.Text.GetHashCode(StringComparison.Ordinal),
            Tokens = AnalyzeTokens(state.Text),
            Completions = completions
        };
    }

    private static IReadOnlyList<TextToken> AnalyzeTokens(string text)
    {
        var tokens = new List<TextToken>();
        if (text.StartsWith('/'))
        {
            var end = text.IndexOfAny([' ', '\t', '\r', '\n']);
            if (end < 0) end = text.Length;
            tokens.Add(new TextToken(0, end, "command"));
        }

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '@' && (index == 0 || char.IsWhiteSpace(text[index - 1])))
            {
                var end = index + 1;
                while (end < text.Length && !char.IsWhiteSpace(text[end])) end++;
                if (end > index + 1) tokens.Add(new TextToken(index, end - index, "mention"));
            }
            else if (text[index] == ':')
            {
                var end = text.IndexOf(':', index + 1);
                if (end > index + 1)
                {
                    var name = text[(index + 1)..end];
                    if (name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
                    {
                        tokens.Add(new TextToken(index, end - index + 1, "emoji"));
                        index = end;
                    }
                }
            }
        }
        return tokens;
    }
}
