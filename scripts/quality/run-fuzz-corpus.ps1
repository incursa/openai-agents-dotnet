param(
    [string]$CorpusRoot = "",
    [switch]$NoRestore,
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "QualityLane.Common.ps1")

Assert-DotNetAvailable

$repoRoot = Get-QualityRepoRoot
$projectPath = Resolve-RepoPath -RepoRoot $repoRoot -Path "fuzz/Incursa.OpenAI.Agents.Fuzz.csproj"
$corpusPath = if ([string]::IsNullOrWhiteSpace($CorpusRoot)) { Join-Path $repoRoot "fuzz\corpus" } else { Resolve-RepoPath -RepoRoot $repoRoot -Path $CorpusRoot }
$summaryPath = Join-Path $repoRoot "artifacts\quality\fuzz-corpus-summary.md"
New-Item -Path (Split-Path $summaryPath -Parent) -ItemType Directory -Force | Out-Null

if (-not (Test-Path $corpusPath)) {
    throw "Fuzz corpus root '$corpusPath' was not found."
}

if (-not $NoRestore) {
    & dotnet restore $projectPath
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE."
    }
}

if (-not $NoBuild) {
    & dotnet build $projectPath --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }
}

$dllPath = Join-Path (Split-Path $projectPath -Parent) "bin\Release\net10.0\Incursa.OpenAI.Agents.Fuzz.dll"
if (-not (Test-Path $dllPath)) {
    throw "The fuzz harness assembly was not found at '$dllPath'."
}

$seedFiles = @(Get-ChildItem -Path $corpusPath -File -Recurse |
    Where-Object { $_.Extension -in '.txt', '.bin' } |
    Sort-Object FullName)
if ($seedFiles.Count -eq 0) {
    throw "No fuzz corpus seeds were found under '$corpusPath'."
}

$summary = New-Object System.Collections.Generic.List[string]
$summary.Add("# Fuzz Corpus Summary")
$summary.Add("")
$summary.Add("| Seed | Result |")
$summary.Add("| --- | --- |")

$failures = 0
foreach ($seed in $seedFiles) {
    $relativeSeed = Get-RelativeArtifactPath -RepoRoot $repoRoot -Path $seed.FullName
    Write-Host "Running fuzz seed $relativeSeed..."

    $process = Start-Process -FilePath dotnet -ArgumentList @($dllPath) -RedirectStandardInput $seed.FullName -PassThru -Wait
    if ($process.ExitCode -eq 0) {
        $summary.Add("| $relativeSeed | Passed |")
    } else {
        $summary.Add("| $relativeSeed | Failed (exit code $($process.ExitCode)) |")
        $failures++
    }
}

$summary.Add("")
$summary.Add("| Total seeds | $($seedFiles.Count) |")
$summary.Add("| Failures | $failures |")
$summary | Set-Content -Path $summaryPath -Encoding UTF8
Write-Host "Fuzz corpus summary written to $summaryPath"

if ($failures -gt 0) {
    exit 1
}
