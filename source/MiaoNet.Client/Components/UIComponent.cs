using Celeste.Mod.ChatInputBox;
using Celeste.Mod.MiaoNet.Client.UI.Chat;
using Celeste.Mod.MiaoNet.Client.UI.Chat.Widgets;
using Celeste.Mod.MiaoNet.Client.UI.Input;
using Celeste.Mod.MiaoNet.Client.UI.PlayerList.Widgets;
using Celeste.Mod.MiaoNet.Client.UI.PlayerList;
using Celeste.Mod.MiaoNet.Client.UI.Settings;
using Celeste.Mod.UIHelper.Controls;
using Celeste.Mod.UIHelper.Core;
using Celeste.Mod.UIHelper.Rendering;
using Celeste.Mod.UIHelper.Runtime;
using Celeste.Mod.UIHelper.Styling;
using Celeste.Mod.UIHelper.Widgets;
using MiaoNet.Shared;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.MiaoNet;

/// <summary>
/// Owns the whole UIHelper UI lifecycle: the runtime and input bus, the shared
/// chat/player-list controllers, the widget tree and its rendering. Chat and
/// player-list business state stays in their components; this component only
/// translates it into widgets and input actions.
/// </summary>
public sealed class UIComponent : MiaoNetComponent
{
    private sealed class PauseUpdateOverlay : Overlay
    {
        public override void Update()
        {
            base.Update();
            if (Engine.Scene is not Level level) return;
            foreach (Entity entity in Engine.Scene[Tags.PauseUpdate])
                if (entity.Active && entity is not TextMenu)
                    entity.Update();
            level.HudRenderer.BackgroundFade = Calc.Approach(
                level.HudRenderer.BackgroundFade,
                level.Paused ? 1f : 0f,
                8f * Engine.RawDeltaTime);
        }
    }

    private readonly UiRuntime runtime = new();
    private readonly CelesteDrawBackend drawBackend = new();
    private readonly MiaoNetUiInputAdapter input;
    private readonly MiaoNetUiInputBindings inputBindings;
    private readonly ChatController chat;
    private readonly PlayerListController playerList;
    private readonly ChatMessageListDataSource chatMessages;
    private readonly ChatComponent chatData;
    private readonly ScrollController scroll = new();
    private readonly MiaoNetSettingsObserver settingsObserver;
    private readonly ChatItemWidgetFactory itemFactory;
    private readonly TextEditingController editor = new() { MaxLength = 64 };
    private readonly ChatTextFieldDisplayController displayController;
    private readonly ChatCompletionProvider completionProvider;
    private readonly CommandParser commandParser;
    private readonly List<string> inputHistory = [];
    private int historyIndex;
    private string lastInput = string.Empty;
    private bool previousCommandsEnabled;
    private bool previousScenePaused;
    private bool previousAllowHudHide = true;
    private readonly PauseUpdateOverlay dummyOverlay = new();
    private bool disposed;

    public bool Active => chat.IsOpen;
    public ChatController Controller => chat;

    public UIComponent(MiaoNetContext context) : base(context)
    {
        input = new MiaoNetUiInputAdapter(runtime);
        chat = new ChatController(input);
        playerList = new PlayerListController(
            input, () => MiaoNetModule.Settings.PlayerListButtonMode);
        inputBindings = new MiaoNetUiInputBindings(input, MiaoNetModule.Settings);

        chatData = context.ChatComponent;
        settingsObserver = new MiaoNetSettingsObserver(MiaoNetModule.Settings);
        itemFactory = new ChatItemWidgetFactory(settingsObserver);
        commandParser = new CommandParser(MiaoNetCommand.Commands);
        completionProvider = new ChatCompletionProvider(context, commandParser);
        displayController = new ChatTextFieldDisplayController(
            editor,
            new MiaoNetChatLanguageService(completionProvider),
            input.Events);
        chatMessages = new ChatMessageListDataSource(chatData, itemFactory.Build);

        chat.CanOpen = () => !playerList.IsOpen;
        chat.ActiveTabChanged += OnActiveTabChanged;
        chat.Opened += ActivateInput;
        chat.Closed += DeactivateInput;
        playerList.Opened += ActivatePlayerList;
        playerList.Closed += DeactivatePlayerList;
        chat.CommandRequested += OpenCommandInput;
        ResetTabs();
    }

