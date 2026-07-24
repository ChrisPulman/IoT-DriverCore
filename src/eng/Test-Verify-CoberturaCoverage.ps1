[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$engPath = $PSScriptRoot
$repositoryRoot = (Resolve-Path (Join-Path $engPath '..\..')).Path
$verifier = Join-Path $engPath 'Verify-CoberturaCoverage.ps1'
$fixturePath = Join-Path $engPath 'TestData\line-without-condition-coverage.cobertura.xml'
$legacyFixturePath = Join-Path $engPath 'TestData\unresolved-legacy-path.cobertura.xml'
$staleReportPath = Join-Path $repositoryRoot 'src\S7PlcRx.Tests\coverage.cobertura.xml'

function Invoke-Verifier([string[]] $Arguments, [int] $ExpectedExitCode, [string] $Scenario) {
    $output = & pwsh -NoProfile -File $verifier @Arguments 2>&1
    if ($LASTEXITCODE -ne $ExpectedExitCode) {
        throw "$Scenario returned exit code $LASTEXITCODE; expected $ExpectedExitCode. Output: $($output -join [Environment]::NewLine)"
    }

    return $output
}

$normalOutput = Invoke-Verifier -Arguments @('-ReportPath', $fixturePath, '-GeneratedAfter', '2000-01-01T00:00:00Z', '-ExpectedModule', 'S7PlcRx') -ExpectedExitCode 0 -Scenario 'Optional condition-coverage aggregation'
if (($normalOutput -join [Environment]::NewLine) -notmatch 'Whole repository: line 100\.00%') {
    throw 'Optional condition-coverage aggregation did not produce the expected whole-repository summary.'
}

$unresolvedOutput = Invoke-Verifier -Arguments @('-ReportPath', $legacyFixturePath, '-GeneratedAfter', '2000-01-01T00:00:00Z') -ExpectedExitCode 2 -Scenario 'Unresolved legacy path rejection'
if (($unresolvedOutput -join [Environment]::NewLine) -notmatch 'unresolved first-party/legacy source paths') {
    throw 'Unresolved legacy source path did not report the expected validation failure.'
}

$staleOutput = Invoke-Verifier -Arguments @('-ReportPath', $staleReportPath, '-GeneratedAfter', ([DateTime]::UtcNow.ToString('o'))) -ExpectedExitCode 2 -Scenario 'Stale report rejection'
if (($staleOutput -join [Environment]::NewLine) -notmatch 'Stale coverage report') {
    throw 'Stale report did not report the expected validation failure.'
}

Write-Host 'Coverage verifier regression checks passed: optional branch metadata, unresolved path rejection, and stale report rejection.'
