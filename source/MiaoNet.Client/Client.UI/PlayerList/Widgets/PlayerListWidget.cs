using Celeste.Mod.MiaoNet.Client.UI.Text;
using Celeste.Mod.UIHelper.Controls;
using Celeste.Mod.UIHelper.Core;
using Celeste.Mod.UIHelper.Rendering;
using Celeste.Mod.UIHelper.Styling;
using Celeste.Mod.UIHelper.Widgets;
using MiaoNet.Shared;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.MiaoNet.Client.UI.PlayerList.Widgets;

/// <summary>UIHelper rendering for the online players grouped by channel.</summary>
public sealed class PlayerListWidget : StatelessWidget
{
    // Single source of truth for the panel's inner insets. The scroll viewport
    // is derived from these, never from a separately-estimated entry count.
    private static readonly EdgeInsets PanelPadding = new(16f, 16f, 24f, 16f);

    public required global::Celeste.Mod.MiaoNet.PlayerListComponent Source { get; init; }
    public required PlayerListController Controller { get; init; }
    public float Width { get; init; }
    /// <summary>Screen-space cap; the panel shrinks to its measured content below it.</summary>
    public float MaxHeight { get; init; }
    public float Scale { get; init; } = 1f;

    public float EstimateWidth()
    {
        var lineHeight = MiaoNetFont.ENZhsLineHeight * Scale;
        var columns = MeasureColumns(Source.Channels, lineHeight);
        var header = Source.Channels.Count == 0
            ? 0f
            : Source.Channels.Max(channel => Measure(channel.Header, Scale));
        var content = columns.Left + columns.Middle + columns.Room + columns.Colon
            + columns.Map + columns.Icon + columns.Mode + columns.Ping
            // Four separators: room|':' is packed together, then ':'->map,
            // map->mode, mode->icon and icon->ping.
            + columns.Space * 4f;
        // Mirror the actual constraint chain exactly:
        // outer PlayerList padding (16 + 24), channel padding (16 * 2),
        // and player-row padding (4 * 2). The previous estimate omitted the
        // latter two levels, making the viewport 40px narrower than the row.
        const float channelPadding = 16f * 2f;
        const float rowPadding = 4f * 2f;
        return MathF.Max(
            header + PanelPadding.Horizontal + channelPadding,
            content + PanelPadding.Horizontal + channelPadding + rowPadding);
    }

    public override Widget Build(BuildContext context)
    {
        _ = context;
        var renderer = MiaoNetTextRenderer.Instance;
        var lineHeight = MiaoNetFont.ENZhsLineHeight * Scale;
        var channels = Source.Channels;
        var columns = MeasureColumns(channels, lineHeight);
        var channelWidgets = new List<Widget>(channels.Count);

        foreach (var channel in channels)
        {
            var rows = new List<Widget>
            {
                new TextWidget
                {
                    Key = $"player-list-channel-{channel.Channel.ID}",
                    Text = channel.Header,
                    Style = TextStyle(Color.Yellow, renderer, Scale, lineHeight)
                }
            };

            for (var index = 0; index < channel.Players.Count; index++)
            {
                var entry = channel.Players[index];
                var player = entry.Player;
                rows.Add(new BoxWidget
                {
                    Key = $"player-list-player-{player.ID}",
                    Style = new UiStyle
                    {
                        Padding = new EdgeInsets(4f, 2f),
                        Background = index % 2 == 0
                            ? new Color(0x00, 0x00, 0x00, 0x22)
                            : new Color(0x22, 0x22, 0x22, 0x88)
                    },
                    Child = BuildPlayerRow(entry, columns, renderer, lineHeight)
                });
            }

            channelWidgets.Add(new BoxWidget
            {
                Key = $"player-list-channel-panel-{channel.Channel.ID}",
                Style = new UiStyle
                {
                    // Legacy Render: RectXPadding/RectYPadding are 16px.
                    Padding = new EdgeInsets(16f),
                    Background = Color.Black * (0xcc / 255f),
                    BorderColor = Color.CornflowerBlue,
                    BorderWidth = 3f
                },
                Child = new VBox
                {
                    Items = rows,
                    Spacing = 0f,
                    Alignment = MainAxisAlignment.Start,
                    CrossAlignment = CrossAxisAlignment.Stretch
                }
            });
        }

        var content = new VBox
        {
            Key = "player-list-content",
            Items = channelWidgets,
            // Legacy Render: RectYMargin is 16px before and after each panel.
            Spacing = 32f,
            Alignment = MainAxisAlignment.Start,
            CrossAlignment = CrossAxisAlignment.Stretch
        };

        return new BoxWidget
        {
            Key = "player-list-panel",
            Style = new UiStyle
            {
                Width = Width,
                MaxHeight = MaxHeight,
                Padding = PanelPadding
            },
            Child = new ScrollView
            {
                Controller = Controller.Scroll,
                Step = lineHeight,
                FadeItemsAtEdges = false,
                Child = content,
                // No explicit height: the ScrollView measures the real content
                // and is capped by the panel's padded constraints, so its clip
                // and the ScrollController's Maximum always agree with it.
                Style = new UiStyle
                {
                    Width = MathF.Max(0f, Width - PanelPadding.Horizontal)
                }
            }
        };
    }