    public override void Update()
    {
        var deltaTime = Engine.RawDeltaTime;
        // Input is polled before the tree is rebuilt so this frame's layout can
        // clamp the scroll controllers against the freshly measured content.
        playerList.BeginFrame();
        input.Poll(deltaTime);
        playerList.EndFrame();
        scroll.Update(deltaTime);
        playerList.Scroll.Update(deltaTime);
        runtime.SetRoot(BuildRoot());
    }

    public override void Render()
        => runtime.PaintFrame(drawBackend, Engine.Width, Engine.Height);

    public override void OnConnected()
        => ResetTabs();

    public override void OnDisconnected()
    {
        chat.Close();
        playerList.Close();
        runtime.SetRoot(null);
        chat.ClearTabs();
        inputHistory.Clear();
        historyIndex = 0;
    }

    public void SendChat(string text)
        => context.QueuePacket(new PacketSendChatMessage(MiaoNetModule.Settings.ChatChannel, text));

    public void HandleCommand(string text)
    {
        var result = commandParser.Parse(text, out var name, out var command, out var args);
        chatData.AddLocalChat(MiaoNetChatText.CreateCommandEcho(text));
        if (result != CommandParser.ParseResult.Success)
        {
            chatData.AddLocalChat(MiaoNetChatText.CreateCommandError(name));
            return;
        }
        var error = command!.OnExecute(new MiaoNetCommand.Context(context, args!));
        if (error is not null) chatData.AddLocalChat(MiaoNetChatText.CreateCommandError(error));
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        chat.Opened -= ActivateInput;
        chat.Closed -= DeactivateInput;
        playerList.Opened -= ActivatePlayerList;
        playerList.Closed -= DeactivatePlayerList;
        chat.CommandRequested -= OpenCommandInput;
        chat.ActiveTabChanged -= OnActiveTabChanged;
        chatMessages.Dispose();
        inputBindings.Dispose();
        displayController.Dispose();
        settingsObserver.Dispose();
        chat.Dispose();
        playerList.Dispose();
        input.Dispose();
        runtime.Dispose();
    }

    private Widget BuildRoot()
    {
        const float margin = 16f;
        const float spacing = 2f;
        var settings = MiaoNetModule.Settings;
        var scale = settings.ChatUIScaleValue;
        var lineHeight = MiaoNetFont.ENZhsLineHeight * scale;
        var width = MathF.Max(0f, Engine.Width - 2f * margin);
        var availableHeight = MathF.Max(0f, Engine.Height - 2f * margin);
        var inputHeight = lineHeight + 16f;
        var tabHeight = lineHeight;
        var chromeHeight = Active ? inputHeight + tabHeight + 2f * spacing : 0f;
        var maxListHeight = MathF.Max(0f, availableHeight - chromeHeight);
        var listRatio = Active ? settings.ActiveChatHeightValue : settings.IdleChatHeightValue;
        var listHeight = MathF.Min(maxListHeight,
            availableHeight * Math.Clamp(listRatio, 0f, 1f));
        var itemExtent = lineHeight + 2f * Math.Max(0f, settings.ChatMessagePadding);
        var input = new ChatInputWidget
        {
            Editor = editor,
            DisplayController = displayController,
            Width = width,
            Scale = scale,
            LineHeight = lineHeight,
            OnSubmitted = SubmitInput
        };
        var list = new ChatMessageListViewWidget
        {
            Key = "miaonet-chat-list",
            Messages = chatMessages,
            ScrollController = scroll,
            Width = width,
            Height = listHeight,
            ItemExtent = itemExtent,
            Padding = 0f
        };
        var chatRoot = new VBox
        {
            Key = "miaonet-chat-root",
            Items = Active
            ? new Widget[]
            {
                list,
                new ChatTabLIstView
                {
                    Controller = chat,
                    Scale = scale,
                    LineHeight = lineHeight
                },
                input
            }
            : new Widget[] { list },
            Alignment = MainAxisAlignment.End,
            CrossAlignment = CrossAxisAlignment.Stretch,
            Style = new UiStyle
            {
                Width = Engine.Width,
                Height = Engine.Height,
                // Legacy Chat Render: keep the 16px screen margin on every
                // edge, including the bottom-left input corner.
                Padding = new EdgeInsets(margin)
            }
        };
        var layers = new List<Widget> { chatRoot };
        if (playerList.IsOpen)
        {
            var playerPreview = new PlayerListWidget
            {
                Source = context.PlayerListComponent,
                Controller = playerList,
                Scale = settings.PlayerListUIScaleValue
            };
            // Legacy Render: panel width is derived from the widest row and is
            // not constrained to Chat's 16px content width.
            var playerWidth = MathF.Max(280f, playerPreview.EstimateWidth());
            layers.Add(new Align
            {
                Alignment = UiAlignment.TopLeft,
                Style = new UiStyle
                {
                    Width = Engine.Width,
                    Height = Engine.Height
                },
                Child = new PlayerListWidget
                {
                    Source = context.PlayerListComponent,
                    Controller = playerList,
                    Width = playerWidth,
                    // Legacy Render used the whole screen for the list. The panel
                    // sizes itself to its measured content and the ScrollView
                    // caps the viewport at this height, so the clip can never be
                    // shorter than the list the player is scrolling through.
                    MaxHeight = Engine.Height,
                    Scale = settings.PlayerListUIScaleValue
                }
            });
        }
        return new Stack
        {
            Key = "miaonet-ui-root",
            Items = layers,
            Style = new UiStyle
            {
                Width = Engine.Width,
                Height = Engine.Height
            }
        };
    }

