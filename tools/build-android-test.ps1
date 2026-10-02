param([string]$Unity = 'C:/Program Files/Unity/Hub/Editor/6000.5.9f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path -LiteralPath $Unity)) { throw 'Install the pinned Unity 6000.5.9f1 Editor and Android Build Support first.' }
# Fresh asset import and IL2CPP linking require substantial workspace and temp space.
# This conservative preflight is not a peak-usage guarantee; never delete files automatically.
$requiredFreeBytes = 20GB
$buildVolumes = @($repo, [IO.Path]::GetTempPath()) | ForEach-Object { [IO.Path]::GetPathRoot($_) } | Select-Object -Unique
foreach ($volume in $buildVolumes) {
    $drive = [IO.DriveInfo]::new($volume)
    if ($drive.AvailableFreeSpace -lt $requiredFreeBytes) {
        throw "Insufficient free space on $volume. At least 20 GiB is required before an isolated build; no files were copied or deleted."
    }
}
$run = Join-Path $repo ('artifacts/android-test/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$project = Join-Path $run 'project'
$output = Join-Path $run 'output'
if (Test-Path -LiteralPath $run) { throw 'Build run already exists; no files overwritten.' }
New-Item -ItemType Directory -Path $project,$output | Out-Null
foreach ($folder in @('Assets','Packages','ProjectSettings')) {
    Copy-Item -LiteralPath (Join-Path $repo "apps/unity/$folder") -Destination $project -Recurse
}
# No Library, UserSettings, layout, private environment, or signing credentials copied.
& python (Join-Path $PSScriptRoot 'verify-build-snapshot.py') --source (Join-Path $repo 'apps/unity') --snapshot $project --output (Join-Path $output 'source-snapshot.json')
if ($LASTEXITCODE -ne 0) { throw 'Source snapshot verification failed; Unity was not launched. Inspect source-snapshot.json.' }
$previousOutput = $env:COMPANION_ANDROID_OUTPUT
$env:COMPANION_ANDROID_OUTPUT = $output
$log = Join-Path $run 'unity-build.log'
Write-Output "Build source copy: $project"
Write-Output "Output: $output"
try {
    $args = @('-batchmode','-nographics','-quit','-projectPath',('"' + $project + '"'),'-buildTarget','Android','-executeMethod','Companion.Editor.AndroidDiagnosticBuild.Run','-logFile',('"' + $log + '"'))
    $process = Start-Process -FilePath $Unity -ArgumentList $args -WindowStyle Hidden -PassThru
    # Wait only for Unity: Gradle may leave a reusable daemon after a successful build.
    # Start-Process -Wait also waits for descendants, delaying artifact verification.
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Unity failed (exit $($process.ExitCode)). Inspect $log" }
    $apk = Join-Path $output 'Companion-CC-Test.apk'
    if (-not (Test-Path -LiteralPath $apk)) { throw "Unity exited without an APK. Inspect $log" }
    Get-FileHash -LiteralPath $apk -Algorithm SHA256 | Format-List
    Get-Content -LiteralPath (Join-Path $output 'build-result.txt')
    & (Join-Path $PSScriptRoot 'verify-android-apk.ps1') -Apk $apk
} finally { $env:COMPANION_ANDROID_OUTPUT = $previousOutput }
