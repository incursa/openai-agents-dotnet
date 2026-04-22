param(
    [switch]$Loop,
    [int]$IntervalMinutes = 5,
    [switch]$Once,
    [string[]]$Sources,
    [string]$ForceFromSha,
    [Alias('AnalyzeOnly')]
    [switch]$PreviewOnly,
    [switch]$SkipPush,
    [switch]$SkipPr,
    [switch]$SkipBuild,
    [switch]$SkipTests,
    [switch]$SkipTraceability,
    [switch]$AllowDirty
)

Set-StrictMode -Version Latest
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
. (Join-Path $ScriptRoot 'UpstreamSync.Common.ps1')

if ($Loop -and $Once) {
    throw 'Cannot specify both -Loop and -Once.'
}

if (-not $Loop -and -not $Once) {
    $Once = $true
}

if ($IntervalMinutes -lt 1) {
    $IntervalMinutes = 1
    Write-SyncLog 'WARN' 'IntervalMinutes must be >= 1; using 1 minute.'
}

$RepoRoot = (Resolve-Path (Join-Path $ScriptRoot '..\..')).Path
Set-Location $RepoRoot

$TrackedStatePath = Join-Path $ScriptRoot 'state.json'
$LocalStatePath = Join-Path $ScriptRoot 'state.local.json'
$CodexNotesPath = Join-Path $ScriptRoot 'CODEX_TRANSLATION_NOTES.md'
$GuidancePath = Join-Path $RepoRoot 'AGENTS.md'
$ManifestPath = Join-Path $RepoRoot 'docs\parity\manifest.md'
$ChecklistPath = Join-Path $RepoRoot 'docs\parity\maintenance-checklist.md'
$TraceabilityScriptPath = Join-Path $RepoRoot 'scripts\quality\validate-library-traceability.ps1'
$RunRoot = Join-Path $ScriptRoot 'runs'
$TriagePromptPath = Join-Path $ScriptRoot 'prompts\TriagePhase.md'
$ApplyPromptPath = Join-Path $ScriptRoot 'prompts\ApplyPhase.md'

Ensure-Tool 'git'
Ensure-Tool 'codex'

if (-not $PreviewOnly -and (-not $SkipBuild -or -not $SkipTests)) {
    Ensure-Tool 'dotnet'
}

if (-not $PreviewOnly -and (-not $SkipPush -or -not $SkipPr)) {
    Ensure-Tool 'gh'
}

function Test-HasProperty {
    param(
        [Parameter(Mandatory)]$Object,
        [Parameter(Mandatory)][string]$PropertyName
    )

    return $null -ne $Object -and $null -ne $Object.PSObject -and $null -ne $Object.PSObject.Properties[$PropertyName]
}

function Get-DefaultTrackedState {
    return [ordered]@{
        schemaVersion = 2
        lastSuccessUtc = $null
        sources = @(
            [ordered]@{
                key = 'python'
                displayName = 'openai-agents-python'
                repoUrl = 'https://github.com/openai/openai-agents-python.git'
                localPath = 'C:\src\openai\openai-agents-python'
                branch = 'main'
                role = 'behavioral-source-of-truth'
                lastAppliedSha = $null
                lastAppliedUtc = $null
            },
            [ordered]@{
                key = 'js'
                displayName = 'openai-agents-js'
                repoUrl = 'https://github.com/openai/openai-agents-js.git'
                localPath = 'C:\src\openai\openai-agents-js'
                branch = 'main'
                role = 'supporting-signal'
                lastAppliedSha = $null
                lastAppliedUtc = $null
            }
        )
    }
}

function New-DefaultLocalSourceState {
    param([Parameter(Mandatory)][string]$Key)

    return [ordered]@{
        key = $Key
        lastReviewedSha = $null
        lastReviewedUtc = $null
        lastAttemptedSha = $null
        lastAttemptedUtc = $null
        bootstrapSha = $null
    }
}

function Get-DefaultLocalState {
    param([Parameter(Mandatory)]$TrackedState)

    $sources = @()
    foreach ($source in @($TrackedState.sources)) {
        $sources += (New-DefaultLocalSourceState -Key $source.key)
    }

    return [ordered]@{
        schemaVersion = 2
        lastRunId = $null
        lastRunUtc = $null
        sources = $sources
    }
}

