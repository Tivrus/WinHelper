using System.Windows.Controls;
using System.Windows.Input;
using TransparentHotkeyUtility.Models;
using Cursors = System.Windows.Input.Cursors;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace TransparentHotkeyUtility.Presentation.Figure;

/// <summary>
/// Управляет перетаскиванием кружков в режиме настроек.
/// Поддерживает рекурсивное перемещение: при перемещении родителя все его дочерние узлы
/// (и их коннекторы) обновляются автоматически благодаря FigureNode.UpdatePositionsRecursively().
/// </summary>
internal sealed class SettingsDragController
{
    private readonly Canvas _canvas;
    private readonly Func<bool> _isActive;

    private FigureNode? _dragTarget;
    private double _dragOffsetX, _dragOffsetY;
    private double _dragStartX, _dragStartY;
    private bool _isDragging;

    public SettingsDragController(Canvas canvas, Func<bool> isActive)
    {
        _canvas = canvas;
        _isActive = isActive;
    }

    /// <summary>
    /// Рекурсивно подключает обработчики перетаскивания ко всем узлам в дереве.
    /// Вызывается после перестроения дерева в режиме настроек.
    /// </summary>
    public void EnableForTree(IEnumerable<FigureNode> nodes, Action<CircleConfig> onSelected)
    {
        foreach (var node in nodes)
        {
            AttachDragHandlers(node, onSelected);
            EnableForTree(node.ChildNodes, onSelected);
        }
    }

    /// <summary>Сбрасывает drag-состояние (вызывать при HidePopup).</summary>
    public void Reset()
    {
        if (_dragTarget is { } target)
        {
            target.Visual.ReleaseMouseCapture();
            _dragTarget = null;
        }
        _isDragging = false;
    }

    private void AttachDragHandlers(FigureNode node, Action<CircleConfig> onSelected)
    {
        var visual = node.Visual;
        var cfg = node.Config;

        visual.Cursor = Cursors.SizeAll;

        // Отписываемся от старых, чтобы не дублировать
        visual.PreviewMouseLeftButtonDown -= Visual_PreviewMouseLeftButtonDown;
        visual.PreviewMouseMove -= Visual_PreviewMouseMove;
        visual.PreviewMouseLeftButtonUp -= Visual_PreviewMouseLeftButtonUp;

        visual.PreviewMouseLeftButtonDown += Visual_PreviewMouseLeftButtonDown;
        visual.PreviewMouseMove += Visual_PreviewMouseMove;
        visual.PreviewMouseLeftButtonUp += Visual_PreviewMouseLeftButtonUp;

        void Visual_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isActive()) return;
            _dragTarget = node;
            var pos = e.GetPosition(_canvas);
            _dragStartX = pos.X;
            _dragStartY = pos.Y;
            _dragOffsetX = pos.X - Canvas.GetLeft(visual);
            _dragOffsetY = pos.Y - Canvas.GetTop(visual);
            _isDragging = false;
            visual.CaptureMouse();
            e.Handled = true; // Захватываем событие, чтобы родительские узлы (если есть) не срабатывали
        }

        void Visual_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragTarget != node || !visual.IsMouseCaptured) return;
            var pos = e.GetPosition(_canvas);

            const double threshold = 5;
            if (!_isDragging
                && Math.Abs(pos.X - _dragStartX) < threshold
                && Math.Abs(pos.Y - _dragStartY) < threshold)
                return;

            _isDragging = true;

            double newL = pos.X - _dragOffsetX;
            double newT = pos.Y - _dragOffsetY;
            
            // Центр кружка
            double newCx = newL + cfg.Radius;
            double newCy = newT + cfg.Radius;

            // Обновляем модель
            if (node.Parent is not null)
            {
                cfg.OffsetX = newCx - node.Parent.Cx;
                cfg.OffsetY = newCy - node.Parent.Cy;
            }
            else
            {
                // Для корня нужен anchor, но мы можем просто вычесть разницу из старого оффсета
                double dx = newCx - node.Cx;
                double dy = newCy - node.Cy;
                cfg.OffsetX += dx;
                cfg.OffsetY += dy;
            }

            node.MoveNodeOnly(newCx, newCy);

            e.Handled = true;
        }

        void Visual_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragTarget != node) return;
            visual.ReleaseMouseCapture();
            bool wasDragging = _isDragging;
            _isDragging = false;
            _dragTarget = null;

            if (wasDragging)
            {
                e.Handled = true;
            }
            else if (_isActive())
            {
                e.Handled = true;
                onSelected(cfg);
            }
        }
    }
}
