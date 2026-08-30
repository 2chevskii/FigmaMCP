using System.Collections.Generic;
using Cake.Common.Tools.DotNet.Build;
using Cake.Common.Tools.DotNet.Restore;
using Cake.Common.Tools.DotNet.Test;
using Cake.Common.Tools.DotNet.Tool;
using Cake.Core;

static class ServerTasks
{
    private const string CsharpierTool = "csharpier";
    private const string InspectorPackage = "@modelcontextprotocol/inspector";
    private const string PublishRuntime = "win-x64";
    private const string TestPublishRuntime = "win-x64";

    public static void Format(ICakeContext context, BuildPaths paths) =>
        context.DotNetTool(
            CsharpierTool,
            new DotNetToolSettings
            {
                WorkingDirectory = paths.ServerDirectory,
                ArgumentCustomization = arguments =>
                    arguments
                        .Append(context.Argument("fix", false) ? "format" : "check")
                        .Append("."),
            }
        );

    public static void Restore(ICakeContext context, BuildPaths paths) =>
        context.DotNetRestore(
            paths.ServerSolution.ToString(),
            new DotNetRestoreSettings
            {
                Runtime = context.Argument("runtime", PublishRuntime),
                WorkingDirectory = paths.ServerDirectory,
            }
        );

    public static void Build(ICakeContext context, BuildPaths paths) =>
        context.DotNetBuild(
            paths.ServerSolution.ToString(),
            new DotNetBuildSettings
            {
                Configuration = context.Argument("configuration", "Release"),
                NoRestore = true,
                WorkingDirectory = paths.ServerDirectory,
                MSBuildSettings = new DotNetMSBuildSettings().AddTo(
                    VersionTasks.Calculate(context, paths)
                ),
            }
        );

    public static void Test(ICakeContext context, BuildPaths paths) => RunTests(context, paths);

    public static void PublishTests(ICakeContext context, BuildPaths paths)
    {
        var runtime = context.Argument("runtime", TestPublishRuntime);
        var outputDirectory = paths.GetServerTestPublishDirectory(runtime);
        context.EnsureDirectoryExists(outputDirectory);
        context.CleanDirectory(outputDirectory);
        context.DotNetPublish(
            paths.ServerTestProject.ToString(),
            new DotNetPublishSettings
            {
                Configuration = context.Argument("configuration", "Release"),
                NoRestore = true,
                Runtime = runtime,
                SelfContained = true,
                OutputDirectory = outputDirectory,
                WorkingDirectory = paths.ServerDirectory,
                MSBuildSettings = new DotNetMSBuildSettings()
                    .WithProperty("PublishSingleFile", "true")
                    .WithProperty("IncludeAllContentForSelfExtract", "true")
                    .WithProperty("DebugSymbols", "true")
                    .WithProperty("DebugType", "portable")
                    .AddTo(VersionTasks.Calculate(context, paths)),
            }
        );
    }

    private static void RunTests(ICakeContext context, BuildPaths paths)
    {
        context.EnsureDirectoryExists(paths.ServerTestResultsDirectory);
        context.CleanDirectory(paths.ServerTestResultsDirectory);
        context.DotNetTest(
            paths.ServerSolution.ToString(),
            new DotNetTestSettings
            {
                Configuration = context.Argument("configuration", "Release"),
                NoBuild = context.Argument("no-build", false),
                WorkingDirectory = paths.ServerDirectory,
                ResultsDirectory = paths.ServerTestResultsDirectory,
                MSBuildSettings = new DotNetMSBuildSettings().AddTo(
                    VersionTasks.Calculate(context, paths)
                ),
                ArgumentCustomization = arguments =>
                    arguments
                        .Append("--report-trx")
                        .Append("--report-trx-filename")
                        .Append(paths.ServerTestReport.GetFilename().ToString())
                        .Append("--coverage")
                        .Append("--coverage-output-format")
                        .Append("cobertura")
                        .Append("--coverage-output")
                        .AppendQuoted(paths.ServerCoverageReport.ToString()),
            }
        );
    }

    public static void Inspect(ICakeContext context, BuildPaths paths)
    {
        var serverAssembly = paths.GetServerAssembly(context.Argument("configuration", "Release"));
        if (!context.FileExists(serverAssembly))
        {
            throw new CakeException($"Server assembly was not found: {serverAssembly}.");
        }

        context.NpmExec(
            InspectorPackage,
            settings =>
            {
                settings.FromPath(paths.RootDirectory);
                settings.EnvironmentVariables = new Dictionary<string, string>
                {
                    ["npm_config_yes"] = "true",
                };
                settings.Arguments.Add("--cwd");
                settings.Arguments.Add(paths.RootDirectory.ToString());
                settings.Arguments.Add("dotnet");
                settings.Arguments.Add(serverAssembly.ToString());
            }
        );
    }

    public static void Publish(ICakeContext context, BuildPaths paths) =>
        Publish(context, paths, context.Argument("runtime", PublishRuntime), noRestore: true);

    public static void PublishForRelease(ICakeContext context, BuildPaths paths, string runtime) =>
        Publish(context, paths, runtime, noRestore: false);

    private static void Publish(
        ICakeContext context,
        BuildPaths paths,
        string runtime,
        bool noRestore
    )
    {
        var outputDirectory = paths.GetServerPublishDirectory(runtime);
        context.EnsureDirectoryExists(outputDirectory);
        context.CleanDirectory(outputDirectory);
        context.DotNetPublish(
            paths.ServerProject.ToString(),
            new DotNetPublishSettings
            {
                Configuration = context.Argument("configuration", "Release"),
                NoRestore = noRestore,
                OutputDirectory = outputDirectory,
                WorkingDirectory = paths.ServerDirectory,
                MSBuildSettings = new DotNetMSBuildSettings()
                    .WithProperty("PublishProfile", runtime)
                    .AddTo(VersionTasks.Calculate(context, paths)),
            }
        );
    }
}