function Merge-TrackedSource {
    param(
        [Parameter(Mandatory)]$Existing,
        [Parameter(Mandatory)]$DefaultSource
    )

    $merged = [ordered]@{}
    foreach ($property in $DefaultSource.Keys) {
        if (Test-HasProperty -Object $Existing -PropertyName $property) {
            $value = $Existing.$property
            if ($null -ne $value -and "$value" -ne '') {
                $merged[$property] = $value
                continue
            }
        }

        $merged[$property] = $DefaultSource[$property]
    }

    return $merged
}

function Convert-TrackedState {
    param($RawState)

    $defaultState = Get-DefaultTrackedState
    if ($null -eq $RawState) {
        return $defaultState
    }

    if (Test-HasProperty -Object $RawState -PropertyName 'sources') {
        $mergedSources = @()
        foreach ($defaultSource in @($defaultState.sources)) {
            $existingSource = @($RawState.sources) | Where-Object { $_.key -eq $defaultSource.key } | Select-Object -First 1
            if ($null -eq $existingSource) {
                $mergedSources += $defaultSource
            } else {
                $mergedSources += (Merge-TrackedSource -Existing $existingSource -DefaultSource $defaultSource)
            }
        }

        return [ordered]@{
            schemaVersion = 2
            lastSuccessUtc = if (Test-HasProperty -Object $RawState -PropertyName 'lastSuccessUtc') { $RawState.lastSuccessUtc } else { $null }
            sources = $mergedSources
        }
    }

    if (Test-HasProperty -Object $RawState -PropertyName 'upstreamRepoUrl') {
        $converted = Get-DefaultTrackedState
        $python = $converted.sources[0]
        $python.repoUrl = $RawState.upstreamRepoUrl
        if (Test-HasProperty -Object $RawState -PropertyName 'upstreamLocalPath' -and $RawState.upstreamLocalPath) {
            $python.localPath = $RawState.upstreamLocalPath
        }
        if (Test-HasProperty -Object $RawState -PropertyName 'upstreamBranch' -and $RawState.upstreamBranch) {
            $python.branch = $RawState.upstreamBranch
        }
        if (Test-HasProperty -Object $RawState -PropertyName 'lastTranslatedSha' -and $RawState.lastTranslatedSha) {
            $python.lastAppliedSha = $RawState.lastTranslatedSha
        }
        if (Test-HasProperty -Object $RawState -PropertyName 'lastSuccessUtc') {
            $converted.lastSuccessUtc = $RawState.lastSuccessUtc
            $python.lastAppliedUtc = $RawState.lastSuccessUtc
        }

        return $converted
    }

    return $defaultState
}

function Convert-LocalState {
    param(
        $RawState,
        [Parameter(Mandatory)]$TrackedState
    )

    $defaultState = Get-DefaultLocalState -TrackedState $TrackedState
    if ($null -eq $RawState) {
        return $defaultState
    }

    if (Test-HasProperty -Object $RawState -PropertyName 'sources') {
        $mergedSources = @()
        foreach ($trackedSource in @($TrackedState.sources)) {
            $existingSource = @($RawState.sources) | Where-Object { $_.key -eq $trackedSource.key } | Select-Object -First 1
            if ($null -eq $existingSource) {
                $mergedSources += (New-DefaultLocalSourceState -Key $trackedSource.key)
                continue
            }

            $mergedSources += [ordered]@{
                key = $trackedSource.key
                lastReviewedSha = if (Test-HasProperty -Object $existingSource -PropertyName 'lastReviewedSha') { $existingSource.lastReviewedSha } else { $null }
                lastReviewedUtc = if (Test-HasProperty -Object $existingSource -PropertyName 'lastReviewedUtc') { $existingSource.lastReviewedUtc } else { $null }
                lastAttemptedSha = if (Test-HasProperty -Object $existingSource -PropertyName 'lastAttemptedSha') { $existingSource.lastAttemptedSha } else { $null }
                lastAttemptedUtc = if (Test-HasProperty -Object $existingSource -PropertyName 'lastAttemptedUtc') { $existingSource.lastAttemptedUtc } else { $null }
                bootstrapSha = if (Test-HasProperty -Object $existingSource -PropertyName 'bootstrapSha') { $existingSource.bootstrapSha } else { $null }
            }
        }

        return [ordered]@{
            schemaVersion = 2
            lastRunId = if (Test-HasProperty -Object $RawState -PropertyName 'lastRunId') { $RawState.lastRunId } else { $null }
            lastRunUtc = if (Test-HasProperty -Object $RawState -PropertyName 'lastRunUtc') { $RawState.lastRunUtc } else { $null }
            sources = $mergedSources
        }
    }

    $converted = Get-DefaultLocalState -TrackedState $TrackedState
    $python = $converted.sources[0]
    if (Test-HasProperty -Object $RawState -PropertyName 'lastAttemptedSha') {
        $python.lastAttemptedSha = $RawState.lastAttemptedSha
    }
    if (Test-HasProperty -Object $RawState -PropertyName 'lastRunUtc') {
        $converted.lastRunUtc = $RawState.lastRunUtc
        $python.lastAttemptedUtc = $RawState.lastRunUtc
    }
    if (Test-HasProperty -Object $RawState -PropertyName 'bootstrapLastTranslatedSha') {
        $python.bootstrapSha = $RawState.bootstrapLastTranslatedSha
        $python.lastReviewedSha = $RawState.bootstrapLastTranslatedSha
    }

    return $converted
}

