using System.Text.RegularExpressions;
using Cake.Common.Tools.DotNet.NuGet.Push;
using Cake.Core;
using Cake.Core.IO;
using Octokit;

record ReleaseMetadata(string Tag, string Version, string Sha, FilePath Package);

static class ReleaseTasks
{
    private const string NuGetOrgSource = "https://api.nuget.org/v3/index.json";
    private const string ReleaseAssetMediaType = "application/octet-stream";
    private static readonly Regex ReleaseTagPattern = new(
        "^v(?<version>(0|[1-9]\\d*)\\.(0|[1-9]\\d*)\\.(0|[1-9]\\d*))$",
        RegexOptions.CultureInvariant
    );
    private static ReleaseMetadata? stagedRelease;

    public static void Stage(ICakeContext context, BuildPaths paths)
    {
        EnsureReleaseSource(context, paths);
        var sha = RunGit(context, paths, "rev-parse", "HEAD");
        var currentTags = RunGit(context, paths, "tag", "--points-at", "HEAD", "--list", "v*")
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(tag => ReleaseTagPattern.IsMatch(tag))
            .ToArray();

        if (currentTags.Length > 1)
        {
            throw new CakeException(
                $"Commit '{sha}' has multiple release tags: {string.Join(", ", currentTags)}."
            );
        }

        string tag;
        string version;
        if (currentTags.Length == 1)
        {
            tag = currentTags[0];
            version = ParseReleaseTag(tag);
        }
        else
        {
            var previousTag = FindLatestReleaseTag(context, paths);
            CommitTasks.CheckRange(context, paths, previousTag, "HEAD");

            var calculated = VersionTasks.Calculate(context, paths);
            version = calculated.MajorMinorPatch;
            if (previousTag is not null && ParseReleaseTag(previousTag) == version)
            {
                throw new CakeException(
                    $"No releasable feat, fix, perf, or breaking commits exist after '{previousTag}'."
                );
            }

            tag = $"v{version}";
            if (GitReferenceExists(context, paths, $"refs/tags/{tag}"))
            {
                var taggedSha = RunGit(context, paths, "rev-parse", $"refs/tags/{tag}^{{}}");
                throw new CakeException(
                    $"Release tag '{tag}' already points to '{taggedSha}', not current commit '{sha}'."
                );
            }

            RunGit(context, paths, "tag", "--annotate", tag, "--message", $"Figma MCP {tag}");
        }

        var stableVersion = VersionTasks.UseStableVersion(version, sha);
        stagedRelease = new ReleaseMetadata(
            tag,
            stableVersion.SemVer,
            sha,
            paths.GetNuGetPackage(stableVersion.SemVer)
        );
        context.Information("Staged release {0} for commit {1}.", tag, sha);
    }

    public static void Validate(ICakeContext context, BuildPaths paths) =>
        GetPublishedReleaseMetadata(context, paths);

    public static async Task CreateOrUpdateDraft(ICakeContext context, BuildPaths paths)
    {
        var metadata =
            stagedRelease
            ?? throw new CakeException("The release must be staged before creating its draft.");
        var assets = new List<FilePath>
        {
            metadata.Package,
            paths.GetNuGetSymbolsPackage(metadata.Version),
            paths.GetPluginReleaseArchive(metadata.Version),
        };
        assets.AddRange(
            PackageTasks.ServerReleaseRuntimes.Select(runtime =>
                paths.GetServerReleaseArchive(runtime, metadata.Version)
            )
        );

        foreach (var asset in assets)
        {
            if (!context.FileExists(asset))
            {
                throw new CakeException($"Release asset '{asset}' was not found.");
            }
        }

        PushReleaseTag(context, paths, metadata);

        var client = CreateGitHubClient(context);
        var repository = GetRepository(context);
        var release = await TryGetRelease(client, repository, metadata.Tag);
        if (release is null)
        {
            release = await client.Repository.Release.Create(
                repository.Owner,
                repository.Name,
                new NewRelease(metadata.Tag)
                {
                    Draft = true,
                    GenerateReleaseNotes = true,
                    Name = $"Figma MCP {metadata.Tag}",
                    TargetCommitish = metadata.Sha,
                }
            );
        }
        else if (!release.Draft)
        {
            throw new CakeException($"Release '{metadata.Tag}' already exists and is not a draft.");
        }

        var existingAssets = await client.Repository.Release.GetAllAssets(
            repository.Owner,
            repository.Name,
            release.Id
        );
        foreach (var asset in assets)
        {
            var fileName = System.IO.Path.GetFileName(asset.FullPath);
            foreach (
                var existingAsset in existingAssets.Where(existing => existing.Name == fileName)
            )
            {
                await client.Repository.Release.DeleteAsset(
                    repository.Owner,
                    repository.Name,
                    existingAsset.Id
                );
            }

            await using var stream = System.IO.File.OpenRead(asset.FullPath);
            await client.Repository.Release.UploadAsset(
                release,
                new ReleaseAssetUpload
                {
                    ContentType = GetContentType(asset),
                    FileName = fileName,
                    RawData = stream,
                }
            );
        }
    }

