function Write-SyncLog {
    param(
        [Parameter(Mandatory)][ValidateSet('INFO','WARN','ERROR','DEBUG')]
        [string]$Level,
        [Parameter(Mandatory)][string]$Message
    )
    $timestamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    Write-Host "$timestamp [$Level] $Message"
}

function Ensure-Tool {
    param([Parameter(Mandatory)][string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required tool '$Name' is not available on PATH."
    }
}

function Load-JsonFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        $Default
    )
    if (Test-Path $Path) {
        return Get-Content $Path -Raw | ConvertFrom-Json
    }
    return $Default
}

function Save-JsonFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)]$Value
    )
    $Value | ConvertTo-Json -Depth 6 | Set-Content -Path $Path
}

function Truncate-Text {
    param(
        [Parameter(Mandatory)][string]$Text,
        [int]$MaxCharacters = 8000
    )
    if ($Text.Length -le $MaxCharacters) {
        return $Text
    }
    return $Text.Substring(0, $MaxCharacters) + "`n... [truncated at $MaxCharacters characters] ..."
}
