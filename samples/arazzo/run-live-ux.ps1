# Runs the live UX suite (web/arazzo-control-plane-ui test/live) against the demo composition,
# booting the Aspire AppHost first if nothing is already listening, and tearing down only what it started.
#
#   pwsh samples/arazzo/run-live-ux.ps1            # reuse a running composition, or boot one and tear it down after
#   pwsh samples/arazzo/run-live-ux.ps1 -Build     # dotnet build the demo slnx first (otherwise --no-build assumes a prior build)
#   pwsh samples/arazzo/run-live-ux.ps1 -KeepAlive # leave a composition this script booted running for follow-up work
#
# The composition reseeds on every boot (reset-each-run), so a fresh boot always matches the suite's seed expectations.
# Teardown is SIGTERM + wait: the AppHost must unwind its containers itself; it is never SIGKILLed.

[CmdletBinding()]
param(
    [switch]$Build,
    [switch]$KeepAlive,
    [int]$ReadyTimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'

$sampleRoot = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $sampleRoot '../..')).Path
$appHostDir = Join-Path $sampleRoot 'Corvus.Text.Json.Arazzo.ControlPlane.Demo.AppHost'
$uiDir = Join-Path $repoRoot 'web/arazzo-control-plane-ui'
# Same scheme the live suite's baseURL uses (playwright.config.mjs: http://localhost:8090).
$controlPlaneHealth = 'http://localhost:8090/health'

function Test-ControlPlaneHealthy {
    try {
        $response = Invoke-WebRequest -Uri $controlPlaneHealth -SkipCertificateCheck -TimeoutSec 3 -ErrorAction Stop
        return $response.StatusCode -eq 200
    }
    catch {
        return $false
    }
}

if ($Build) {
    Write-Host '>> Building the demo composition (Corvus.Text.Json.Arazzo.ControlPlane.Demo.slnx)...'
    dotnet build (Join-Path $sampleRoot 'Corvus.Text.Json.Arazzo.ControlPlane.Demo.slnx') -c Debug
    if ($LASTEXITCODE -ne 0) {
        throw "The demo build failed (exit $LASTEXITCODE)."
    }
}

$appHostPid = $null
$bootedHere = $false
$ready = $false

if (Test-ControlPlaneHealthy) {
    Write-Host ">> Control plane already healthy at $controlPlaneHealth - reusing the running composition."
    $ready = $true
}
else {
    # aspire start DETACHES: it spawns the AppHost, prints its PID, and returns - so the AppHost pid comes from
    # parsing that output (after stripping ANSI colour and OSC 8 hyperlink escapes), not from the CLI process.
    Write-Host '>> Booting the Aspire composition (aspire start --no-build --isolated --non-interactive)...'
    Push-Location $appHostDir
    try {
        $startOutput = (& aspire start --no-build --isolated --non-interactive 2>&1) -join "`n"
    }
    finally {
        Pop-Location
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Host $startOutput
        throw "aspire start failed (exit $LASTEXITCODE)."
    }

    $esc = [char]27
    $plain = $startOutput -replace "$esc\][^$esc\a]*(\a|$esc\\)", '' -replace "$esc\[[0-9;]*m", ''
    if ($plain -notmatch 'PID:\s*(\d+)') {
        Write-Host $startOutput
        throw 'aspire start returned success but its output carried no AppHost PID.'
    }

    $appHostPid = [int]$Matches[1]
    $bootedHere = $true
    Write-Host ">> AppHost detached as pid $appHostPid."

    Write-Host ">> Waiting up to $ReadyTimeoutSeconds s for $controlPlaneHealth (a cold boot takes ~170 s)..."
    $deadline = [DateTime]::UtcNow.AddSeconds($ReadyTimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (-not (Get-Process -Id $appHostPid -ErrorAction SilentlyContinue)) {
            throw "The AppHost (pid $appHostPid) exited before the control plane became healthy."
        }

        if (Test-ControlPlaneHealthy) {
            $ready = $true
            break
        }

        Start-Sleep -Seconds 5
    }

    if (-not $ready) {
        Write-Warning "The control plane did not become healthy within $ReadyTimeoutSeconds s; tearing down."
    }
}

$suiteExit = 1
try {
    if ($bootedHere -and -not $ready) {
        throw 'Composition never became healthy; the suite was not run.'
    }

    Write-Host '>> Running the live UX suite (npm run test:live)...'
    Push-Location $uiDir
    try {
        npm run test:live
        $suiteExit = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
}
finally {
    $appHostAlive = $null -ne $appHostPid -and (Get-Process -Id $appHostPid -ErrorAction SilentlyContinue)
    if ($bootedHere -and -not $KeepAlive -and $appHostAlive) {
        # SIGTERM lets the AppHost unwind its containers; SIGKILL would strand them (see the aspire-on-podman notes).
        Write-Host ">> Stopping the composition this script booted (SIGTERM $appHostPid + wait)..."
        /bin/kill -TERM $appHostPid
        $stopDeadline = [DateTime]::UtcNow.AddSeconds(120)
        while ([DateTime]::UtcNow -lt $stopDeadline -and (Get-Process -Id $appHostPid -ErrorAction SilentlyContinue)) {
            Start-Sleep -Seconds 3
        }

        if (Get-Process -Id $appHostPid -ErrorAction SilentlyContinue) {
            Write-Warning "The AppHost (pid $appHostPid) is still unwinding after 120 s; leaving it to finish - do not SIGKILL it."
        }
        else {
            Write-Host '>> Composition stopped.'
        }
    }
    elseif ($bootedHere -and $KeepAlive) {
        Write-Host ">> Leaving the composition running (pid $appHostPid); SIGTERM it when done."
    }
}

if ($suiteExit -ne 0) {
    throw "The live UX suite failed (exit $suiteExit)."
}

Write-Host '>> Live UX suite passed.'