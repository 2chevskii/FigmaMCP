#:sdk Cake.Sdk@6.2.0

record BuildPaths(
    DirectoryPath RootDirectory,
    DirectoryPath ArtifactsDirectory,
    DirectoryPath ReleaseDirectory,
    DirectoryPath ReleaseDownloadDirectory,
    DirectoryPath DocsDirectory,
    DirectoryPath PluginDirectory,
    DirectoryPath PluginDistributionDirectory,
    DirectoryPath PluginTestResultsDirectory,
    DirectoryPath PluginTestReportsDirectory,
    DirectoryPath PluginCoverageDirectory,
    DirectoryPath ServerDirectory,
    DirectoryPath ServerProjectDirectory,
    DirectoryPath ServerPublishRootDirectory,
    DirectoryPath ServerTestPublishRootDirectory,
    DirectoryPath ServerTestResultsDirectory,
    FilePath GitVersionConfiguration,
    FilePath CommitMessageHook,
    FilePath ServerSolution,
    FilePath ServerProject,
    FilePath ServerTestProject,
    FilePath ServerTestReport,
    FilePath ServerCoverageReport
)
{
    public static BuildPaths Create(DirectoryPath rootDirectory)
    {
        var artifactsDirectory = rootDirectory.Combine("artifacts");
        var releaseDirectory = artifactsDirectory.Combine("release");
        var pluginDirectory = rootDirectory.Combine("packages").Combine("plugin");
        var pluginTestResultsDirectory = pluginDirectory.Combine("TestResults");
        var serverDirectory = rootDirectory.Combine("packages").Combine("server");
        var serverProjectDirectory = serverDirectory.Combine("src").Combine("FigmaMCP");
        var serverTestProjectDirectory = serverDirectory
            .Combine("tests")
            .Combine("FigmaMCP.Tests.Unit");
        var serverTestResultsDirectory = serverDirectory.Combine("TestResults");

        return new BuildPaths(
            rootDirectory,
            artifactsDirectory,
            releaseDirectory,
            artifactsDirectory.Combine("release-download"),
            rootDirectory.Combine("docs"),
            pluginDirectory,
            pluginDirectory.Combine("dist"),
            pluginTestResultsDirectory,
            pluginTestResultsDirectory.Combine("tests"),
            pluginTestResultsDirectory.Combine("coverage"),
            serverDirectory,
            serverProjectDirectory,
            artifactsDirectory.Combine("server"),
            artifactsDirectory.Combine("server-tests"),
            serverTestResultsDirectory,
            rootDirectory.CombineWithFilePath("GitVersion.yml"),
            rootDirectory.Combine(".githooks").CombineWithFilePath("commit-msg"),
            serverDirectory.CombineWithFilePath("FigmaMcp.slnx"),
            serverProjectDirectory.CombineWithFilePath("FigmaMCP.csproj"),
            serverTestProjectDirectory.CombineWithFilePath("FigmaMCP.Tests.Unit.csproj"),
            serverTestResultsDirectory.CombineWithFilePath("server.trx"),
            serverTestResultsDirectory.CombineWithFilePath("coverage.cobertura.xml")
        );
    }

    public DirectoryPath GetServerBuildDirectory(string configuration) =>
        ServerProjectDirectory.Combine("bin").Combine(configuration).Combine("net10.0");

    public FilePath GetServerAssembly(string configuration) =>
        GetServerBuildDirectory(configuration).CombineWithFilePath("FigmaMCP.dll");

    public DirectoryPath GetServerPublishDirectory(string runtime) =>
        ServerPublishRootDirectory.Combine(runtime);

    public DirectoryPath GetServerTestPublishDirectory(string runtime) =>
        ServerTestPublishRootDirectory.Combine(runtime);

    public FilePath GetNuGetPackage(string version) =>
        ReleaseDirectory.CombineWithFilePath($"FigmaMCP.{version}.nupkg");

    public FilePath GetNuGetSymbolsPackage(string version) =>
        ReleaseDirectory.CombineWithFilePath($"FigmaMCP.{version}.snupkg");

    public FilePath GetDownloadedNuGetPackage(string version) =>
        ReleaseDownloadDirectory.CombineWithFilePath($"FigmaMCP.{version}.nupkg");

    public FilePath GetDownloadedNuGetSymbolsPackage(string version) =>
        ReleaseDownloadDirectory.CombineWithFilePath($"FigmaMCP.{version}.snupkg");

    public FilePath GetServerReleaseArchive(string runtime, string version) =>
        ReleaseDirectory.CombineWithFilePath($"figma-mcp-server-{runtime}.{version}.zip");

    public FilePath GetPluginReleaseArchive(string version) =>
        ReleaseDirectory.CombineWithFilePath($"figma-mcp-plugin.{version}.zip");
}
