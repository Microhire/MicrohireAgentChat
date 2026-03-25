#Requires -Version 5.1
<#
.SYNOPSIS
  Verifies a dotnet publish folder has a complete Playwright PDF bundle (driver + Chromium).

.PARAMETER PublishRoot
  Folder that contains playwright.ps1, .playwright\node, and pw-browsers (same layout as Build-AndDeploy.ps1 output).

.PARAMETER CheckVcRedist
  If set, warns when Visual C++ 2015-2022 x64 redistributable is not detected (Chromium often needs it on Windows Server / minimal VMs).

.EXAMPLE
  .\Verify-PlaywrightBundle.ps1 -PublishRoot C:\work\MicrohireAgentChat\publish_out
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $PublishRoot,

    [switch] $CheckVcRedist
)

$ErrorActionPreference = "Stop"

$PublishRoot = (Resolve-Path -LiteralPath $PublishRoot).Path
$fail = @()

$ps1 = Join-Path $PublishRoot "playwright.ps1"
if (-not (Test-Path -LiteralPath $ps1)) {
    $fail += "Missing playwright.ps1 under publish root (Playwright package not copied to output)."
}

$node = Join-Path $PublishRoot ".playwright\node"
if (-not (Test-Path -LiteralPath $node)) {
    $fail += "Missing .playwright\node - driver bundle incomplete."
}

$browserRoot = Join-Path $PublishRoot "pw-browsers"
if (-not (Test-Path -LiteralPath $browserRoot)) {
    $fail += "Missing pw-browsers directory."
}
else {
    $chrome = Get-ChildItem -Path $browserRoot -Recurse -Filter "chrome.exe" -File -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if (-not $chrome) {
        $fail += "pw-browsers exists but chrome.exe was not found - run: `$env:PLAYWRIGHT_BROWSERS_PATH='$browserRoot'; .\playwright.ps1 install chromium"
    }
    else {
        Write-Host "OK: Chromium executable: $($chrome.FullName)"
        $sizeMb = [math]::Round(((Get-ChildItem -Path $browserRoot -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB), 1)
        Write-Host "OK: pw-browsers total size ~${sizeMb} MB"
    }
}

if ($CheckVcRedist) {
    $hasVc = $false
    $keys = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*"
    )
    foreach ($pattern in $keys) {
        Get-ItemProperty $pattern -ErrorAction SilentlyContinue | ForEach-Object {
            $n = $_.DisplayName
            if ($n -and $n -match 'Microsoft Visual C\+\+ 2015-2022 Redistributable' -and $n -match 'x64') {
                $hasVc = $true
            }
        }
    }
    if (-not $hasVc) {
        Write-Warning "Visual C++ 2015-2022 (x64) not found in Add/Remove Programs. Chromium may fail to start. Install: winget install Microsoft.VCRedist.2015+.x64"
    }
    else {
        Write-Host "OK: Visual C++ 2015-2022 x64 redistributable appears installed."
    }
}

if ($fail.Count -gt 0) {
    throw ($fail -join " ")
}

Write-Host "Verify-PlaywrightBundle: all checks passed for $PublishRoot"
