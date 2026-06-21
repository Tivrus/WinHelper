using System.Text.Json.Serialization;

namespace TransparentHotkeyUtility.Models;

/// <summary>
/// Описание одного кружка в JSON-конфиге.
/// Все числа — в логических пикселях (WPF DIP).
/// </summary>
public sealed class CircleConfig
{
    /// <summary>Стабильный идентификатор кружка (используется для восстановления выбора в дереве).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

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

    /// <summary>
    /// Устаревшее поле (раньше — встроенные действия). Сейчас клик обрабатывается через
    /// <see cref="ExecutablePath"/>, <see cref="Type"/> и <see cref="FormFields"/>.
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Путь к исполняемому файлу (.exe), запускаемому при клике.
    /// Актуально только при <see cref="Type"/> == <see cref="CircleType.FireAndForget"/>.
    /// </summary>
    public string? ExecutablePath { get; set; }

    /// <summary>
    /// Режим поведения кружка: прямая команда, форма с полями ввода или группа.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CircleType Type { get; set; } = CircleType.FireAndForget;

    /// <summary>
    /// Дочерние кружки (актуально только при <see cref="Type"/> == <see cref="CircleType.Group"/>).
    /// </summary>
    public List<CircleConfig> Children { get; set; } = new();

    /// <summary>
    /// Поля динамической формы.
    /// Для <see cref="CircleType.Modal"/> — аргументы к .exe этого кружка;
    /// для <see cref="CircleType.Group"/> — общий префикс аргументов для любого дочернего кружка с .exe (перед аргументами ребёнка).
    /// Значения (кроме <see cref="FormFieldType.Label"/>) передаются в том же порядке, в котором перечислены поля.
    /// </summary>
    public List<FormFieldConfig> FormFields { get; set; } = new();
}
