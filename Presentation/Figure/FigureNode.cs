using System.Windows.Controls;
using System.Windows.Shapes;
using TransparentHotkeyUtility.Models;

namespace TransparentHotkeyUtility.Presentation.Figure;

internal sealed class FigureNode
{
    public CircleConfig Config { get; }
    public Grid Visual { get; }
    public Line? Connector { get; }
    public FigureNode? Parent { get; }
    public List<FigureNode> ChildNodes { get; } = new();

    public double Cx { get; set; }
    public double Cy { get; set; }

    /// <summary>Текущее смещение магнита (визуальное, Cx/Cy — точка покоя).</summary>
    public double MagnetOx { get; set; }
    public double MagnetOy { get; set; }
    public double MagnetScale { get; set; } = 1.0;

    public double DisplayCx => Cx + MagnetOx;
    public double DisplayCy => Cy + MagnetOy;

    public bool IsExpanded => ChildNodes.Count > 0;

    public FigureNode(CircleConfig config, Grid visual, Line? connector, FigureNode? parent)
    {
        Config = config;
        Visual = visual;
        Connector = connector;
        Parent = parent;
    }

    /// <summary>
    /// Перемещает ТОЛЬКО этот узел (и его конец коннектора).
    /// Дочерние узлы остаются на своих абсолютных позициях на экране,
    /// для этого их OffsetX/OffsetY автоматически корректируются.
    /// </summary>
    public void MoveNodeOnly(double newCx, double newCy)
    {
        double dx = newCx - Cx;
        double dy = newCy - Cy;

        Cx = newCx;
        Cy = newCy;
        MagnetOx = 0;
        MagnetOy = 0;
        MagnetScale = 1;

        ApplyDisplayTransform();

        // Корректируем детей, чтобы они остались на месте
        foreach (var child in ChildNodes)
        {
            child.Config.OffsetX -= dx;
            child.Config.OffsetY -= dy;
        }
    }

    /// <summary>
    /// Применяет визуальную позицию с учётом магнита (Canvas + коннектор + scale).
    /// </summary>
    public void ApplyDisplayTransform()
    {
        // Снимаем HoldEnd от storyboard раскрытия группы — иначе Canvas.SetLeft игнорируется
        Visual.BeginAnimation(Canvas.LeftProperty, null);
        Visual.BeginAnimation(Canvas.TopProperty, null);

        Canvas.SetLeft(Visual, DisplayCx - Config.Radius);
        Canvas.SetTop(Visual, DisplayCy - Config.Radius);

        if (Connector is not null)
        {
            Connector.BeginAnimation(Line.X2Property, null);
            Connector.BeginAnimation(Line.Y2Property, null);
            Connector.X2 = DisplayCx;
            Connector.Y2 = DisplayCy;
        }

        CircleElementFactory.SetMagnetScale(Visual, MagnetScale);

        foreach (var child in ChildNodes)
        {
            if (child.Connector is not null)
            {
                child.Connector.BeginAnimation(Line.X1Property, null);
                child.Connector.BeginAnimation(Line.Y1Property, null);
                child.Connector.X1 = DisplayCx;
                child.Connector.Y1 = DisplayCy;
            }
        }
    }

    /// <summary>
    /// Мгновенно возвращает визуальные элементы в позицию покоя (Cx, Cy).
    /// </summary>
    public void SnapToRest()
    {
        MagnetOx = 0;
        MagnetOy = 0;
        MagnetScale = 1;
        ApplyDisplayTransform();
    }
}
