# Install PdfRenderService as a Windows Service (runs after logoff / reboot; no terminal needed).
# Run PowerShell as Administrator from the repo root, for example:
#   cd C:\Users\microhire\Desktop\MicrohireAgentChat
#   powershell -ExecutionPolicy Bypass -File .\Scripts\Install-PdfRenderService.ps1
# (Windows PowerShell 5.1: use -ExecutionPolicy Bypass on the command line; do not use "Set-ExecutionPolicy -Bypass".)

$ErrorActionPreference = "Stop"
$publishDir = "C:\Services\MicrohirePdfRender"
# Scripts\ -> repo root is one level up (two levels was wrong and pointed at Desktop).
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path (Join-Path $repoRoot "PdfRenderService"))) {
    $repoRoot = "C:\Users\microhire\Desktop\MicrohireAgentChat"
}

$svcName = "MicrohirePdfRender"
$dotnet = "$env:ProgramFiles\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { throw "dotnet.exe not found at $dotnet" }

Write-Host "Publishing to $publishDir ..."
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
Push-Location (Join-Path $repoRoot "PdfRenderService")
try {
    dotnet publish -c Release -o $publishDir --no-self-contained
}
finally {
    Pop-Location
}

# Reuse Chromium bundle from your main app publish (same Playwright major as PdfRenderService).
$pwSrc = Join-Path $repoRoot "publish_out\pw-browsers"
$pwDst = Join-Path $publishDir "pw-browsers"
if (Test-Path $pwSrc) {
    Write-Host "Copying pw-browsers from $pwSrc ..."
    if (Test-Path $pwDst) { Remove-Item -Recurse -Force $pwDst }
    Copy-Item -Recurse -Force $pwSrc $pwDst
} elseif (-not (Test-Path $pwDst)) {
    Write-Host "No $pwSrc - run: cd PdfRenderService; playwright install chromium"
    Write-Host "Then copy publish_out\pw-browsers into $pwDst"
}

$dll = Join-Path $publishDir "PdfRenderService.dll"
if (-not (Test-Path $dll)) { throw "Publish failed: missing $dll" }

$existing = Get-Service -Name $svcName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Stopping/removing existing service..."
    if ($existing.Status -eq "Running") { Stop-Service -Name $svcName -Force }
    if (Get-Command Remove-Service -ErrorAction SilentlyContinue) {
        Remove-Service -Name $svcName -Force
    } else {
        sc.exe delete $svcName | Out-Null
    }
    Start-Sleep -Seconds 2
}

$binaryPath = "`"$dotnet`" `"$dll`""
Write-Host "Creating service $svcName ..."
New-Service -Name $svcName `
    -BinaryPathName $binaryPath `
    -DisplayName "Microhire PDF Render (Playwright)" `
    -Description "POST /pdf/from-html for Microhire quote PDFs. Azure App Service calls this VM." `
    -StartupType Automatic | Out-Null

Write-Host "Starting service..."
Start-Service -Name $svcName
Get-Service $svcName | Format-List *

Write-Host ""
Write-Host 'Smoke test (from this VM):'
Write-Host '  curl.exe -s http://127.0.0.1:5100/health'
Write-Host 'Azure App Service application setting: PdfService__BaseUrl = http://YOUR_VM_PUBLIC_IP:5100'
