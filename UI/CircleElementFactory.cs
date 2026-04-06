using System.Windows;
using System.Windows.Controls;
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

namespace TransparentHotkeyUtility.UI;

/// <summary>Creates and animates WPF visual elements for a single <see cref="CircleConfig"/>.</summary>
internal static class CircleElementFactory
{
    private static readonly Duration AnimDuration = new(TimeSpan.FromMilliseconds(160));
    private static readonly CubicEase AnimEase    = new() { EasingMode = EasingMode.EaseOut };

    // Resting stroke opacity, stored as a ratio so animation can target it
    private const double StrokeRestOpacity = 140.0 / 255.0;

    internal static Grid Create(CircleConfig cfg)
    {
        var body  = ColorUtils.Parse(cfg.Color);
        var dark  = ColorUtils.Darken(body, 0.35f);
        var light = ColorUtils.Lighten(body, 0.35f);

        // Glow colour: slightly shifted toward blue for the purple family
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

        var container = new Grid
        {
            Width                 = cfg.Radius * 2,
            Height                = cfg.Radius * 2,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform       = new ScaleTransform(1, 1),
            Tag                   = new CircleState(glowEffect, stroke),
        };
        container.Children.Add(ellipse);
        container.Children.Add(label);

        container.MouseEnter += (s, _) => Animate((Grid)s!, true);
        container.MouseLeave += (s, _) => Animate((Grid)s!, false);

        return container;
    }

    // ── Animation ────────────────────────────────────────────────────────────

    private static void Animate(Grid g, bool hovered)
    {
        if (g.Tag is not CircleState s) return;

        double scale = hovered ? 1.13 : 1.0;
        if (g.RenderTransform is ScaleTransform st)
        {
            st.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(scale, AnimDuration) { EasingFunction = AnimEase });
            st.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(scale, AnimDuration) { EasingFunction = AnimEase });
        }

        s.Glow.BeginAnimation(DropShadowEffect.OpacityProperty,
            new DoubleAnimation(hovered ? 0.9 : 0.0, AnimDuration));

        s.Stroke.BeginAnimation(Brush.OpacityProperty,
            new DoubleAnimation(hovered ? 1.0 : StrokeRestOpacity, AnimDuration));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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
}
