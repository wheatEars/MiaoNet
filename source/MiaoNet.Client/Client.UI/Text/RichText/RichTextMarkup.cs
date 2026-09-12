using System.Text;
using System.Collections.Immutable;
using Celeste.Mod.UIHelper.Styling;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.MiaoNet.Client.UI.Text;

/// <summary>Application-level parser compatible with MiaoNet ChatText escape sequences.</summary>
public static class RichTextMarkup
{
    private static readonly Color[] CommonColors =
    [
        new(0x00, 0x00, 0x00), new(0x00, 0x00, 0xaa), new(0x00, 0xaa, 0x00), new(0x00, 0xaa, 0xaa),
        new(0xaa, 0x00, 0x00), new(0xaa, 0x00, 0xaa), new(0xff, 0xaa, 0x00), new(0xaa, 0xaa, 0xaa),
        new(0x55, 0x55, 0x55), new(0x55, 0x55, 0xff), new(0x55, 0xff, 0x55), new(0x55, 0xff, 0xff),
        new(0xff, 0x55, 0x55), new(0xff, 0x55, 0xff), new(0xff, 0xff, 0x55), new(0xff, 0xff, 0xff)
    ];

    public static ImmutableArray<RichTextSegment> Parse(string input, Color defaultColor)
    {
        if (input.Length == 0) return [];
        var result = new List<RichTextSegment>();
        var buffer = new StringBuilder(input.Length);
        var color = defaultColor;
        var decorations = TextDecoration.None;

        for (var index = 0; index < input.Length; index++)
        {
            var character = input[index];
            if (character != '\\' || index + 1 >= input.Length)
            {
                buffer.Append(character);
                continue;
            }

            var command = input[index + 1];
            if (command == '\\')
            {
                buffer.Append('\\');
                index++;
                continue;
            }

            if (command == '#' && index + 7 < input.Length && TryHex(input.AsSpan(index + 2, 6), out var customColor))
            {
                Flush();
                color = customColor;
                index += 7;
                continue;
            }

            var handled = true;
            switch (command)
            {
                case 'r': Flush(); color = defaultColor; decorations = TextDecoration.None; break;
                case 'u': Flush(); decorations ^= TextDecoration.Underline; break;
                case 's': Flush(); decorations ^= TextDecoration.Strikethrough; break;
                case 'o': Flush(); decorations ^= TextDecoration.Outline; break;
                default:
                    var colorIndex = HexValue(command);
                    if (colorIndex >= 0) { Flush(); color = CommonColors[colorIndex]; }
                    else handled = false;
                    break;
            }

            if (handled) index++;
            else buffer.Append('\\');
        }

        Flush();
        return result.ToImmutableArray();

        void Flush()
        {
            if (buffer.Length == 0) return;
            result.Add(new(buffer.ToString(), new TextStyle { Color = color, Decorations = decorations }));
            buffer.Clear();
        }
    }

    private static bool TryHex(ReadOnlySpan<char> value, out Color color)
    {
        var r1 = HexValue(value[0]); var r2 = HexValue(value[1]);
        var g1 = HexValue(value[2]); var g2 = HexValue(value[3]);
        var b1 = HexValue(value[4]); var b2 = HexValue(value[5]);
        if ((r1 | r2 | g1 | g2 | b1 | b2) < 0) { color = default; return false; }
        color = new((byte)(r1 << 4 | r2), (byte)(g1 << 4 | g2), (byte)(b1 << 4 | b2));
        return true;
    }

    private static int HexValue(char value) => value switch
    {
        >= '0' and <= '9' => value - '0',
        >= 'a' and <= 'f' => value - 'a' + 10,
        >= 'A' and <= 'F' => value - 'A' + 10,
        _ => -1
    };
}
