#Requires -Version 5.1
<#
.SYNOPSIS
  One-time setup on a Windows build VM: install .NET 8 SDK, Git, and Azure CLI (winget).

.NOTES
  Run in an elevated PowerShell (Administrator) if winget requires it.
  If winget is unavailable, install manually from:
  - https://dotnet.microsoft.com/download/dotnet/8.0
  - https://git-scm.com/download/win
  - https://learn.microsoft.com/cli/azure/install-azure-cli-windows
#>
$ErrorActionPreference = "Stop"

if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
    Write-Warning "winget not found. Install .NET 8 SDK, Git, and Azure CLI manually (see script comments)."
    exit 1
}

Write-Host "Installing Microsoft.DotNet.SDK.8 ..."
winget install --id Microsoft.DotNet.SDK.8 --accept-package-agreements --accept-source-agreements

Write-Host "Installing Git.Git ..."
winget install --id Git.Git --accept-package-agreements --accept-source-agreements

Write-Host "Installing Microsoft.AzureCLI ..."
winget install --id Microsoft.AzureCLI --accept-package-agreements --accept-source-agreements

Write-Host "Done. Open a new terminal and verify: dotnet --version, git --version, az version"
