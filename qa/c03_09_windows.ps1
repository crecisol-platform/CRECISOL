# CRECISOL C03-09: Windows PowerShell launcher to WSL2 (no wslpath dependency).
# Never installs tools, connects to external databases, deploys, or changes billing.
[CmdletBinding()]
param(
    [ValidateSet("Probe", "Run")]
    [string] $Mode = "Probe"
)

$ErrorActionPreference = "Stop"

function Stop-LocalLauncher([string] $Reason) {
    [Console]::Error.WriteLine("BLOCKED C03-09: $Reason")
    exit 2
}

$wsl = Get-Command "wsl.exe" -ErrorAction SilentlyContinue
if (-not $wsl) {
    Stop-LocalLauncher "WSL2 is unavailable; see docs/c03/C03_09_GUIA_PRUEBAS_LOCALES.md"
}

try {
    $repo = (Resolve-Path -LiteralPath (Join-Path -Path $PSScriptRoot -ChildPath "..")).ProviderPath
} catch {
    Stop-LocalLauncher "Cannot resolve local CRECISOL checkout"
}

if (-not (Test-Path -LiteralPath (Join-Path -Path $repo -ChildPath "qa/c03_09_local_suite.sh") -PathType Leaf)) {
    Stop-LocalLauncher "The C03-09 Bash suite is missing from this checkout"
}

$option = if ($Mode -eq "Run") { "--run" } else { "--probe" }
$wslArgs = @()

# A repository checked out in WSL can appear as \\wsl.localhost\Ubuntu\home\user\CRECISOL
# or \\wsl$\Ubuntu\home\user\CRECISOL from Windows.
if ($repo -match '^\\\\wsl(?:\.localhost|\$)\\([^\\]+)\\(.+)$') {
    $distro = $Matches[1]
    $linuxRelative = $Matches[2].Replace('\', '/')
    if ($distro -notmatch '^[A-Za-z0-9._-]+$' -or $linuxRelative -match '(^|/)\.\.?(/|$)') {
        Stop-LocalLauncher "Invalid WSL distribution or repository path"
    }
    $linuxRepo = "/" + $linuxRelative
    $wslArgs = @("--distribution", $distro, "--cd", $linuxRepo,
                 "--exec", "bash", "qa/c03_09_local_suite.sh", $option)
} elseif ($repo -match '^[A-Za-z]:\\') {
    # WSL --cd natively accepts an absolute Windows path. No wslpath conversion needed.
    $wslArgs = @("--cd", $repo, "--exec", "bash", "qa/c03_09_local_suite.sh", $option)
} else {
    Stop-LocalLauncher "Unsupported checkout location; use the suite directly inside WSL2"
}

Write-Host "CRECISOL C03-09 | $Mode | WSL2 local suite"
try {
    & $wsl.Source @wslArgs
    $exitCode = $LASTEXITCODE
} catch {
    Stop-LocalLauncher "Could not execute the suite in WSL2; run the Bash command directly"
}
if ($null -eq $exitCode) {
    Stop-LocalLauncher "WSL2 did not return an execution status"
}
exit ([int]$exitCode)