    private Widget BuildPlayerRow(
        global::Celeste.Mod.MiaoNet.PlayerListComponent.PlayerListEntry entry,
        ColumnWidths columns,
        ITextRenderer renderer,
        float lineHeight)
    {
        var player = entry.Player;
        var room = entry.Player.Location.IsInDebugMap
            ? string.Empty
            : entry.MapRoom ?? string.Empty;
        if (MiaoNetModule.Settings.LiveMode && !entry.Player.Location.IsEmpty)
            room = "*";
        var map = entry.Player.Location.IsEmpty
            ? string.Empty
            : entry.IsLocallyKnownMap ? entry.MapName ?? string.Empty : MiaoNetModule.Settings.LiveMode ? "*" : entry.MapName ?? string.Empty;
        var mode = entry.AreaModeText ?? string.Empty;

        var leftItems = new List<Widget>
        {
            new TextWidget
            {
                Text = entry.DisplayName,
                Style = TextStyle(entry.Player.Info.Color, renderer, Scale, lineHeight)
            }
        };

        // Legacy Render: status textures are drawn left-to-right immediately
        // after the player name; they are intentionally not column-aligned.
        var statusIcons = GetStatusIcons(player);
        for (var iconIndex = 0; iconIndex < statusIcons.Count; iconIndex++)
        {
            var icon = statusIcons[iconIndex];
            leftItems.Add(new MTextureWidget
            {
                // Include the texture identity so a removed interaction icon
                // cannot be reused for a newly enabled live-mode icon.
                Key = $"player-list-status-{player.ID}-{iconIndex}-{icon.GetHashCode()}",
                Texture = icon,
                Width = lineHeight * icon.Width / Math.Max(1f, icon.Height),
                Height = lineHeight,
                Style = new UiStyle { Margin = new EdgeInsets(2f, 0f) }
            });
        }

        var rightItems = new List<Widget>
        {
            // "room: map" is one location unit, not three aligned columns. The
            // colon has to sit directly after the room text and the map directly
            // after the colon, so the pieces flow as contiguous segments inside a
            // single right-aligned widget. (Aligning them separately pushed the
            // colon away from the room and the map away from the colon.)
            LocationBlock(room, entry.Player.Location.IsEmpty ? string.Empty : ":",
                map, entry.MapNameColor, renderer, columns, lineHeight),
            Field(mode, columns.Mode, entry.MapSideColor, renderer, Scale, lineHeight)
        };

        if (entry.AreaIconTexture is { } areaIcon)
        {
            rightItems.Add(new Align
            {
                Alignment = UiAlignment.TopRight,
                Style = new UiStyle { Width = columns.Icon, Height = lineHeight },
                Child = new MTextureWidget
                {
                    Key = $"player-list-area-icon-{player.ID}",
                    Texture = areaIcon,
                    Width = MathF.Min(columns.Icon, lineHeight * areaIcon.Width / Math.Max(1f, areaIcon.Height)),
                    Height = lineHeight
                }
            });
        }
        else
        {
            rightItems.Add(new Spacer { Width = columns.Icon, Height = lineHeight });
        }

        // Legacy Render: ping is right-justified with PlayerEntryXPadding
        // inside the panel, rather than touching the final column edge.
        rightItems.Add(Field(entry.PingText ?? string.Empty, columns.Ping,
            Color.LightGray, renderer, Scale, lineHeight, 4f));

        return new HBox
        {
            Items =
            [
                new BoxWidget
                {
                    Style = new UiStyle { Width = columns.Left, Height = lineHeight },
                    Child = new HBox
                    {
                        Items = leftItems,
                        Spacing = 0f,
                        CrossAlignment = CrossAxisAlignment.Start
                    }
                },
                new HBox
                {
                    Items = rightItems,
                    Spacing = columns.Space,
                    CrossAlignment = CrossAxisAlignment.Start
                }
            ],
            Spacing = columns.Middle,
            CrossAlignment = CrossAxisAlignment.Start
        };
    }

