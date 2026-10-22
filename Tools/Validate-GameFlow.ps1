param([string]$UnityEditorPath, [switch]$CheckPlayerProtection, [switch]$CaptureScreens, [switch]$EditorOnly)

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
# Synchronize only this tool's isolated code copies. Moving production sources must not leave
# stale scripts or duplicate GUIDs behind; saves, art caches and profiling captures are preserved.
function Remove-StaleValidationCode([string]$folder) {
    $source = Join-Path $taskProjectRoot "Assets/$folder"
    $target = [IO.Path]::GetFullPath((Join-Path $taskValidationRoot "Assets/$folder"))
    $allowed = [IO.Path]::GetFullPath((Join-Path $taskValidationRoot 'Assets')) + [IO.Path]::DirectorySeparatorChar
    if (-not $target.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase) -or $folder -notin @('Scripts','Editor')) { throw 'Invalid isolated source cleanup path.' }
    if (-not (Test-Path -LiteralPath $target)) { return }
    foreach ($file in @(Get-ChildItem -LiteralPath $target -Recurse -File)) {
        $relative = $file.FullName.Substring($target.Length + 1)
        $code = $file.Name.EndsWith('.cs') -or $file.Name.EndsWith('.cs.meta')
        $folderMeta = $file.Extension -eq '.meta' -and (Test-Path -LiteralPath $file.FullName.Substring(0,$file.FullName.Length-5) -PathType Container)
        if (($code -or $folderMeta) -and -not (Test-Path -LiteralPath (Join-Path $source $relative))) {
            Remove-Item -LiteralPath $file.FullName -Force
        }
    }
    foreach ($directory in @(Get-ChildItem -LiteralPath $target -Recurse -Directory | Sort-Object { $_.FullName.Length } -Descending)) {
        if (@(Get-ChildItem -LiteralPath $directory.FullName -Force).Count -eq 0) { Remove-Item -LiteralPath $directory.FullName }
    }
}
Remove-StaleValidationCode 'Scripts'
Remove-StaleValidationCode 'Editor'
# The sandbox moved with its original GUID; remove its obsolete isolated path before copying it.
foreach ($taskSandboxName in @('Dungeon_Test.unity', 'Dungeon_Test.unity.meta')) {
    $taskSandboxPath = Join-Path $taskValidationRoot "Assets/Scenes/$taskSandboxName"
    if (Test-Path -LiteralPath $taskSandboxPath) { Remove-Item -LiteralPath $taskSandboxPath -Force }
}
foreach ($taskFolder in @('Scripts', 'Resources', 'Prefabs', 'Scenes', 'Settings', 'Editor', 'Art', 'Audio', 'Models', 'Tests')) {
    Copy-Item -LiteralPath (Join-Path $taskProjectRoot "Assets/$taskFolder") -Destination "$taskValidationRoot/Assets" -Recurse -Force
    $taskFolderMeta = Join-Path $taskProjectRoot "Assets/$taskFolder.meta"
    if (Test-Path -LiteralPath $taskFolderMeta) { Copy-Item -LiteralPath $taskFolderMeta -Destination "$taskValidationRoot/Assets" -Force }
}
Copy-Item -LiteralPath (Join-Path $taskProjectRoot 'ProjectSettings') -Destination $taskValidationRoot -Recurse -Force
Copy-Item -LiteralPath (Join-Path $taskProjectRoot 'Packages/manifest.json'), (Join-Path $taskProjectRoot 'Packages/packages-lock.json') -Destination "$taskValidationRoot/Packages" -Force
# Test-only scripts never live beside the shipping runtime sources.
$taskRuntimeChecks = @('ValidationRuntimeDriver', 'SaveProtectionChecks', 'ReleaseLogicChecks', 'TownEconomyChecks', 'FocusPauseChecks', 'DungeonPopulationChecks', 'DungeonPrefabChecks', 'ContinueEntryChecks', 'DungeonPoolChecks', 'DungeonBuilderChecks', 'DungeonGenerationChecks', 'PlayerProtectionValidationDriver')
$taskEditorChecks = @('GameFlowSmokeChecks', 'DungeonGenerationEditorChecks', 'DungeonDeletionEditorChecks', 'OptimizationEditorChecks', 'DungeonSelectionEditorChecks', 'SourceCleanupEditorChecks', 'LootRewardEditorChecks', 'RenderingBatchEditorChecks', 'DungeonVisionEditorChecks', 'HidingPhysicsEditorChecks', 'MenuUiArtEditorChecks', 'PlayerProtectionValidationBuild', 'ValidationContentFixture', 'ProductionRefactorEditorChecks')
foreach ($taskGroup in @('Runtime','Editor')) {
    $taskCheckRoot = "$taskValidationRoot/Assets/Validation/$taskGroup"
    New-Item -ItemType Directory -Path $taskCheckRoot -Force | Out-Null
    $taskChecks = if ($taskGroup -eq 'Runtime') { $taskRuntimeChecks } else { $taskEditorChecks }
    foreach ($taskCheck in $taskChecks) {
        Copy-Item -LiteralPath (Join-Path $taskProjectRoot "Tests/$taskCheck.cs") -Destination "$taskCheckRoot/$taskCheck.cs" -Force
    }
}
# Only the isolated copy writes test saves into the workspace and uses test preferences.
$taskSaveServicePath = "$taskValidationRoot/Assets/Scripts/Runtime/Persistence/GameSaveService.cs"
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

