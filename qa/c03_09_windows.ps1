# CRECISOL C03-09: Windows PowerShell launcher for a WSL2/Linux local suite.
# Does not install tools, connect to production, deploy or change billing.
[CmdletBinding()]
param(
    [ValidateSet("Probe", "Run")]
    [string] $Mode = "Probe"
)
$ErrorActionPreference = "Stop"
$wsl = Get-Command "wsl.exe" -ErrorAction SilentlyContinue
if (-not $wsl) {
    Write-Error "BLOCKED: WSL2 Ubuntu is required. Review docs/c03/C03_09_GUIA_PRUEBAS_LOCALES.md"
    exit 2
}
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$wslRepo = (& wsl.exe --exec wslpath -a $repo 2>&1 | Select-Object -First 1)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($wslRepo)) {
    Write-Error "BLOCKED: could not map the CRECISOL Windows repository path into WSL2."
    exit 2
}
$wslRepo = $wslRepo.Trim()
$option = if ($Mode -eq "Run") { "--run" } else { "--probe" }
Write-Host "CRECISOL C03-09 | $Mode | Linux directory: $wslRepo"
& wsl.exe --cd $wslRepo --exec bash "qa/c03_09_local_suite.sh" $option
exit $LASTEXITCODE
