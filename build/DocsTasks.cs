using Cake.Core;

static class DocsTasks
{
    public static void Install(ICakeContext context, BuildPaths paths) =>
        context.NpmCi(settings => settings.FromPath(paths.DocsDirectory));

    public static void Typecheck(ICakeContext context, BuildPaths paths) =>
        context.NpmRunScript("typecheck", settings => settings.FromPath(paths.DocsDirectory));

    public static void Build(ICakeContext context, BuildPaths paths) =>
        context.NpmRunScript("docs:build", settings => settings.FromPath(paths.DocsDirectory));
}
