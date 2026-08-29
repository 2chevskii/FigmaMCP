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
    DirectoryPath ServerPublishDirectory,
    DirectoryPath ServerTestResultsDirectory,
    FilePath ServerSolution,
    FilePath ServerProject,
    FilePath ServerTestReport,
    FilePath ServerCoverageReport,
    FilePath ServerReleaseArchive,
    FilePath PluginReleaseArchive
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
            artifactsDirectory.Combine("server").Combine("win-x64"),
            serverTestResultsDirectory,
            serverDirectory.CombineWithFilePath("FigmaMcp.slnx"),
            serverProjectDirectory.CombineWithFilePath("FigmaMCP.csproj"),
            serverTestResultsDirectory.CombineWithFilePath("server.trx"),
            serverTestResultsDirectory.CombineWithFilePath("coverage.cobertura.xml"),
            releaseDirectory.CombineWithFilePath("figma-mcp-server-win-x64.zip"),
            releaseDirectory.CombineWithFilePath("figma-mcp-plugin.zip")
        );
    }

    public DirectoryPath GetServerBuildDirectory(string configuration) =>
        ServerProjectDirectory.Combine("bin").Combine(configuration).Combine("net10.0");

    public FilePath GetServerAssembly(string configuration) =>
        GetServerBuildDirectory(configuration).CombineWithFilePath("figma-mcp-server.dll");

    public FilePath GetNuGetPackage(string version) =>
        ReleaseDirectory.CombineWithFilePath($"FigmaMCP.{version}.nupkg");

    public FilePath GetDownloadedNuGetPackage(string version) =>
        ReleaseDownloadDirectory.CombineWithFilePath($"FigmaMCP.{version}.nupkg");
}
