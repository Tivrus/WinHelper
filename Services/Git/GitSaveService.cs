using System.IO;

namespace TransparentHotkeyUtility.Services.Git;

/// <summary>
/// Полный save-workflow: git add → remote → commit → push.
/// </summary>
internal static class GitSaveService
{
    public static async Task<GitRunner.StepFailure?> RunAsync(
        string workingDirectory,
        string remoteUrl,
        string commitMessage,
        string branch = "main",
        CancellationToken cancellationToken = default)
    {
        var path = Path.GetFullPath(workingDirectory);
        if (!Directory.Exists(path))
            return GitRunner.FolderMissing();

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var add = GitRunner.Run(path, "git add .", ["add", "."]);
            if (!add.Ok) return GitRunner.Fail(add);

            cancellationToken.ThrowIfCancellationRequested();

            var probe  = GitRunner.Run(path, "git remote get-url origin", ["remote", "get-url", "origin"]);
            var remote = probe.Ok
                ? GitRunner.Run(path, "git remote set-url origin", ["remote", "set-url", "origin", remoteUrl])
                : GitRunner.Run(path, "git remote add origin",     ["remote", "add",     "origin", remoteUrl]);

            if (!remote.Ok) return GitRunner.Fail(remote);

            cancellationToken.ThrowIfCancellationRequested();

            var commit = GitRunner.Run(path, "git commit -m \"…\"", ["commit", "-m", commitMessage]);
            if (!commit.Ok && !GitRunner.IsNothingToCommit(commit.StdOut, commit.StdErr))
                return GitRunner.Fail(commit);

            cancellationToken.ThrowIfCancellationRequested();

            var push = GitRunner.Run(
                path,
                "git push --set-upstream origin " + branch,
                ["push", "--set-upstream", "origin", branch]);

            return push.Ok ? null : GitRunner.Fail(push);

        }, cancellationToken).ConfigureAwait(false);
    }
}
