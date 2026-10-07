<#
.SYNOPSIS
    Writes the one comment on a pull request that says where its documentation preview is, replacing it if it exists.

.PARAMETER OnlyIfExists
    Change the comment if there is one, and otherwise leave the pull request without one.
#>
[CmdletBinding()]
param (
    [Parameter(Mandatory)][int] $PullRequest,
    [Parameter(Mandatory)][string] $Body,
    [string] $Repository = $env:GITHUB_REPOSITORY,
    [switch] $OnlyIfExists
)

$ErrorActionPreference = 'Stop'

# The comment is found again by this marker, which GitHub does not show.
$marker = '<!-- corvus-documentation-preview -->'
$text = "$marker`n$Body"

$ids = @(gh api "repos/$Repository/issues/$PullRequest/comments" --paginate --jq ".[] | select(.body | startswith(`"$marker`")) | .id")
if ($LASTEXITCODE -ne 0) { throw "Could not read the comments of pull request $PullRequest." }

if ($ids.Count -gt 0) {
    gh api --method PATCH "repos/$Repository/issues/comments/$($ids[0])" -f "body=$text" --silent
}
elseif (!$OnlyIfExists) {
    gh api --method POST "repos/$Repository/issues/$PullRequest/comments" -f "body=$text" --silent
}
if ($LASTEXITCODE -ne 0) { throw "Could not write the preview comment on pull request $PullRequest." }
