using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using TransparentHotkeyUtility.Models;

// Disambiguate types that clash between WPF and WinForms implicit usings
using Color               = System.Windows.Media.Color;
using Brush               = System.Windows.Media.Brush;
using FontFamily          = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment   = System.Windows.VerticalAlignment;
using Point               = System.Windows.Point;

namespace TransparentHotkeyUtility.Presentation.Figure;

/// <summary>Creates and animates WPF visual elements for a single <see cref="CircleConfig"/>.</summary>
internal static class CircleElementFactory
{
    private static readonly Duration AnimDuration = new(TimeSpan.FromMilliseconds(160));
    private static readonly CubicEase AnimEase    = new() { EasingMode = EasingMode.EaseOut };

    private const double StrokeRestOpacity = 140.0 / 255.0;
    private const double HoverScale        = 1.13;

    internal sealed class PinState
    {
        public bool IsPinned { get; set; }
        public bool ScaleWhenPinned { get; set; } = true;
    }

    internal static Grid Create(CircleConfig cfg, Action<CircleConfig>? onActivated = null)
    {
        var body  = ColorUtils.Parse(cfg.Color);
        var dark  = ColorUtils.Darken(body, 0.35f);
        var light = ColorUtils.Lighten(body, 0.35f);

        var glowColor = Color.FromRgb(
            (byte)Math.Max(0,   body.R - 30),
            (byte)Math.Max(0,   body.G - 20),
            (byte)Math.Min(255, body.B + 10));

        var glowEffect = new DropShadowEffect
        {
            Color       = glowColor,
            BlurRadius  = cfg.Radius * 0.8,
            ShadowDepth = 0,
            Opacity     = 0,
        };

        var stroke = new SolidColorBrush(Color.FromArgb(
            140,
            (byte)Math.Min(255, body.R + 60),
            (byte)Math.Min(255, body.G + 40),
            255))
        {
            Opacity = StrokeRestOpacity,
        };

        var ellipse = new Ellipse
        {
            Fill = new RadialGradientBrush(new GradientStopCollection
            {
                new(light, 0.00),
                new(body,  0.55),
                new(dark,  1.00),
            }),
            Stroke          = stroke,
            StrokeThickness = 1.5,
            Effect          = glowEffect,
        };

        var label = new TextBlock
        {
            Text                = cfg.Label,
            FontSize            = LabelFontSize(cfg),
            FontFamily          = new FontFamily("Segoe UI"),
            Foreground          = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            TextAlignment       = TextAlignment.Center,
            IsHitTestVisible    = false,
        };

        var state = new CircleState(glowEffect, stroke);
        var pin   = new PinState();
        var container = new Grid
        {
            Width                 = cfg.Radius * 2,
            Height                = cfg.Radius * 2,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform       = new ScaleTransform(1, 1),
            Tag                   = new CircleVisualTag(state, cfg, onActivated, pin),
        };
        bool isClickable = !string.IsNullOrWhiteSpace(cfg.Action)
                        || !string.IsNullOrWhiteSpace(cfg.ExecutablePath)
                        || cfg.Type == CircleType.Group
                        || (cfg.Type == CircleType.Modal && cfg.FormFields.Count > 0);
        if (isClickable)
            container.Cursor = System.Windows.Input.Cursors.Hand;

        container.Children.Add(ellipse);
        container.Children.Add(label);

        // Для группы — иконка папки в правом нижнем углу
        if (cfg.Type == CircleType.Group)
        {
            var folderMark = new TextBlock
            {
                Text                = "📂",
                FontSize            = Math.Max(7, cfg.Radius * 0.22),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment   = VerticalAlignment.Bottom,
                Margin              = new Thickness(0, 0, 2, 1),
                IsHitTestVisible    = false,
                Opacity             = 0.80,
            };
            container.Children.Add(folderMark);
        }

        container.MouseEnter += (s, _) => ApplyVisualState((Grid)s!);
        container.MouseLeave += (s, _) => ApplyVisualState((Grid)s!);

        container.MouseLeftButtonUp += (_, e) =>
        {
            if (!isClickable || onActivated is null) return;
            e.Handled = true;
            onActivated(cfg);
        };

        return container;
    }

    /// <summary>Закрепляет визуальное состояние; можно отключить scale-анимацию для pinned.</summary>
    internal static void SetPinned(Grid? g, bool pinned, bool scaleWhenPinned = true)
    {
        if (g?.Tag is not CircleVisualTag cvt) return;
        cvt.Pin.IsPinned = pinned;
        cvt.Pin.ScaleWhenPinned = scaleWhenPinned;
        ApplyVisualState(g);
    }

    private static void ApplyVisualState(Grid g)
    {
        if (g.Tag is not CircleVisualTag { State: var s, Pin: var pin }) return;

        bool lookHovered = pin.IsPinned || g.IsMouseOver;
        bool scaleUp     = g.IsMouseOver || (pin.IsPinned && pin.ScaleWhenPinned);
        double scale     = scaleUp ? HoverScale : 1.0;

        if (g.RenderTransform is ScaleTransform st)
        {
            st.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(scale, AnimDuration) { EasingFunction = AnimEase });
            st.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(scale, AnimDuration) { EasingFunction = AnimEase });
        }

        s.Glow.BeginAnimation(DropShadowEffect.OpacityProperty,
            new DoubleAnimation(lookHovered ? 0.9 : 0.0, AnimDuration));

        s.Stroke.BeginAnimation(Brush.OpacityProperty,
            new DoubleAnimation(lookHovered ? 1.0 : StrokeRestOpacity, AnimDuration));
    }

    private static double LabelFontSize(CircleConfig cfg)
    {
        double raw = cfg.Label.Length switch
        {
            <= 1 => cfg.Radius * 0.45,
            <= 2 => cfg.Radius * 0.40,
            <= 4 => cfg.Radius * 0.32,
            _    => cfg.Radius * 0.24,
        };
        return Math.Max(8, raw);
    }

    private record CircleState(DropShadowEffect Glow, SolidColorBrush Stroke);

    private sealed record CircleVisualTag(
        CircleState   State,
        CircleConfig  Config,
        Action<CircleConfig>? OnActivated,
        PinState      Pin);
}
