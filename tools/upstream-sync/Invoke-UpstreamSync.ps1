Set-StrictMode -Version Latest
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
. (Join-Path $ScriptRoot 'UpstreamSync.Common.ps1')

param(
    [switch]$Loop,
    [int]$IntervalMinutes = 5,
    [switch]$Once,
    [string]$UpstreamPath = 'C:\src\openai\openai-agents-python',
    [string]$UpstreamBranch = 'main',
    [switch]$SkipPush,
    [switch]$SkipPr,
    [switch]$SkipBuild,
    [switch]$SkipTests,
    [string]$ForceFromSha,
    [switch]$AllowDirty
)

if ($Loop -and $Once) {
    throw 'Cannot specify both -Loop and -Once.'
}

if ($IntervalMinutes -lt 1) {
    $IntervalMinutes = 1
    Write-SyncLog 'WARN' 'IntervalMinutes must be >= 1; using 1 minute.'
}

$RepoRoot = Split-Path -Parent $ScriptRoot
Set-Location $RepoRoot

$TrackedStatePath = Join-Path $ScriptRoot 'state.json'
$LocalStatePath = Join-Path $ScriptRoot 'state.local.json'
$CodexNotesPath = Join-Path $ScriptRoot 'CODEX_TRANSLATION_NOTES.md'
$GuidancePath = Join-Path $RepoRoot 'AGENTS.md'

Ensure-Tool 'git'
Ensure-Tool 'gh'
Ensure-Tool 'codex'
Ensure-Tool 'dotnet'

function Initialize-State {
    if (-not (Test-Path $LocalStatePath)) {
        $defaultLocal = [ordered]@{
            lastAttemptedSha = $null
            lastRunUtc = $null
            bootstrapLastTranslatedSha = $null
        }
        Save-JsonFile -Path $LocalStatePath -Value $defaultLocal
    }

    $defaultTracked = [ordered]@{
        upstreamRepoUrl = 'https://github.com/openai/openai-agents-python.git'
        upstreamLocalPath = $UpstreamPath
        upstreamBranch = $UpstreamBranch
        lastTranslatedSha = $null
        lastSuccessUtc = $null
    }

    $tracked = Load-JsonFile -Path $TrackedStatePath -Default $defaultTracked
    if (-not $tracked.upstreamRepoUrl) {
        $tracked.upstreamRepoUrl = $defaultTracked.upstreamRepoUrl
    }
    if (-not $tracked.upstreamLocalPath) {
        $tracked.upstreamLocalPath = $UpstreamPath
    }
    if (-not $tracked.upstreamBranch) {
        $tracked.upstreamBranch = $UpstreamBranch
    }
    # Always reflect the current parameter so the prompt stays accurate.
    $tracked.upstreamLocalPath = $UpstreamPath
    $tracked.upstreamBranch = $UpstreamBranch

    $local = Load-JsonFile -Path $LocalStatePath -Default ([ordered]@{
        lastAttemptedSha = $null
        lastRunUtc = $null
        bootstrapLastTranslatedSha = $null
    })

    return @{ tracked = $tracked; local = $local }
}

function Persist-TrackedState($state) {
    Save-JsonFile -Path $TrackedStatePath -Value $state
}

function Persist-LocalState($state) {
    Save-JsonFile -Path $LocalStatePath -Value $state
}

function Ensure-UpstreamRepo {
    if (-not (Test-Path $UpstreamPath)) {
        throw "Upstream path '$UpstreamPath' does not exist."
    }
    Push-Location $UpstreamPath
    try {
        & git rev-parse --is-inside-work-tree > $null
    } finally {
        Pop-Location
    }
}

function Ensure-WorktreeClean {
    Push-Location $RepoRoot
    try {
        $status = & git status --porcelain
    } finally {
        Pop-Location
    }
    $dirty = $status | Where-Object { $_ -and $_ -notmatch 'tools/upstream-sync/state\.local\.json' }
    if ($dirty) {
        if (-not $AllowDirty) {
            throw 'Working tree has uncommitted changes; stash or commit them before running.'
        }
        Write-SyncLog 'WARN' 'Working tree is dirty but -AllowDirty was provided.'
    }
}

function Reset-WorkingMain {
    Push-Location $RepoRoot
    try {
        Write-SyncLog 'INFO' 'Refreshing local main branch against origin.'
        & git fetch origin main
        & git checkout main
        & git pull --ff-only origin main
    } finally {
        Pop-Location
    }
}

