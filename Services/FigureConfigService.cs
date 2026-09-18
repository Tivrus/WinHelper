using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TransparentHotkeyUtility.Models;

namespace TransparentHotkeyUtility.Services;

internal static class FigureConfigService
{
    // figure.json лежит рядом с exe-файлом (в корне проекта в режиме разработки)
    private static readonly string FilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "figure.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented          = true,
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling    = JsonCommentHandling.Skip,   // разрешаем // комментарии
        AllowTrailingCommas    = true,
    };

    /// <summary>
    /// Загружает figure.json из директории exe.
    /// Если файл не найден, пуст или содержит пустой массив circles — возвращает
    /// конфиг с нулём кружков (фигура не отображается).
    /// </summary>
    public static FigureConfig Load()
    {
        EnsureConfigFileExists();

        try
        {
            if (File.Exists(FilePath))
            {
                var config = JsonSerializer.Deserialize<FigureConfig>(
                    File.ReadAllText(FilePath), JsonOpts);
                config ??= new FigureConfig();
                Normalize(config);
                return config;
            }
        }
        catch { }

        return new FigureConfig(); // файл отсутствует или повреждён → нет кружков
    }

    public static void EnsureConfigFileExists()
    {
        try
        {
            if (File.Exists(FilePath))
                return;

            var emptyConfig = new FigureConfig();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(emptyConfig, JsonOpts));
        }
        catch { }
    }

    public static void Save(FigureConfig config)
    {
        try
        {
            Normalize(config);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(config, JsonOpts));
        }
        catch { }
    }

    /// <summary>Миграция: если страниц нет, но есть circles — создаём первую страницу.</summary>
    private static void Normalize(FigureConfig config)
    {
        if (config.Pages.Count == 0)
        {
            config.Pages.Add(new FigurePage
            {
                Name    = "Страница 1",
                Circles = config.Circles,
            });
        }

        // Держим Circles синхронизированным с первой страницей (обратная совместимость)
        config.Circles = config.Pages[0].Circles;

        foreach (var page in config.Pages)
        {
            if (string.IsNullOrWhiteSpace(page.Id))
                page.Id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(page.Name))
                page.Name = "Страница";
            EnsureCircleIds(page.Circles);
        }
    }

    private static void EnsureCircleIds(IEnumerable<CircleConfig> circles)
    {
        foreach (var c in circles)
        {
            if (string.IsNullOrWhiteSpace(c.Id))
                c.Id = Guid.NewGuid().ToString("N");
            if (c.Children.Count > 0)
                EnsureCircleIds(c.Children);
        }
    }
}
