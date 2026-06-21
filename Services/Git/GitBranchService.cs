using System.IO;

namespace TransparentHotkeyUtility.Services.Git;

/// <summary>git branch [&lt;name&gt; | -M &lt;name&gt;]</summary>
internal static class GitBranchService
{
    public static async Task<GitRunner.StepFailure?> RunAsync(
        string workingDirectory,
        string branchName,
        bool forceRename = false,
        CancellationToken cancellationToken = default)
    {
        var path = Path.GetFullPath(workingDirectory);

        return await Task.Run(() =>
        {
            if (!Directory.Exists(path))
                return GitRunner.FolderMissing();

            var label  = forceRename ? "git branch -M" : "git branch";
            string[] args = forceRename
                ? ["branch", "-M", branchName]
                : ["branch",       branchName];

            var result = GitRunner.Run(path, label, args);
            return result.Ok ? null : GitRunner.Fail(result);

        }, cancellationToken).ConfigureAwait(false);
    }
}
