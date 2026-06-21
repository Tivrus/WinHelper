using System.Diagnostics;
using System.IO;
using System.Text;

namespace TransparentHotkeyUtility.Services.Git;

/// <summary>
/// Низкоуровневый запуск git-процессов.
/// Все высокоуровневые git-сервисы делегируют сюда фактический вызов.
/// </summary>
internal static class GitRunner
{
    /// <summary>
    /// Если false — при ошибке не открывается отдельное окно cmd (для консольного утилитного .exe).
    /// </summary>
    internal static bool ReportFailuresToConsole { get; set; } = true;
    // ── Public types ──────────────────────────────────────────────────────────

    /// <summary>Описание упавшего шага (используется вызывающим кодом).</summary>
    public sealed record StepFailure(string StepLabel, int ExitCode, string StdOut, string StdErr);

    // ── Internal types ────────────────────────────────────────────────────────

    internal readonly record struct ProcessResult(
        bool   Ok,
        int    ExitCode,
        string StepLabel,
        string StdOut,
        string StdErr);

    // ── Core runner ───────────────────────────────────────────────────────────

    /// <summary>Запускает git с переданными аргументами в скрытом процессе.</summary>
    internal static ProcessResult Run(
        string workingDirectory,
        string stepLabel,
        IReadOnlyList<string> gitArgs)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = "git",
                WorkingDirectory       = workingDirectory,
                CreateNoWindow         = true,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding  = Encoding.UTF8,
            };

            foreach (var arg in gitArgs)
                psi.ArgumentList.Add(arg);

            using var p = new Process { StartInfo = psi };
            p.Start();

            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();

            return new ProcessResult(p.ExitCode == 0, p.ExitCode, stepLabel, stdout, stderr);
        }
        catch (Exception ex)
        {
            return new ProcessResult(
                false, -1, stepLabel, string.Empty,
                "Не удалось запустить git. Убедитесь, что Git установлен и доступен в PATH.\r\n\r\n"
                + ex.Message);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Git возвращает exit-code 1 при «nothing to commit» — это не ошибка.
    /// </summary>
    internal static bool IsNothingToCommit(string stdout, string stderr)
    {
        static bool Has(string s, string sub) =>
            s.Contains(sub, StringComparison.OrdinalIgnoreCase);

        return Has(stdout, "nothing to commit")
            || Has(stderr, "nothing to commit")
            || Has(stdout, "nothing added to commit")
            || Has(stderr, "nothing added to commit");
    }

    /// <summary>Открывает видимое окно cmd с текстом ошибки из temp-файла.</summary>
    internal static void OpenConsoleWithError(
        string stepName, int exitCode, string stdout, string stderr)
    {
        var sb = new StringBuilder();
        sb.AppendLine("gitHelper — ошибка выполнения команд git");
        sb.AppendLine();
        sb.AppendLine($"Шаг: {stepName}");
        sb.AppendLine($"Код выхода: {exitCode}");
        sb.AppendLine();

        var o = (stdout ?? string.Empty).TrimEnd();
        var e = (stderr ?? string.Empty).TrimEnd();

        if (o.Length > 0) { sb.AppendLine("--- stdout ---"); sb.AppendLine(o); sb.AppendLine(); }
        if (e.Length > 0) { sb.AppendLine("--- stderr ---"); sb.AppendLine(e); sb.AppendLine(); }
        if (o.Length == 0 && e.Length == 0) sb.AppendLine("(Пустой вывод git.)");

        var logPath = Path.Combine(
            Path.GetTempPath(),
            "githelper_err_" + Guid.NewGuid().ToString("N") + ".txt");

        File.WriteAllText(logPath, sb.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Process.Start(new ProcessStartInfo
        {
            FileName        = "cmd.exe",
            Arguments       = $"/k chcp 65001>nul & echo. & type \"{logPath}\" & echo. & echo. & pause",
            UseShellExecute = true,
        });
    }

    // ── Factory helpers ───────────────────────────────────────────────────────

    /// <summary>Открывает консоль с ошибкой и возвращает StepFailure.</summary>
    internal static StepFailure Fail(ProcessResult r)
    {
        if (ReportFailuresToConsole)
            OpenConsoleWithError(r.StepLabel, r.ExitCode, r.StdOut, r.StdErr);
        return new StepFailure(r.StepLabel, r.ExitCode, r.StdOut, r.StdErr);
    }

    /// <summary>StepFailure для «папка не существует» с открытием консоли.</summary>
    internal static StepFailure FolderMissing()
    {
        const string msg = "Папка не существует.";
        if (ReportFailuresToConsole)
            OpenConsoleWithError("проверка папки", -1, string.Empty, msg);
        return new StepFailure("проверка папки", -1, string.Empty, msg);
    }
}