$taskResultName = if ($EditorOnly) { 'source-cleanup-result.txt' } else { 'validation-result.txt' }
$taskResultPath = Join-Path $taskValidationRoot $taskResultName
if (Test-Path -LiteralPath $taskResultPath) { Remove-Item -LiteralPath $taskResultPath }
$taskLogPath = Join-Path $taskValidationRoot 'unity-validation.log'
$taskMethod = if ($EditorOnly) { 'SourceCleanupEditorChecks.RunStandalone' } else { 'GameFlowSmokeChecks.Run' }
$taskUnityArgs = @('-batchmode', '-nographics', '-projectPath', ('"' + $taskValidationRoot + '"'), '-logFile', ('"' + $taskLogPath + '"'), '-executeMethod', $taskMethod)
if ($EditorOnly) { $taskUnityArgs += '-quit' }
$taskUnityProcess = Start-Process -FilePath $UnityEditorPath -ArgumentList $taskUnityArgs -WindowStyle Hidden -PassThru
# Wait for Unity itself, not long-lived compiler services spawned by the editor.
$taskUnityProcess.WaitForExit()
if ($taskUnityProcess.ExitCode -ne 0) { throw "Unity validation failed. See $taskLogPath" }
if ($EditorOnly -and (Select-String -LiteralPath $taskLogPath -Pattern 'error CS\d+|Scripts have compiler errors|Shader error|Failed to call static function|MissingReferenceException|NullReferenceException|GUI Error' -Quiet)) {
    throw "Editor validation logged a code or GUI error. See $taskLogPath"
}
if (-not (Test-Path -LiteralPath $taskResultPath)) { throw "Unity produced no validation result. See $taskLogPath" }
Get-Content -LiteralPath $taskResultPath
if ($EditorOnly) { return }

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
            # 숨겨진 창의 D3D12 초기 backbuffer는 검게 캡처될 수 있어 화면 검증은 D3D11을 사용한다.
            $taskPlayerArgs += '-force-d3d11'
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
        foreach ($taskCapture in @('release-main-menu', 'release-town', 'release-art-gallery', 'release-attack-gallery', 'release-dungeon', 'release-wall-north', 'release-wall-west', 'release-wall-east', 'release-loot-modal', 'release-loot-scrolled', 'release-loot-small', 'release-warehouse', 'release-warehouse-small', 'release-market', 'release-bag-upgrade', 'release-expanded-bag', 'release-spawn-layout-0', 'release-spawn-layout-1', 'release-spawn-layout-2')) {
            $taskCapturePath = Join-Path $taskValidationRoot ($taskCapture + '.png')
            if (-not (Test-Path -LiteralPath $taskCapturePath)) { throw "Release screen capture missing: $taskCapturePath" }
            if ((Get-Item -LiteralPath $taskCapturePath).Length -lt 10000) { throw "Release screen capture appears blank: $taskCapturePath" }
        }
        Write-Output "Screen captures: $taskValidationRoot"
    }
}