function Get-UpstreamLatestSha {
    Push-Location $UpstreamPath
    try {
        Write-SyncLog 'INFO' 'Fetching upstream changes.'
        & git fetch origin $UpstreamBranch
        $sha = (& git rev-parse "origin/$UpstreamBranch").Trim()
        return $sha
    } finally {
        Pop-Location
    }
}

function Get-CommitLines($fromSha, $toSha) {
    Push-Location $UpstreamPath
    try {
        if (-not $fromSha) {
            return & git log --oneline $toSha
        }
        return & git log --oneline "$fromSha..$toSha"
    } finally {
        Pop-Location
    }
}

function Get-UpstreamDiff($fromSha, $toSha) {
    Push-Location $UpstreamPath
    try {
        if (-not $fromSha) {
            return & git diff $toSha
        }
        return & git diff "$fromSha..$toSha"
    } finally {
        Pop-Location
    }
}

function Get-ChangedFiles($fromSha, $toSha) {
    Push-Location $UpstreamPath
    try {
        if (-not $fromSha) {
            return & git diff --name-only $toSha
        }
        return & git diff --name-only "$fromSha..$toSha"
    } finally {
        Pop-Location
    }
}

function Build-Prompt($baseSha, $latestSha, $commitLines, $diff, $files) {
    $guidance = ''
    if (Test-Path $GuidancePath) {
        $guidance = Get-Content $GuidancePath -Raw
    }
    $notes = ''
    if (Test-Path $CodexNotesPath) {
        $notes = Get-Content $CodexNotesPath -Raw
    }
    $commitListing = ($commitLines | Where-Object { $_ }) | ForEach-Object { "- $_" }
    $filesListing = ($files | Where-Object { $_ }) | ForEach-Object { "- $_" }
    $commitSection = $commitListing -join "`n"
    if (-not $commitSection) {
        $commitSection = '- (no commits were returned by git log)'
    }
    $filesSection = $filesListing -join "`n"
    if (-not $filesSection) {
        $filesSection = '- (no files changed in diff)'
    }
    $briefDiff = Truncate-Text -Text ($diff -join "`n") -MaxCharacters 6000

    $prompt = @"
Translate the upstream delta described below into the semantic .NET port in this repo. Treat the existing C# codebase as the source of truth for architecture, naming, and tests.

Upstream repository: $($trackedState.tracked.upstreamRepoUrl)
Branch: $UpstreamBranch
Range: $baseSha..$latestSha

Commits included:
$commitSection

Changed files:
$filesSection

Relevant diff excerpt (truncated if needed):
$briefDiff

Translation constraints:
1. Translate only the behavior represented in the upstream diff; avoid unrelated refactors.
2. Preserve current .NET architecture and naming conventions; keep changes minimal.
3. Update tests only when upstream behavior requires it.
4. Document any assumptions you make inline as comments where appropriate.

Existing repo guidance:
$guidance

Additional notes:
$notes

End of instructions.
"@

    return $prompt
}

function Invoke-CodexTranslation($promptContent) {
    $tempPrompt = [System.IO.Path]::GetTempFileName()
    try {
        Set-Content -Path $tempPrompt -Value $promptContent -Encoding UTF8
        Write-SyncLog 'INFO' 'Invoking Codex to translate the upstream diff.'
        $stdin = Get-Content $tempPrompt -Raw
        $result = $stdin | codex exec --dangerously-bypass-approvals-and-sandbox -C $RepoRoot --add-dir $UpstreamPath -
        Write-SyncLog 'INFO' 'Codex finished translating.'
        return $result
    } finally {
        Remove-Item $tempPrompt -ErrorAction SilentlyContinue
    }
}

function Run-DotnetCommand($args) {
    Write-SyncLog 'INFO' "Running dotnet $args."
    Push-Location $RepoRoot
    try {
        & dotnet @($args)
    } finally {
        Pop-Location
    }
}

function Create-SyncBranch($latestSha) {
    $shortSha = $latestSha.Substring(0, 7)
    $branchName = "sync/upstream-$shortSha"
    Push-Location $RepoRoot
    try {
        & git checkout main
        & git rev-parse --verify --quiet "refs/heads/$branchName" > $null
        if ($LASTEXITCODE -ne 0) {
            & git checkout -b $branchName
        } else {
            & git checkout $branchName
        }
    } finally {
        Pop-Location
    }
    return $branchName
}

function Commit-Changes($branchName, $latestSha) {
    $shortSha = $latestSha.Substring(0, 7)
    $message = "Sync upstream openai-agents-python through $shortSha"
    Push-Location $RepoRoot
    try {
        & git add -A
        & git commit -m $message
    } finally {
        Pop-Location
    }
}

