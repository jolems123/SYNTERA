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
if ($BuildWindows) {
    $buildLog = Join-Path $logs "syntera-windows-build.log"
    & $UnityExe -batchmode -nographics -quit -projectPath $project -executeMethod Syntera.Editor.SynteraEditorTools.BatchBuildWindows -logFile $buildLog
    if ($LASTEXITCODE -ne 0) { throw "Windows build failed ($LASTEXITCODE). Inspect $buildLog" }
    $exe = Join-Path $project "Builds\Windows\SYNTERA.exe"
    if (-not (Test-Path $exe)) { throw "Unity returned successfully but the executable is missing: $exe" }
    Write-Host "Build produced: $exe"
    Write-Host "You must still launch the executable and manually test the campaign."
}
