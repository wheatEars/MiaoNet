namespace Celeste.Mod.MiaoNet.Client.UI.Input;

/// <summary>
/// Registers MiaoNet's configurable UI bindings on the common input adapter.
/// The settings object remains owned by MiaoNet; this class only exposes its
/// current pressed/held state as UI events.
/// </summary>
public sealed class MiaoNetUiInputBindings : IDisposable
{
    private readonly List<IDisposable> registrations = [];

    public MiaoNetUiInputBindings(
        MiaoNetUiInputAdapter input,
        global::Celeste.Mod.MiaoNet.MiaoNetModuleSettings settings
    )
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(settings);

        registrations.Add(input.RegisterPressed(
            MiaoNetUiInputTypes.ChatToggle,
            () => settings.ChatButton.Pressed,
            () => settings.ChatButton.ConsumePress()
        ));
        registrations.Add(input.RegisterPressed(
            MiaoNetUiInputTypes.ChatCommand,
            () => settings.ChatCommandButton.Pressed,
            () => settings.ChatCommandButton.ConsumePress()
        ));
        registrations.Add(input.RegisterPressed(
            MiaoNetUiInputTypes.PlayerListToggle,
            () => settings.PlayerListButtonMode == global::Celeste.Mod.MiaoNet.ButtonMode.Press && settings.PlayerListButton.Pressed,
            () => settings.PlayerListButton.ConsumePress()
        ));
        registrations.Add(input.RegisterHeld(
            MiaoNetUiInputTypes.PlayerListToggle,
            () => settings.PlayerListButtonMode == global::Celeste.Mod.MiaoNet.ButtonMode.Hold && settings.PlayerListButton.Check
        ));
        registrations.Add(input.RegisterHeld(
            MiaoNetUiInputTypes.PlayerListScrollUp,
            () => settings.PlayerListScrollUp.Check
        ));
        registrations.Add(input.RegisterHeld(
            MiaoNetUiInputTypes.PlayerListScrollDown,
            () => settings.PlayerListScrollDown.Check
        ));
        registrations.Add(input.RegisterPressed(
            MiaoNetUiInputTypes.CreateFireworks,
            () => settings.CreateFireworksButton.Pressed,
            () => settings.CreateFireworksButton.ConsumePress()
        ));
        registrations.Add(input.RegisterPressed(
            MiaoNetUiInputTypes.EmoteWheel,
            () => settings.EmoteWheelSendEmote.Pressed,
            () => settings.EmoteWheelSendEmote.ConsumePress()
        ));

        for (var index = 0; index < settings.EmoteButtons.Count; index++)
        {
            var binding = settings.EmoteButtons[index];
            var action = MiaoNetUiInputTypes.EmoteAt(index);
            registrations.Add(input.RegisterPressed(
                action,
                () => binding.Pressed,
                binding.ConsumePress
            ));
        }
    }

    public void Dispose()
    {
        foreach (var registration in registrations)
            registration.Dispose();
        registrations.Clear();
    }
}
