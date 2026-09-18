using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using TransparentHotkeyUtility.Models;
using TransparentHotkeyUtility.Presentation.Figure;
using Color = System.Windows.Media.Color;

namespace TransparentHotkeyUtility.Services;

/// <summary>Загрузка и разбор <c>circles.css</c> рядом с exe.</summary>
internal static class CircleStyleService
{
    private static readonly string FilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "circles.css");

    private static readonly Regex BlockRx = new(
        @"\.circle\s*\{([^}]*)\}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex PropRx = new(
        @"([a-zA-Z0-9\-]+)\s*:\s*([^;]+);",
        RegexOptions.CultureInvariant);

    /// <summary>Читает circles.css; при отсутствии/ошибке — стиль по умолчанию (как раньше в коде).</summary>
    public static CircleStyle Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return CircleStyle.Default;

            var css = File.ReadAllText(FilePath);
            css = Regex.Replace(css, @"/\*.*?\*/", " ", RegexOptions.Singleline);

            var m = BlockRx.Match(css);
            if (!m.Success)
                return CircleStyle.Default;

            var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match pm in PropRx.Matches(m.Groups[1].Value))
                props[pm.Groups[1].Value.Trim()] = pm.Groups[2].Value.Trim();

            return Parse(props);
        }
        catch
        {
            return CircleStyle.Default;
        }
    }

    private static CircleStyle Parse(Dictionary<string, string> p)
    {
        var fillMode = CircleFillMode.Gradient;
        if (p.TryGetValue("fill-mode", out var fm))
        {
            fillMode = fm.Equals("solid", StringComparison.OrdinalIgnoreCase)
                ? CircleFillMode.Solid
                : CircleFillMode.Gradient;
        }

        float lighten = 0.35f, darken = 0.35f;
        if (p.TryGetValue("gradient-lighten", out var gl) && TryFloat(gl, out var glv))
            lighten = Clamp01(glv);
        if (p.TryGetValue("gradient-darken", out var gd) && TryFloat(gd, out var gdv))
            darken = Clamp01(gdv);

        var textColor = Color.FromArgb(230, 255, 255, 255);
        if (p.TryGetValue("color", out var colorRaw) && TryParseColor(colorRaw, out var tc))
            textColor = tc;

        bool borderNone = false;
        Color? borderColor = null;
        if (p.TryGetValue("border-color", out var bc))
        {
            if (bc.Equals("none", StringComparison.OrdinalIgnoreCase))
                borderNone = true;
            else if (!bc.Equals("auto", StringComparison.OrdinalIgnoreCase)
                     && TryParseColor(bc, out var bcol))
                borderColor = bcol;
        }

        double borderWidth = 1.5;
        if (p.TryGetValue("border-width", out var bw) && TryLengthPx(bw, out var bwv))
            borderWidth = Math.Max(0, bwv);

        double borderOpacity = 140.0 / 255.0;
        if (p.TryGetValue("border-opacity", out var bo) && TryFloat(bo, out var bov))
            borderOpacity = Clamp01(bov);

        var fontSize = CircleFontSize.Auto();
        if (p.TryGetValue("font-size", out var fs))
            fontSize = ParseFontSize(fs);

        var fontFamily = "Segoe UI";
        if (p.TryGetValue("font-family", out var ff) && !string.IsNullOrWhiteSpace(ff))
            fontFamily = ff.Trim().Trim('"', '\'');

        var fontWeight = System.Windows.FontWeights.SemiBold;
        if (p.TryGetValue("font-weight", out var fw))
            fontWeight = ParseFontWeight(fw);

        bool glow = true;
        if (p.TryGetValue("glow", out var glowRaw))
        {
            glow = glowRaw.Equals("on", StringComparison.OrdinalIgnoreCase)
                || glowRaw.Equals("true", StringComparison.OrdinalIgnoreCase)
                || glowRaw.Equals("1", StringComparison.OrdinalIgnoreCase);
        }

        bool magnet = true;
        if (p.TryGetValue("magnet", out var magRaw))
        {
            magnet = magRaw.Equals("on", StringComparison.OrdinalIgnoreCase)
                || magRaw.Equals("true", StringComparison.OrdinalIgnoreCase)
                || magRaw.Equals("1", StringComparison.OrdinalIgnoreCase);
        }

        double magnetRadius = 140, magnetAttract = 16, magnetRepel = 10;
        double magnetScaleNear = 1.12, magnetScaleFar = 0.92, magnetSmooth = 0.28;

        if (p.TryGetValue("magnet-radius", out var mr) && TryLengthPx(mr, out var mrv))
            magnetRadius = Math.Max(1, mrv);
        if (p.TryGetValue("magnet-attract", out var ma) && TryLengthPx(ma, out var mav))
            magnetAttract = Math.Max(0, mav);
        if (p.TryGetValue("magnet-repel", out var mrep) && TryLengthPx(mrep, out var mrepv))
            magnetRepel = Math.Max(0, mrepv);
        if (p.TryGetValue("magnet-scale-near", out var msn) && TryFloat(msn, out var msnv))
            magnetScaleNear = Math.Clamp(msnv, 0.5f, 2f);
        if (p.TryGetValue("magnet-scale-far", out var msf) && TryFloat(msf, out var msfv))
            magnetScaleFar = Math.Clamp(msfv, 0.5f, 2f);
        if (p.TryGetValue("magnet-smooth", out var ms) && TryFloat(ms, out var msv))
            magnetSmooth = Clamp01(msv);

        return new CircleStyle
        {
            FillMode         = fillMode,
            GradientLighten  = lighten,
            GradientDarken   = darken,
            TextColor        = textColor,
            BorderColor      = borderColor,
            BorderNone       = borderNone,
            BorderWidth      = borderWidth,
            BorderOpacity    = borderOpacity,
            FontSize         = fontSize,
            FontFamily       = fontFamily,
            FontWeight       = fontWeight,
            Glow             = glow,
            MagnetEnabled    = magnet,
            MagnetRadius     = magnetRadius,
            MagnetAttract    = magnetAttract,
            MagnetRepel      = magnetRepel,
            MagnetScaleNear  = magnetScaleNear,
            MagnetScaleFar   = magnetScaleFar,
            MagnetSmooth     = magnetSmooth <= 0 ? 0.28 : magnetSmooth,
        };
    }

    private static System.Windows.FontWeight ParseFontWeight(string raw)
    {
        raw = raw.Trim();
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
        {
            return n switch
            {
                <= 100 => System.Windows.FontWeights.Thin,
                <= 200 => System.Windows.FontWeights.ExtraLight,
                <= 300 => System.Windows.FontWeights.Light,
                <= 400 => System.Windows.FontWeights.Normal,
                <= 500 => System.Windows.FontWeights.Medium,
                <= 600 => System.Windows.FontWeights.SemiBold,
                <= 700 => System.Windows.FontWeights.Bold,
                <= 800 => System.Windows.FontWeights.ExtraBold,
                _      => System.Windows.FontWeights.Black,
            };
        }

        return raw.ToLowerInvariant() switch
        {
            "thin" or "100" => System.Windows.FontWeights.Thin,
            "extralight" or "ultralight" => System.Windows.FontWeights.ExtraLight,
            "light" => System.Windows.FontWeights.Light,
            "normal" or "regular" => System.Windows.FontWeights.Normal,
            "medium" => System.Windows.FontWeights.Medium,
            "semibold" or "demibold" => System.Windows.FontWeights.SemiBold,
            "bold" => System.Windows.FontWeights.Bold,
            "extrabold" or "ultrabold" => System.Windows.FontWeights.ExtraBold,
            "black" or "heavy" => System.Windows.FontWeights.Black,
            _ => System.Windows.FontWeights.SemiBold,
        };
    }

    private static CircleFontSize ParseFontSize(string raw)
    {
        raw = raw.Trim();
        if (raw.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return CircleFontSize.Auto();

        if (raw.EndsWith("px", StringComparison.OrdinalIgnoreCase)
            && TryFloat(raw[..^2], out var px))
            return CircleFontSize.Pixels(px);

        if (raw.EndsWith("r", StringComparison.OrdinalIgnoreCase)
            && TryFloat(raw[..^1], out var frac))
            return CircleFontSize.RadiusFraction(frac);

        if (TryFloat(raw, out var bare))
            return CircleFontSize.Pixels(bare);

        return CircleFontSize.Auto();
    }

    internal static bool TryParseColor(string raw, out Color color)
    {
        color = default;
        raw = raw.Trim();

        if (raw.Equals("white", StringComparison.OrdinalIgnoreCase))
        {
            color = ColorsWhite;
            return true;
        }
        if (raw.Equals("black", StringComparison.OrdinalIgnoreCase))
        {
            color = ColorsBlack;
            return true;
        }
        if (raw.Equals("transparent", StringComparison.OrdinalIgnoreCase))
        {
            color = Color.FromArgb(0, 0, 0, 0);
            return true;
        }

        // rgb(r, g, b) / rgba(r, g, b, a)
        if (raw.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var open  = raw.IndexOf('(');
            var close = raw.LastIndexOf(')');
            if (open > 0 && close > open)
            {
                var parts = raw[(open + 1)..close].Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length >= 3
                    && byte.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var r)
                    && byte.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var g)
                    && byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var b))
                {
                    byte a = 255;
                    if (parts.Length >= 4
                        && double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var af))
                    {
                        a = af <= 1.0
                            ? (byte)Math.Clamp((int)Math.Round(af * 255), 0, 255)
                            : (byte)Math.Clamp((int)Math.Round(af), 0, 255);
                    }
                    color = Color.FromArgb(a, r, g, b);
                    return true;
                }
            }
        }

        try
        {
            color = ColorUtils.Parse(raw);
            // ColorUtils.Parse при ошибке возвращает fallback — считаем успехом только hex-подобные
            var h = raw.TrimStart('#');
            return h.Length is 3 or 6 or 8;
        }
        catch
        {
            return false;
        }
    }

    private static readonly Color ColorsWhite = Color.FromRgb(255, 255, 255);
    private static readonly Color ColorsBlack = Color.FromRgb(0, 0, 0);

    private static bool TryFloat(string s, out float v) =>
        float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);

    private static bool TryLengthPx(string s, out double v)
    {
        s = s.Trim();
        if (s.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            s = s[..^2];
        return double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
    }

    private static float Clamp01(float v) => Math.Clamp(v, 0f, 1f);
    private static double Clamp01(double v) => Math.Clamp(v, 0.0, 1.0);
}
