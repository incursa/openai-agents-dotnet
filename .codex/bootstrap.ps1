#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [string]$WorkerUser = 'samuel',
    [switch]$RunTests,
    [switch]$VerifyOnly,
    [switch]$SkipCodexUpdate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$CodexInstallerUrl = 'https://chatgpt.com/codex/install.sh'
$DotNetInstallerUrl = 'https://dot.net/v1/dotnet-install.sh'
$DotNetInstallDir = '/usr/local/share/dotnet'
$DotNetSymlink = '/usr/local/bin/dotnet'

function Write-Step {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-Native {
    param(
        [Parameter(Mandatory)][string]$File,
        [string[]]$Arguments = @(),
        [string]$WorkingDirectory,
        [switch]$AllowFailure
    )

    $oldLocation = $null

    try {
        if (-not [string]::IsNullOrWhiteSpace($WorkingDirectory)) {
            $oldLocation = Get-Location
            Set-Location -LiteralPath $WorkingDirectory
        }

        & $File @Arguments
        $exitCode = $LASTEXITCODE

        if ($exitCode -ne 0 -and -not $AllowFailure) {
            throw "Command failed with exit code $exitCode`: $File $($Arguments -join ' ')"
        }

        return $exitCode
    }
    finally {
        if ($null -ne $oldLocation) {
            Set-Location -LiteralPath $oldLocation
        }
    }
}

function Get-EffectiveUid {
    $uidText = (& id -u 2>$null | Out-String).Trim()
    $uid = 0

    if (-not [int]::TryParse($uidText, [ref]$uid)) {
        throw "Unable to determine the effective Linux user ID."
    }

    return $uid
}

function Test-IsRoot {
    return (Get-EffectiveUid) -eq 0
}

function Get-UserHome {
    param([Parameter(Mandatory)][string]$UserName)

    $entry = (& getent passwd $UserName 2>$null | Out-String).Trim()
    if ([string]::IsNullOrWhiteSpace($entry)) {
        throw "Linux user '$UserName' does not exist."
    }

    $parts = $entry.Split(':')
    if ($parts.Count -lt 6 -or [string]::IsNullOrWhiteSpace($parts[5])) {
        throw "Unable to determine the home directory for '$UserName'."
    }

    return $parts[5]
}

function Invoke-Root {
    param(
        [Parameter(Mandatory)][string]$File,
        [string[]]$Arguments = @()
    )

    if (Test-IsRoot) {
        Invoke-Native -File $File -Arguments $Arguments | Out-Null
        return
    }

    if (-not (Get-Command sudo -ErrorAction SilentlyContinue)) {
        throw "This bootstrap needs root privileges to install system packages. Run it as root or install sudo."
    }

    $sudoArgs = @($File) + $Arguments
    Invoke-Native -File 'sudo' -Arguments $sudoArgs | Out-Null
}

function Invoke-AsWorker {
    param(
        [Parameter(Mandatory)][string]$File,
        [string[]]$Arguments = @(),
        [string]$WorkingDirectory
    )

    $currentUser = (& id -un | Out-String).Trim()

    if ($currentUser -eq $WorkerUser) {
        Invoke-Native -File $File -Arguments $Arguments -WorkingDirectory $WorkingDirectory | Out-Null
        return
    }

    if (-not (Test-IsRoot)) {
        throw "Bootstrap is running as '$currentUser' but worker commands must run as '$WorkerUser'. Run as root or as '$WorkerUser'."
    }

    $workerHome = Get-UserHome -UserName $WorkerUser
    $path = "$workerHome/.local/bin:$workerHome/bin:/usr/local/bin:/usr/bin:/bin"

    $args = @(
        '-u', $WorkerUser,
        '--',
        'env',
        "HOME=$workerHome",
        "PATH=$path",
        $File
    ) + $Arguments

    $oldLocation = $null
    try {
        if (-not [string]::IsNullOrWhiteSpace($WorkingDirectory)) {
            $oldLocation = Get-Location
            Set-Location -LiteralPath $WorkingDirectory
        }

        Invoke-Native -File 'runuser' -Arguments $args | Out-Null
    }
    finally {
        if ($null -ne $oldLocation) {
            Set-Location -LiteralPath $oldLocation
        }
    }
}

function Test-AsWorkerCommand {
    param(
        [Parameter(Mandatory)][string]$Command
    )

    $currentUser = (& id -un | Out-String).Trim()
    $workerHome = Get-UserHome -UserName $WorkerUser
    $workerPath = "$workerHome/.local/bin:$workerHome/bin:/usr/local/bin:/usr/bin:/bin"

    if ($currentUser -eq $WorkerUser) {
        & env "HOME=$workerHome" "PATH=$workerPath" bash -lc $Command *> $null
        return $LASTEXITCODE -eq 0
    }

    if (-not (Test-IsRoot)) {
        return $false
    }

    & runuser -u $WorkerUser -- env "HOME=$workerHome" "PATH=$workerPath" bash -lc $Command *> $null
    return $LASTEXITCODE -eq 0
}

function Get-AsWorkerOutput {
    param(
        [Parameter(Mandatory)][string]$Command
    )

    $currentUser = (& id -un | Out-String).Trim()
    $workerHome = Get-UserHome -UserName $WorkerUser
    $workerPath = "$workerHome/.local/bin:$workerHome/bin:/usr/local/bin:/usr/bin:/bin"

    if ($currentUser -eq $WorkerUser) {
        return (& env "HOME=$workerHome" "PATH=$workerPath" bash -lc $Command 2>&1 | Out-String).Trim()
    }

    if (-not (Test-IsRoot)) {
        throw "Cannot execute command as '$WorkerUser' from '$currentUser'."
    }

    return (& runuser -u $WorkerUser -- env "HOME=$workerHome" "PATH=$workerPath" bash -lc $Command 2>&1 | Out-String).Trim()
}

function Read-OsRelease {
    $path = '/etc/os-release'
    if (-not (Test-Path -LiteralPath $path)) {
        throw "This bootstrap currently supports Linux workers and requires /etc/os-release."
    }

    $values = @{}

    foreach ($line in Get-Content -LiteralPath $path) {
        if ($line -match '^(?<key>[A-Z0-9_]+)=(?<value>.*)$') {
            $value = $Matches.value.Trim()
            if ($value.Length -ge 2 -and
                (($value.StartsWith('"') -and $value.EndsWith('"')) -or
                 ($value.StartsWith("'") -and $value.EndsWith("'")))) {
                $value = $value.Substring(1, $value.Length - 2)
            }

            $values[$Matches.key] = $value
        }
    }

    return $values
}

function Ensure-AptPackages {
    param([string[]]$Packages)

    if ($null -eq $Packages -or $Packages.Count -eq 0) {
        return
    }

    Write-Step "Ensuring Debian packages: $($Packages -join ', ')"
    Invoke-Root -File 'apt-get' -Arguments @('update')
    Invoke-Root -File 'apt-get' -Arguments (@('install', '-y') + $Packages)
}

function Ensure-ExactDotNetSdk {
    param(
        [Parameter(Mandatory)][string]$Version
    )

    $installed = @()

    if (Get-Command dotnet -ErrorAction SilentlyContinue) {
        $installed = @(
            (& dotnet --list-sdks 2>$null) |
                ForEach-Object {
                    if ($_ -match '^(?<version>\S+)\s') {
                        $Matches.version
                    }
                }
        )
    }

    if ($installed -contains $Version) {
        Write-Host "    .NET SDK $Version already installed."
        return
    }

    if ($VerifyOnly) {
        throw ".NET SDK $Version is required but is not installed."
    }

    Write-Step "Installing exact .NET SDK $Version from global.json"

    $installer = Join-Path ([System.IO.Path]::GetTempPath()) "dotnet-install-$([Guid]::NewGuid().ToString('N')).sh"

    try {
        Invoke-Native -File 'curl' -Arguments @('-fsSL', $DotNetInstallerUrl, '-o', $installer) | Out-Null
        Invoke-Root -File 'mkdir' -Arguments @('-p', $DotNetInstallDir)
        Invoke-Root -File 'bash' -Arguments @(
            $installer,
            '--version', $Version,
            '--install-dir', $DotNetInstallDir,
            '--no-path'
        )
        Invoke-Root -File 'ln' -Arguments @('-sfn', "$DotNetInstallDir/dotnet", $DotNetSymlink)
    }
    finally {
        Remove-Item -LiteralPath $installer -Force -ErrorAction SilentlyContinue
    }

    $versions = @(
        (& $DotNetSymlink --list-sdks 2>$null) |
            ForEach-Object {
                if ($_ -match '^(?<version>\S+)\s') {
                    $Matches.version
                }
            }
    )

    if ($versions -notcontains $Version) {
        throw ".NET SDK $Version was installed but could not be verified."
    }
}

function Ensure-CodexCli {
    if (-not [bool]$config.dependencies.codexCli.install) {
        return
    }

    $codexExists = Test-AsWorkerCommand -Command 'command -v codex >/dev/null 2>&1'

    if ($VerifyOnly) {
        if (-not $codexExists) {
            throw "Codex CLI is required but is not installed for '$WorkerUser'."
        }

        return
    }

    $shouldInstall = (-not $codexExists) -or
        ([bool]$config.dependencies.codexCli.updateOnBootstrap -and -not $SkipCodexUpdate)

    if (-not $shouldInstall) {
        Write-Host "    Codex CLI already installed; update skipped."
        return
    }

    Write-Step $(if ($codexExists) { 'Updating Codex CLI' } else { 'Installing Codex CLI' })

    $installer = Join-Path ([System.IO.Path]::GetTempPath()) "codex-install-$([Guid]::NewGuid().ToString('N')).sh"

    try {
        Invoke-Native -File 'curl' -Arguments @('-fsSL', $CodexInstallerUrl, '-o', $installer) | Out-Null
        Invoke-Root -File 'chmod' -Arguments @('0755', $installer)
        Invoke-AsWorker -File 'sh' -Arguments @($installer)
    }
    finally {
        Remove-Item -LiteralPath $installer -Force -ErrorAction SilentlyContinue
    }

    if (-not (Test-AsWorkerCommand -Command 'command -v codex >/dev/null 2>&1')) {
        throw "Codex installer completed, but 'codex' is not available for '$WorkerUser'."
    }
}

function Invoke-ConfiguredCommands {
    param(
        [Parameter(Mandatory)]$Commands,
        [Parameter(Mandatory)][string]$RepositoryRoot
    )

    foreach ($command in @($Commands)) {
        $file = [string]$command.file
        $args = @()

        if ($null -ne $command.args) {
            $args = @($command.args | ForEach-Object { [string]$_ })
        }

        Write-Step "$file $($args -join ' ')"
        Invoke-AsWorker -File $file -Arguments $args -WorkingDirectory $RepositoryRoot
    }
}

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$configPath = Join-Path $PSScriptRoot 'worker.json'

if (-not (Test-Path -LiteralPath $configPath)) {
    throw "Worker configuration not found: $configPath"
}

$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json

if ([int]$config.schemaVersion -ne 1) {
    throw "Unsupported .codex/worker.json schemaVersion '$($config.schemaVersion)'."
}

$os = Read-OsRelease
$expectedOs = [string]$config.platform.os
$expectedMajor = [string]$config.platform.majorVersion

if ($os['ID'] -ne $expectedOs) {
    throw "This worker expects '$expectedOs', but /etc/os-release reports '$($os['ID'])'."
}

$actualMajor = ([string]$os['VERSION_ID']).Split('.')[0]
if ($actualMajor -ne $expectedMajor) {
    throw "This worker expects $expectedOs $expectedMajor, but VERSION_ID is '$($os['VERSION_ID'])'."
}

# Ensure the configured worker account exists before doing any user-scoped work.
$workerHome = Get-UserHome -UserName $WorkerUser

Write-Host ''
Write-Host "Codex worker bootstrap" -ForegroundColor Green
Write-Host "  Repository : $repositoryRoot"
Write-Host "  Worker user: $WorkerUser"
Write-Host "  OS         : $($os['PRETTY_NAME'])"
Write-Host "  Mode       : $(if ($VerifyOnly) { 'verify only' } else { 'ensure desired state' })"
Write-Host ''

$requiredSdk = if ($config.dependencies.dotnet.PSObject.Properties.Name -contains 'version') {
    [string]$config.dependencies.dotnet.version
}
else {
    ''
}

if ([string]::IsNullOrWhiteSpace($requiredSdk)) {
    $globalJsonRelative = if ($config.dependencies.dotnet.PSObject.Properties.Name -contains 'globalJson') {
        [string]$config.dependencies.dotnet.globalJson
    }
    else {
        ''
    }

    if ([string]::IsNullOrWhiteSpace($globalJsonRelative)) {
        throw 'Worker configuration must define dependencies.dotnet.version or dependencies.dotnet.globalJson.'
    }

    $globalJsonPath = Join-Path $repositoryRoot $globalJsonRelative

    if (-not (Test-Path -LiteralPath $globalJsonPath)) {
        throw "Configured global.json was not found: $globalJsonPath"
    }

    $globalJson = Get-Content -LiteralPath $globalJsonPath -Raw | ConvertFrom-Json
    $requiredSdk = [string]$globalJson.sdk.version

    if ([string]::IsNullOrWhiteSpace($requiredSdk)) {
        throw "No sdk.version was found in $globalJsonPath."
    }
}

if (-not $VerifyOnly) {
    Ensure-AptPackages -Packages @($config.dependencies.aptPackages | ForEach-Object { [string]$_ })
}

Ensure-ExactDotNetSdk -Version $requiredSdk
Ensure-CodexCli

$codexVersion = Get-AsWorkerOutput -Command 'codex --version'
$dotnetVersion = (& dotnet --version 2>&1 | Out-String).Trim()

Write-Host ''
Write-Host 'Verified toolchain:' -ForegroundColor Green
Write-Host "  .NET  : $dotnetVersion"
Write-Host "  Codex : $codexVersion"
Write-Host "  APT   : $(@($config.dependencies.aptPackages) -join ', ')"

if (-not $VerifyOnly) {
    Invoke-ConfiguredCommands -Commands $config.commands.bootstrap -RepositoryRoot $repositoryRoot

    if ($RunTests) {
        Invoke-ConfiguredCommands -Commands $config.commands.test -RepositoryRoot $repositoryRoot
    }
}

$codexAuthenticated = Test-AsWorkerCommand -Command 'codex login status >/dev/null 2>&1'

Write-Host ''
if ($codexAuthenticated) {
    Write-Host "Codex authentication: ready." -ForegroundColor Green
    Write-Host 'CODEX_WORKER_STATUS=ready'
}
else {
    Write-Host "Codex authentication: ACTION REQUIRED." -ForegroundColor Yellow
    Write-Host "Run this once as '$WorkerUser':"
    Write-Host '  codex login --device-auth'
    Write-Host ''
    Write-Host 'Device-code authentication is appropriate for a headless worker.'
    Write-Host 'CODEX_WORKER_STATUS=ready-needs-codex-auth'
}

if (-not $VerifyOnly) {
    Write-Host ''
    Write-Host 'Repository dependency bootstrap complete.' -ForegroundColor Green

    if (-not $RunTests) {
        Write-Host 'Tests were not run. Use -RunTests when you want bootstrap to include the test suite.'
    }
}
