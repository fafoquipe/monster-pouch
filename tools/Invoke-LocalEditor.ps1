param(
    [Parameter(Mandatory=$true)][string]$Action,
    [string]$Type,
    [string]$Method,
    [string]$Argument,
    [int]$TimeoutSeconds = 30
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$queuePath = Join-Path $projectRoot 'Library\MonsterPouchCommand.json'
if (Test-Path -LiteralPath $queuePath) { throw 'The editor has a pending command. Wait for it before sending another.' }
$requestId = 'command-' + [Guid]::NewGuid().ToString('N')
$resultPath = Join-Path $projectRoot ('Logs\local-validation\' + $requestId + '.txt')
$request = @{id=$requestId;action=$Action;type=$Type;method=$Method;argument=$Argument} | ConvertTo-Json -Compress
[IO.File]::WriteAllText($queuePath, $request, [Text.UTF8Encoding]::new($false))
$watch = [Diagnostics.Stopwatch]::StartNew()
while ($watch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
    if (Test-Path -LiteralPath $resultPath) { Get-Content -LiteralPath $resultPath -Encoding UTF8; exit 0 }
    Start-Sleep -Milliseconds 250
}
Write-Output ('Command is still running. Result: ' + $resultPath)
