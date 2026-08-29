using System.IO.Compression;
using Cake.Common.Tools.DotNet.Pack;
using Cake.Core;

static class PackageTasks
{
    private const string ServerReleaseRuntime = "win-x64";

    public static void CreateNuGetPackage(ICakeContext context, BuildPaths paths)
    {
        context.EnsureDirectoryExists(paths.ReleaseDirectory);
        context.DotNetPack(
            paths.ServerProject.ToString(),
            new DotNetPackSettings
            {
                Configuration = context.Argument("configuration", "Release"),
                NoRestore = true,
                OutputDirectory = paths.ReleaseDirectory,
                WorkingDirectory = paths.ServerDirectory,
                MSBuildSettings = new DotNetMSBuildSettings().AddTo(
                    VersionTasks.Calculate(context, paths)
                ),
            }
        );
    }

    public static void CreateServerArchive(ICakeContext context, BuildPaths paths) =>
        CreateArchive(
            context,
            paths.GetServerPublishDirectory(ServerReleaseRuntime),
            paths.ServerReleaseArchive
        );

    public static void CreatePluginArchive(ICakeContext context, BuildPaths paths) =>
        CreateArchive(context, paths.PluginDistributionDirectory, paths.PluginReleaseArchive);

    private static void CreateArchive(
        ICakeContext context,
        DirectoryPath source,
        FilePath destination
    )
    {
        context.EnsureDirectoryExists(destination.GetDirectory());
        if (context.FileExists(destination))
        {
            context.DeleteFile(destination);
        }

        ZipFile.CreateFromDirectory(source.FullPath, destination.FullPath);
    }
}
