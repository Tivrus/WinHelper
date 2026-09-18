using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace TransparentHotkeyUtility.Infrastructure;

/// <summary>
/// Мгновенный запуск .exe / AutoHotkey v2 (.ahk).
/// Путь к AHK кэшируется при старте приложения; Process.Start вызывается сразу на вызывающем потоке.
/// </summary>
internal static class ExternalProcessLauncher
{
    private static readonly object AhkLock = new();
    private static string? _cachedAhkPath;
    private static bool _ahkResolved;

    /// <summary>Синхронный прогрев кэша AHK v2 — вызывать при старте, до первого клика.</summary>
    public static void Warmup()
    {
        _ = GetAutoHotkeyV2Cached();
    }

    public static void Start(string fileName, IReadOnlyList<string>? argumentParts = null)
    {
        fileName = NormalizeToken(fileName);
        var args = argumentParts is null
            ? Array.Empty<string>()
            : argumentParts.Select(NormalizeToken).ToArray();

        // Process.Start на UI-потоке может на секунды заморозить ввод (CreateProcess + shell hooks).
        // Уводим в фон — клик отрабатывает мгновенно, мышь не лагает.
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                StartCore(fileName, args);
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    System.Windows.MessageBox.Show(
                        $"Не удалось запустить:\n{ex.Message}",
                        "gitHelper",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                });
            }
        });
    }

    private static void StartCore(string fileName, string[] argumentParts)
    {
        if (IsAhkScript(fileName))
        {
            StartAhkV2(fileName, argumentParts);
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName         = fileName,
            UseShellExecute  = false,
            CreateNoWindow   = true,
            WorkingDirectory = SafeDirectory(fileName),
        };

        foreach (var part in argumentParts)
            psi.ArgumentList.Add(part);

        Process.Start(psi)?.Dispose();
    }

    private static void StartAhkV2(string scriptPath, string[] argumentParts)
    {
        var ahk = GetAutoHotkeyV2Cached()
            ?? throw new FileNotFoundException(
                "Не найден AutoHotkey v2 (AutoHotkey64.exe).\n" +
                "Установите AHK v2 или задайте путь в переменной окружения AUTOHOTKEY_V2.");

        var psi = new ProcessStartInfo
        {
            FileName         = ahk,
            UseShellExecute  = false,
            CreateNoWindow   = true,
            WorkingDirectory = SafeDirectory(scriptPath),
        };

        // AutoHotkey v2: AutoHotkey64.exe script.ahk [args...] → A_Args
        psi.ArgumentList.Add(scriptPath);
        foreach (var part in argumentParts)
            psi.ArgumentList.Add(part);

        Process.Start(psi)?.Dispose();
    }

    private static bool IsAhkScript(string path) =>
        path.EndsWith(".ahk", StringComparison.OrdinalIgnoreCase);

    private static string? GetAutoHotkeyV2Cached()
    {
        lock (AhkLock)
        {
            if (_ahkResolved)
                return _cachedAhkPath;

            _cachedAhkPath = FindAutoHotkeyV2();
            _ahkResolved   = true;
            return _cachedAhkPath;
        }
    }

    /// <summary>
    /// Ищет интерпретатор AutoHotkey v2.
    /// Приоритет: AUTOHOTKEY_V2 → стандартные пути установки → реестр.
    /// </summary>
    internal static string? FindAutoHotkeyV2()
    {
        var env = Environment.GetEnvironmentVariable("AUTOHOTKEY_V2");
        if (!string.IsNullOrWhiteSpace(env))
        {
            env = NormalizeToken(env);
            if (File.Exists(env)) return env;
        }

        foreach (var candidate in EnumerateAhkV2Candidates())
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateAhkV2Candidates()
    {
        var programFiles    = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData    = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        string[] roots =
        [
            Path.Combine(programFiles, "AutoHotkey", "v2"),
            Path.Combine(programFilesX86, "AutoHotkey", "v2"),
            Path.Combine(localAppData, "Programs", "AutoHotkey", "v2"),
            Path.Combine(programFiles, "AutoHotkey"),
            Path.Combine(programFilesX86, "AutoHotkey"),
        ];

        string[] names = ["AutoHotkey64.exe", "AutoHotkey.exe", "AutoHotkey32.exe"];

        foreach (var root in roots)
        foreach (var name in names)
            yield return Path.Combine(root, name);

        foreach (var installDir in ReadAhkInstallDirsFromRegistry())
        {
            var v2 = Path.Combine(installDir, "v2");
            foreach (var name in names)
            {
                yield return Path.Combine(v2, name);
                yield return Path.Combine(installDir, name);
            }
        }
    }

    private static IEnumerable<string> ReadAhkInstallDirsFromRegistry()
    {
        string[] keys =
        [
            @"SOFTWARE\AutoHotkey\v2",
            @"SOFTWARE\AutoHotkey",
            @"SOFTWARE\WOW6432Node\AutoHotkey\v2",
            @"SOFTWARE\WOW6432Node\AutoHotkey",
        ];

        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            foreach (var keyPath in keys)
            {
                using var key = hive.OpenSubKey(keyPath);
                var dir = key?.GetValue("InstallDir") as string
                       ?? key?.GetValue("InstallPath") as string;
                if (!string.IsNullOrWhiteSpace(dir))
                    yield return dir.Trim().TrimEnd('\\', '/');
            }
        }
    }

    private static string SafeDirectory(string filePath)
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            return !string.IsNullOrEmpty(dir) ? dir : Environment.CurrentDirectory;
        }
        catch
        {
            return Environment.CurrentDirectory;
        }
    }

    private static string NormalizeToken(string value)
    {
        var t = value.Trim();
        if (t.Length >= 2 && t[0] == '"' && t[^1] == '"')
            t = t[1..^1].Trim();
        return t;
    }
}
