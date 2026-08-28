using Cake.Common.Tools.DotNet.NuGet.Push;
using Cake.Core;
using Octokit;
using System.Xml.Linq;

record ReleaseMetadata(string Tag, string Version, FilePath Package);

static class ReleaseTasks
{
    private const string NuGetOrgSource = "https://api.nuget.org/v3/index.json";
    private const string ReleaseAssetMediaType = "application/octet-stream";

    public static void Validate(ICakeContext context, BuildPaths paths) => GetMetadata(context, paths);

    public static async Task CreateOrUpdateDraft(ICakeContext context, BuildPaths paths)
    {
        var metadata = GetMetadata(context, paths);
        var assets = new[]
        {
            metadata.Package,
            paths.ServerReleaseArchive,
            paths.PluginReleaseArchive,
        };

        foreach (var asset in assets)
        {
            if (!context.FileExists(asset))
            {
                throw new CakeException($"Release asset '{asset}' was not found.");
            }
        }

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
            foreach (var existingAsset in existingAssets.Where(existing => existing.Name == fileName))
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
        var metadata = GetMetadata(context, paths);
        context.EnsureDirectoryExists(paths.ReleaseDownloadDirectory);
        var package = paths.GetDownloadedNuGetPackage(metadata.Version);
        if (context.FileExists(package))
        {
            context.DeleteFile(package);
        }

        var client = CreateGitHubClient(context);
        var repository = GetRepository(context);
        var release = await GetRelease(client, repository, metadata.Tag);
        var asset = (await client.Repository.Release.GetAllAssets(
            repository.Owner,
            repository.Name,
            release.Id
        )).SingleOrDefault(candidate => candidate.Name == System.IO.Path.GetFileName(metadata.Package.FullPath));
        if (asset is null)
        {
            throw new CakeException($"Release package '{metadata.Package.GetFilename()}' was not found.");
        }

        var connection = new ApiConnection(client.Connection);
        await using var source = await connection.GetRawStream(
            new Uri(asset.Url),
            new Dictionary<string, string>
            {
                ["Accept"] = ReleaseAssetMediaType,
            }
        );
        await using var destination = System.IO.File.Create(package.FullPath);
        await source.CopyToAsync(destination);
    }

    public static void PublishToNuGet(ICakeContext context, BuildPaths paths)
    {
        var metadata = GetMetadata(context, paths);
        context.DotNetNuGetPush(
            paths.GetDownloadedNuGetPackage(metadata.Version),
            new DotNetNuGetPushSettings
            {
                ApiKey = GetRequiredEnvironmentVariable(context, "NUGET_API_KEY"),
                SkipDuplicate = true,
                Source = NuGetOrgSource,
            }
        );
    }

    public static void PublishToGitHubPackages(ICakeContext context, BuildPaths paths)
    {
        var metadata = GetMetadata(context, paths);
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

    private static GitHubClient CreateGitHubClient(ICakeContext context) =>
        new(new ProductHeaderValue("figma-mcp"))
        {
            Credentials = new Credentials(GetRequiredEnvironmentVariable(context, "GITHUB_TOKEN")),
        };

    private static async Task<Release> GetRelease(
        GitHubClient client,
        GitHubRepository repository,
        string tag
    ) =>
        await client.Repository.Release.Get(repository.Owner, repository.Name, tag);

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

    private static ReleaseMetadata GetMetadata(ICakeContext context, BuildPaths paths)
    {
        var tag = context.Argument(
            "release-tag",
            context.EnvironmentVariable<string>("GITHUB_REF_NAME", string.Empty)
        );
        if (string.IsNullOrWhiteSpace(tag) || !tag.StartsWith('v') || tag.Length == 1)
        {
            throw new CakeException("Release tag must start with 'v' and include a version.");
        }

        var version = XDocument
            .Load(paths.ServerProject.FullPath)
            .Descendants("Version")
            .SingleOrDefault()
            ?.Value;
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new CakeException($"Project version was not found in '{paths.ServerProject}'.");
        }

        if (tag[1..] != version)
        {
            throw new CakeException($"Project version '{version}' must match release tag '{tag}'.");
        }

        return new ReleaseMetadata(tag, version, paths.GetNuGetPackage(version));
    }

    private static GitHubRepository GetRepository(ICakeContext context)
    {
        var parts = GetRequiredEnvironmentVariable(context, "GITHUB_REPOSITORY").Split('/', 2);
        if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
        {
            throw new CakeException("Environment variable 'GITHUB_REPOSITORY' must use the 'owner/repository' format.");
        }

        return new GitHubRepository(parts[0], parts[1]);
    }

    private static string GetContentType(FilePath asset) =>
        asset.GetExtension().Equals(".nupkg", StringComparison.OrdinalIgnoreCase)
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
}
