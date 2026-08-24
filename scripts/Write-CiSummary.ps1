[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ServerResultsPath,

    [Parameter(Mandatory)]
    [string] $PluginResultsPath,

    [string] $OutputPath = $env:GITHUB_STEP_SUMMARY
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-FirstReportFile {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $Filter
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        return $null
    }

    return Get-ChildItem -LiteralPath $Path -Recurse -File -Filter $Filter | Select-Object -First 1
}

function Format-CoverageValue {
    param(
        [long] $Covered,
        [long] $Total
    )

    if ($Total -eq 0) {
        return "n/a"
    }

    $percentage = 100.0 * $Covered / $Total
    return [string]::Format(
        [Globalization.CultureInfo]::InvariantCulture,
        "{0} / {1} ({2:0.00}%)",
        $Covered,
        $Total,
        $percentage
    )
}

function Get-LcovTotal {
    param(
        [Parameter(Mandatory)]
        [string[]] $Content,

        [Parameter(Mandatory)]
        [string] $Prefix
    )

    $total = 0L
    foreach ($line in $Content) {
        if ($line -match "^$([regex]::Escape($Prefix)):(?<value>\d+)$") {
            $total += [long] $Matches.value
        }
    }

    return $total
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    throw "OutputPath is required when GITHUB_STEP_SUMMARY is not set."
}

$testRows = [Collections.Generic.List[string]]::new()
$coverageRows = [Collections.Generic.List[string]]::new()
$notes = [Collections.Generic.List[string]]::new()

$serverTrx = Get-FirstReportFile -Path $ServerResultsPath -Filter "*.trx"
if ($null -ne $serverTrx) {
    [xml] $trx = Get-Content -Raw -Encoding UTF8 -LiteralPath $serverTrx.FullName
    $counters = $trx.TestRun.ResultSummary.Counters
    $total = [int] $counters.total
    $passed = [int] $counters.passed
    $failed = [int] $counters.failed + [int] $counters.error + [int] $counters.timeout + [int] $counters.aborted
    $skipped = [int] $counters.notExecuted + [int] $counters.inconclusive
    $testRows.Add("| Server | $total | $passed | $failed | $skipped |")
} else {
    $testRows.Add("| Server | n/a | n/a | n/a | n/a |")
    $notes.Add("Server TRX report was not available.")
}

$pluginJunit = Get-FirstReportFile -Path $PluginResultsPath -Filter "*.xml"
if ($null -ne $pluginJunit) {
    [xml] $junit = Get-Content -Raw -Encoding UTF8 -LiteralPath $pluginJunit.FullName
    $testCases = @($junit.testsuites.testcase)
    $failed = @($testCases | Where-Object { $null -ne $_.SelectSingleNode("failure") -or $null -ne $_.SelectSingleNode("error") }).Count
    $skipped = @($testCases | Where-Object { $null -ne $_.SelectSingleNode("skipped") }).Count
    $passed = $testCases.Count - $failed - $skipped
    $testRows.Add("| Plugin | $($testCases.Count) | $passed | $failed | $skipped |")
} else {
    $testRows.Add("| Plugin | n/a | n/a | n/a | n/a |")
    $notes.Add("Plugin JUnit report was not available.")
}

$serverCoverage = Get-FirstReportFile -Path $ServerResultsPath -Filter "*.cobertura.xml"
if ($null -ne $serverCoverage) {
    [xml] $cobertura = Get-Content -Raw -Encoding UTF8 -LiteralPath $serverCoverage.FullName
    $coverage = $cobertura.coverage
    $lines = Format-CoverageValue -Covered ([long] $coverage."lines-covered") -Total ([long] $coverage."lines-valid")
    $branches = Format-CoverageValue -Covered ([long] $coverage."branches-covered") -Total ([long] $coverage."branches-valid")
    $coverageRows.Add("| Server | $lines | $branches | n/a |")
} else {
    $coverageRows.Add("| Server | n/a | n/a | n/a |")
    $notes.Add("Server Cobertura report was not available.")
}

$pluginCoverage = Get-FirstReportFile -Path $PluginResultsPath -Filter "*.info"
if ($null -ne $pluginCoverage) {
    $lcov = Get-Content -Encoding UTF8 -LiteralPath $pluginCoverage.FullName
    $lines = Format-CoverageValue -Covered (Get-LcovTotal -Content $lcov -Prefix "LH") -Total (Get-LcovTotal -Content $lcov -Prefix "LF")
    $branches = Format-CoverageValue -Covered (Get-LcovTotal -Content $lcov -Prefix "BRH") -Total (Get-LcovTotal -Content $lcov -Prefix "BRF")
    $functions = Format-CoverageValue -Covered (Get-LcovTotal -Content $lcov -Prefix "FNH") -Total (Get-LcovTotal -Content $lcov -Prefix "FNF")
    $coverageRows.Add("| Plugin | $lines | $branches | $functions |")
} else {
    $coverageRows.Add("| Plugin | n/a | n/a | n/a |")
    $notes.Add("Plugin LCOV report was not available.")
}

$summary = [Collections.Generic.List[string]]::new()
$summary.Add("# Test and coverage results")
$summary.Add("")
$summary.Add("## Tests")
$summary.Add("")
$summary.Add("| Component | Total | Passed | Failed | Skipped |")
$summary.Add("| --- | ---: | ---: | ---: | ---: |")
$summary.AddRange($testRows)
$summary.Add("")
$summary.Add("## Coverage")
$summary.Add("")
$summary.Add("| Component | Lines | Branches | Functions |")
$summary.Add("| --- | ---: | ---: | ---: |")
$summary.AddRange($coverageRows)

if ($notes.Count -gt 0) {
    $summary.Add("")
    $summary.Add("## Notes")
    $summary.Add("")
    foreach ($note in $notes) {
        $summary.Add("- $note")
    }
}

Add-Content -Encoding UTF8 -LiteralPath $OutputPath -Value $summary
