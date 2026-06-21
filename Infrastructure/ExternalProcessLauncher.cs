using System.Diagnostics;

namespace TransparentHotkeyUtility.Infrastructure;

/// <summary>
/// Запуск внешнего .exe через <see cref="ProcessStartInfo.ArgumentList"/> (без <c>cmd.exe</c>).
/// Так аргументы не «ломаются» правилами cmd и путь к exe не превращается в «не команду».
/// </summary>
internal static class ExternalProcessLauncher
{
    public static void Start(string fileName, IReadOnlyList<string>? argumentParts = null)
    {
        fileName = NormalizeToken(fileName);

        var psi = new ProcessStartInfo
        {
            FileName        = fileName,
            UseShellExecute = false,
        };

        if (argumentParts is not null)
        {
            foreach (var part in argumentParts)
                psi.ArgumentList.Add(NormalizeToken(part));
        }

        Process.Start(psi);
    }

    /// <summary>Снимает пробельные кавычки по краям (если путь/аргумент случайно сохранили с кавычками в JSON).</summary>
    private static string NormalizeToken(string value)
    {
        var t = value.Trim();
        if (t.Length >= 2 && t[0] == '"' && t[^1] == '"')
            t = t[1..^1].Trim();
        return t;
    }
}