    private void SubmitInput(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0) { chat.Close(); return; }
        inputHistory.Add(trimmed);
        historyIndex = inputHistory.Count;
        if (trimmed.StartsWith(CommandParser.CommandPrefix, StringComparison.Ordinal)) HandleCommand(trimmed);
        else if (!MiaoNetModule.Settings.LiveMode) SendChat(trimmed);
        else chatData.AddLocalChat(MiaoNetChatText.CreateCommandError(Dialog.Get("miaonet_chat_disabled")));
        chat.Close();
    }

    private void ActivateInput()
    {
        if (playerList.IsOpen)
            playerList.Close();
        if (!context.IsSuitableToOpenUI)
        {
            chat.Close();
            return;
        }
        historyIndex = inputHistory.Count;
        input.EnableTextInput();
        previousCommandsEnabled = Engine.Commands.Enabled;
        Engine.Commands.Enabled = false;
        previousScenePaused = Engine.Scene.Paused;
        Engine.Scene.Paused = true;
        if (Engine.Scene is Level level)
        {
            previousAllowHudHide = level.AllowHudHide;
            level.Add(dummyOverlay);
            level.AllowHudHide = false;
        }
        context.HasComponentFocus = true;
        chatData.SetChatActive(true);
        runtime.SetRoot(BuildRoot());
        runtime.FocusByKey("miaonet-chat-input");
    }

    private void DeactivateInput()
    {
        chatData.SetChatActive(false);
        runtime.ClearFocus();
        input.DisableTextInput();
        editor.Clear();
        lastInput = string.Empty;
        Engine.Commands.Enabled = previousCommandsEnabled;
        Engine.Scene.Paused = previousScenePaused;
        if (Engine.Scene is Level level)
        {
            level.CompletelyRemove(dummyOverlay);
            level.AllowHudHide = previousAllowHudHide;
        }
        if (context.HasComponentFocus)
            context.HasComponentFocus = false;
    }

    private void ActivatePlayerList()
    {
        if (!context.IsSuitableToOpenUI)
        {
            playerList.Close();
            return;
        }
        if (chat.IsOpen)
            chat.Close();
        context.HasComponentFocus = true;
        context.PlayerListComponent.Active = true;
        runtime.SetRoot(BuildRoot());
    }

    private void DeactivatePlayerList()
    {
        if (!chat.IsOpen)
            context.HasComponentFocus = false;
        context.PlayerListComponent.Active = false;
        runtime.SetRoot(BuildRoot());
    }

    private void OpenCommandInput()
    {
        editor.SetText(CommandParser.CommandPrefix);
        runtime.SetRoot(BuildRoot());
        runtime.FocusByKey("miaonet-chat-input");
    }

    private void ResetTabs()
    {
        chat.ClearTabs();
        chat.AddTab(Dialog.Get("miaonet_initial_chat_tab_name"));
        foreach (var tab in Enum.GetValues<ChatChannel>())
            chat.AddTab(ChatChannelMatcher.GetLocalizedName(tab) ?? tab.ToString());
        chat.SetActiveTab(null);
    }

    private void OnActiveTabChanged(int index)
    {
        var channel = index < 0 ? (ChatChannel?)null : (ChatChannel)index;
        chatData.SetActiveChannel(channel);
        MiaoNetModule.Settings.ChatChannel = channel ?? ChatChannel.Global;
    }
}
