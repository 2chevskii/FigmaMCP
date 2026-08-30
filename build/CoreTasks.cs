using Cake.Core;

static class CoreTasks
{
    public static void Clean(ICakeContext context, BuildPaths paths) =>
        context.CleanDirectory(paths.ArtifactsDirectory);

    public static void RestoreTools(ICakeContext context)
    {
        var exitCode = context.StartProcess("dotnet", "tool restore");
        if (exitCode != 0)
        {
            throw new CakeException($"dotnet tool restore exited with code {exitCode}.");
        }
    }
}
