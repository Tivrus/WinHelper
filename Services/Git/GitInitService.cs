using System.IO;

namespace TransparentHotkeyUtility.Services.Git;

/// <summary>git init → git remote add/set-url origin → git branch -M main</summary>
internal static class GitInitService
{
    public static async Task<GitRunner.StepFailure?> RunAsync(
        string workingDirectory,
        string remoteUrl,
        CancellationToken cancellationToken = default)
    {
        var path = Path.GetFullPath(workingDirectory);

        return await Task.Run(() =>
        {
            if (!Directory.Exists(path))
                return GitRunner.FolderMissing();

            var init = GitRunner.Run(path, "git init", ["init"]);
            if (!init.Ok) return GitRunner.Fail(init);

            cancellationToken.ThrowIfCancellationRequested();

            var probe  = GitRunner.Run(path, "git remote get-url origin", ["remote", "get-url", "origin"]);
            var remote = probe.Ok
                ? GitRunner.Run(path, "git remote set-url origin", ["remote", "set-url", "origin", remoteUrl])
                : GitRunner.Run(path, "git remote add origin",     ["remote", "add",     "origin", remoteUrl]);

            if (!remote.Ok) return GitRunner.Fail(remote);

            cancellationToken.ThrowIfCancellationRequested();

            var branch = GitRunner.Run(path, "git branch -M main", ["branch", "-M", "main"]);
            return branch.Ok ? null : GitRunner.Fail(branch);

        }, cancellationToken).ConfigureAwait(false);
    }
}
