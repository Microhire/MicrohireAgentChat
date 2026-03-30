#Requires -Version 5.1
<#
.SYNOPSIS
  Windows-only: publish MicrohireAgentChat for win-x64, zip to site.zip, optionally deploy to Azure App Service.

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

# Azure Run Command and scheduled tasks often run as SYSTEM, which may not inherit the interactive user's PATH.
function Add-DirToPathFront {
    param([string]$Directory)
    if ([string]::IsNullOrWhiteSpace($Directory)) { return }
    if (-not (Test-Path -LiteralPath $Directory)) { return }
    $env:Path = $Directory + ";" + $env:Path
}

function Ensure-DotNetOnPath {
    if (Get-Command dotnet -ErrorAction SilentlyContinue) { return }
    $dotnetRoot = [Environment]::GetEnvironmentVariable("DOTNET_ROOT", "Machine")
    if ($dotnetRoot) { Add-DirToPathFront $dotnetRoot }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Add-DirToPathFront (Join-Path $env:ProgramFiles "dotnet")
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        foreach ($userDir in Get-ChildItem -Path "C:\Users" -Directory -ErrorAction SilentlyContinue) {
            $local = Join-Path $userDir.FullName "AppData\Local\Microsoft\dotnet"
            if (Test-Path -LiteralPath (Join-Path $local "dotnet.exe")) {
                Add-DirToPathFront $local
                break
            }
        }
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "dotnet.exe not found. Install .NET SDK (https://dotnet.microsoft.com/download) or set Machine DOTNET_ROOT to the folder containing dotnet.exe."
    }
}

function Ensure-AzOnPath {
    if (Get-Command az -ErrorAction SilentlyContinue) { return }
    Add-DirToPathFront (Join-Path $env:ProgramFiles "Microsoft SDKs\Azure\CLI2\wbin")
    $pf86 = [Environment]::GetEnvironmentVariable("ProgramFiles(x86)", "Machine")
    if ($pf86) { Add-DirToPathFront (Join-Path $pf86 "Microsoft SDKs\Azure\CLI2\wbin") }
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        throw "Azure CLI (az) not found. Install Azure CLI (https://aka.ms/installazurecliwindows) or use -SkipDeploy."
    }
}

$isWindows = ($PSVersionTable.PSVersion.Major -ge 6 -and $IsWindows) -or
    ($PSVersionTable.PSVersion.Major -lt 6 -and $env:OS -like "*Windows*")
if (-not $isWindows) {
    throw "This script must run on Windows."
}

Ensure-DotNetOnPath

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

Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $OutputZip) {
    Remove-Item -LiteralPath $OutputZip -Force
}

Write-Host "ZipFile.CreateFromDirectory -> $OutputZip"
[System.IO.Compression.ZipFile]::CreateFromDirectory($publishOut, $OutputZip)

$zipBytes = (Get-Item -LiteralPath $OutputZip).Length
Write-Host ('OK: {0} ({1} bytes)' -f $OutputZip, $zipBytes)

if ($SkipDeploy) {
    Write-Host "SkipDeploy: not running az webapp deploy"
    exit 0
}

Ensure-AzOnPath

Write-Host "az webapp deploy -> $WebAppName (RG $ResourceGroup)"
az webapp deploy --resource-group $ResourceGroup --name $WebAppName --src-path $OutputZip --type zip --async true
if ($LASTEXITCODE -ne 0) { throw "az webapp deploy failed with exit code $LASTEXITCODE" }
Write-Host 'Deploy initiated (async). Check App Service deployment logs in Azure Portal.'
