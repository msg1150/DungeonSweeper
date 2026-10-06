param([string]$UnityEditorPath, [switch]$CheckPlayerProtection, [switch]$CaptureScreens)

$ErrorActionPreference = 'Stop'
$taskProjectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $UnityEditorPath) {
    $taskVersion = [regex]::Match((Get-Content -LiteralPath (Join-Path $taskProjectRoot 'ProjectSettings/ProjectVersion.txt') -Raw), 'm_EditorVersion: (\S+)').Groups[1].Value
    $UnityEditorPath = Join-Path ${env:ProgramFiles} "Unity/Hub/Editor/$taskVersion/Editor/Unity.exe"
}
if (-not (Test-Path -LiteralPath $UnityEditorPath)) { throw 'Unity editor executable was not found. Pass -UnityEditorPath.' }

$taskValidationRoot = Join-Path $taskProjectRoot '.utmp/UnityValidation'
New-Item -ItemType Directory -Path "$taskValidationRoot/Assets/Editor", "$taskValidationRoot/ProjectSettings", "$taskValidationRoot/Packages" -Force | Out-Null
# Remove only the obsolete prefab copy left by earlier validations; preserve saves and profiling results.
$taskOldPrefabRoot = [IO.Path]::GetFullPath((Join-Path $taskValidationRoot 'Assets/Resources/Prefabs'))
if (-not $taskOldPrefabRoot.StartsWith(([IO.Path]::GetFullPath($taskValidationRoot) + [IO.Path]::DirectorySeparatorChar), [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid isolated prefab cleanup path.' }
if (Test-Path -LiteralPath $taskOldPrefabRoot) { Remove-Item -LiteralPath $taskOldPrefabRoot -Recurse -Force }
if (Test-Path -LiteralPath ($taskOldPrefabRoot + '.meta')) { Remove-Item -LiteralPath ($taskOldPrefabRoot + '.meta') -Force }
foreach ($taskFolder in @('Scripts', 'Resources', 'Prefabs', 'Scenes', 'Settings', 'Editor')) {
    Copy-Item -LiteralPath (Join-Path $taskProjectRoot "Assets/$taskFolder") -Destination "$taskValidationRoot/Assets" -Recurse -Force
    $taskFolderMeta = Join-Path $taskProjectRoot "Assets/$taskFolder.meta"
    if (Test-Path -LiteralPath $taskFolderMeta) { Copy-Item -LiteralPath $taskFolderMeta -Destination "$taskValidationRoot/Assets" -Force }
}
Copy-Item -LiteralPath (Join-Path $taskProjectRoot 'ProjectSettings') -Destination $taskValidationRoot -Recurse -Force
Copy-Item -LiteralPath (Join-Path $taskProjectRoot 'Packages/manifest.json'), (Join-Path $taskProjectRoot 'Packages/packages-lock.json') -Destination "$taskValidationRoot/Packages" -Force
Copy-Item -LiteralPath (Join-Path $taskProjectRoot 'Tests/GameFlowSmokeChecks.cs') -Destination "$taskValidationRoot/Assets/Editor/GameFlowSmokeChecks.cs" -Force
Copy-Item -LiteralPath (Join-Path $taskProjectRoot 'Tests/ValidationRuntimeDriver.cs') -Destination "$taskValidationRoot/Assets/Scripts/Demo/ValidationRuntimeDriver.cs" -Force
Copy-Item -LiteralPath (Join-Path $taskProjectRoot 'Tests/SaveProtectionChecks.cs') -Destination "$taskValidationRoot/Assets/Scripts/Demo/SaveProtectionChecks.cs" -Force
Copy-Item -LiteralPath (Join-Path $taskProjectRoot 'Tests/ReleaseLogicChecks.cs') -Destination "$taskValidationRoot/Assets/Scripts/Demo/ReleaseLogicChecks.cs" -Force
Copy-Item -LiteralPath (Join-Path $taskProjectRoot 'Tests/TownEconomyChecks.cs') -Destination "$taskValidationRoot/Assets/Scripts/Demo/TownEconomyChecks.cs" -Force
Copy-Item -LiteralPath (Join-Path $taskProjectRoot 'Tests/FocusPauseChecks.cs') -Destination "$taskValidationRoot/Assets/Scripts/Demo/FocusPauseChecks.cs" -Force
Copy-Item -LiteralPath (Join-Path $taskProjectRoot 'Tests/DungeonPopulationChecks.cs') -Destination "$taskValidationRoot/Assets/Scripts/Demo/DungeonPopulationChecks.cs" -Force
Copy-Item -LiteralPath (Join-Path $taskProjectRoot 'Tests/DungeonPrefabChecks.cs') -Destination "$taskValidationRoot/Assets/Scripts/Demo/DungeonPrefabChecks.cs" -Force
Copy-Item -LiteralPath (Join-Path $taskProjectRoot 'Tests/ContinueEntryChecks.cs') -Destination "$taskValidationRoot/Assets/Scripts/Demo/ContinueEntryChecks.cs" -Force
Copy-Item -LiteralPath (Join-Path $taskProjectRoot 'Tests/DungeonPoolChecks.cs') -Destination "$taskValidationRoot/Assets/Scripts/Demo/DungeonPoolChecks.cs" -Force
Copy-Item -LiteralPath (Join-Path $taskProjectRoot 'Tests/PlayerProtectionValidationBuild.cs') -Destination "$taskValidationRoot/Assets/Editor/PlayerProtectionValidationBuild.cs" -Force
Copy-Item -LiteralPath (Join-Path $taskProjectRoot 'Tests/PlayerProtectionValidationDriver.cs') -Destination "$taskValidationRoot/Assets/Scripts/Demo/PlayerProtectionValidationDriver.cs" -Force

# Only the isolated copy writes test saves into the workspace and uses test preferences.
$taskSaveServicePath = "$taskValidationRoot/Assets/Scripts/Demo/GameSaveService.cs"
$taskText = Get-Content -LiteralPath $taskSaveServicePath -Raw
$taskOriginalExpression = 'Path.Combine(Application.persistentDataPath, "Saves")'
if (-not $taskText.Contains($taskOriginalExpression)) { throw 'Save directory expression changed; update the isolated test fixture.' }
$taskText = $taskText.Replace($taskOriginalExpression, 'Path.Combine(Application.dataPath, "../ValidationSaves")')
[IO.File]::WriteAllText($taskSaveServicePath, $taskText)
$taskSettingsPath = "$taskValidationRoot/ProjectSettings/ProjectSettings.asset"
$taskText = Get-Content -LiteralPath $taskSettingsPath -Raw
$taskText = [regex]::Replace($taskText, '(?m)^  companyName:.*$', '  companyName: CodexValidation')
$taskText = [regex]::Replace($taskText, '(?m)^  productName:.*$', '  productName: DungeonSweeperValidation')
[IO.File]::WriteAllText($taskSettingsPath, $taskText)

$taskResultPath = Join-Path $taskValidationRoot 'validation-result.txt'
if (Test-Path -LiteralPath $taskResultPath) { Remove-Item -LiteralPath $taskResultPath }
$taskLogPath = Join-Path $taskValidationRoot 'unity-validation.log'
$taskUnityProcess = Start-Process -FilePath $UnityEditorPath -ArgumentList @('-batchmode', '-nographics', '-projectPath', ('"' + $taskValidationRoot + '"'), '-logFile', ('"' + $taskLogPath + '"'), '-executeMethod', 'GameFlowSmokeChecks.Run') -WindowStyle Hidden -PassThru
# Wait for Unity itself, not long-lived compiler services spawned by the editor.
$taskUnityProcess.WaitForExit()
if ($taskUnityProcess.ExitCode -ne 0) { throw "Unity validation failed. See $taskLogPath" }
if (-not (Test-Path -LiteralPath $taskResultPath)) { throw "Unity produced no validation result. See $taskLogPath" }
Get-Content -LiteralPath $taskResultPath

if ($CheckPlayerProtection -or $CaptureScreens) {
    $taskBuildLogPath = Join-Path $taskValidationRoot 'player-build.log'
    $taskBuildProcess = Start-Process -FilePath $UnityEditorPath -ArgumentList @('-batchmode', '-nographics', '-quit', '-projectPath', ('"' + $taskValidationRoot + '"'), '-logFile', ('"' + $taskBuildLogPath + '"'), '-executeMethod', 'PlayerProtectionValidationBuild.Run') -WindowStyle Hidden -PassThru
    $taskBuildProcess.WaitForExit()
    if ($taskBuildProcess.ExitCode -ne 0) { throw "Release protection build failed. See $taskBuildLogPath" }
    $taskPlayerPath = Join-Path $taskValidationRoot 'SaveValidationPlayer.exe'
    $taskPlayerResult = Join-Path $taskValidationRoot 'player-protection-result.txt'
    foreach ($taskRestart in @($false, $true)) {
        if (Test-Path -LiteralPath $taskPlayerResult) { Remove-Item -LiteralPath $taskPlayerResult }
        $taskPlayerArgs = @('-logFile', ('"' + (Join-Path $taskValidationRoot "player-protection-$taskRestart.log") + '"'), '-saveProtectionValidation')
        if ($CaptureScreens -and -not $taskRestart) {
            Get-ChildItem -LiteralPath $taskValidationRoot -Filter 'release-*.png' -File | Remove-Item
            $taskPlayerArgs += '-captureReleaseScreens'
        }
        else { $taskPlayerArgs += @('-batchmode', '-nographics') }
        if ($taskRestart) { $taskPlayerArgs += '-readSavedProtection' }
        $taskPlayerProcess = Start-Process -FilePath $taskPlayerPath -ArgumentList $taskPlayerArgs -WindowStyle Hidden -PassThru
        $taskPlayerProcess.WaitForExit()
        if ($taskPlayerProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $taskPlayerResult)) { throw 'Release player protection validation failed.' }
        $taskPlayerText = Get-Content -LiteralPath $taskPlayerResult -Raw
        if (-not $taskPlayerText.StartsWith('PASS:')) { throw $taskPlayerText }
        Write-Output $taskPlayerText
    }
    if ($CaptureScreens) {
        foreach ($taskCapture in @('release-main-menu', 'release-town', 'release-dungeon', 'release-loot-modal', 'release-loot-scrolled', 'release-loot-small', 'release-warehouse', 'release-warehouse-small', 'release-market', 'release-bag-upgrade', 'release-expanded-bag', 'release-spawn-layout-0', 'release-spawn-layout-1', 'release-spawn-layout-2')) {
            $taskCapturePath = Join-Path $taskValidationRoot ($taskCapture + '.png')
            if (-not (Test-Path -LiteralPath $taskCapturePath)) { throw "Release screen capture missing: $taskCapturePath" }
        }
        Write-Output "Screen captures: $taskValidationRoot"
    }
}
