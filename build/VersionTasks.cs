using System.Text.Json;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Core;
using Cake.Core.IO;

record ProductVersion(
    string SemVer,
    string MajorMinorPatch,
    string AssemblyVersion,
    string FileVersion,
    string InformationalVersion,
    string Sha
);

static class VersionTasks
{
    private static ProductVersion? cachedVersion;

    public static ProductVersion Calculate(ICakeContext context, BuildPaths paths)
    {
        if (cachedVersion is not null)
        {
            return cachedVersion;
        }

        // GitVersion is pinned in the repository's local tool manifest. Version-aware
        // targets can be invoked directly, so they cannot rely on another Cake target
        // having restored the manifest first.
        CoreTasks.RestoreTools(context);
        var gitVersion = RunGitVersion(context, paths);

        cachedVersion = new ProductVersion(
            GetRequiredProperty(gitVersion, "SemVer"),
            GetRequiredProperty(gitVersion, "MajorMinorPatch"),
            GetRequiredProperty(gitVersion, "AssemblySemVer"),
            GetRequiredProperty(gitVersion, "AssemblySemFileVer"),
            GetRequiredProperty(gitVersion, "InformationalVersion"),
            GetRequiredProperty(gitVersion, "Sha")
        );
        return cachedVersion;
    }

    public static ProductVersion UseStableVersion(string version, string sha)
    {
        cachedVersion = new ProductVersion(
            version,
            version,
            CreateAssemblyVersion(version),
            CreateFileVersion(version),
            $"{version}+Sha.{sha}",
            sha
        );
        return cachedVersion;
    }

    public static void Print(ICakeContext context, BuildPaths paths)
    {
        var version = Calculate(context, paths);
        context.Information("Product version: {0}", version.SemVer);
        context.Information("Informational version: {0}", version.InformationalVersion);
        context.Information("Commit: {0}", version.Sha);

        var outputPath = context.Argument("version-output", string.Empty);
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            var outputFile = new FilePath(outputPath).MakeAbsolute(context.Environment);
            context.EnsureDirectoryExists(outputFile.GetDirectory());
            System.IO.File.WriteAllText(outputFile.FullPath, version.SemVer);
        }
    }

    public static DotNetMSBuildSettings AddTo(
        this DotNetMSBuildSettings settings,
        ProductVersion version
    ) =>
        settings
            .WithProperty("Version", version.SemVer)
            .WithProperty("PackageVersion", version.SemVer)
            .WithProperty("AssemblyVersion", version.AssemblyVersion)
            .WithProperty("FileVersion", version.FileVersion)
            .WithProperty("InformationalVersion", version.InformationalVersion)
            .WithProperty("IncludeSourceRevisionInInformationalVersion", "false");

    private static JsonElement RunGitVersion(ICakeContext context, BuildPaths paths)
    {
        DirectoryPath? temporaryRepository = null;
        var workingDirectory = paths.RootDirectory;
        var configFile = paths.GitVersionConfiguration;

        try
        {
            var codexRefs = paths.RootDirectory.Combine(".git/refs/codex");
            if (OperatingSystem.IsWindows() && context.DirectoryExists(codexRefs))
            {
                temporaryRepository = new DirectoryPath(
                    System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(),
                        $"figma-mcp-gitversion-{Guid.NewGuid():N}"
                    )
                );
                RunProcess(
                    context,
                    paths.RootDirectory,
                    "git",
                    "clone",
                    "--quiet",
                    "--no-hardlinks",
                    "--single-branch",
                    paths.RootDirectory.FullPath,
                    temporaryRepository.FullPath
                );

                workingDirectory = temporaryRepository;
                configFile = temporaryRepository.CombineWithFilePath("GitVersion.yml");
                context.CopyFile(paths.GitVersionConfiguration, configFile);
                context.CopyFile(
                    paths.RootDirectory.CombineWithFilePath("dotnet-tools.json"),
                    temporaryRepository.CombineWithFilePath("dotnet-tools.json")
                );
                RunProcess(context, workingDirectory, "dotnet", "tool", "restore");
            }

            var output = RunProcess(
                context,
                workingDirectory,
                "dotnet",
                "tool",
                "run",
                "dotnet-gitversion",
                "--",
                "/output",
                "json",
                "/config",
                configFile.FullPath,
                "/nofetch"
            );
            using var document = JsonDocument.Parse(output);
            return document.RootElement.Clone();
        }
        finally
        {
            if (temporaryRepository is not null && context.DirectoryExists(temporaryRepository))
            {
                context.DeleteDirectory(
                    temporaryRepository,
                    new DeleteDirectorySettings { Recursive = true, Force = true }
                );
            }
        }
    }

    private static string RunProcess(
        ICakeContext context,
        DirectoryPath workingDirectory,
        string executable,
        params string[] arguments
    )
    {
        var builder = new ProcessArgumentBuilder();
        foreach (var argument in arguments)
        {
            builder.AppendQuoted(argument);
        }

        var exitCode = context.StartProcess(
            executable,
            new ProcessSettings
            {
                Arguments = builder,
                RedirectStandardOutput = true,
                WorkingDirectory = workingDirectory,
            },
            out var output
        );
        if (exitCode != 0)
        {
            throw new CakeException(
                $"{executable} exited with code {exitCode}: {string.Join(' ', arguments)}"
            );
        }

        return string.Join(Environment.NewLine, output).Trim();
    }

    private static string GetRequiredProperty(JsonElement gitVersion, string name)
    {
        if (
            !gitVersion.TryGetProperty(name, out var property)
            || string.IsNullOrWhiteSpace(property.GetString())
        )
        {
            throw new CakeException($"GitVersion did not return '{name}'.");
        }

        return property.GetString()!;
    }

    private static string CreateAssemblyVersion(string version)
    {
        var components = ParseVersion(version);
        return $"{components[0]}.0.0.0";
    }

    private static string CreateFileVersion(string version)
    {
        var components = ParseVersion(version);
        return $"{components[0]}.{components[1]}.{components[2]}.0";
    }

    private static int[] ParseVersion(string version)
    {
        var components = version.Split('.');
        if (components.Length != 3 || components.Any(component => !int.TryParse(component, out _)))
        {
            throw new CakeException($"Stable product version '{version}' is not valid SemVer.");
        }

        return components.Select(int.Parse).ToArray();
    }
}
