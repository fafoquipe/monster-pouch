#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot),
    [string]$UnityPath,
    [ValidateSet('Auto','Editor','Batch')][string]$Mode = 'Auto',
    [ValidateSet('Auto','ARM64','Universal')][string]$Architectures = 'Auto',
    [string]$ApplicationIdentifier = 'com.monsterpouch.game',
    [ValidateRange(1,180)][int]$TimeoutMinutes = 45,
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$utf8 = New-Object System.Text.UTF8Encoding($false)
$buildLock = $null
$runDirectory = $null
$unityProcess = $null

function Test-EditorLock([string]$Root) {
    $path = Join-Path $Root 'Temp\UnityLockfile'
    if (-not (Test-Path -LiteralPath $path)) { return $false }
    try {
        $stream = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
        $stream.Dispose()
        return $false
    } catch [IO.IOException] { return $true }
}

function Quote-NativeArgument([string]$Value) {
    # Windows CommandLineToArgvW quoting, including embedded quotes and trailing slashes.
    $escaped = [regex]::Replace($Value, '(\\*)"', '$1$1\"')
    $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
    return '"' + $escaped + '"'
}

function Write-Json([string]$Path, $Value) {
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 12), $utf8)
}

try {
    $ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
    $versionFile = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
    if (-not (Test-Path -LiteralPath $versionFile)) { throw "No es un proyecto Unity: $ProjectPath" }
    $versionMatch = [regex]::Match([IO.File]::ReadAllText($versionFile), '(?m)^m_EditorVersion:\s*(\S+)')
    if (-not $versionMatch.Success) { throw 'No se pudo leer la version de Unity del proyecto.' }
    $editorVersion = $versionMatch.Groups[1].Value
    if ([string]::IsNullOrWhiteSpace($UnityPath)) {
        if ($env:UNITY_EDITOR_PATH) { $UnityPath = $env:UNITY_EDITOR_PATH }
        else { $UnityPath = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$editorVersion\Editor\Unity.exe" }
    }
    if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
        throw "No se encuentra Unity $editorVersion. Usa -UnityPath 'ruta\Editor\Unity.exe'."
    }
    $UnityPath = (Resolve-Path -LiteralPath $UnityPath).Path
    $installedVersion = (Get-Item -LiteralPath $UnityPath).VersionInfo.ProductVersion
    if (-not $installedVersion.StartsWith($editorVersion + '_') -and $installedVersion -ne $editorVersion) {
        throw "La version elegida ($installedVersion) no coincide con la del proyecto ($editorVersion)."
    }
    $androidRoot = Join-Path (Split-Path -Parent $UnityPath) 'Data\PlaybackEngines\AndroidPlayer'
    $required = @(
        'OpenJDK\bin\java.exe', 'NDK\source.properties',
        'NDK\toolchains\llvm\prebuilt\windows-x86_64\bin\clang++.exe', 'SDK\platform-tools\adb.exe',
        'Variations\il2cpp\Development\Libs\arm64-v8a'
    )
    $missing = @($required | Where-Object { -not (Test-Path -LiteralPath (Join-Path $androidRoot $_)) })
    if ($missing.Count -gt 0) {
        throw "Falta soporte Android de Unity $editorVersion ($($missing -join ', ')). En Unity Hub instala Android Build Support, Android SDK & NDK Tools y OpenJDK. Este script no descarga modulos."
    }
    $platforms = @(Get-ChildItem -LiteralPath (Join-Path $androidRoot 'SDK\platforms') -Directory -Filter 'android-*' | Select-Object -ExpandProperty Name)
    $buildTools = @(Get-ChildItem -LiteralPath (Join-Path $androidRoot 'SDK\build-tools') -Directory | Select-Object -ExpandProperty Name)
    if ($platforms.Count -eq 0 -or $buildTools.Count -eq 0) { throw 'El SDK no contiene plataformas o build-tools.' }
    $hasArmV7 = Test-Path -LiteralPath (Join-Path $androidRoot 'Variations\il2cpp\Development\Libs\armeabi-v7a')
    if ($Architectures -eq 'Universal' -and -not $hasArmV7) { throw 'No hay IL2CPP ARMv7 instalado. Usa -Architectures ARM64.' }
    if ($ApplicationIdentifier -notmatch '^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)+$') {
        throw 'ApplicationIdentifier debe tener formato com.empresa.juego.'
    }
    $editorOpen = Test-EditorLock $ProjectPath
    $selectedMode = $Mode
    if ($Mode -eq 'Auto') { $selectedMode = if ($editorOpen) { 'Editor' } else { 'Batch' } }
    if ($selectedMode -eq 'Batch' -and $editorOpen) { throw 'El proyecto ya esta abierto. Usa -Mode Editor o cierra Unity; no se iniciara otra instancia.' }
    if ($selectedMode -eq 'Editor' -and -not $editorOpen) { throw 'No hay un editor abierto para este proyecto. Usa -Mode Auto o Batch.' }
    $environment = [ordered]@{
        project=$ProjectPath; unity=$UnityPath; requiredUnityVersion=$editorVersion; mode=$selectedMode;
        androidModule=$androidRoot; sdkPlatforms=$platforms; sdkBuildTools=$buildTools;
        ndk=([IO.File]::ReadAllText((Join-Path $androidRoot 'NDK\source.properties')).Trim());
        jdk=(@(Get-Content -LiteralPath (Join-Path $androidRoot 'OpenJDK\release') | Where-Object { $_ -match '^JAVA_VERSION=' }) -join '');
        architectures=if ($Architectures -eq 'ARM64' -or -not $hasArmV7) { 'ARM64' } else { 'ARM64 + ARMv7' };
        backend='IL2CPP'; applicationIdentifier=$ApplicationIdentifier; orientation='Portrait'; development=$true
    }
    if ($CheckOnly) { $environment | ConvertTo-Json -Depth 6; return }
    foreach ($relative in @('Assets\Editor\MonsterPouchAndroidBuild.cs','Assets\Scenes\main-scene.unity')) {
        if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath $relative))) { throw "Falta $relative en el proyecto." }
    }

    $outputRoot = Join-Path $ProjectPath 'Builds\Android'
    [IO.Directory]::CreateDirectory($outputRoot) | Out-Null
    try { $buildLock = [IO.File]::Open((Join-Path $outputRoot '.build.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) }
    catch [IO.IOException] { throw 'Ya hay otro Build-Android.ps1 ejecutandose para este proyecto.' }
    $runId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,6)
    $runDirectory = Join-Path $outputRoot $runId
    [IO.Directory]::CreateDirectory($runDirectory) | Out-Null
    Write-Json (Join-Path $runDirectory 'environment.json') $environment
    $request = [ordered]@{ outputDirectory=$runDirectory; applicationIdentifier=$ApplicationIdentifier; architectures=$Architectures }
    $requestPath = Join-Path $runDirectory 'request.json'
    Write-Json $requestPath $request
    $reportPath = Join-Path $runDirectory 'build-report.json'
    $deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
    $responsePath = $null
    Write-Host "Android: $selectedMode / $($environment.architectures). Salida: $runDirectory"

    if ($selectedMode -eq 'Editor') {
        if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath 'Assets\Editor\MonsterPouchLocalBridge.cs'))) { throw 'Falta el bridge del editor; cierra Unity y usa -Mode Batch.' }
        $commandId = 'android-' + $runId
        $responsePath = Join-Path $ProjectPath "Logs\local-validation\$commandId.txt"
        $queue = Join-Path $ProjectPath 'Library\MonsterPouchCommand.json'
        if (Test-Path -LiteralPath $queue) { throw 'El bridge tiene otro comando pendiente. Espera a que termine.' }
        $command = [ordered]@{ id=$commandId; action='invoke'; type='MonsterPouchAndroidBuild'; method='RequestFromJson'; argument=($request | ConvertTo-Json -Compress) }
        $temporaryQueue = Join-Path $ProjectPath "Library\$commandId.tmp"
        Write-Json $temporaryQueue $command
        try { [IO.File]::Move($temporaryQueue,$queue) }
        catch { if (Test-Path -LiteralPath $temporaryQueue) { Remove-Item -LiteralPath $temporaryQueue }; throw }
    } else {
        $arguments = @('-batchmode','-nographics','-projectPath',$ProjectPath,'-buildTarget','Android',
            '-executeMethod','MonsterPouchAndroidBuild.BuildBatch','-monsterPouchBuildRequest',$requestPath,
            '-logFile',(Join-Path $runDirectory 'unity.log'))
        $quotedArguments = ($arguments | ForEach-Object { Quote-NativeArgument $_ }) -join ' '
        $unityProcess = Start-Process -FilePath $UnityPath -ArgumentList $quotedArguments -PassThru -WindowStyle Hidden
    }

    $lastProgress = [DateTime]::UtcNow
    while ($true) {
        if ($responsePath -and (Test-Path -LiteralPath $responsePath)) {
            $response = [IO.File]::ReadAllText($responsePath)
            if ($response.StartsWith('ERROR')) { throw "El editor rechazo el build: $response" }
        }
        if (Test-Path -LiteralPath $reportPath) {
            try { $report = [IO.File]::ReadAllText($reportPath) | ConvertFrom-Json }
            catch { $report = $null } # A report can be updated while this process reads it.
            if ($null -ne $report -and $report.completedUtc) { break }
        }
        if ($null -ne $unityProcess -and $unityProcess.HasExited) { throw "Unity termino antes del informe (codigo $($unityProcess.ExitCode)). Consulta unity.log." }
        if ([DateTime]::UtcNow -gt $deadline) { throw "Tiempo de espera agotado. Unity NO fue cerrado; revisa si sigue compilando. Carpeta: $runDirectory" }
        if (([DateTime]::UtcNow - $lastProgress).TotalSeconds -ge 30) {
            Write-Host 'Unity sigue trabajando (importacion, IL2CPP, Gradle o restauracion de plataforma)...'
            $lastProgress = [DateTime]::UtcNow
        }
        Start-Sleep -Milliseconds 500
    }
    if ($report.status -ne 'Succeeded') { throw "Build fallido: $($report.error)" }
    if (-not (Test-Path -LiteralPath $report.apkPath -PathType Leaf)) { throw 'El informe declara exito, pero no existe el APK.' }
    $actualHash = (Get-FileHash -LiteralPath $report.apkPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $report.sha256) { throw 'El SHA-256 del APK no coincide con el informe.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($report.apkPath)
    try {
        foreach ($entry in @('AndroidManifest.xml','classes.dex','lib/arm64-v8a/libunity.so')) {
            if ($null -eq $zip.GetEntry($entry)) { throw "APK incompleto: falta $entry" }
        }
    } finally { $zip.Dispose() }
    Write-Json (Join-Path $runDirectory 'launcher-report.json') @{status='Succeeded';apk=$report.apkPath;sha256=$actualHash;finishedUtc=[DateTime]::UtcNow.ToString('O')}
    Write-Host "APK listo: $($report.apkPath)"
    Write-Host "Ultimo APK: $(Join-Path $outputRoot 'latest\Monster-Pouch.apk')"
    Write-Host "Informe: $reportPath"
} catch {
    if ($runDirectory) { Write-Json (Join-Path $runDirectory 'launcher-report.json') @{status='Failed';error=$_.Exception.Message;finishedUtc=[DateTime]::UtcNow.ToString('O')} }
    Write-Error $_
    exit 1
} finally {
    if ($null -ne $buildLock) { $buildLock.Dispose() }
}
