using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using TransparentHotkeyUtility.Models;

using Color               = System.Windows.Media.Color;
using Brush               = System.Windows.Media.Brush;
using FontFamily          = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment   = System.Windows.VerticalAlignment;
using Point               = System.Windows.Point;
using Brushes             = System.Windows.Media.Brushes;

namespace TransparentHotkeyUtility.Presentation.Figure;

/// <summary>Creates and animates WPF visual elements for a single <see cref="CircleConfig"/>.</summary>
internal static class CircleElementFactory
{
    private static readonly Duration AnimDuration = new(TimeSpan.FromMilliseconds(160));
    private static readonly CubicEase AnimEase    = new() { EasingMode = EasingMode.EaseOut };

    private const double HoverScale = 1.13;

    internal sealed class PinState
    {
        public bool IsPinned { get; set; }
        public bool ScaleWhenPinned { get; set; } = true;
        /// <summary>Режим настроек: без hover-scale, выделение — кольцом.</summary>
        public bool SettingsMode { get; set; }
        public bool IsSelected { get; set; }
        /// <summary>Масштаб кружка ведёт магнит — обычный hover-scale отключён.</summary>
        public bool MagnetHandlesScale { get; set; }
    }

    internal static Grid Create(
        CircleConfig cfg,
        Action<CircleConfig>? onActivated = null,
        CircleStyle? style = null)
    {
        style ??= CircleStyle.Default;
        var body = ColorUtils.Parse(cfg.Color);

        DropShadowEffect? glowEffect = null;
        if (style.Glow)
        {
            var glowColor = Color.FromRgb(
                (byte)Math.Max(0,   body.R - 30),
                (byte)Math.Max(0,   body.G - 20),
                (byte)Math.Min(255, body.B + 10));

            glowEffect = new DropShadowEffect
            {
                Color       = glowColor,
                BlurRadius  = cfg.Radius * 0.8,
                ShadowDepth = 0,
                Opacity     = 0,
            };
        }

        SolidColorBrush? stroke = null;
        if (!style.BorderNone && style.BorderWidth > 0)
        {
            Color strokeColor;
            if (style.BorderColor is { } fixedBorder)
            {
                strokeColor = fixedBorder;
            }
            else
            {
                strokeColor = Color.FromArgb(
                    255,
                    (byte)Math.Min(255, body.R + 60),
                    (byte)Math.Min(255, body.G + 40),
                    255);
            }

            stroke = new SolidColorBrush(strokeColor)
            {
                Opacity = style.BorderOpacity,
            };
        }

        var ellipse = new Ellipse
        {
            Fill            = BuildFill(body, style),
            Stroke          = stroke,
            StrokeThickness = stroke is null ? 0 : style.BorderWidth,
            Effect          = glowEffect,
        };

        // Кольцо выделения для режима настроек (не меняет размер кружка)
        var selectionRing = new Ellipse
        {
            Fill             = Brushes.Transparent,
            Stroke           = new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0x5A)),
            StrokeThickness  = 2.5,
            StrokeDashArray  = new DoubleCollection { 4, 2.5 },
            Margin           = new Thickness(-5),
            IsHitTestVisible = false,
            Visibility       = Visibility.Collapsed,
        };

        var label = new TextBlock
        {
            Text                = cfg.Label,
            FontSize            = ResolveFontSize(cfg, style),
            FontFamily          = new FontFamily(style.FontFamily),
            FontWeight          = style.FontWeight,
            Foreground          = new SolidColorBrush(style.TextColor),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            TextAlignment       = TextAlignment.Center,
            IsHitTestVisible    = false,
        };

        var state = new CircleState(glowEffect, stroke, style.BorderOpacity, selectionRing);
        var pin   = new PinState { MagnetHandlesScale = style.MagnetEnabled };
        var container = new Grid
        {
            Width                 = cfg.Radius * 2,
            Height                = cfg.Radius * 2,
            ClipToBounds          = false,
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
        container.Children.Add(selectionRing);
        container.Children.Add(label);

        if (cfg.Type == CircleType.Group)
        {
            var folderMark = new TextBlock
            {
                Text                = "📂",
                FontSize            = Math.Max(7, cfg.Radius * 0.22),
                Foreground          = new SolidColorBrush(style.TextColor),
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

    private static Brush BuildFill(Color body, CircleStyle style)
    {
        if (style.FillMode == CircleFillMode.Solid)
            return new SolidColorBrush(body);

        var dark  = ColorUtils.Darken(body, style.GradientDarken);
        var light = ColorUtils.Lighten(body, style.GradientLighten);
        return new RadialGradientBrush(new GradientStopCollection
        {
            new(light, 0.00),
            new(body,  0.55),
            new(dark,  1.00),
        });
    }

    /// <summary>Закрепляет визуальное состояние; можно отключить scale-анимацию для pinned.</summary>
    internal static void SetPinned(Grid? g, bool pinned, bool scaleWhenPinned = true)
    {
        if (g?.Tag is not CircleVisualTag cvt) return;
        cvt.Pin.IsPinned = pinned;
        cvt.Pin.ScaleWhenPinned = scaleWhenPinned;
        ApplyVisualState(g);
    }

    /// <summary>
    /// Режим редактирования: без увеличения при hover/выборе.
    /// Выделение показывается пунктирным кольцом.
    /// </summary>
    internal static void SetSettingsMode(Grid? g, bool enabled)
    {
        if (g?.Tag is not CircleVisualTag cvt) return;
        cvt.Pin.SettingsMode = enabled;
        if (enabled)
        {
            cvt.Pin.IsPinned = false;
            cvt.Pin.ScaleWhenPinned = false;
            // Сбрасываем scale мгновенно
            if (g.RenderTransform is ScaleTransform st)
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                st.ScaleX = 1;
                st.ScaleY = 1;
            }
        }
        ApplyVisualState(g);
    }

    internal static void SetSelected(Grid? g, bool selected)
    {
        if (g?.Tag is not CircleVisualTag cvt) return;
        cvt.Pin.IsSelected = selected;
        cvt.State.SelectionRing.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        ApplyVisualState(g);
    }

    /// <summary>Масштаб от магнита (без анимации storyboard — сглаживание снаружи).</summary>
    internal static void SetMagnetScale(Grid? g, double scale)
    {
        if (g?.RenderTransform is not ScaleTransform st) return;
        st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        st.ScaleX = scale;
        st.ScaleY = scale;
    }

    private static void ApplyVisualState(Grid g)
    {
        if (g.Tag is not CircleVisualTag { State: var s, Pin: var pin }) return;

        // В настройках размер кружка не трогаем — только кольцо выделения
        if (pin.SettingsMode)
        {
            if (g.RenderTransform is ScaleTransform st)
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                st.ScaleX = 1;
                st.ScaleY = 1;
            }

            if (s.Glow is not null)
            {
                s.Glow.BeginAnimation(DropShadowEffect.OpacityProperty, null);
                s.Glow.Opacity = 0;
            }

            if (s.Stroke is not null)
            {
                s.Stroke.BeginAnimation(Brush.OpacityProperty, null);
                s.Stroke.Opacity = s.StrokeRestOpacity;
            }

            s.SelectionRing.Visibility = pin.IsSelected ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        bool lookHovered = pin.IsPinned || g.IsMouseOver;

        // Масштаб: либо магнит, либо классический hover/pin
        if (!pin.MagnetHandlesScale)
        {
            bool scaleUp = g.IsMouseOver || (pin.IsPinned && pin.ScaleWhenPinned);
            double scale = scaleUp ? HoverScale : 1.0;

            if (g.RenderTransform is ScaleTransform st2)
            {
                st2.BeginAnimation(ScaleTransform.ScaleXProperty,
                    new DoubleAnimation(scale, AnimDuration) { EasingFunction = AnimEase });
                st2.BeginAnimation(ScaleTransform.ScaleYProperty,
                    new DoubleAnimation(scale, AnimDuration) { EasingFunction = AnimEase });
            }
        }

        if (s.Glow is not null)
        {
            s.Glow.BeginAnimation(DropShadowEffect.OpacityProperty,
                new DoubleAnimation(lookHovered ? 0.9 : 0.0, AnimDuration));
        }

        if (s.Stroke is not null)
        {
            s.Stroke.BeginAnimation(Brush.OpacityProperty,
                new DoubleAnimation(lookHovered ? 1.0 : s.StrokeRestOpacity, AnimDuration));
        }
    }

    private static double ResolveFontSize(CircleConfig cfg, CircleStyle style) =>
        style.FontSize.Mode switch
        {
            // Фиксированный размер — без подгонки под длину
            CircleFontSize.Kind.Pixels => Math.Max(1, style.FontSize.Value),
            // 0.4r — верхний предел (доля радиуса); итоговый размер подгоняется под длину текста
            CircleFontSize.Kind.RadiusFraction => FitFontSizeToLabel(cfg, style, style.FontSize.Value),
            // auto — то же: влезает в кружок исходя из длины подписи
            _ => FitFontSizeToLabel(cfg, style, maxRadiusFraction: 0.55),
        };

    /// <summary>
    /// Подбирает размер шрифта по длине текста, чтобы подпись влезла в кружок.
    /// <paramref name="maxRadiusFraction"/> — потолок относительно радиуса (короткий текст).
    /// </summary>
    private static double FitFontSizeToLabel(CircleConfig cfg, CircleStyle style, double maxRadiusFraction)
    {
        string text = cfg.Label ?? string.Empty;
        double maxSize = Math.Max(8, cfg.Radius * Math.Max(0.05, maxRadiusFraction));
        if (text.Length == 0)
            return maxSize;

        // Доступная «хорда» внутри кружка с небольшим запасом по краям
        double avail = cfg.Radius * 2.0 * 0.78;

        var typeface = new Typeface(
            new FontFamily(style.FontFamily),
            FontStyles.Normal,
            style.FontWeight,
            FontStretches.Normal);

        double lo = 6;
        double hi = maxSize;
        for (int i = 0; i < 14; i++)
        {
            double mid = (lo + hi) * 0.5;
            var ft = new FormattedText(
                text,
                CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight,
                typeface,
                mid,
                Brushes.Black,
                pixelsPerDip: 1.0);

            if (ft.Width <= avail && ft.Height <= avail)
                lo = mid;
            else
                hi = mid;
        }

        return Math.Max(6, lo);
    }

    private record CircleState(
        DropShadowEffect? Glow,
        SolidColorBrush?  Stroke,
        double            StrokeRestOpacity,
        Ellipse           SelectionRing);

    private sealed record CircleVisualTag(
        CircleState   State,
        CircleConfig  Config,
        Action<CircleConfig>? OnActivated,
        PinState      Pin);
}
