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
        try
        {
            if (File.Exists(FilePath))
            {
                var config = JsonSerializer.Deserialize<FigureConfig>(
                    File.ReadAllText(FilePath), JsonOpts);
                return config ?? new FigureConfig();
            }
        }
        catch { }

        return new FigureConfig(); // файл отсутствует или повреждён → нет кружков
    }
}