function Initialize-State {
    $trackedRaw = Load-JsonFile -Path $TrackedStatePath -Default $null
    $tracked = Convert-TrackedState -RawState $trackedRaw
    Persist-TrackedState -State $tracked

    $localRaw = Load-JsonFile -Path $LocalStatePath -Default $null
    $local = Convert-LocalState -RawState $localRaw -TrackedState $tracked
    Persist-LocalState -State $local

    return @{
        tracked = $tracked
        local = $local
    }
}

function Persist-TrackedState {
    param([Parameter(Mandatory)]$State)
    Save-JsonFile -Path $TrackedStatePath -Value $State
}

function Persist-LocalState {
    param([Parameter(Mandatory)]$State)
    Save-JsonFile -Path $LocalStatePath -Value $State
}

function Get-SourceByKey {
    param(
        [Parameter(Mandatory)]$SourcesCollection,
        [Parameter(Mandatory)][string]$Key
    )

    return @($SourcesCollection) | Where-Object { $_.key -eq $Key } | Select-Object -First 1
}

function Get-SelectedSourceContexts {
    param(
        [Parameter(Mandatory)]$TrackedState,
        [Parameter(Mandatory)]$LocalState
    )

    $selectedKeys = @(
        if ($Sources -and $Sources.Count -gt 0) {
            @($Sources | Where-Object { $_ } | ForEach-Object { $_.Trim() } | Where-Object { $_ } | Select-Object -Unique)
        } else {
            @($TrackedState.sources | ForEach-Object { $_.key })
        }
    )

    if ($selectedKeys.Count -eq 0) {
        throw 'No source keys were selected.'
    }

    $contexts = @()
    foreach ($key in $selectedKeys) {
        $trackedSource = Get-SourceByKey -SourcesCollection $TrackedState.sources -Key $key
        if ($null -eq $trackedSource) {
            throw "Unknown source key '$key'."
        }

        $localSource = Get-SourceByKey -SourcesCollection $LocalState.sources -Key $key
        if ($null -eq $localSource) {
            $localSource = New-DefaultLocalSourceState -Key $key
            $LocalState.sources += $localSource
        }

        $contexts += [pscustomobject]@{
            key = $key
            tracked = $trackedSource
            local = $localSource
        }
    }

    return $contexts
}

