using System.Collections.Immutable;
using Celeste.Mod.UIHelper.Controls;
using Celeste.Mod.UIHelper.Rendering;
using Celeste.Mod.UIHelper.Styling;
using Celeste.Mod.UIHelper.Events;
using Celeste.Mod.UIHelper.State;
using Microsoft.Xna.Framework.Input;

using Celeste.Mod.MiaoNet.Client.UI.Text;

namespace Celeste.Mod.MiaoNet.Client.UI.Chat;

/// <summary>
/// Holds the derived display state for a chat TextField: highlighted text,
/// completion candidates and the selected candidate.
/// </summary>
public readonly record struct ChatTextFieldDisplayState(
    TextEditingState Editing,
    TextAnalysis Analysis,
    int SelectedCompletionIndex);

public sealed class ChatTextFieldDisplayController : ITextEditingExtension, IObservableValue<ChatTextFieldDisplayState>, IDisposable
{
    private readonly TextEditingController editor;
    private readonly ITextLanguageService languageService;
    private bool disposed;

    public TextAnalysis Analysis { get; private set; } = TextAnalysis.Empty;
    public int SelectedCompletionIndex { get; private set; } = -1;
    public ChatTextFieldDisplayState Value => new(editor.State, Analysis, SelectedCompletionIndex);
    public event Action<ChatTextFieldDisplayState>? Changed;

    public ChatTextFieldDisplayController(TextEditingController editor, ITextLanguageService languageService, UiEventBus? events = null)
    {
        this.editor = editor ?? throw new ArgumentNullException(nameof(editor));
        this.languageService = languageService ?? throw new ArgumentNullException(nameof(languageService));
        editor.Extension = this;
        keySubscription = events?.Subscribe<KeyUiEvent>(UiEventTypes.KeyDown, OnKeyDown);
        Update(editor.State);
    }

    public TextCompletionItem? SelectedCompletion =>
        SelectedCompletionIndex >= 0 && SelectedCompletionIndex < Analysis.Completions.Count
            ? Analysis.Completions[SelectedCompletionIndex]
            : null;

    public void OnStateChanged(TextEditingController _, TextEditingState state) => Update(state);

    public void MoveSelection(int delta)
    {
        if (Analysis.Completions.Count == 0) return;
        var count = Analysis.Completions.Count;
        SelectedCompletionIndex = SelectedCompletionIndex < 0
            ? (delta >= 0 ? 0 : count - 1)
            : (SelectedCompletionIndex + delta % count + count) % count;
        Changed?.Invoke(Value);
    }

    public bool ApplySelected()
    {
        var completion = SelectedCompletion;
        if (completion is null) return false;
        editor.ReplaceRange(completion.Edit.Start, completion.Edit.Length, completion.Edit.NewText);
        return true;
    }

    public RichText BuildHighlightedText(Color fallbackColor)
    {
        var text = editor.Text;
        if (text.Length == 0)
            return new RichText(ImmutableArray<RichTextSegment>.Empty);

        var segments = ImmutableArray.CreateBuilder<RichTextSegment>();
        var cursor = 0;
        foreach (var token in Analysis.Tokens.OrderBy(token => token.Start))
        {
            var start = Math.Clamp(token.Start, cursor, text.Length);
            var end = Math.Clamp(token.End, start, text.Length);
            if (start > cursor)
                segments.Add(new RichTextSegment(text[cursor..start], new TextStyle { Color = fallbackColor }));
            if (end > start)
                segments.Add(new RichTextSegment(text[start..end], new TextStyle { Color = TokenColor(token.Kind) }));
            cursor = Math.Max(cursor, end);
        }
        if (cursor < text.Length)
            segments.Add(new RichTextSegment(text[cursor..], new TextStyle { Color = fallbackColor }));
        return new RichText(segments.ToImmutable());
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (ReferenceEquals(editor.Extension, this)) editor.Extension = null;
        keySubscription?.Dispose();
    }

    private void Update(TextEditingState state)
    {
        Analysis = languageService.Analyze(state);
        if (SelectedCompletionIndex >= Analysis.Completions.Count)
            SelectedCompletionIndex = -1;
        Changed?.Invoke(Value);
    }

    private static Color TokenColor(string kind) => kind switch
    {
        "command" => Color.Yellow,
        "mention" => Color.Gold,
        "emoji" => Color.Cyan,
        _ => Color.White
    };

    private IDisposable? keySubscription;

    private void OnKeyDown(KeyUiEvent key)
    {
        if (!editor.Focused || key.Handled) return;
        switch (key.Key)
        {
            case Keys.Up when Analysis.Completions.Count > 0:
                MoveSelection(-1); key.Handled = true; break;
            case Keys.Down when Analysis.Completions.Count > 0:
                MoveSelection(1); key.Handled = true; break;
            case Keys.Tab when Analysis.Completions.Count == 1 || SelectedCompletion is not null:
                key.Handled = ApplySelected(); break;
        }
    }
}
