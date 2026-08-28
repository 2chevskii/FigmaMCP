using Cake.Core;

static class PluginTasks
{
    public static void Install(ICakeContext context, BuildPaths paths) =>
        context.NpmCi(settings => settings.FromPath(paths.PluginDirectory));

    public static void Format(ICakeContext context, BuildPaths paths) =>
        context.NpmRunScript("format:check", settings => settings.FromPath(paths.PluginDirectory));

    public static void Lint(ICakeContext context, BuildPaths paths) =>
        context.NpmRunScript("lint", settings => settings.FromPath(paths.PluginDirectory));

    public static void Test(ICakeContext context, BuildPaths paths)
    {
        context.EnsureDirectoryExists(paths.PluginTestResultsDirectory);
        context.CleanDirectory(paths.PluginTestResultsDirectory);
        context.EnsureDirectoryExists(paths.PluginTestReportsDirectory);
        context.EnsureDirectoryExists(paths.PluginCoverageDirectory);
        context.NpmRunScript("test:coverage", settings => settings.FromPath(paths.PluginDirectory));
    }

    public static void Build(ICakeContext context, BuildPaths paths) =>
        context.NpmRunScript("build", settings => settings.FromPath(paths.PluginDirectory));
}
