using Celeste.Mod.UIHelper.Controls;
using Celeste.Mod.UIHelper.Widgets;

namespace Celeste.Mod.MiaoNet.Client.UI.Chat;

/// <summary>
/// Adapts an application-owned chat source to UIHelper's virtual list API.
/// Keep one instance alive across UI root rebuilds so the list can preserve its
/// mounted elements and scroll controller.
/// </summary>
public sealed class ChatMessageListDataSource : IVirtualListDataSource, IDisposable
{
    private readonly IChatMessageSource source;
    private readonly Func<ChatItem, Widget> itemBuilder;
    private IReadOnlyList<ChatItem> items = [];

    public ChatMessageListDataSource(
        IChatMessageSource source,
        Func<ChatItem, Widget> itemBuilder
    )
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.itemBuilder = itemBuilder ?? throw new ArgumentNullException(nameof(itemBuilder));
        source.Changed += OnSourceChanged;
        Refresh();
    }

    public int Count => items.Count;

    public event Action? Changed;

    public Widget BuildItem(int index) => itemBuilder(items[index]);

    public string GetItemKey(int index)
        => items[index].Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public void Refresh()
    {
        items = source.VisibleMessages;
        Changed?.Invoke();
    }

    public void Dispose() => source.Changed -= OnSourceChanged;

    private void OnSourceChanged() => Refresh();
}