    private static Widget Field(string text, float width, Color color,
        ITextRenderer renderer, float scale, float lineHeight, float rightInset = 0f)
        => new Align
        {
            Alignment = UiAlignment.TopRight,
            Style = new UiStyle
            {
                // Align.Measure includes padding in its desired size. Keep
                // the field's total width equal to the measured column while
                // reserving the inset inside it.
                Width = MathF.Max(0f, width - rightInset),
                Height = lineHeight,
                Padding = rightInset > 0f ? new EdgeInsets(0f, 0f, rightInset, 0f) : null
            },
            Child = new TextWidget
            {
                Text = text,
                Style = TextStyle(color, renderer, scale, lineHeight)
            }
        };

    /// <summary>
    /// Lays out the location as one contiguous, right-aligned unit: room, then the
    /// colon, then a single space, then the map name (with the map's own colour).
    /// The unit reserves the same total width on every row so the surrounding
    /// columns still line up, but inside it the segments are packed together
    /// instead of being right-aligned in separate columns.
    /// </summary>
    private Widget LocationBlock(string room, string colon, string map, Color mapColor,
        ITextRenderer renderer, ColumnWidths columns, float lineHeight)
    {
        var items = new List<Widget>(4);
        if (room.Length > 0)
            items.Add(new TextWidget
            {
                Text = room,
                Style = TextStyle(Color.LightGray, renderer, Scale, lineHeight)
            });
        if (colon.Length > 0)
            items.Add(new TextWidget
            {
                Text = colon,
                Style = TextStyle(Color.LightGray, renderer, Scale, lineHeight)
            });
        if (map.Length > 0)
        {
            items.Add(new Spacer { Width = columns.Space, Height = lineHeight });
            items.Add(new TextWidget
            {
                Text = map,
                Style = TextStyle(mapColor, renderer, Scale, lineHeight)
            });
        }

        return new Align
        {
            Alignment = UiAlignment.TopRight,
            Style = new UiStyle
            {
                Width = columns.Room + columns.Colon + columns.Space + columns.Map,
                Height = lineHeight
            },
            Child = new HBox
            {
                Items = items,
                Spacing = 0f,
                CrossAlignment = CrossAxisAlignment.Start
            }
        };
    }

    private ColumnWidths MeasureColumns(
        IReadOnlyList<global::Celeste.Mod.MiaoNet.PlayerListComponent.PlayerListChannelEntry> channels,
        float lineHeight)
    {
        var scale = Scale;
        var space = MiaoNetTextRenderer.Instance.Measure(" ", new TextStyle { Scale = scale }).Width;
        var left = 0f;
        var room = 0f;
        var map = 0f;
        var mode = 0f;
        var ping = 0f;
        var icon = 0f;
        foreach (var entry in channels.SelectMany(channel => channel.Players))
        {
            var player = entry.Player;
            var entryLeft = Measure(entry.DisplayName, scale);
            foreach (var statusIcon in GetStatusIcons(player))
                entryLeft += lineHeight * statusIcon.Width / Math.Max(1f, statusIcon.Height) + 4f * scale;
            left = MathF.Max(left, entryLeft);
            var roomText = player.Location.IsInDebugMap ? string.Empty : entry.MapRoom ?? string.Empty;
            if (MiaoNetModule.Settings.LiveMode && !player.Location.IsEmpty) roomText = "*";
            var mapText = player.Location.IsEmpty ? string.Empty : entry.IsLocallyKnownMap
                ? entry.MapName ?? string.Empty
                : MiaoNetModule.Settings.LiveMode ? "*" : entry.MapName ?? string.Empty;
            room = MathF.Max(room, Measure(roomText, scale));
            map = MathF.Max(map, Measure(mapText, scale));
            mode = MathF.Max(mode, Measure(entry.AreaModeText ?? string.Empty, scale));
            ping = MathF.Max(ping, Measure(entry.PingText ?? string.Empty, scale));
            if (entry.AreaIconTexture is { } texture)
                icon = MathF.Max(icon, lineHeight * texture.Width / Math.Max(1f, texture.Height));
        }
        return new(left, room, Measure(":", scale), map, mode, ping,
            MathF.Max(icon, lineHeight), space, 32f * scale);
    }

