<#
.SYNOPSIS
    Writes the documentation site, or a pull request's preview of it, to the gh-pages branch as a single commit.

.DESCRIPTION
    Every write replaces the branch's one commit, so gh-pages has no history. That matters because the build's jobs
    fetch every branch (the shared pipeline checks out with fetch-depth 0), and a branch that gains a copy of the
    whole site for every preview and every deployment grew to gigabytes: those fetches took longer each week and
    then began to stall for an hour.

    Previews of pull requests that are no longer open are removed on every write, whichever mode runs, so a missed
    clean-up does not leave a copy of the site behind.

    Run it from a checkout of the repository whose 'origin' can push (actions/checkout leaves it so).

.PARAMETER Mode
    Site replaces the site at the root of the branch and keeps the previews. Preview replaces one pull request's
    preview. RemovePreview removes one pull request's preview.

.PARAMETER Source
    The directory holding the built site (Site and Preview).

.PARAMETER PullRequest
    The pull request number (Preview and RemovePreview).

.PARAMETER OpenPullRequests
    The numbers of the open pull requests, as text separated by commas or spaces (an empty string for none).
    Previews of any other pull request are removed. Omit the parameter to remove none. It is text because a list
    passed to 'pwsh -File' arrives as one string, which PowerShell would otherwise read as a single number.

.PARAMETER Branch
    The branch to write. gh-pages.

.PARAMETER PreviewDirectory
    The directory of the branch that holds the previews, one 'pr-<number>' directory each.

.PARAMETER MaxAttempts
    Another job may write the branch at the same time. A push that finds the branch changed starts again from the
    new tip, up to this many times.
#>
[CmdletBinding()]
param (
    [Parameter(Mandatory)][ValidateSet('Site', 'Preview', 'RemovePreview')][string] $Mode,
    [string] $Source,
    [int] $PullRequest,
    [AllowEmptyString()][string] $OpenPullRequests,
    [string] $Branch = 'gh-pages',
    [string] $PreviewDirectory = 'pr-preview',
    [string] $Remote = 'origin',
    [int] $MaxAttempts = 6
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Mode -ne 'RemovePreview') {
    if (!$Source -or !(Test-Path $Source -PathType Container)) { throw "The site directory '$Source' does not exist." }
    $Source = (Resolve-Path $Source).Path
    if (@(Get-ChildItem -Force $Source).Count -eq 0) { throw "The site directory '$Source' is empty." }
}
if ($Mode -ne 'Site' -and $PullRequest -le 0) { throw "Mode $Mode needs -PullRequest." }
if ($PSBoundParameters.ContainsKey('OpenPullRequests') -and $OpenPullRequests -notmatch '^[\d,\s]*$') {
    throw "-OpenPullRequests must be numbers separated by commas or spaces, not '$OpenPullRequests'."
}

function Invoke-Git {
    param ([Parameter(ValueFromRemainingArguments)][string[]] $Arguments)
    $output = & git @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') failed ($LASTEXITCODE): $($output -join "`n")" }
    return $output
}

$identity = @('-c', 'user.name=github-actions[bot]', '-c', 'user.email=41898282+github-actions[bot]@users.noreply.github.com')
$message = switch ($Mode) {
    'Site' { 'Deploy the documentation site' }
    'Preview' { "Deploy the preview of pull request $PullRequest" }
    'RemovePreview' { "Remove the preview of pull request $PullRequest" }
}

