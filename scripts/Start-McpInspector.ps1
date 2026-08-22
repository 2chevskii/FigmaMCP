[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

foreach ($command in @("dotnet", "npx")) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "'$command' was not found on PATH. Install it before running the MCP Inspector."
    }
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot "packages\server\FigmaMcp.slnx"
$serverAssembly = Join-Path $repositoryRoot "packages\server\src\FigmaMcp.Server\bin\$Configuration\net10.0\figma-mcp-server.dll"

if (-not $NoBuild) {
    & dotnet build $solutionPath --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

if (-not (Test-Path -LiteralPath $serverAssembly -PathType Leaf)) {
    throw "Server assembly was not found: $serverAssembly. Run without -NoBuild first."
}

& npx --yes @modelcontextprotocol/inspector --cwd $repositoryRoot dotnet $serverAssembly
exit $LASTEXITCODE
