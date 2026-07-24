[CmdletBinding()]
param(
    # One or more explicitly selected Cobertura XML reports. Wildcards are allowed.
    [string[]] $ReportPath = @(),

    # Explicit result directories to scan for Cobertura XML reports. Nothing is discovered from git.
    [string[]] $ReportDirectory = @(),

    # Include nested TestResults directories when -ReportDirectory is supplied.
    [switch] $Recurse,

    # UTC timestamp immediately before the coverage test run started.
    [Parameter(Mandatory)]
    [datetime] $GeneratedAfter,

    [ValidateRange(0, 100)]
    [double] $Threshold = 95,

    [string] $RepositoryRoot = (Join-Path $PSScriptRoot '..\..'),

    # Production modules that must have at least one included source line in the supplied reports.
    [string[]] $ExpectedModule = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Fail([string] $Message) {
    [Console]::Error.WriteLine($Message)
    exit 2
}

function Get-ReportFiles {
    $files = [System.Collections.Generic.List[System.IO.FileInfo]]::new()

    foreach ($path in $ReportPath) {
        $matches = @(Get-ChildItem -Path $path -File -ErrorAction SilentlyContinue)
        if ($matches.Count -eq 0) {
            Fail "Coverage report was not found: $path"
        }

        foreach ($match in $matches) {
            $files.Add($match)
        }
    }

    foreach ($directory in $ReportDirectory) {
        if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
            Fail "Coverage report directory was not found: $directory"
        }

        $searchOptions = if ($Recurse) { @{ Recurse = $true } } else { @{} }
        $matches = @(Get-ChildItem -LiteralPath $directory -File @searchOptions |
                Where-Object { $_.Name -match '(?i)(cobertura|coverage).*\\.xml$' })
        if ($matches.Count -eq 0) {
            Fail "No Cobertura-style XML reports were found in explicitly selected directory: $directory"
        }

        foreach ($match in $matches) {
            $files.Add($match)
        }
    }

    if ($files.Count -eq 0) {
        Fail 'Provide -ReportPath and/or -ReportDirectory. Coverage reports are never inferred from git state.'
    }

    return @($files | Sort-Object FullName -Unique)
}

function Get-SourcePath([string] $Filename, [string] $ReportFile, [string] $RepositoryPath, [string] $SourcePath) {
    if ([string]::IsNullOrWhiteSpace($Filename)) {
        return [pscustomobject] @{ Kind = 'External'; Path = $null; Message = 'empty filename' }
    }

    $candidate = if ([System.IO.Path]::IsPathRooted($Filename)) {
        [System.IO.Path]::GetFullPath($Filename)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $ReportFile) $Filename))
    }

    if ($candidate.StartsWith($SourcePath, [System.StringComparison]::OrdinalIgnoreCase)) {
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            return [pscustomobject] @{ Kind = 'Unresolved'; Path = $candidate; Message = 'source path is under this workspace but does not exist' }
        }

        return [pscustomobject] @{ Kind = 'FirstParty'; Path = $candidate; Message = $null }
    }

    # A reported source path from another checkout is ambiguous: never rewrite it to this checkout.
    if ($Filename -match '(?i)(^|[\\/])src([\\/]|$)') {
        return [pscustomobject] @{ Kind = 'Unresolved'; Path = $candidate; Message = 'source path resolves outside this workspace (legacy checkout paths are rejected)' }
    }

    return [pscustomobject] @{ Kind = 'External'; Path = $candidate; Message = 'outside repository source root' }
}