for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
    $worktree = Join-Path ([IO.Path]::GetTempPath()) "gh-pages-$([Guid]::NewGuid().ToString('N'))"
    try {
        # Only the tip: one commit, and nothing of whatever history the branch still has.
        & git fetch --quiet --depth=1 $Remote "+refs/heads/${Branch}:refs/remotes/$Remote/$Branch" 2>&1 | Out-Null
        $exists = $LASTEXITCODE -eq 0
        $tip = if ($exists) { (Invoke-Git rev-parse "refs/remotes/$Remote/$Branch").Trim() } else { $null }

        if ($exists) {
            Invoke-Git worktree add --quiet --detach $worktree $tip | Out-Null
        }
        else {
            Invoke-Git worktree add --quiet --detach $worktree HEAD | Out-Null
            Get-ChildItem -Force $worktree | Where-Object Name -ne '.git' | Remove-Item -Recurse -Force
        }

        $previews = Join-Path $worktree $PreviewDirectory
        switch ($Mode) {
            'Site' {
                Get-ChildItem -Force $worktree | Where-Object { $_.Name -ne '.git' -and $_.Name -ne $PreviewDirectory } | Remove-Item -Recurse -Force
                Copy-Item -Path (Join-Path $Source '*') -Destination $worktree -Recurse -Force
                Get-ChildItem -Force $Source -Filter '.*' -File | Copy-Item -Destination $worktree -Force
            }
            'Preview' {
                $target = Join-Path $previews "pr-$PullRequest"
                if (Test-Path $target) { Remove-Item -Recurse -Force $target }
                New-Item -ItemType Directory -Force $target | Out-Null
                Copy-Item -Path (Join-Path $Source '*') -Destination $target -Recurse -Force
                Get-ChildItem -Force $Source -Filter '.*' -File | Copy-Item -Destination $target -Force
            }
            'RemovePreview' {
                $target = Join-Path $previews "pr-$PullRequest"
                if (Test-Path $target) { Remove-Item -Recurse -Force $target }
            }
        }

        if ($PSBoundParameters.ContainsKey('OpenPullRequests') -and (Test-Path $previews)) {
            $keep = @([regex]::Matches($OpenPullRequests, '\d+') | ForEach-Object { [int]$_.Value })
            if ($Mode -eq 'Preview') { $keep += $PullRequest }
            foreach ($directory in Get-ChildItem -Directory $previews) {
                if ($directory.Name -match '^pr-(\d+)$' -and [int]$Matches[1] -notin $keep) {
                    Write-Host "Removing the preview of pull request $($Matches[1]), which is not open"
                    Remove-Item -Recurse -Force $directory.FullName
                }
            }
        }
        if ((Test-Path $previews) -and @(Get-ChildItem -Force $previews).Count -eq 0) { Remove-Item -Force $previews }

        # GitHub Pages must serve the files as they are.
        New-Item -ItemType File -Force (Join-Path $worktree '.nojekyll') | Out-Null

        Invoke-Git -C $worktree add --all | Out-Null
        $tree = (Invoke-Git -C $worktree write-tree).Trim()
        if ($exists) {
            $tipTree = (Invoke-Git rev-parse "$tip^{tree}").Trim()
            $tipParents = @((Invoke-Git rev-list --parents -n 1 $tip).Trim() -split ' ').Count - 1
            $shallow = Test-Path (Join-Path (Invoke-Git rev-parse --git-common-dir).Trim() 'shallow')
            # A shallow fetch hides the tip's parents, so only a branch read in full can be known to be one commit.
            if ($tree -eq $tipTree -and $tipParents -eq 0 -and !$shallow) {
                Write-Host "$Branch already holds this content as a single commit."
                return
            }
        }

        # A commit with no parent: the branch is this one commit.
        $commit = (Invoke-Git @identity -C $worktree commit-tree $tree -m $message).Trim()
        $lease = if ($exists) { "--force-with-lease=refs/heads/${Branch}:$tip" } else { "--force-with-lease=refs/heads/${Branch}:" }
        & git push --quiet $lease $Remote "${commit}:refs/heads/$Branch" 2>&1 | Write-Host
        if ($LASTEXITCODE -eq 0) {
            Write-Host "$message ($commit), as the only commit of $Branch."
            return
        }
        Write-Host "The push was refused (attempt $attempt of $MaxAttempts): $Branch moved, or the push failed. Starting again from its new tip."
    }
    finally {
        if (Test-Path $worktree) {
            & git worktree remove --force $worktree 2>&1 | Out-Null
            if (Test-Path $worktree) { Remove-Item -Recurse -Force $worktree }
            & git worktree prune 2>&1 | Out-Null
        }
    }
    Start-Sleep -Seconds (5 * $attempt)
}

throw "Could not write $Branch after $MaxAttempts attempts."