function Push-Branch($branchName) {
    Write-SyncLog 'INFO' "Pushing branch $branchName to origin."
    Push-Location $RepoRoot
    try {
        & git push -u origin $branchName
    } finally {
        Pop-Location
    }
}

function Create-PullRequest($branchName, $baseSha, $latestSha, $commitLines) {
    $shortSha = $latestSha.Substring(0, 7)
    $title = "Sync upstream openai-agents-python through $shortSha"
    $body = @"
Upstream repository: $($trackedState.tracked.upstreamRepoUrl)
Translated range: $baseSha..$latestSha

Commits translated:
$($commitLines | ForEach-Object { "- $_" } | Out-String)

This PR was generated by the local upstream sync automation.
"@
    $tempBody = [System.IO.Path]::GetTempFileName()
    try {
        Set-Content -Path $tempBody -Value $body -Encoding UTF8
        Push-Location $RepoRoot
        try {
            & gh pr create --title $title --body-file $tempBody --base main --head $branchName
        } finally {
            Pop-Location
        }
    } finally {
        Remove-Item $tempBody -ErrorAction SilentlyContinue
    }
}

function Update-LocalMetadata($sha) {
    $localState = $trackedState.local
    $localState.lastAttemptedSha = $sha
    $localState.lastRunUtc = (Get-Date).ToUniversalTime().ToString('o')
    Persist-LocalState $localState
}

function Run-SyncCycle {
    Ensure-UpstreamRepo
    Ensure-WorktreeClean
    Reset-WorkingMain

    $latestSha = Get-UpstreamLatestSha
    if (-not $latestSha) {
        throw 'Unable to resolve latest upstream SHA.'
    }

    $baseSha = $ForceFromSha ?? $trackedState.tracked.lastTranslatedSha ?? $trackedState.local.bootstrapLastTranslatedSha
    if (-not $baseSha) {
        $trackedState.local.bootstrapLastTranslatedSha = $latestSha
        Persist-LocalState $trackedState.local
        Write-SyncLog 'INFO' "Bootstrapped last translated SHA to $latestSha. Run again to translate future commits."
        Update-LocalMetadata($latestSha)
        return
    }

    if ($baseSha -eq $latestSha) {
        Write-SyncLog 'INFO' 'No new upstream commits detected.'
        Update-LocalMetadata($latestSha)
        return
    }

    $commitLines = Get-CommitLines -fromSha $baseSha -toSha $latestSha
    $diff = Get-UpstreamDiff -fromSha $baseSha -toSha $latestSha
    $files = Get-ChangedFiles -fromSha $baseSha -toSha $latestSha

    try {
        $prompt = Build-Prompt $baseSha $latestSha $commitLines $diff $files
        Invoke-CodexTranslation $prompt

        if (-not $SkipBuild) {
            Run-DotnetCommand 'build'
        } else {
            Write-SyncLog 'INFO' 'Skipping dotnet build.'
        }

        if (-not $SkipTests) {
            Run-DotnetCommand 'test'
        } else {
            Write-SyncLog 'INFO' 'Skipping dotnet test.'
        }

        $branchName = Create-SyncBranch $latestSha
        $trackedState.tracked.lastTranslatedSha = $latestSha
        $trackedState.tracked.lastSuccessUtc = (Get-Date).ToUniversalTime().ToString('o')
        Persist-TrackedState $trackedState.tracked

        Commit-Changes $branchName $latestSha

        if (-not $SkipPush) {
            Push-Branch $branchName
            if (-not $SkipPr) {
                Create-PullRequest $branchName $baseSha $latestSha $commitLines
            } else {
                Write-SyncLog 'INFO' 'Skipping PR creation per -SkipPr.'
            }
        } else {
            Write-SyncLog 'INFO' 'Skipping push (and PR) per -SkipPush.'
        }
    } catch {
        Update-LocalMetadata($latestSha)
        throw
    }

    Update-LocalMetadata($latestSha)

    Push-Location $RepoRoot
    try {
        & git checkout main
    } finally {
        Pop-Location
    }
}

$trackedState = Initialize-State

if ($Loop) {
    do {
        try {
            Run-SyncCycle
        } catch {
            Write-SyncLog 'ERROR' $_.Exception.Message
            exit 1
        }
        Write-SyncLog 'INFO' "Sleeping for $IntervalMinutes minute(s)."
        Start-Sleep -Seconds ($IntervalMinutes * 60)
    } while ($Loop)
} else {
    try {
        Run-SyncCycle
    } catch {
        Write-SyncLog 'ERROR' $_.Exception.Message
        exit 1
    }
}