    public static async Task DownloadNuGetPackage(ICakeContext context, BuildPaths paths)
    {
        var metadata = GetPublishedReleaseMetadata(context, paths);
        context.EnsureDirectoryExists(paths.ReleaseDownloadDirectory);
        var client = CreateGitHubClient(context);
        var repository = GetRepository(context);
        var release = await GetRelease(client, repository, metadata.Tag);
        var assets = await client.Repository.Release.GetAllAssets(
            repository.Owner,
            repository.Name,
            release.Id
        );
        await DownloadReleaseAsset(
            context,
            client,
            assets,
            metadata.Package,
            paths.GetDownloadedNuGetPackage(metadata.Version)
        );
        await DownloadReleaseAsset(
            context,
            client,
            assets,
            paths.GetNuGetSymbolsPackage(metadata.Version),
            paths.GetDownloadedNuGetSymbolsPackage(metadata.Version)
        );
    }

    public static void PublishToNuGet(ICakeContext context, BuildPaths paths)
    {
        var metadata = GetPublishedReleaseMetadata(context, paths);
        var packageSettings = new DotNetNuGetPushSettings
        {
            ApiKey = GetRequiredEnvironmentVariable(context, "NUGET_API_KEY"),
            IgnoreSymbols = true,
            SkipDuplicate = true,
            Source = NuGetOrgSource,
        };

        context.DotNetNuGetPush(paths.GetDownloadedNuGetPackage(metadata.Version), packageSettings);
        context.DotNetNuGetPush(
            paths.GetDownloadedNuGetSymbolsPackage(metadata.Version),
            new DotNetNuGetPushSettings
            {
                ApiKey = packageSettings.ApiKey,
                SkipDuplicate = true,
                Source = NuGetOrgSource,
            }
        );
    }

    public static void PublishToGitHubPackages(ICakeContext context, BuildPaths paths)
    {
        var metadata = GetPublishedReleaseMetadata(context, paths);
        var repository = GetRepository(context);
        context.DotNetNuGetPush(
            paths.GetDownloadedNuGetPackage(metadata.Version),
            new DotNetNuGetPushSettings
            {
                ApiKey = GetRequiredEnvironmentVariable(context, "GITHUB_TOKEN"),
                SkipDuplicate = true,
                Source = $"https://nuget.pkg.github.com/{repository.Owner}/index.json",
            }
        );
    }

    private static void EnsureReleaseSource(ICakeContext context, BuildPaths paths)
    {
        var githubRef = context.EnvironmentVariable<string>("GITHUB_REF", string.Empty);
        if (!string.IsNullOrWhiteSpace(githubRef) && githubRef != "refs/heads/master")
        {
            throw new CakeException(
                $"Releases must run from 'refs/heads/master', but workflow ref is '{githubRef}'."
            );
        }

        var status = RunGit(context, paths, "status", "--porcelain", "--untracked-files=no");
        if (!string.IsNullOrWhiteSpace(status))
        {
            throw new CakeException("The release source contains tracked working-tree changes.");
        }

        if (!GitReferenceExists(context, paths, "refs/remotes/origin/master"))
        {
            return;
        }

        var head = RunGit(context, paths, "rev-parse", "HEAD");
        var remoteMaster = RunGit(context, paths, "rev-parse", "refs/remotes/origin/master");
        if (head != remoteMaster)
        {
            throw new CakeException(
                $"Release commit '{head}' is not the current origin/master commit '{remoteMaster}'."
            );
        }
    }

    private static string? FindLatestReleaseTag(ICakeContext context, BuildPaths paths)
    {
        var tags = RunGit(
                context,
                paths,
                "tag",
                "--merged",
                "HEAD",
                "--list",
                "v*",
                "--sort=-version:refname"
            )
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return tags.FirstOrDefault(tag => ReleaseTagPattern.IsMatch(tag));
    }

    private static ReleaseMetadata GetPublishedReleaseMetadata(
        ICakeContext context,
        BuildPaths paths
    )
    {
        var tag = context.Argument(
            "release-tag",
            context.EnvironmentVariable<string>("GITHUB_REF_NAME", string.Empty)
        );
        var version = ParseReleaseTag(tag);
        var sha = RunGit(context, paths, "rev-parse", "HEAD");
        if (GitReferenceExists(context, paths, $"refs/tags/{tag}"))
        {
            var taggedSha = RunGit(context, paths, "rev-parse", $"refs/tags/{tag}^{{}}");
            if (taggedSha != sha)
            {
                throw new CakeException(
                    $"Release tag '{tag}' points to '{taggedSha}', not checked-out commit '{sha}'."
                );
            }
        }

        return new ReleaseMetadata(tag, version, sha, paths.GetNuGetPackage(version));
    }

