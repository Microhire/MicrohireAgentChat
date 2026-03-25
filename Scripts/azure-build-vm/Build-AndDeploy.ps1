#Requires -Version 5.1
<#
.SYNOPSIS
  Windows-only: publish MicrohireAgentChat for win-x64, install Playwright Chromium into pw-browsers,
  zip to site.zip, optionally deploy to Azure App Service (same flow as .github/workflows/publish-windows-azure.yml).

.PARAMETER RepoRoot
  Path to the repository root (folder that contains MicrohireAgentChat\MicrohireAgentChat.csproj).

.PARAMETER ResourceGroup
  Azure resource group of the App Service (default matches deploy.sh).

.PARAMETER WebAppName
  App Service name (default: microhire).

.PARAMETER OutputZip
  Path for site.zip output.

.PARAMETER SkipDeploy
  If set, only build site.zip; do not run az webapp deploy.

.PARAMETER GitPull
  If set, run `git pull` in RepoRoot before build (requires git and a configured remote).

.EXAMPLE
  .\Build-AndDeploy.ps1 -RepoRoot D:\work\MicrohireAgentChat

.EXAMPLE
  .\Build-AndDeploy.ps1 -RepoRoot D:\work\MicrohireAgentChat -SkipDeploy

.EXAMPLE
  az login --identity   # on VM with managed identity
  .\Build-AndDeploy.ps1 -RepoRoot D:\work\MicrohireAgentChat
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $RepoRoot,

    [string] $ResourceGroup = "rg-JennyJunkeer-9509",

    [string] $WebAppName = "microhire",

    [string] $OutputZip = "",

    [switch] $SkipDeploy,

    [switch] $GitPull
)

$ErrorActionPreference = "Stop"

$isWindows = ($PSVersionTable.PSVersion.Major -ge 6 -and $IsWindows) -or
    ($PSVersionTable.PSVersion.Major -lt 6 -and $env:OS -like "*Windows*")
if (-not $isWindows) {
    throw "This script must run on Windows (Playwright Chromium for win-x64)."
}

$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
$ProjectFile = Join-Path $RepoRoot "MicrohireAgentChat\MicrohireAgentChat.csproj"
if (-not (Test-Path -LiteralPath $ProjectFile)) {
    throw "Project not found: $ProjectFile (check -RepoRoot)"
}

if ([string]::IsNullOrWhiteSpace($OutputZip)) {
    $OutputZip = Join-Path $RepoRoot "site.zip"
}
else {
    if (-not [System.IO.Path]::IsPathRooted($OutputZip)) {
        $OutputZip = [System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $OutputZip))
    }
    else {
        $OutputZip = [System.IO.Path]::GetFullPath($OutputZip)
    }
}

if ($GitPull) {
    Push-Location $RepoRoot
    try {
        if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
            throw "Git not found; install Git or omit -GitPull"
        }
        git pull
        if ($LASTEXITCODE -ne 0) { throw "git pull failed with exit code $LASTEXITCODE" }
    }
    finally {
        Pop-Location
    }
}

$publishOut = Join-Path $RepoRoot "publish_out"
if (Test-Path -LiteralPath $publishOut) {
    Remove-Item -LiteralPath $publishOut -Recurse -Force
}

Write-Host "dotnet publish -> $publishOut"
dotnet publish $ProjectFile -c Release -o $publishOut -r win-x64 --self-contained false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

$browserDir = Join-Path $publishOut "pw-browsers"
New-Item -ItemType Directory -Force -Path $browserDir | Out-Null
$env:PLAYWRIGHT_BROWSERS_PATH = $browserDir
Push-Location $publishOut
try {
    if (-not (Test-Path -LiteralPath ".\playwright.ps1")) {
        throw "playwright.ps1 missing from publish output (.playwright driver layout broken?)"
    }
    Write-Host "playwright.ps1 install chromium -> $browserDir"
    & .\playwright.ps1 install chromium
    if ($LASTEXITCODE -ne 0) { throw "playwright install chromium failed with exit code $LASTEXITCODE" }
}
finally {
    Pop-Location
}

$playwrightNode = Join-Path $publishOut ".playwright\node"
if (-not (Test-Path -LiteralPath $playwrightNode)) {
    throw ".playwright/node missing under publish_out"
}
$browserFiles = Get-ChildItem -Path $browserDir -Recurse -File -ErrorAction SilentlyContinue
if (-not $browserFiles) {
    throw "pw-browsers is empty after playwright install"
}

if (Test-Path -LiteralPath $OutputZip) {
    Remove-Item -LiteralPath $OutputZip -Force
}
Write-Host "Compress-Archive -> $OutputZip"
Compress-Archive -Path (Join-Path $publishOut "*") -DestinationPath $OutputZip -Force

Write-Host "OK: $OutputZip ($((Get-Item $OutputZip).Length) bytes)"

if ($SkipDeploy) {
    Write-Host "SkipDeploy: not running az webapp deploy"
    exit 0
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI (az) not found. Install Azure CLI or use -SkipDeploy and deploy from another machine."
}

Write-Host "az webapp deploy -> $WebAppName (RG $ResourceGroup)"
az webapp deploy --resource-group $ResourceGroup --name $WebAppName --src-path $OutputZip --type zip --async true
if ($LASTEXITCODE -ne 0) { throw "az webapp deploy failed with exit code $LASTEXITCODE" }
Write-Host "Deploy initiated (async). Check App Service deployment logs in Azure Portal."
