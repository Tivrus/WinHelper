using Color = System.Windows.Media.Color;

namespace TransparentHotkeyUtility.Models;

/// <summary>Разобранный стиль кружка из <c>circles.css</c>.</summary>
internal sealed class CircleStyle
{
    public CircleFillMode FillMode { get; init; } = CircleFillMode.Gradient;

    /// <summary>0..1 — насколько осветлять центр радиального градиента.</summary>
    public float GradientLighten { get; init; } = 0.35f;

    /// <summary>0..1 — насколько затемнять край радиального градиента.</summary>
    public float GradientDarken { get; init; } = 0.35f;

    public Color TextColor { get; init; } = Color.FromArgb(230, 255, 255, 255);

    /// <summary>null = auto от цвета кружка; Transparent / A=0 = без обводки.</summary>
    public Color? BorderColor { get; init; }

    public bool BorderNone { get; init; }

    public double BorderWidth { get; init; } = 1.5;

    /// <summary>Прозрачность обводки в покое (0..1).</summary>
    public double BorderOpacity { get; init; } = 140.0 / 255.0;

    /// <summary>null = авто по длине текста и радиусу.</summary>
    public CircleFontSize FontSize { get; init; } = CircleFontSize.Auto();

    public string FontFamily { get; init; } = "Segoe UI";

    /// <summary>Толщина шрифта (WPF FontWeights).</summary>
    public System.Windows.FontWeight FontWeight { get; init; } = System.Windows.FontWeights.SemiBold;

    public bool Glow { get; init; } = true;

    /// <summary>Магнит: ближний кружок тянется к курсору и растёт, остальные чуть сжимаются и отходят.</summary>
    public bool MagnetEnabled { get; init; } = true;

    /// <summary>Радиус зоны влияния курсора (px).</summary>
    public double MagnetRadius { get; init; } = 140;

    /// <summary>Насколько ближний кружок смещается к мыши (px).</summary>
    public double MagnetAttract { get; init; } = 16;

    /// <summary>Насколько остальные отодвигаются (px).</summary>
    public double MagnetRepel { get; init; } = 10;

    /// <summary>Масштаб ближнего кружка при полной силе (1 = без изменений).</summary>
    public double MagnetScaleNear { get; init; } = 1.12;

    /// <summary>Масштаб остальных при полной силе.</summary>
    public double MagnetScaleFar { get; init; } = 0.92;

    /// <summary>Сглаживание 0..1 (больше = резче следует за мышью).</summary>
    public double MagnetSmooth { get; init; } = 0.28;

    public static CircleStyle Default { get; } = new();
}

internal enum CircleFillMode
{
    Gradient,
    Solid
}

internal readonly struct CircleFontSize
{
    public enum Kind { Auto, Pixels, RadiusFraction }

    public Kind Mode { get; }
    public double Value { get; }

    private CircleFontSize(Kind mode, double value)
    {
        Mode  = mode;
        Value = value;
    }

    public static CircleFontSize Auto() => new(Kind.Auto, 0);
    public static CircleFontSize Pixels(double px) => new(Kind.Pixels, px);
    public static CircleFontSize RadiusFraction(double fraction) => new(Kind.RadiusFraction, fraction);
}