    private IReadOnlyList<MTexture> GetStatusIcons(global::Celeste.Mod.MiaoNet.OnlinePlayer player)
    {
        var icons = new List<MTexture>(5);
        if (player.GlobalFlags.HasFlag(PlayerGlobalFlags.Paused)) icons.Add(Source.PausedStatusIcon);
        var isSelf = Source.IsSelf(player);
        if ((isSelf ? MiaoNetModule.Settings.PlayerInteractions : player.GlobalFlags.HasFlag(PlayerGlobalFlags.Interactions)))
            icons.Add(Source.InteractionsStatusIcon);
        if ((isSelf ? MiaoNetModule.Settings.LiveMode : player.GlobalFlags.HasFlag(PlayerGlobalFlags.LiveMode)))
            icons.Add(Source.LiveStatusIcon);
        if (player.GlobalFlags.HasFlag(PlayerGlobalFlags.TakingGolden)) icons.Add(Source.TakingGoldenStatusIcon);
        if (player.GlobalFlags.HasFlag(PlayerGlobalFlags.GroupPhotoMode)) icons.Add(Source.GroupPhotoStatusIcon);
        if (player.GlobalFlags.HasFlag(PlayerGlobalFlags.Watching)) icons.Add(Source.DebugMapIcon);
        return icons;
    }

    private static float Measure(string text, float scale)
        => MiaoNetFont.Measure(text).X * scale;

    private static UiStyle TextStyle(Color color, ITextRenderer renderer,
        float scale, float lineHeight)
        => new()
        {
            Foreground = color,
            FontScale = scale,
            LineHeight = lineHeight,
            TextRenderer = renderer
        };

    private readonly record struct ColumnWidths(
        float Left, float Room, float Colon, float Map, float Mode, float Ping,
        float Icon, float Space, float Middle);

    private sealed class MTextureWidget : RenderObjectWidget
    {
        public required MTexture Texture { get; init; }
        public float Width { get; init; }
        public float Height { get; init; }
        public override RenderObject CreateRenderObject(UiStyle style)
            => new MTextureRenderObject(style, Texture, Width, Height);
        public override void UpdateRenderObject(RenderObject renderObject, UiStyle style)
        {
            base.UpdateRenderObject(renderObject, style);
            ((MTextureRenderObject)renderObject).Update(Texture, Width, Height);
        }
    }

    private sealed class MTextureRenderObject(
        UiStyle style, MTexture texture, float width, float height) : RenderObject(style)
    {
        private MTexture texture = texture;
        private float width = width;
        private float height = height;

        public void Update(MTexture nextTexture, float nextWidth, float nextHeight)
        {
            texture = nextTexture;
            width = nextWidth;
            height = nextHeight;
        }

        public override UiSize Measure(BoxConstraints constraints)
            => DesiredSize = constraints.Constrain(new(width, height));

        public override void Paint(IUiCanvas canvas)
        {
            var opacity = Style.Opacity ?? 1f;
            var textureScale = Bounds.Height / Math.Max(1f, texture.Height);
            texture.Draw(new(Bounds.X, Bounds.Y), Vector2.Zero,
                Color.White * opacity, Vector2.One * textureScale);
        }
    }
}
