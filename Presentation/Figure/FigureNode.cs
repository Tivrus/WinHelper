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

        // 1. Обновляем визуальное положение самого узла
        Canvas.SetLeft(Visual, Cx - Config.Radius);
        Canvas.SetTop(Visual, Cy - Config.Radius);

        // 2. Обновляем конец коннектора к родителю
        if (Connector is not null)
        {
            Connector.X2 = Cx;
            Connector.Y2 = Cy;
        }

        // 3. Корректируем детей, чтобы они остались на месте
        foreach (var child in ChildNodes)
        {
            child.Config.OffsetX -= dx;
            child.Config.OffsetY -= dy;

            // Начало коннектора ребёнка - это новый центр текущего узла
            if (child.Connector is not null)
            {
                child.Connector.X1 = Cx;
                child.Connector.Y1 = Cy;
            }
        }
    }

    /// <summary>
    /// Мгновенно возвращает визуальные элементы в позицию покоя (Cx, Cy).
    /// </summary>
    public void SnapToRest()
    {
        Canvas.SetLeft(Visual, Cx - Config.Radius);
        Canvas.SetTop(Visual, Cy - Config.Radius);
        if (Connector is not null)
        {
            Connector.X2 = Cx;
            Connector.Y2 = Cy;
        }
    }
}
