using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using TransparentHotkeyUtility.Models;
using Color = System.Windows.Media.Color;

namespace TransparentHotkeyUtility.Presentation.Figure;

internal sealed class FigureManager
{
    private static readonly Duration AnimDuration = new(TimeSpan.FromMilliseconds(380));
    private static readonly CubicEase AnimEase    = new() { EasingMode = EasingMode.EaseInOut };

    private readonly Canvas _lineLayer;
    private readonly Canvas _circleLayer;

    private readonly List<FigureNode> _rootNodes = new();
    private Action<CircleConfig>? _onActivated;
    private FigureConfig? _config;

    public IReadOnlyList<FigureNode> RootNodes => _rootNodes;

    public FigureManager(Canvas lineLayer, Canvas circleLayer)
    {
        _lineLayer = lineLayer;
        _circleLayer = circleLayer;
    }

    public void Build(double anchorCx, double anchorCy, FigureConfig config, Action<CircleConfig>? onActivated)
    {
        _config = config;
        _onActivated = onActivated;

        _lineLayer.Children.Clear();
        _circleLayer.Children.Clear();
        _rootNodes.Clear();

        if (config.Circles.Count == 0) return;

        foreach (var cfg in config.Circles)
        {
            double cx = anchorCx + cfg.OffsetX;
            double cy = anchorCy + cfg.OffsetY;

            var line = new Line
            {
                X1 = anchorCx, Y1 = anchorCy,
                X2 = cx, Y2 = cy,
                Stroke = ConnectorBrush(cfg.Color),
                StrokeThickness = 1.0,
            };
            _lineLayer.Children.Add(line);

            var visual = CircleElementFactory.Create(cfg, _onActivated);
            _circleLayer.Children.Add(visual);

            var node = new FigureNode(cfg, visual, line, null)
            {
                Cx = cx,
                Cy = cy
            };
            node.SnapToRest();
            _rootNodes.Add(node);
        }
    }

    public FigureNode? FindNode(CircleConfig config)
    {
        foreach (var root in _rootNodes)
        {
            if (FindNodeRecursive(root, config) is { } found)
                return found;
        }
        return null;
    }

    private static FigureNode? FindNodeRecursive(FigureNode node, CircleConfig config)
    {
        if (ReferenceEquals(node.Config, config)) return node;
        foreach (var child in node.ChildNodes)
        {
            if (FindNodeRecursive(child, config) is { } found)
                return found;
        }
        return null;
    }