function Ensure-UpstreamRepo {
    param([Parameter(Mandatory)]$SourceContext)

    $path = $SourceContext.tracked.localPath
    if (-not (Test-Path $path)) {
        throw "Upstream path '$path' for source '$($SourceContext.key)' does not exist."
    }

    Push-Location $path
    try {
        & git rev-parse --is-inside-work-tree > $null
        if ($LASTEXITCODE -ne 0) {
            throw "Path '$path' for source '$($SourceContext.key)' is not a Git worktree."
        }
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

    $dirty = @(
        @($status) | Where-Object {
        $_ -and
        $_ -notmatch 'tools/upstream-sync/state\.local\.json' -and
        $_ -notmatch 'tools/upstream-sync/runs/'
        }
    )

    if ($dirty.Count -gt 0) {
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
    param([Parameter(Mandatory)]$SourceContext)

    Push-Location $SourceContext.tracked.localPath
    try {
        Write-SyncLog 'INFO' "Fetching upstream changes for '$($SourceContext.key)'."
        & git fetch origin $SourceContext.tracked.branch
        $sha = (& git rev-parse "origin/$($SourceContext.tracked.branch)").Trim()
        return $sha
    } finally {
        Pop-Location
    }
}

function Get-CommitLines {
    param(
        [Parameter(Mandatory)]$SourceContext,
        [Parameter(Mandatory)][string]$FromSha,
        [Parameter(Mandatory)][string]$ToSha
    )

    Push-Location $SourceContext.tracked.localPath
    try {
        return & git log --oneline "$FromSha..$ToSha"
    } finally {
        Pop-Location
    }
}

function Get-UpstreamDiff {
    param(
        [Parameter(Mandatory)]$SourceContext,
        [Parameter(Mandatory)][string]$FromSha,
        [Parameter(Mandatory)][string]$ToSha
    )

    Push-Location $SourceContext.tracked.localPath
    try {
        return & git diff "$FromSha..$ToSha"
    } finally {
        Pop-Location
    }
}

function Get-ChangedFiles {
    param(
        [Parameter(Mandatory)]$SourceContext,
        [Parameter(Mandatory)][string]$FromSha,
        [Parameter(Mandatory)][string]$ToSha
    )

    Push-Location $SourceContext.tracked.localPath
    try {
        return & git diff --name-only "$FromSha..$ToSha"
    } finally {
        Pop-Location
    }
}

function New-RunId {
    return (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss')
}

function Get-RelativeRepoPath {
    param([Parameter(Mandatory)][string]$FullPath)

    $repoPath = [System.IO.Path]::GetFullPath($RepoRoot)
    $targetPath = [System.IO.Path]::GetFullPath($FullPath)

    if (-not $repoPath.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $repoPath += [System.IO.Path]::DirectorySeparatorChar
    }

    $repoUri = New-Object System.Uri($repoPath)
    $targetUri = New-Object System.Uri($targetPath)
    $relativePath = $repoUri.MakeRelativeUri($targetUri).ToString()
    return [System.Uri]::UnescapeDataString($relativePath).Replace('\', '/')
}

function Write-RunArtifacts {
    param(
        [Parameter(Mandatory)][string]$RunId,
        [Parameter(Mandatory)]$SourceDeltas
    )

    $runDirectory = Join-Path $RunRoot $RunId
    $sourcesDirectory = Join-Path $runDirectory 'sources'

    New-Item -ItemType Directory -Path $sourcesDirectory -Force | Out-Null

    $manifest = [ordered]@{
        runId = $RunId
        createdUtc = (Get-Date).ToUniversalTime().ToString('o')
        sources = @()
    }

    $summaryLines = New-Object System.Collections.Generic.List[string]
    $summaryLines.Add("# Upstream Sync Run $RunId")
    $summaryLines.Add('')
    $summaryLines.Add('| Source | Role | Base | Latest |')
    $summaryLines.Add('| --- | --- | --- | --- |')

    foreach ($delta in @($SourceDeltas)) {
        $sourceDirectory = Join-Path $sourcesDirectory $delta.key
        New-Item -ItemType Directory -Path $sourceDirectory -Force | Out-Null

        $commitsPath = Join-Path $sourceDirectory 'commits.txt'
        $filesPath = Join-Path $sourceDirectory 'files.txt'
        $diffPath = Join-Path $sourceDirectory 'diff.patch'

        @($delta.commitLines) | Set-Content -Path $commitsPath -Encoding UTF8
        @($delta.changedFiles) | Set-Content -Path $filesPath -Encoding UTF8
        @($delta.diffLines) | Set-Content -Path $diffPath -Encoding UTF8

        $manifest.sources += [ordered]@{
            key = $delta.key
            displayName = $delta.displayName
            role = $delta.role
            repoUrl = $delta.repoUrl
            localPath = $delta.localPath
            branch = $delta.branch
            baseSha = $delta.baseSha
            latestSha = $delta.latestSha
            commitsPath = Get-RelativeRepoPath -FullPath $commitsPath
            filesPath = Get-RelativeRepoPath -FullPath $filesPath
            diffPath = Get-RelativeRepoPath -FullPath $diffPath
        }

        $summaryLines.Add("| $($delta.key) | $($delta.role) | $($delta.baseSha.Substring(0, 7)) | $($delta.latestSha.Substring(0, 7)) |")
    }

    $summaryLines.Add('')

    foreach ($delta in @($SourceDeltas)) {
        $sourceDirectory = Join-Path $sourcesDirectory $delta.key
        $summaryLines.Add("## $($delta.displayName) ($($delta.key))")
        $summaryLines.Add(("- Role: {0}" -f $delta.role))
        $summaryLines.Add(("- Repository: {0}" -f $delta.repoUrl))
        $summaryLines.Add(("- Local path: {0}" -f $delta.localPath))
        $summaryLines.Add(("- Branch: {0}" -f $delta.branch))
        $summaryLines.Add(("- Range: {0}..{1}" -f $delta.baseSha, $delta.latestSha))
        $summaryLines.Add(("- Commit list: [sources/{0}/commits.txt](sources/{0}/commits.txt)" -f $delta.key))
        $summaryLines.Add(("- Changed files: [sources/{0}/files.txt](sources/{0}/files.txt)" -f $delta.key))
        $summaryLines.Add(("- Full diff: [sources/{0}/diff.patch](sources/{0}/diff.patch)" -f $delta.key))
        $summaryLines.Add('')
    }

    $manifestPath = Join-Path $runDirectory 'manifest.json'
    $summaryPath = Join-Path $runDirectory 'summary.md'
    Save-JsonFile -Path $manifestPath -Value $manifest
    $summaryLines | Set-Content -Path $summaryPath -Encoding UTF8

    return [pscustomobject]@{
        runDirectory = $runDirectory
        manifestPath = $manifestPath
        summaryPath = $summaryPath
        triageJsonPath = Join-Path $runDirectory 'triage.json'
        triageMarkdownPath = Join-Path $runDirectory 'triage.md'
        triageResponsePath = Join-Path $runDirectory 'triage-response.txt'
        applyResponsePath = Join-Path $runDirectory 'apply-response.txt'
    }
}

function Build-PhasePrompt {
    param(
        [Parameter(Mandatory)][string]$TemplatePath,
        [Parameter(Mandatory)]$RunArtifacts
    )

    if (-not (Test-Path $TemplatePath)) {
        throw "Prompt template was not found: $TemplatePath"
    }

    $prompt = Get-Content $TemplatePath -Raw
    $replacements = [ordered]@{
        '{{RUN_ID}}' = Split-Path -Leaf $RunArtifacts.runDirectory
        '{{RUN_SUMMARY_PATH}}' = Get-RelativeRepoPath -FullPath $RunArtifacts.summaryPath
        '{{TRIAGE_JSON_PATH}}' = Get-RelativeRepoPath -FullPath $RunArtifacts.triageJsonPath
        '{{TRIAGE_MD_PATH}}' = Get-RelativeRepoPath -FullPath $RunArtifacts.triageMarkdownPath
    }

    foreach ($entry in $replacements.GetEnumerator()) {
        $prompt = $prompt.Replace($entry.Key, $entry.Value)
    }

    return $prompt
}

function Invoke-CodexPhase {
    param(
        [Parameter(Mandatory)][string]$PhaseName,
        [Parameter(Mandatory)][string]$PromptContent,
        [Parameter(Mandatory)][string]$OutputPath,
        [Parameter(Mandatory)]$SourceContexts
    )

    $codexArgs = @(
        'exec',
        '--dangerously-bypass-approvals-and-sandbox',
        '-C',
        $RepoRoot,
        '-o',
        $OutputPath
    )

    foreach ($sourceContext in @($SourceContexts)) {
        $codexArgs += @('--add-dir', $sourceContext.tracked.localPath)
    }

    $codexArgs += '-'

    Write-SyncLog 'INFO' "Invoking Codex for phase '$PhaseName'."
    $null = $PromptContent | & codex @codexArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Codex phase '$PhaseName' failed with exit code $LASTEXITCODE."
    }
}

function Read-TriagePacket {
    param(
        [Parameter(Mandatory)]$RunArtifacts,
        [Parameter(Mandatory)]$SelectedSourceContexts
    )

    if (-not (Test-Path $RunArtifacts.triageJsonPath)) {
        throw "Triage JSON was not produced at '$($RunArtifacts.triageJsonPath)'."
    }

    $triage = Get-Content $RunArtifacts.triageJsonPath -Raw | ConvertFrom-Json
    if ($triage.action -notin @('apply', 'no-op')) {
        throw "Triage action '$($triage.action)' is invalid."
    }

    $selectedKeys = @($SelectedSourceContexts | ForEach-Object { $_.key })
    $includedSources = @($triage.includedSources)
    $excludedSources = @($triage.excludedSources)

    foreach ($sourceKey in @($includedSources + $excludedSources)) {
        if ($selectedKeys -notcontains $sourceKey) {
            throw "Triage referenced source '$sourceKey' which was not selected for this run."
        }
    }

    if ($triage.action -eq 'no-op' -and $includedSources.Count -gt 0) {
        throw 'Triage action no-op must not include sources that require tracked repo changes.'
    }

    if ($triage.action -eq 'apply' -and $includedSources.Count -eq 0) {
        throw 'Triage action apply must include at least one source key.'
    }

    return $triage
}

function Run-DotnetCommand {
    param([Parameter(Mandatory)][string[]]$Arguments)

    Write-SyncLog 'INFO' "Running dotnet $($Arguments -join ' ')."
    Push-Location $RepoRoot
    try {
        & dotnet @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
        }
    } finally {
        Pop-Location
    }
}

function Run-TraceabilityValidation {
    Write-SyncLog 'INFO' 'Running library traceability validation.'
    Push-Location $RepoRoot
    try {
        & pwsh -NoProfile -File $TraceabilityScriptPath
        if ($LASTEXITCODE -ne 0) {
            throw "Traceability validation failed with exit code $LASTEXITCODE."
        }
    } finally {
        Pop-Location
    }
}

function Get-MeaningfulStatusLines {
    Push-Location $RepoRoot
    try {
        $status = & git status --porcelain --untracked-files=all
    } finally {
        Pop-Location
    }

    return @(
        @($status) | Where-Object {
            $_ -and
            $_ -notmatch 'tools/upstream-sync/state\.local\.json' -and
            $_ -notmatch 'tools/upstream-sync/runs/'
        }
    )
}

function Create-SyncBranch {
    param([Parameter(Mandatory)][string]$RunId)

    $branchName = "sync/upstream-$RunId"
    Push-Location $RepoRoot
    try {
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

function Get-SourceSummarySuffix {
    param(
        [Parameter(Mandatory)]$SelectedSourceContexts,
        [Parameter(Mandatory)]$LatestByKey
    )

    $parts = foreach ($context in @($SelectedSourceContexts)) {
        $sha = $LatestByKey[$context.key]
        "$($context.key) $($sha.Substring(0, 7))"
    }

    return ($parts -join ', ')
}

function Commit-Changes {
    param(
        [Parameter(Mandatory)][string]$BranchName,
        [Parameter(Mandatory)][string]$Message
    )

    Push-Location $RepoRoot
    try {
        & git add -A
        & git commit -m $Message
        if ($LASTEXITCODE -ne 0) {
            throw "git commit failed with exit code $LASTEXITCODE."
        }
    } finally {
        Pop-Location
    }
}

function Push-Branch {
    param([Parameter(Mandatory)][string]$BranchName)

    Write-SyncLog 'INFO' "Pushing branch '$BranchName' to origin."
    Push-Location $RepoRoot
    try {
        & git push -u origin $BranchName
        if ($LASTEXITCODE -ne 0) {
            throw "git push failed with exit code $LASTEXITCODE."
        }
    } finally {
        Pop-Location
    }
}

function Create-PullRequest {
    param(
        [Parameter(Mandatory)][string]$BranchName,
        [Parameter(Mandatory)]$SourceDeltas,
        [Parameter(Mandatory)]$Triage
    )

    $title = "Sync upstream agents: $($Triage.summary)"
    $bodyLines = New-Object System.Collections.Generic.List[string]
    $bodyLines.Add("Summary: $($Triage.summary)")
    $bodyLines.Add('')

    foreach ($delta in @($SourceDeltas)) {
        $bodyLines.Add("## $($delta.displayName) ($($delta.key))")
        $bodyLines.Add("- Role: $($delta.role)")
        $bodyLines.Add("- Range: $($delta.baseSha)..$($delta.latestSha)")
        $bodyLines.Add("- Commits translated:")
        foreach ($commitLine in @($delta.commitLines)) {
            $bodyLines.Add("  - $commitLine")
        }
        $bodyLines.Add('')
    }

    $bodyLines.Add('This PR was generated by the local upstream sync automation.')

    $tempBodyPath = [System.IO.Path]::GetTempFileName()
    try {
        $bodyLines | Set-Content -Path $tempBodyPath -Encoding UTF8
        Push-Location $RepoRoot
        try {
            & gh pr create --title $title --body-file $tempBodyPath --base main --head $BranchName
            if ($LASTEXITCODE -ne 0) {
                throw "gh pr create failed with exit code $LASTEXITCODE."
            }
        } finally {
            Pop-Location
        }
    } finally {
        Remove-Item $tempBodyPath -ErrorAction SilentlyContinue
    }
}

function Update-LocalReviewState {
    param(
        [Parameter(Mandatory)]$LocalState,
        [Parameter(Mandatory)]$SelectedSourceContexts,
        [Parameter(Mandatory)]$LatestByKey,
        [Parameter(Mandatory)][string]$RunId
    )

    $timestamp = (Get-Date).ToUniversalTime().ToString('o')
    foreach ($context in @($SelectedSourceContexts)) {
        $context.local.lastReviewedSha = $LatestByKey[$context.key]
        $context.local.lastReviewedUtc = $timestamp
        $context.local.lastAttemptedSha = $LatestByKey[$context.key]
        $context.local.lastAttemptedUtc = $timestamp
    }

    $LocalState.lastRunId = $RunId
    $LocalState.lastRunUtc = $timestamp
}

function Update-AttemptState {
    param(
        [Parameter(Mandatory)]$LocalState,
        [Parameter(Mandatory)]$SelectedSourceContexts,
        [Parameter(Mandatory)]$LatestByKey
    )

    $timestamp = (Get-Date).ToUniversalTime().ToString('o')
    foreach ($context in @($SelectedSourceContexts)) {
        $context.local.lastAttemptedSha = $LatestByKey[$context.key]
        $context.local.lastAttemptedUtc = $timestamp
    }

    $LocalState.lastRunUtc = $timestamp
}

function Update-AppliedState {
    param(
        [Parameter(Mandatory)]$TrackedState,
        [Parameter(Mandatory)]$Triage,
        [Parameter(Mandatory)]$LatestByKey
    )

    $timestamp = (Get-Date).ToUniversalTime().ToString('o')
    foreach ($sourceKey in @($Triage.includedSources)) {
        $source = Get-SourceByKey -SourcesCollection $TrackedState.sources -Key $sourceKey
        if ($null -eq $source) {
            throw "Tracked state is missing source '$sourceKey'."
        }

        $source.lastAppliedSha = $LatestByKey[$sourceKey]
        $source.lastAppliedUtc = $timestamp
    }

    $TrackedState.lastSuccessUtc = $timestamp
}

function Bootstrap-MissingReviewBaselines {
    param(
        [Parameter(Mandatory)]$SelectedSourceContexts,
        [Parameter(Mandatory)]$LatestByKey
    )

    $bootstrappedKeys = @()
    $timestamp = (Get-Date).ToUniversalTime().ToString('o')
    foreach ($context in @($SelectedSourceContexts)) {
        if ($context.local.lastReviewedSha) {
            continue
        }

        $latestSha = $LatestByKey[$context.key]
        $context.local.bootstrapSha = $latestSha
        $context.local.lastReviewedSha = $latestSha
        $context.local.lastReviewedUtc = $timestamp
        $context.local.lastAttemptedSha = $latestSha
        $context.local.lastAttemptedUtc = $timestamp
        $bootstrappedKeys += $context.key
    }

    return $bootstrappedKeys
}

function Run-SyncCycle {
    $trackedState = $script:stateBundle.tracked
    $localState = $script:stateBundle.local
    $selectedSourceContexts = @(Get-SelectedSourceContexts -TrackedState $trackedState -LocalState $localState)

    if ($ForceFromSha -and $selectedSourceContexts.Count -ne 1) {
        throw '-ForceFromSha can only be used when exactly one source is selected.'
    }

    foreach ($context in @($selectedSourceContexts)) {
        Ensure-UpstreamRepo -SourceContext $context
    }

    Ensure-WorktreeClean
    Reset-WorkingMain

    $latestByKey = @{}
    foreach ($context in @($selectedSourceContexts)) {
        $latestByKey[$context.key] = Get-UpstreamLatestSha -SourceContext $context
    }

    $bootstrappedKeys = @(Bootstrap-MissingReviewBaselines -SelectedSourceContexts $selectedSourceContexts -LatestByKey $latestByKey)
    if ($bootstrappedKeys.Count -gt 0 -and -not $ForceFromSha) {
        Persist-LocalState -State $localState
        Write-SyncLog 'INFO' ("Bootstrapped local review baselines for: {0}. Re-run the sync to translate future commits." -f ($bootstrappedKeys -join ', '))

        $remainingContexts = @($selectedSourceContexts | Where-Object { $bootstrappedKeys -notcontains $_.key })
        if ($remainingContexts.Count -eq 0) {
            return
        }

        $selectedSourceContexts = $remainingContexts
    }

    $sourceDeltas = @()
    foreach ($context in @($selectedSourceContexts)) {
        $baseSha = if ($ForceFromSha) { $ForceFromSha } else { $context.local.lastReviewedSha }
        $latestSha = $latestByKey[$context.key]

        if (-not $baseSha) {
            throw "Source '$($context.key)' does not have a review baseline."
        }

        if ($baseSha -eq $latestSha) {
            continue
        }

        $sourceDeltas += [pscustomobject]@{
            key = $context.key
            displayName = $context.tracked.displayName
            role = $context.tracked.role
            repoUrl = $context.tracked.repoUrl
            localPath = $context.tracked.localPath
            branch = $context.tracked.branch
            baseSha = $baseSha
            latestSha = $latestSha
            commitLines = @(Get-CommitLines -SourceContext $context -FromSha $baseSha -ToSha $latestSha)
            changedFiles = @(Get-ChangedFiles -SourceContext $context -FromSha $baseSha -ToSha $latestSha)
            diffLines = @(Get-UpstreamDiff -SourceContext $context -FromSha $baseSha -ToSha $latestSha)
        }
    }

    if ($sourceDeltas.Count -eq 0) {
        Update-AttemptState -LocalState $localState -SelectedSourceContexts $selectedSourceContexts -LatestByKey $latestByKey
        Persist-LocalState -State $localState
        Write-SyncLog 'INFO' 'No new upstream commits detected for the selected sources.'
        return
    }

    $runId = New-RunId
    $runArtifacts = Write-RunArtifacts -RunId $runId -SourceDeltas $sourceDeltas

    $triagePrompt = Build-PhasePrompt -TemplatePath $TriagePromptPath -RunArtifacts $runArtifacts
    Invoke-CodexPhase -PhaseName 'triage' -PromptContent $triagePrompt -OutputPath $runArtifacts.triageResponsePath -SourceContexts $selectedSourceContexts
    $triage = Read-TriagePacket -RunArtifacts $runArtifacts -SelectedSourceContexts $selectedSourceContexts

    if ($PreviewOnly) {
        Write-SyncLog 'INFO' "Preview mode finished. Review the run packet in '$($runArtifacts.runDirectory)'."
        return
    }

    if ($triage.action -eq 'no-op') {
        Update-LocalReviewState -LocalState $localState -SelectedSourceContexts $selectedSourceContexts -LatestByKey $latestByKey -RunId $runId
        Persist-LocalState -State $localState
        Write-SyncLog 'INFO' 'Triage concluded that the upstream deltas are outside the included surface. Local review baselines were advanced.'
        return
    }

    $applyPrompt = Build-PhasePrompt -TemplatePath $ApplyPromptPath -RunArtifacts $runArtifacts
    Invoke-CodexPhase -PhaseName 'apply' -PromptContent $applyPrompt -OutputPath $runArtifacts.applyResponsePath -SourceContexts $selectedSourceContexts

    $meaningfulStatus = @(Get-MeaningfulStatusLines)
    if ($meaningfulStatus.Count -eq 0) {
        throw 'Apply phase completed without producing tracked repository changes.'
    }

    if (-not $SkipBuild) {
        Run-DotnetCommand -Arguments @('build')
    } else {
        Write-SyncLog 'INFO' 'Skipping dotnet build.'
    }

    if (-not $SkipTests) {
        Run-DotnetCommand -Arguments @('test')
    } else {
        Write-SyncLog 'INFO' 'Skipping dotnet test.'
    }

    if (-not $SkipTraceability) {
        Run-TraceabilityValidation
    } else {
        Write-SyncLog 'INFO' 'Skipping library traceability validation.'
    }

    Update-AppliedState -TrackedState $trackedState -Triage $triage -LatestByKey $latestByKey
    Persist-TrackedState -State $trackedState

    Update-LocalReviewState -LocalState $localState -SelectedSourceContexts $selectedSourceContexts -LatestByKey $latestByKey -RunId $runId
    Persist-LocalState -State $localState

    $branchName = Create-SyncBranch -RunId $runId
    $commitMessage = "Sync upstream agents through $(Get-SourceSummarySuffix -SelectedSourceContexts $selectedSourceContexts -LatestByKey $latestByKey)"
    Commit-Changes -BranchName $branchName -Message $commitMessage

    if (-not $SkipPush) {
        Push-Branch -BranchName $branchName
        if (-not $SkipPr) {
            Create-PullRequest -BranchName $branchName -SourceDeltas $sourceDeltas -Triage $triage
        } else {
            Write-SyncLog 'INFO' 'Skipping PR creation per -SkipPr.'
        }
    } else {
        Write-SyncLog 'INFO' 'Skipping push (and PR) per -SkipPush.'
    }

    Push-Location $RepoRoot
    try {
        & git checkout main
    } finally {
        Pop-Location
    }
}

$script:stateBundle = Initialize-State

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
