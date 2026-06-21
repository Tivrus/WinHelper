using Color = System.Windows.Media.Color;

namespace TransparentHotkeyUtility.Presentation.Figure;

/// <summary>Pure colour manipulation helpers for WPF Media colours.</summary>
internal static class ColorUtils
{
    private static readonly Color Fallback = Color.FromRgb(140, 75, 255);

    /// <summary>Парсит #RRGGBB или #AARRGGBB. При ошибке возвращает фиолетовый.</summary>
    internal static Color Parse(string hex)
    {
        try
        {
            var h = hex.TrimStart('#');
            return h.Length switch
            {
                6 => Color.FromRgb(HexByte(h, 0), HexByte(h, 2), HexByte(h, 4)),
                8 => Color.FromArgb(HexByte(h, 0), HexByte(h, 2), HexByte(h, 4), HexByte(h, 6)),
                _ => Fallback,
            };
        }
        catch { return Fallback; }
    }

    internal static Color Lighten(Color c, float t) => Color.FromRgb(
        Clamp(c.R + (int)(t * 255)),
        Clamp(c.G + (int)(t * 255)),
        Clamp(c.B + (int)(t * 255)));

    internal static Color Darken(Color c, float t) => Color.FromRgb(
        Clamp(c.R - (int)(t * 255)),
        Clamp(c.G - (int)(t * 255)),
        Clamp(c.B - (int)(t * 255)));

    private static byte HexByte(string s, int i) => Convert.ToByte(s[i..(i + 2)], 16);
    private static byte Clamp(int v)              => (byte)Math.Clamp(v, 0, 255);
}
