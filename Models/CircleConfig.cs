namespace TransparentHotkeyUtility.Models;

/// <summary>
/// Описание одного кружка в JSON-конфиге.
/// Все числа — в логических пикселях (WPF DIP).
/// </summary>
public sealed class CircleConfig
{
    /// <summary>Смещение центра кружка по X относительно точки появления фигуры.</summary>
    public double OffsetX { get; set; } = 0;

    /// <summary>Смещение центра кружка по Y относительно точки появления фигуры.</summary>
    public double OffsetY { get; set; } = -150;

    /// <summary>Радиус кружка в пикселях.</summary>
    public double Radius { get; set; } = 40;

    /// <summary>Цвет кружка в формате #RRGGBB или #AARRGGBB.</summary>
    public string Color { get; set; } = "#8C4BFF";

    /// <summary>Текст / иконка в центре кружка (один символ или короткое слово).</summary>
    public string Label { get; set; } = "★";
}
