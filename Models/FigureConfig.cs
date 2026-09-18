namespace TransparentHotkeyUtility.Models;

/// <summary>Корневой объект figure.json.</summary>
public sealed class FigureConfig
{
    /// <summary>Страницы фигур (переключение колёсиком). Если пусто — используется <see cref="Circles"/>.</summary>
    public List<FigurePage> Pages { get; set; } = [];

    /// <summary>Кружки первой страницы (обратная совместимость со старым форматом).</summary>
    public List<CircleConfig> Circles { get; set; } = [];
}

/// <summary>Одна страница фигуры со своим набором кружков.</summary>
public sealed class FigurePage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Страница";
    public List<CircleConfig> Circles { get; set; } = [];
}