    private static void PushReleaseTag(
        ICakeContext context,
        BuildPaths paths,
        ReleaseMetadata metadata
    )
    {
        RunGit(context, paths, "push", "origin", $"refs/tags/{metadata.Tag}");
        context.Information("Published release tag {0} at {1}.", metadata.Tag, metadata.Sha);
    }

    private static bool GitReferenceExists(
        ICakeContext context,
        BuildPaths paths,
        string reference
    ) => RunGit(context, paths, true, "show-ref", "--verify", "--quiet", reference).ExitCode == 0;

    private static string ParseReleaseTag(string tag)
    {
        var match = ReleaseTagPattern.Match(tag ?? string.Empty);
        if (!match.Success)
        {
            throw new CakeException(
                $"Release tag '{tag}' must use the stable SemVer format 'vMAJOR.MINOR.PATCH'."
            );
        }

        return match.Groups["version"].Value;
    }

    private static string RunGit(ICakeContext context, BuildPaths paths, params string[] arguments)
    {
        var result = RunGit(context, paths, false, arguments);
        return result.Output;
    }

    private static GitResult RunGit(
        ICakeContext context,
        BuildPaths paths,
        bool allowFailure,
        params string[] arguments
    )
    {
        var builder = new ProcessArgumentBuilder();
        foreach (var argument in arguments)
        {
            builder.AppendQuoted(argument);
        }

        var exitCode = context.StartProcess(
            "git",
            new ProcessSettings
            {
                Arguments = builder,
                RedirectStandardOutput = true,
                WorkingDirectory = paths.RootDirectory,
            },
            out var output
        );
        if (exitCode != 0 && !allowFailure)
        {
            throw new CakeException(
                $"Git command failed with exit code {exitCode}: git {string.Join(' ', arguments)}"
            );
        }

        return new GitResult(exitCode, string.Join(Environment.NewLine, output).Trim());
    }

    private static GitHubClient CreateGitHubClient(ICakeContext context) =>
        new(new ProductHeaderValue("figma-mcp"))
        {
            Credentials = new Credentials(GetRequiredEnvironmentVariable(context, "GITHUB_TOKEN")),
        };

    private static async Task<Release> GetRelease(
        GitHubClient client,
        GitHubRepository repository,
        string tag
    ) => await client.Repository.Release.Get(repository.Owner, repository.Name, tag);

    private static async Task<Release?> TryGetRelease(
        GitHubClient client,
        GitHubRepository repository,
        string tag
    )
    {
        try
        {
            return await GetRelease(client, repository, tag);
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    private static async Task DownloadReleaseAsset(
        ICakeContext context,
        GitHubClient client,
        IReadOnlyList<ReleaseAsset> assets,
        FilePath expectedAsset,
        FilePath destination
    )
    {
        if (context.FileExists(destination))
        {
            context.DeleteFile(destination);
        }

        var fileName = System.IO.Path.GetFileName(expectedAsset.FullPath);
        var asset = assets.SingleOrDefault(candidate => candidate.Name == fileName);
        if (asset is null)
        {
            throw new CakeException($"Release package '{fileName}' was not found.");
        }

        var response = await client.Connection.Get<Stream>(
            new Uri(asset.Url),
            new Dictionary<string, string>(),
            ReleaseAssetMediaType
        );
        await using var source =
            response.Body
            ?? throw new CakeException($"Release package '{fileName}' returned no binary content.");
        await using var output = System.IO.File.Create(destination.FullPath);
        await source.CopyToAsync(output);
    }

    private static GitHubRepository GetRepository(ICakeContext context)
    {
        var parts = GetRequiredEnvironmentVariable(context, "GITHUB_REPOSITORY").Split('/', 2);
        if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
        {
            throw new CakeException(
                "Environment variable 'GITHUB_REPOSITORY' must use the 'owner/repository' format."
            );
        }

        return new GitHubRepository(parts[0], parts[1]);
    }

    private static string GetContentType(FilePath asset) =>
        asset.GetExtension().Equals(".nupkg", StringComparison.OrdinalIgnoreCase)
        || asset.GetExtension().Equals(".snupkg", StringComparison.OrdinalIgnoreCase)
            ? "application/vnd.nuget.package"
            : "application/zip";

    private static string GetRequiredEnvironmentVariable(ICakeContext context, string name)
    {
        var value = context.EnvironmentVariable<string>(name, string.Empty);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CakeException($"Environment variable '{name}' is required.");
        }

        return value;
    }

    private record GitHubRepository(string Owner, string Name);

    private record GitResult(int ExitCode, string Output);
}
