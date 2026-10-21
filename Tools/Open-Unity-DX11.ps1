param([string]$UnityEditorPath)

$ErrorActionPreference = 'Stop'
$taskProjectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $UnityEditorPath) {
    $taskVersionFile = Join-Path $taskProjectRoot 'ProjectSettings/ProjectVersion.txt'
    $taskVersion = [regex]::Match((Get-Content -LiteralPath $taskVersionFile -Raw), 'm_EditorVersion: (\S+)').Groups[1].Value
    if (-not $taskVersion) { throw 'Unity version was not found in ProjectSettings/ProjectVersion.txt.' }
    $UnityEditorPath = Join-Path ${env:ProgramFiles} "Unity/Hub/Editor/$taskVersion/Editor/Unity.exe"
}
if (-not (Test-Path -LiteralPath $UnityEditorPath -PathType Leaf)) {
    throw 'Unity editor was not found. Pass -UnityEditorPath "C:/path/to/Unity.exe" or install this project version through Unity Hub.'
}

# 사용자용 대화형 실행기. 이미 실행 중인 에디터를 종료하거나 다시 시작하지 않는다.
Start-Process -FilePath $UnityEditorPath -ArgumentList @('-projectPath', ('"' + $taskProjectRoot + '"'), '-force-d3d11') | Out-Null
