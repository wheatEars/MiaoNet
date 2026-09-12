using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Celeste.Mod.MiaoNet;

public sealed partial class PlayerListComponent
{
    public sealed class PlayerListChannelEntry
    {
        public OnlineChannel Channel { get; }
        public List<PlayerListEntry> Players { get; }
        public string Header { get; private set; }

        public PlayerListChannelEntry(OnlineChannel channel, List<PlayerListEntry> players)
        {
            Channel = channel;
            Players = players;
            Update();
        }

        [MemberNotNull(nameof(Header))]
        public void Update()
        {
            Header = PFormat.Format(
                CultureInfo.CurrentCulture,
                Dialog.Get("miaonet_player_list_channel_header"),
                Channel.Info.Name,
                Players.Count
            );
        }
    }
}
