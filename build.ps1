[CmdletBinding(PositionalBinding = $false)]
param(
  [Parameter(ValueFromRemainingArguments = $true)]
  [string[]] $CakeArguments
)

$ErrorActionPreference = 'Stop'

Push-Location $PSScriptRoot
try {
  & dotnet ./build/build.cs -- @CakeArguments
  $exitCode = $LASTEXITCODE
}
finally {
  Pop-Location
}

exit $exitCode
