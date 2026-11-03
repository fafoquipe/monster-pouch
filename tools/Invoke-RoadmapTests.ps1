param(
    [ValidateSet('EditMode','PlayMode')][string]$Mode = 'EditMode',
    [string]$Filter = '',
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$RunName = 'roadmap'
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$versionText = Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt') -Raw
$editorVersion = [regex]::Match($versionText, 'm_EditorVersion: (\S+)').Groups[1].Value
$editorPath = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$editorVersion\Editor\Unity.exe"
if (-not (Test-Path -LiteralPath $editorPath)) { throw "No se encontró Unity $editorVersion en $editorPath." }
$editors = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"
if ($editors | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($projectRoot) }) {
    throw 'El proyecto está abierto en otra instancia de Unity. Espera a que termine la prueba o usa el editor abierto.'
}
if ($Filter.Contains('"')) { throw 'El filtro no puede contener comillas.' }
$reports = Join-Path $projectRoot 'Logs\roadmap-v1.2'
New-Item -ItemType Directory -Path $reports -Force | Out-Null
$resultPath = Join-Path $reports "$RunName.xml"
$logPath = Join-Path $reports "$RunName.log"
if ((Test-Path -LiteralPath $resultPath) -or (Test-Path -LiteralPath $logPath)) {
    throw 'Ese RunName ya existe. Elige otro para conservar la evidencia anterior.'
}
$graphics = if ($Mode -eq 'EditMode') { '-nographics' } else { '-force-d3d11 -screen-width 540 -screen-height 960' }
$arguments = "-batchmode $graphics -projectPath `"$projectRoot`" -runTests -testPlatform $Mode -testResults `"$resultPath`" -logFile `"$logPath`""
if ($Filter) { $arguments += " -testFilter `"$Filter`"" }
$process = Start-Process -FilePath $editorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
[pscustomobject]@{ ProcessId = $process.Id; Results = $resultPath; Log = $logPath }
