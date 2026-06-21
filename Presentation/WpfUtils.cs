using System.Windows;
using System.Windows.Media;

namespace TransparentHotkeyUtility.Presentation;

/// <summary>Вспомогательные утилиты для работы с WPF, не привязанные к конкретному окну.</summary>
internal static class WpfUtils
{
    /// <summary>
    /// Возвращает коэффициент масштабирования DPI текущего дисплея.
    /// На 96 DPI возвращает (1.0, 1.0).
    /// </summary>
    public static (double X, double Y) GetDpiScale()
    {
        using var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
        return (g.DpiX / 96.0, g.DpiY / 96.0);
    }

    /// <summary>
    /// Проверяет, является ли <paramref name="child"/> потомком <paramref name="root"/>
    /// в визуальном дереве WPF.
    /// </summary>
    public static bool IsDescendantOf(DependencyObject? child, DependencyObject root)
    {
        while (child is not null)
        {
            if (ReferenceEquals(child, root)) return true;
            child = VisualTreeHelper.GetParent(child);
        }
        return false;
    }
}
