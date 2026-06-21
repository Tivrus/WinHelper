using System.Windows.Controls;
using System.Windows.Shapes;
using TransparentHotkeyUtility.Models;

namespace TransparentHotkeyUtility.Presentation.Figure;

/// <summary>
/// Привязка визуального представления кружка к его данным.
/// Заменяет анонимные кортежи (Grid, Line?, CircleConfig) по всему коду.
/// </summary>
internal sealed record CircleElement(
    Grid         Visual,
    Line?        Connector,
    CircleConfig Config);
