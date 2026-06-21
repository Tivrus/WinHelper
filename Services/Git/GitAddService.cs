using System.IO;

namespace TransparentHotkeyUtility.Services.Git;

/// <summary>git add &lt;path&gt;</summary>
internal static class GitAddService
{
    public static async Task<GitRunner.StepFailure?> RunAsync(
        string workingDirectory,
        string addPath = ".",
        CancellationToken cancellationToken = default)
    {
        var path = Path.GetFullPath(workingDirectory);

        return await Task.Run(() =>
        {
            if (!Directory.Exists(path))
                return GitRunner.FolderMissing();

            var result = GitRunner.Run(path, "git add " + addPath, ["add", addPath]);
            return result.Ok ? null : GitRunner.Fail(result);

        }, cancellationToken).ConfigureAwait(false);
    }
}
