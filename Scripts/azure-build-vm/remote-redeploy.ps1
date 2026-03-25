# Run ON the Windows VM in PowerShell (RDP or Run Command). Pulls latest `master` and runs Build-AndDeploy.ps1.
# Azure Run Command runs as NT AUTHORITY\SYSTEM, so $env:USERPROFILE is NOT your interactive user -
# it points at C:\Windows\system32\config\systemprofile. This script resolves the repo without relying on that.
#
# Resolution order:
#   1) Machine or process env MICROHIRE_REPO (set to your clone root, e.g. C:\work\MicrohireAgentChat)
#   2) Common paths: C:\build\MicrohireAgentChat, C:\work\MicrohireAgentChat, C:\MicrohireAgentChat
#   3) First matching C:\Users\*\Desktop\MicrohireAgentChat
#   4) $env:USERPROFILE\Desktop\MicrohireAgentChat (works if you run this script manually as your user)
#
# Requires: Git for Windows, .NET SDK, Azure CLI on VM (logged in for deploy).
# Run Command uses SYSTEM (not your PATH). We prepend Git, dotnet, and az here so the *child*
# Build-AndDeploy.ps1 inherits PATH even if the VM's git pull has not yet picked up PATH fixes in that file.
$ErrorActionPreference = "Stop"

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
        throw "dotnet.exe not found. Install .NET SDK (https://dotnet.microsoft.com/download) or set Machine DOTNET_ROOT."
    }
}

function Ensure-AzOnPath {
    if (Get-Command az -ErrorAction SilentlyContinue) { return }
    Add-DirToPathFront (Join-Path $env:ProgramFiles "Microsoft SDKs\Azure\CLI2\wbin")
    $pf86 = [Environment]::GetEnvironmentVariable("ProgramFiles(x86)", "Machine")
    if ($pf86) { Add-DirToPathFront (Join-Path $pf86 "Microsoft SDKs\Azure\CLI2\wbin") }
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        throw "Azure CLI (az) not found. Install for all users (https://aka.ms/installazurecliwindows)."
    }
}

function Ensure-GitOnPath {
    if (Get-Command git -ErrorAction SilentlyContinue) { return }
    $prefixes = @($env:ProgramFiles)
    $pf86 = [Environment]::GetEnvironmentVariable("ProgramFiles(x86)", "Machine")
    if ($pf86) { $prefixes += $pf86 }
    foreach ($root in $prefixes) {
        if ([string]::IsNullOrWhiteSpace($root)) { continue }
        $cmdDir = Join-Path $root "Git\cmd"
        $gitExe = Join-Path $cmdDir "git.exe"
        if (Test-Path -LiteralPath $gitExe) {
            $env:Path = $cmdDir + ";" + $env:Path
            return
        }
    }
    throw "git.exe not found under Program Files\Git\cmd. Install Git for Windows (https://git-scm.com/download/win)."
}

function Get-MicrohireRepoRoot {
    if ($env:MICROHIRE_REPO) {
        $p = $env:MICROHIRE_REPO.Trim()
        if ($p -and (Test-Path -LiteralPath $p)) { return (Resolve-Path -LiteralPath $p).Path }
    }

    # Prefer fixed paths; USERPROFILE last (under Run Command it is SYSTEM, not your RDP user).
    $candidates = @(
        "C:\work\MicrohireAgentChat"
        "C:\build\MicrohireAgentChat"
        "C:\src\MicrohireAgentChat"
        "C:\MicrohireAgentChat"
        (Join-Path $env:USERPROFILE "Desktop\MicrohireAgentChat")
    )
    foreach ($c in $candidates) {
        if ([string]::IsNullOrWhiteSpace($c)) { continue }
        if (Test-Path -LiteralPath $c) { return (Resolve-Path -LiteralPath $c).Path }
    }

    foreach ($userDir in Get-ChildItem -Path "C:\Users" -Directory -ErrorAction SilentlyContinue) {
        $desktop = Join-Path $userDir.FullName "Desktop\MicrohireAgentChat"
        if (Test-Path -LiteralPath $desktop) { return (Resolve-Path -LiteralPath $desktop).Path }
    }

    return $null
}

$repo = Get-MicrohireRepoRoot
if (-not $repo) {
    throw @"
Repo not found. Azure Run Command runs as SYSTEM; Desktop path is not your user's Desktop.

Fix one of:
  - Set machine env MICROHIRE_REPO to the repo root (e.g. C:\work\MicrohireAgentChat), then run this script again from PowerShell
  - Or clone/copy the repo to C:\work\MicrohireAgentChat (or C:\build\MicrohireAgentChat)
"@
}

Ensure-GitOnPath
Ensure-DotNetOnPath
Ensure-AzOnPath
Set-Location $repo
git pull origin master
if ($LASTEXITCODE -ne 0) { throw "git pull failed with exit code $LASTEXITCODE" }
& (Join-Path $repo "Scripts\azure-build-vm\Build-AndDeploy.ps1") -RepoRoot $repo
