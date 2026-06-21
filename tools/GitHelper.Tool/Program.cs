using System.Diagnostics;
using System.IO;
using TransparentHotkeyUtility.Models;
using TransparentHotkeyUtility.Services;
using TransparentHotkeyUtility.Services.Git;

namespace TransparentHotkeyUtility;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        GitRunner.ReportFailuresToConsole = false;

        if (args.Length == 0 || args[0] is "-h" or "--help" or "/?")
        {
            PrintUsage();
            return args.Length == 0 ? 2 : 0;
        }

        var cmd = args[0].Trim().ToLowerInvariant();
        var rest = args.Skip(1).ToArray();

        return cmd switch
        {
            "save"   => await RunSave(rest).ConfigureAwait(false),
            "branch" => await RunBranch(rest).ConfigureAwait(false),
            "add"    => await RunAdd(rest).ConfigureAwait(false),
            "init"   => await RunInit().ConfigureAwait(false),
            "status" => RunStatus(),
            _        => Unknown(cmd),
        };
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            GitHelper.Tool — команды git с теми же настройками, что и gitHelper (settings.json).

            Использование:
              GitHelper.Tool save <сообщение коммита> [ветка]
              GitHelper.Tool branch <имя_ветки> [-M]
              GitHelper.Tool add [путь]
              GitHelper.Tool init
              GitHelper.Tool status

            Папка и URL репозитория берутся из %LocalAppData%\\gitHelper\\settings.json
            (поля folderPath и repoUrl), как в основном приложении.
            """);
    }

    private static int Unknown(string cmd)
    {
        Console.Error.WriteLine($"Неизвестная команда: {cmd}");
        PrintUsage();
        return 2;
    }

    private static AppSettings LoadSettings() => SettingsService.Load();

    private static bool ValidateFolder(string folder, out string error)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            error = "не указан путь к папке (settings.json → folderPath)";
            return false;
        }

        if (!Directory.Exists(folder))
        {
            error = "указанная папка не существует";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateRepo(string repo, out string error)
    {
        if (string.IsNullOrWhiteSpace(repo))
        {
            error = "не указана ссылка на репозиторий (settings.json → repoUrl)";
            return false;
        }

        if (!Uri.TryCreate(repo, UriKind.Absolute, out var u)
            || (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps))
        {
            error = "repoUrl должен быть http:// или https://";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static int PrintFailure(GitRunner.StepFailure? f)
    {
        if (f is null) return 0;
        Console.Error.WriteLine($"Ошибка: {f.StepLabel} (код {f.ExitCode})");
        var o = (f.StdOut ?? string.Empty).TrimEnd();
        var e = (f.StdErr ?? string.Empty).TrimEnd();
        if (o.Length > 0) Console.Error.WriteLine(o);
        if (e.Length > 0) Console.Error.WriteLine(e);
        return 1;
    }

    private static async Task<int> RunSave(string[] rest)
    {
        if (rest.Length == 0 || string.IsNullOrWhiteSpace(rest[0]))
        {
            Console.Error.WriteLine("Укажите сообщение коммита: GitHelper.Tool save \"текст\" [ветка]");
            return 2;
        }

        var s = LoadSettings();
        if (!ValidateFolder(s.FolderPath, out var fe))
        {
            Console.Error.WriteLine(fe);
            return 2;
        }

        if (!ValidateRepo(s.RepoUrl, out var re))
        {
            Console.Error.WriteLine(re);
            return 2;
        }

        var msg    = rest[0];
        var branch = rest.Length > 1 && !string.IsNullOrWhiteSpace(rest[1]) ? rest[1].Trim() : "main";

        var fail = await GitSaveService.RunAsync(s.FolderPath, s.RepoUrl, msg, branch).ConfigureAwait(false);
        return PrintFailure(fail);
    }

    private static async Task<int> RunBranch(string[] rest)
    {
        if (rest.Length == 0 || string.IsNullOrWhiteSpace(rest[0]))
        {
            Console.Error.WriteLine("Укажите имя ветки: GitHelper.Tool branch <имя> [-M]");
            return 2;
        }

        var s = LoadSettings();
        if (!ValidateFolder(s.FolderPath, out var fe))
        {
            Console.Error.WriteLine(fe);
            return 2;
        }

        var name = rest[0].Trim();
        var forceM = rest.Skip(1).Any(a => string.Equals(a, "-M", StringComparison.OrdinalIgnoreCase));

        var fail = await GitBranchService.RunAsync(s.FolderPath, name, forceM).ConfigureAwait(false);
        return PrintFailure(fail);
    }

    private static async Task<int> RunAdd(string[] rest)
    {
        var s = LoadSettings();
        if (!ValidateFolder(s.FolderPath, out var fe))
        {
            Console.Error.WriteLine(fe);
            return 2;
        }

        var path = rest.Length > 0 && !string.IsNullOrWhiteSpace(rest[0]) ? rest[0] : ".";

        var fail = await GitAddService.RunAsync(s.FolderPath, path).ConfigureAwait(false);
        return PrintFailure(fail);
    }

    private static async Task<int> RunInit()
    {
        var s = LoadSettings();
        if (!ValidateFolder(s.FolderPath, out var fe))
        {
            Console.Error.WriteLine(fe);
            return 2;
        }

        if (!ValidateRepo(s.RepoUrl, out var re))
        {
            Console.Error.WriteLine(re);
            return 2;
        }

        var fail = await GitInitService.RunAsync(s.FolderPath, s.RepoUrl).ConfigureAwait(false);
        return PrintFailure(fail);
    }

    private static int RunStatus()
    {
        var s = LoadSettings();
        if (!ValidateFolder(s.FolderPath, out var fe))
        {
            Console.Error.WriteLine(fe);
            return 2;
        }

        var escaped = s.FolderPath.Replace("\"", "\\\"", StringComparison.Ordinal);
        Process.Start(new ProcessStartInfo
        {
            FileName        = "cmd.exe",
            Arguments       = $"/k chcp 65001>nul & cd /d \"{escaped}\" & git status & echo. & pause",
            UseShellExecute = true,
        });

        return 0;
    }
}
