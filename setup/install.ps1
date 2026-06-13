# =====================================================================
#  GitHub Copilot CLI Hands-on - Windows Auto Installer
# =====================================================================
#  IMPORTANT: Run PowerShell AS ADMINISTRATOR
#    (Start menu -> type "PowerShell" -> right-click -> "Run as administrator")
#
#  One-line install (paste into an ADMIN PowerShell):
#     Set-ExecutionPolicy Bypass -Scope Process -Force; `
#       Invoke-RestMethod https://raw.githubusercontent.com/asomi7007/copilot-cli-handson/main/setup/install.ps1 | Invoke-Expression
#
#  Or run the downloaded file directly (in ADMIN PowerShell):
#     Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
#     .\install.ps1
#
#  What it installs: Node.js 22 -> GitHub Copilot CLI
#  Administrator rights are required (winget + global npm install).
#  If auto-install fails, manual download links are shown on screen.
# =====================================================================

$ErrorActionPreference = "Stop"

# ===== Folder settings (change these two if you want; ASCII only) =====
$BaseFolderName = "copilot-workspace"   # base folder under the user home
$GameFolderName = "invaders"            # this lab's game folder
$DashboardUrl   = "https://rg-hub-web-0614.azurewebsites.net"  # shared gallery
# =====================================================================
# Fixed location: %USERPROFILE%\copilot-workspace\invaders

function Write-Step($m) { Write-Host "`n==> $m" -ForegroundColor Cyan }
function Write-OK($m)   { Write-Host "  [OK] $m" -ForegroundColor Green }
function Write-Warn2($m){ Write-Host "  [WARN] $m" -ForegroundColor Yellow }

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  GitHub Copilot CLI Hands-on - Windows Installer" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# --- 0. Check administrator rights ----------------------------------
$isAdmin = ([Security.Principal.WindowsPrincipal] `
    [Security.Principal.WindowsIdentity]::GetCurrent() `
    ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Warn2 "This installer needs Administrator rights (for winget + global npm install)."
    Write-Host  "  Please close this window and reopen PowerShell as Administrator:" -ForegroundColor White
    Write-Host  "    Start menu -> type 'PowerShell' -> right-click -> 'Run as administrator'" -ForegroundColor White
    Write-Host  "  Then paste the install command again." -ForegroundColor White
    exit 1
}
Write-OK "Running as Administrator"

# --- 1. Node.js ------------------------------------------------------
Write-Step "Step 1/2: Check Node.js"
$nodeOk = $false
try {
    $nodeVer = (node --version) 2>$null
    if ($nodeVer -match "v(\d+)\.") {
        if ([int]$Matches[1] -ge 22) { Write-OK "Node.js $nodeVer already installed"; $nodeOk = $true }
        else { Write-Warn2 "Node.js $nodeVer is too old (need 22+). Installing..." }
    }
} catch { }

if (-not $nodeOk) {
    Write-Step "Installing Node.js 22 (via winget)"
    try {
        winget install --id OpenJS.NodeJS.LTS -e --accept-source-agreements --accept-package-agreements
        Write-OK "Node.js installed"
        Write-Warn2 "Close this window, open a NEW PowerShell, and run the installer again to refresh PATH."
    } catch {
        Write-Warn2 "Auto-install failed. Install manually:"
        Write-Host "    https://nodejs.org/  (download LTS -> install)" -ForegroundColor White
        Write-Host "    Then open a new PowerShell and run this installer again." -ForegroundColor White
        exit 1
    }
}

# --- 2. GitHub Copilot CLI ------------------------------------------
Write-Step "Step 2/2: Install GitHub Copilot CLI"
try {
    npm install -g @github/copilot
    Write-OK "GitHub Copilot CLI installed"
} catch {
    Write-Warn2 "Auto-install failed. Try manually:"
    Write-Host "    npm install -g @github/copilot" -ForegroundColor White
    Write-Host "  Docs: https://docs.github.com/en/copilot/how-tos/copilot-cli/set-up-copilot-cli/install-copilot-cli" -ForegroundColor White
    exit 1
}

# --- 3. Workspace folder (fixed location) ---------------------------
Write-Step "Prepare workspace folder"
$BasePath = Join-Path $env:USERPROFILE $BaseFolderName
$GamePath = Join-Path $BasePath $GameFolderName
if (-not (Test-Path $BasePath)) { New-Item -ItemType Directory -Path $BasePath | Out-Null }
if (Test-Path $GamePath) {
    Write-OK "Using existing workspace: $GamePath"
} else {
    New-Item -ItemType Directory -Path $GamePath | Out-Null
    Write-OK "Created workspace: $GamePath"
}
Push-Location $GamePath
if (-not (Test-Path (Join-Path $GamePath ".git"))) {
    git init 2>$null | Out-Null
    Write-OK "git initialized (so you can undo changes)"
}
Pop-Location

# --- Done ------------------------------------------------------------
Write-Host "`n============================================================" -ForegroundColor Green
Write-Host "  Done! Your workspace is always here:" -ForegroundColor Green
Write-Host "    $GamePath" -ForegroundColor White
Write-Host "============================================================" -ForegroundColor Green
Write-Host "  Next steps:"
Write-Host "  1) Open a new PowerShell window."
Write-Host "  2) Go to your workspace (copy this line):"
Write-Host "        cd `"$GamePath`"" -ForegroundColor White
Write-Host "  3) Register the shared gallery URL (BEFORE starting copilot):"
Write-Host "        `$env:LAB502_DASHBOARD_URL = `"$DashboardUrl`"" -ForegroundColor White
Write-Host "  4) Start Copilot:"
Write-Host "        copilot" -ForegroundColor White
Write-Host "  5) In the chat, type  /login  to sign in to GitHub."
Write-Host "`n  Then follow docs/00-intro onward. Have fun!" -ForegroundColor Green
Write-Host "  (Can't find the folder? In File Explorer address bar type: %USERPROFILE%\$BaseFolderName)" -ForegroundColor DarkGray
