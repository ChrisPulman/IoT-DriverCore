# Cobertura coverage verifier

`Verify-CoberturaCoverage.ps1` aggregates fresh, explicitly selected Cobertura reports for the entire repository. It is intentionally independent of test projects and build props.

Run the tests with coverage first, capture a UTC timestamp immediately before that run, and then pass either the exact reports or the result directories that contain them:

```powershell
$runStarted = [DateTime]::UtcNow
# run all required TUnit/MTP coverage commands here
./src/eng/Verify-CoberturaCoverage.ps1 \
  -GeneratedAfter $runStarted \
  -ReportDirectory ./src \
  -Recurse \
  -ExpectedModule CP.IoT.Core,S7PlcRx,MockS7Plc \
  -Threshold 95
```

For CI, prefer fully explicit files:

```powershell
./src/eng/Verify-CoberturaCoverage.ps1 \
  -GeneratedAfter '2026-07-23T12:00:00Z' \
  -ReportPath ./src/S7PlcRx.Tests/bin/Release/net10.0/TestResults/coverage.cobertura.xml \
  -ExpectedModule S7PlcRx,MockS7Plc
```

The verifier only counts files that resolve under this checkout's `src` directory. Test-project source and third-party source are excluded. Generated source within a first-party production module is deliberately included. Source lines are keyed by normalized file path and line number, so a source file compiled by both regular and reactive/shared projects counts once. The script sums observed hits for that one line and uses the largest observed Cobertura branch count for that one source line because Cobertura does not expose stable branch identities across reports.

Reports are rejected when missing, malformed, stale relative to `-GeneratedAfter`, empty of production lines, or when they contain legacy/unresolved source paths. Both whole-repository line and branch coverage must meet `-Threshold` (95% by default); failures return a non-zero exit code.

Run the verifier regression checks with `pwsh ./src/eng/Test-Verify-CoberturaCoverage.ps1`. They cover a Cobertura line without optional `condition-coverage`, stale-report rejection, and unresolved legacy-path rejection.