    public IEnumerable<FigureNode> GetAllNodes()
    {
        var stack = new Stack<FigureNode>(_rootNodes);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            foreach (var child in node.ChildNodes)
                stack.Push(child);
        }
    }

    public void ToggleGroup(FigureNode groupNode)
    {
        if (groupNode.IsExpanded)
            CollapseGroup(groupNode);
        else
            ExpandGroup(groupNode);
    }

    private void CollapseSiblings(FigureNode node, bool immediate)
    {
        var siblings = node.Parent?.ChildNodes ?? (IReadOnlyList<FigureNode>)_rootNodes;
        foreach (var sibling in siblings)
        {
            if (sibling != node && sibling.IsExpanded)
            {
                if (immediate)
                    CollapseGroupImmediate(sibling);
                else
                    CollapseGroup(sibling);
            }
        }
    }

    public void ExpandGroup(FigureNode groupNode)
    {
        if (groupNode.IsExpanded || groupNode.Config.Children.Count == 0) return;

        CollapseSiblings(groupNode, immediate: false);

        CircleElementFactory.SetPinned(groupNode.Visual, true);

        foreach (var childCfg in groupNode.Config.Children)
        {
            double restX = groupNode.Cx + childCfg.OffsetX;
            double restY = groupNode.Cy + childCfg.OffsetY;

            var line = new Line
            {
                X1 = groupNode.Cx, Y1 = groupNode.Cy,
                X2 = groupNode.Cx, Y2 = groupNode.Cy,
                Stroke = ConnectorBrush(childCfg.Color),
                StrokeThickness = 1.0,
            };
            // Вставляем линию под кружки
            _lineLayer.Children.Add(line);

            var visual = CircleElementFactory.Create(childCfg, _onActivated);
            visual.Opacity = 0;
            Canvas.SetLeft(visual, groupNode.Cx - childCfg.Radius);
            Canvas.SetTop(visual, groupNode.Cy - childCfg.Radius);
            _circleLayer.Children.Add(visual);

            var childNode = new FigureNode(childCfg, visual, line, groupNode)
            {
                Cx = restX,
                Cy = restY
            };
            groupNode.ChildNodes.Add(childNode);

            var sb = BuildStoryboard(
                visual, line,
                groupNode.Cx - childCfg.Radius, groupNode.Cy - childCfg.Radius,
                restX - childCfg.Radius, restY - childCfg.Radius,
                groupNode.Cx, groupNode.Cy,
                restX, restY,
                () => { });

            AddOpacityAnim(sb, visual, 0, 1);
            sb.Begin();
        }
    }

    public void CollapseGroup(FigureNode groupNode, Action? onComplete = null)
    {
        if (!groupNode.IsExpanded)
        {
            onComplete?.Invoke();
            return;
        }

        CircleElementFactory.SetPinned(groupNode.Visual, false);

        var toAnimate = groupNode.ChildNodes.ToList();
        groupNode.ChildNodes.Clear();

        int remaining = toAnimate.Count;
        foreach (var childNode in toAnimate)
        {
            // Если дочерний узел сам был раскрыт, схлопываем его мгновенно
            CollapseGroupImmediate(childNode);

            var sb = BuildStoryboard(
                childNode.Visual, childNode.Connector,
                Canvas.GetLeft(childNode.Visual), Canvas.GetTop(childNode.Visual),
                groupNode.Cx - childNode.Config.Radius, groupNode.Cy - childNode.Config.Radius,
                childNode.Connector?.X2 ?? groupNode.Cx, childNode.Connector?.Y2 ?? groupNode.Cy,
                groupNode.Cx, groupNode.Cy,
                () =>
                {
                    _circleLayer.Children.Remove(childNode.Visual);
                    if (childNode.Connector is not null)
                        _lineLayer.Children.Remove(childNode.Connector);
                    if (--remaining == 0)
                        onComplete?.Invoke();
                });

            AddOpacityAnim(sb, childNode.Visual, 1, 0);
            sb.Begin();
        }
    }

    public void CollapseGroupImmediate(FigureNode groupNode)
    {
        if (!groupNode.IsExpanded) return;

        CircleElementFactory.SetPinned(groupNode.Visual, false);

        foreach (var childNode in groupNode.ChildNodes)
        {
            CollapseGroupImmediate(childNode); // Рекурсивно убираем всех детей
            _circleLayer.Children.Remove(childNode.Visual);
            if (childNode.Connector is not null)
                _lineLayer.Children.Remove(childNode.Connector);
        }
        groupNode.ChildNodes.Clear();
    }

    public void CollapseAllGroupsImmediate()
    {
        foreach (var root in _rootNodes)
            CollapseGroupImmediate(root);
    }

    // ── Модальные анимации (в центр) ──────────────────────────────────────────

    public void AnimateToCenter(FigureNode node, double anchorCx, double anchorCy, Action onComplete)
    {
        BuildStoryboard(
            node.Visual, node.Connector,
            Canvas.GetLeft(node.Visual), Canvas.GetTop(node.Visual),
            anchorCx - node.Config.Radius, anchorCy - node.Config.Radius,
            node.Connector?.X2 ?? anchorCx, node.Connector?.Y2 ?? anchorCy,
            anchorCx, anchorCy,
            onComplete).Begin();
    }

    public void AnimateToRest(FigureNode node, Action onComplete)
    {
        BuildStoryboard(
            node.Visual, node.Connector,
            Canvas.GetLeft(node.Visual), Canvas.GetTop(node.Visual),
            node.Cx - node.Config.Radius, node.Cy - node.Config.Radius,
            node.Connector?.X2 ?? node.Cx, node.Connector?.Y2 ?? node.Cy,
            node.Cx, node.Cy,
            onComplete).Begin();
    }

    public void SnapToRest(FigureNode node)
    {
        node.SnapToRest();
    }

    // ── Настройки (без анимации) ──────────────────────────────────────────────

    public void ExpandGroupImmediate(FigureNode groupNode)
    {
        if (groupNode.IsExpanded || groupNode.Config.Children.Count == 0) return;

        CollapseSiblings(groupNode, immediate: true);

        CircleElementFactory.SetPinned(groupNode.Visual, true, scaleWhenPinned: false);

        foreach (var childCfg in groupNode.Config.Children)
        {
            double restX = groupNode.Cx + childCfg.OffsetX;
            double restY = groupNode.Cy + childCfg.OffsetY;

            var line = new Line
            {
                X1 = groupNode.Cx, Y1 = groupNode.Cy,
                X2 = restX, Y2 = restY,
                Stroke = ConnectorBrush(childCfg.Color),
                StrokeThickness = 1.0,
            };
            _lineLayer.Children.Add(line);

            // Без onActivated для режима настроек — клики обрабатывает контроллер
            var visual = CircleElementFactory.Create(childCfg, null);
            Canvas.SetLeft(visual, restX - childCfg.Radius);
            Canvas.SetTop(visual, restY - childCfg.Radius);
            _circleLayer.Children.Add(visual);

            var childNode = new FigureNode(childCfg, visual, line, groupNode)
            {
                Cx = restX,
                Cy = restY
            };
            groupNode.ChildNodes.Add(childNode);
        }
    }

    // ── Утилиты ───────────────────────────────────────────────────────────────

    private static Storyboard BuildStoryboard(
        Grid visual, Line? connector,
        double fromL, double fromT, double toL, double toT,
        double lineFromX, double lineFromY, double lineToX, double lineToY,
        Action onComplete)
    {
        var sb = new Storyboard();

        AddCanvasAnim(sb, visual, "(Canvas.Left)", fromL, toL);
        AddCanvasAnim(sb, visual, "(Canvas.Top)", fromT, toT);

        if (connector is not null)
        {
            AddPropAnim(sb, connector, Line.X2Property, lineFromX, lineToX);
            AddPropAnim(sb, connector, Line.Y2Property, lineFromY, lineToY);
        }

        sb.Completed += (_, _) => onComplete();
        return sb;
    }

    private static void AddCanvasAnim(Storyboard sb, Grid target, string path, double from, double to)
    {
        var a = new DoubleAnimation(from, to, AnimDuration) { EasingFunction = AnimEase };
        Storyboard.SetTarget(a, target);
        Storyboard.SetTargetProperty(a, new PropertyPath(path));
        sb.Children.Add(a);
    }

    private static void AddPropAnim(Storyboard sb, DependencyObject target, DependencyProperty prop, double from, double to)
    {
        var a = new DoubleAnimation(from, to, AnimDuration) { EasingFunction = AnimEase };
        Storyboard.SetTarget(a, target);
        Storyboard.SetTargetProperty(a, new PropertyPath(prop));
        sb.Children.Add(a);
    }

    private static void AddOpacityAnim(Storyboard sb, UIElement target, double from, double to)
    {
        var a = new DoubleAnimation(from, to, AnimDuration) { EasingFunction = AnimEase };
        Storyboard.SetTarget(a, target);
        Storyboard.SetTargetProperty(a, new PropertyPath(UIElement.OpacityProperty));
        sb.Children.Add(a);
    }

    private static SolidColorBrush ConnectorBrush(string? colorHex)
    {
        var c = ColorUtils.Parse(colorHex ?? "#8C4BFF");
        return new SolidColorBrush(Color.FromArgb(50, c.R, c.G, c.B));
    }
}