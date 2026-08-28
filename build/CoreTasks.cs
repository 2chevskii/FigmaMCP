using Cake.Core;

static class CoreTasks
{
    public static void Clean(ICakeContext context, BuildPaths paths) =>
        context.CleanDirectory(paths.ArtifactsDirectory);

    public static void RestoreTools(ICakeContext context) => context.StartProcess("dotnet", "tool restore");
}
