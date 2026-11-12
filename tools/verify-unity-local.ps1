# Execute real Unity Editor checks/build on this Windows PC, never on GitHub Actions.
param(
    [string]$UnityExe = "",
    [switch]$BuildWindows
)
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$project = Join-Path $root "Syntera-Unity"
if (-not (Test-Path (Join-Path $project "ProjectSettings\ProjectVersion.txt"))) {
    throw "Unity project not found at $project"
}
if (-not $UnityExe) {
    $required = ((Get-Content (Join-Path $project "ProjectSettings\ProjectVersion.txt") | Select-String "m_EditorVersion:") -split ":\s*")[1].Trim()
    $direct = @("D:\Unity\$required\Editor\Unity.exe", "C:\Program Files\Unity\Hub\Editor\$required\Editor\Unity.exe", "C:\Program Files\Unity $required\Editor\Unity.exe")
    foreach ($binary in $direct) { if (Test-Path $binary) { $UnityExe = $binary; break } }
}
if (-not $UnityExe) {
    $editorDir = "C:\Program Files\Unity\Hub\Editor"
    if (Test-Path $editorDir) {
        $candidates = @(Get-ChildItem $editorDir -Directory | Sort-Object Name -Descending)
        foreach ($candidate in $candidates) {
            $binary = Join-Path $candidate.FullName "Editor\Unity.exe"
            if (Test-Path $binary) { $UnityExe = $binary; break }
        }
    }
}
if (-not $UnityExe -or -not (Test-Path $UnityExe)) {
    throw "Unity Editor executable not located. Install Unity 6.3 with Unity Hub, or pass -UnityExe 'C:\...\Unity.exe'."
}
$logs = Join-Path $root "local-unity-logs"
New-Item -ItemType Directory -Force -Path $logs | Out-Null
$logPath = Join-Path $logs "syntera-editor-validation.log"
Write-Host "Running local Unity Editor verification..."
& $UnityExe -batchmode -nographics -quit -projectPath $project -executeMethod Syntera.Editor.SynteraReleaseGate.Run -logFile $logPath
if ($LASTEXITCODE -ne 0) { throw "Unity verification failed ($LASTEXITCODE). Inspect $logPath" }
if (-not (Select-String -Path $logPath -Pattern "\[SYNTERA UNITY VALIDATION\] PASS" -Quiet)) {
    throw "Unity verification did not record its PASS marker. Inspect $logPath"
}
Write-Host "Unity validation passed. Logs: $logPath"

# EditMode tests: rules core, section 14 simulation parity, audit chain. -runTests must not be combined with -quit.
$testLog = Join-Path $logs "syntera-editmode-tests.log"
$testResults = Join-Path $logs "syntera-editmode-results.xml"
& $UnityExe -batchmode -nographics -projectPath $project -runTests -testPlatform EditMode -testResults $testResults -logFile $testLog
if ($LASTEXITCODE -ne 0) { throw "EditMode tests failed ($LASTEXITCODE). Inspect $testResults and $testLog" }
if (-not (Test-Path $testResults)) { throw "Unity did not write test results: $testResults" }
[xml]$results = Get-Content $testResults
$run = $results.'test-run'
Write-Host "EditMode tests: $($run.passed) passed, $($run.failed) failed, $($run.skipped) skipped (total $($run.total))"
if ([int]$run.failed -gt 0 -or [int]$run.total -eq 0) { throw "EditMode tests did not all pass. Inspect $testResults" }
if ($BuildWindows) {
    $buildLog = Join-Path $logs "syntera-windows-build.log"
    & $UnityExe -batchmode -nographics -quit -projectPath $project -executeMethod Syntera.Editor.SynteraEditorTools.BatchBuildWindows -logFile $buildLog
    if ($LASTEXITCODE -ne 0) { throw "Windows build failed ($LASTEXITCODE). Inspect $buildLog" }
    $exe = Join-Path $project "Builds\Windows\SYNTERA.exe"
    if (-not (Test-Path $exe)) { throw "Unity returned successfully but the executable is missing: $exe" }
    Write-Host "Build produced: $exe"
    Write-Host "You must still launch the executable and manually test the campaign."
}
