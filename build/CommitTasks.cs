using Cake.Core;
using Cake.Core.IO;

static class CommitTasks
{
    public static void Install(ICakeContext context, BuildPaths paths) =>
        context.NpmCi(settings => settings.FromPath(paths.RootDirectory));

    public static void Check(ICakeContext context, BuildPaths paths)
    {
        var from = context.Argument("from", string.Empty);
        var to = context.Argument("to", "HEAD");
        var messageFile = context.Argument("message-file", string.Empty);

        RunCommitlint(context, paths, from, to, messageFile);
    }

    public static void CheckRange(
        ICakeContext context,
        BuildPaths paths,
        string? from,
        string to
    ) => RunCommitlint(context, paths, from ?? string.Empty, to, string.Empty);

    private static void RunCommitlint(
        ICakeContext context,
        BuildPaths paths,
        string from,
        string to,
        string messageFile
    )
    {
        context.NpmExec(
            "commitlint",
            settings =>
            {
                settings.FromPath(paths.RootDirectory);
                settings.Arguments.Add("--verbose");
                if (!string.IsNullOrWhiteSpace(messageFile))
                {
                    settings.Arguments.Add("--edit");
                    settings.Arguments.Add(messageFile);
                    return;
                }

                if (string.IsNullOrWhiteSpace(from))
                {
                    settings.Arguments.Add("--last");
                    return;
                }

                settings.Arguments.Add("--from");
                settings.Arguments.Add(from);
                settings.Arguments.Add("--to");
                settings.Arguments.Add(to);
            }
        );
    }

    public static void InstallHook(ICakeContext context, BuildPaths paths)
    {
        var exitCode = context.StartProcess(
            "git",
            new ProcessSettings
            {
                Arguments = "config --local core.hooksPath .githooks",
                WorkingDirectory = paths.RootDirectory,
            }
        );
        if (exitCode != 0)
        {
            throw new CakeException("Failed to configure the repository Git hooks path.");
        }

        if (!OperatingSystem.IsWindows())
        {
            System.IO.File.SetUnixFileMode(
                paths.CommitMessageHook.FullPath,
                UnixFileMode.UserRead
                    | UnixFileMode.UserWrite
                    | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead
                    | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead
                    | UnixFileMode.OtherExecute
            );
        }

        context.Information("Git commit-msg hook installed from .githooks/commit-msg.");
    }
}