function Get-ModuleName([string] $SourceFile, [string] $SourcePath) {
    $relative = $SourceFile.Substring($SourcePath.Length).TrimStart('\', '/')
    $parts = $relative -split '[\\/]'
    if ($parts.Length -lt 2) {
        return '<root-source>'
    }

    return $parts[0]
}

function Test-TestSource([string] $SourceFile, [string] $SourcePath) {
    $relative = $SourceFile.Substring($SourcePath.Length).TrimStart('\', '/')
    return $relative -match '(?i)(^|[\\/])[^\\/]*(?:\\.tests?|integrationtests|benchmarks?|testapp|testconsole|drivertest|testing)(?:[\\/]|$)'
}

function Get-BranchCounts($Line) {
    # condition-coverage is optional in Cobertura. GetAttribute returns an empty string when it is absent,
    # which is safe under StrictMode and correctly represents a line with no branch denominator.
    $coverage = [string] $Line.GetAttribute('condition-coverage')
    if ($coverage -match '\((\d+)\/(\d+)\)') {
        return [pscustomobject] @{ Covered = [int] $Matches[1]; Valid = [int] $Matches[2] }
    }

    return [pscustomobject] @{ Covered = 0; Valid = 0 }
}

$repositoryPath = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\', '/')
$sourcePath = Join-Path $repositoryPath 'src'
if (-not (Test-Path -LiteralPath $sourcePath -PathType Container)) {
    Fail "Repository source directory was not found: $sourcePath"
}

$generatedAfterUtc = $GeneratedAfter.ToUniversalTime()
$reports = @(Get-ReportFiles)
$lineRecords = @{}
$reportModules = @{}
$unresolvedPaths = [System.Collections.Generic.List[string]]::new()
$reportsWithoutProductionLines = [System.Collections.Generic.List[string]]::new()
$externalEntries = 0
$testEntries = 0

foreach ($report in $reports) {
    if ($report.LastWriteTimeUtc -lt $generatedAfterUtc) {
        Fail "Stale coverage report: $($report.FullName) was written $($report.LastWriteTimeUtc.ToString('o')); expected >= $($generatedAfterUtc.ToString('o'))."
    }

    try {
        [xml] $document = Get-Content -LiteralPath $report.FullName -Raw
    }
    catch {
        Fail "Coverage report is not valid XML: $($report.FullName). $($_.Exception.Message)"
    }

    if ($null -eq $document.coverage -or $null -eq $document.coverage.packages) {
        Fail "Coverage report is not Cobertura XML: $($report.FullName)"
    }

    $reportProducedLines = 0
    foreach ($package in @($document.coverage.packages.package)) {
        foreach ($class in @($package.classes.class)) {
            $source = Get-SourcePath -Filename ([string] $class.filename) -ReportFile $report.FullName -RepositoryPath $repositoryPath -SourcePath $sourcePath
            if ($source.Kind -eq 'Unresolved') {
                $unresolvedPaths.Add("$($report.FullName): $($class.filename) ($($source.Message))")
                continue
            }
            if ($source.Kind -ne 'FirstParty') {
                $externalEntries++
                continue
            }
            if (Test-TestSource -SourceFile $source.Path -SourcePath $sourcePath) {
                $testEntries++
                continue
            }

            $module = Get-ModuleName -SourceFile $source.Path -SourcePath $sourcePath
            foreach ($line in @($class.lines.line)) {
                $lineNumber = [int] $line.number
                if ($lineNumber -lt 1) {
                    Fail "Invalid line number in $($report.FullName): $lineNumber"
                }

                $reportModules[$module] = $true

                $key = '{0}|{1}' -f $source.Path.ToUpperInvariant(), $lineNumber
                $branches = Get-BranchCounts $line
                if (-not $lineRecords.ContainsKey($key)) {
                    $lineRecords[$key] = [pscustomobject] @{
                        File = $source.Path
                        Line = $lineNumber
                        Module = $module
                        Hits = [long] 0
                        BranchCovered = 0
                        BranchValid = 0
                        Reports = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
                    }
                }

                $record = $lineRecords[$key]
                # A duplicated shared/reactive compilation represents one source line. Keep one denominator row,
                # but retain every observed hit so the result is transparent and no execution evidence is lost.
                $record.Hits += [long] $line.hits
                # Cobertura does not provide stable condition identities. The best complete observation for an
                # identically compiled source line is used, avoiding duplicate branch denominators.
                $record.BranchCovered = [Math]::Max($record.BranchCovered, $branches.Covered)
                $record.BranchValid = [Math]::Max($record.BranchValid, $branches.Valid)
                [void] $record.Reports.Add($report.FullName)
                $reportProducedLines++
            }
        }
    }

    if ($reportProducedLines -eq 0) {
        $reportsWithoutProductionLines.Add($report.FullName)
    }
}

if ($unresolvedPaths.Count -gt 0) {
    $details = ($unresolvedPaths | Select-Object -First 10) -join [Environment]::NewLine
    Fail "Coverage report contains unresolved first-party/legacy source paths. Regenerate it from this checkout; paths are never rebased.`n$details"
}

if ($reportsWithoutProductionLines.Count -gt 0) {
    Fail "Coverage report contains no first-party production source lines: $($reportsWithoutProductionLines -join ', ')"
}

foreach ($module in $ExpectedModule) {
    if (-not $reportModules.ContainsKey($module)) {
        Fail "Missing coverage for expected production module '$module'. Supply its fresh Cobertura report explicitly."
    }
}

$records = @($lineRecords.Values)
if ($records.Count -eq 0) {
    Fail 'No first-party production source lines remained after excluding tests and external packages.'
}

function Get-CoverageSummary($Name, $Items) {
    $lineValid = @($Items).Count
    $lineCovered = @($Items | Where-Object { $_.Hits -gt 0 }).Count
    $branchValid = [int64] (@($Items | Measure-Object -Property BranchValid -Sum).Sum)
    $branchCovered = [int64] (@($Items | Measure-Object -Property BranchCovered -Sum).Sum)
    [pscustomobject] @{
        Module = $Name
        LinesCovered = $lineCovered
        LinesValid = $lineValid
        LinePercent = if ($lineValid) { 100.0 * $lineCovered / $lineValid } else { 0.0 }
        BranchesCovered = $branchCovered
        BranchesValid = $branchValid
        BranchPercent = if ($branchValid) { 100.0 * $branchCovered / $branchValid } else { 100.0 }
    }
}

$moduleSummaries = @($records | Group-Object Module | ForEach-Object { Get-CoverageSummary $_.Name $_.Group } | Sort-Object Module)
$wholeRepository = Get-CoverageSummary '<whole-repository>' $records

Write-Host "Validated fresh Cobertura reports: $($reports.Count) (generated after $($generatedAfterUtc.ToString('o')))"
Write-Host "Excluded test entries: $testEntries; external-package entries: $externalEntries; normalized production source lines: $($records.Count)"
Write-Host ''
Write-Host 'Per-module coverage (first-party production source only):'
$moduleSummaries | Format-Table Module, LinesCovered, LinesValid, @{ Label = 'Line %'; Expression = { '{0:N2}' -f $_.LinePercent } }, BranchesCovered, BranchesValid, @{ Label = 'Branch %'; Expression = { '{0:N2}' -f $_.BranchPercent } } -AutoSize
Write-Host ('Whole repository: line {0:N2}% ({1}/{2}), branch {3:N2}% ({4}/{5})' -f $wholeRepository.LinePercent, $wholeRepository.LinesCovered, $wholeRepository.LinesValid, $wholeRepository.BranchPercent, $wholeRepository.BranchesCovered, $wholeRepository.BranchesValid)

if ($wholeRepository.LinePercent -lt $Threshold -or $wholeRepository.BranchPercent -lt $Threshold) {
    Write-Error ('Coverage threshold failed: required {0:N2}% line and branch; actual line {1:N2}%, branch {2:N2}%.' -f $Threshold, $wholeRepository.LinePercent, $wholeRepository.BranchPercent)
    exit 1
}

Write-Host ('Coverage threshold passed: {0:N2}% line and branch minimum.' -f $Threshold)
