#!/usr/bin/env bash
# =====================================================================
#  GitHub Copilot CLI Hands-on - macOS / Linux Auto Installer
# =====================================================================
#  One-line install (paste into Terminal):
#     curl -fsSL https://raw.githubusercontent.com/asomi7007/copilot-cli-handson/main/setup/install.sh | bash
#
#  Or run the downloaded file directly:
#     bash install.sh
#
#  No "sudo" needed normally (Homebrew installs to your user).
#  If the global npm install fails with a permission error, the script
#  prints a "sudo npm install -g @github/copilot" fallback.
#  Do NOT run the whole script with sudo (Homebrew refuses to run as root).
#
#  What it does: check Homebrew -> Node.js 22 -> GitHub Copilot CLI
# =====================================================================
set -e

# --- 0. Warn if run as root (Homebrew won't run as root) ------------
if [ "$(id -u)" = "0" ]; then
  printf "\033[33m  [WARN] Do not run this script with sudo. Run it as your normal user.\033[0m\n"
  printf "         (Homebrew refuses to run as root. Re-run without sudo.)\n"
  exit 1
fi

# ===== Folder settings (change these if you want; ASCII only) =====
BASE_FOLDER_NAME="copilot-workspace"   # base folder under home
GAME_FOLDER_NAME="invaders"            # this lab's game folder
DASHBOARD_URL="https://rg-hub-web-0614.azurewebsites.net"  # shared gallery
# ==================================================================
# Fixed location: ~/copilot-workspace/invaders

cyan(){ printf "\033[36m%s\033[0m\n" "$1"; }
green(){ printf "\033[32m%s\033[0m\n" "$1"; }
yellow(){ printf "\033[33m%s\033[0m\n" "$1"; }

cyan "============================================================"
cyan "  GitHub Copilot CLI Hands-on - macOS/Linux Installer"
cyan "============================================================"

# --- 1. Node.js ------------------------------------------------------
cyan ""
cyan "==> Step 1/2: Check Node.js"
NODE_OK=0
if command -v node >/dev/null 2>&1; then
  VER=$(node --version | sed 's/v//' | cut -d. -f1)
  if [ "$VER" -ge 22 ] 2>/dev/null; then
    green "  [OK] Node.js $(node --version) already installed"
    NODE_OK=1
  else
    yellow "  [WARN] Node.js $(node --version) is too old (need 22+). Installing..."
  fi
fi

if [ "$NODE_OK" -eq 0 ]; then
  if command -v brew >/dev/null 2>&1; then
    cyan "==> Installing Node.js via Homebrew"
    if brew install node@22 2>/dev/null || brew install node; then
      green "  [OK] Node.js installed"
    else
      yellow "  [WARN] Auto-install failed. Install manually:"
      echo   "    https://nodejs.org/  (download LTS -> install)"
      exit 1
    fi
  else
    yellow "  [WARN] Homebrew not found. Use one of:"
    echo   "    (a) Install Homebrew: https://brew.sh  then re-run this script"
    echo   "    (b) Install Node.js directly: https://nodejs.org/  (LTS)"
    exit 1
  fi
fi

# --- 2. GitHub Copilot CLI ------------------------------------------
cyan ""
cyan "==> Step 2/2: Install GitHub Copilot CLI"
if npm install -g @github/copilot; then
  green "  [OK] GitHub Copilot CLI installed"
else
  yellow "  [WARN] Permission issue? Try:"
  echo   "    sudo npm install -g @github/copilot"
  echo   "  Docs: https://docs.github.com/en/copilot/how-tos/copilot-cli/set-up-copilot-cli/install-copilot-cli"
  exit 1
fi

# --- 3. Workspace folder (fixed location) ---------------------------
cyan ""
cyan "==> Prepare workspace folder"
BASE_PATH="$HOME/$BASE_FOLDER_NAME"
GAME_PATH="$BASE_PATH/$GAME_FOLDER_NAME"
mkdir -p "$BASE_PATH"
if [ -d "$GAME_PATH" ]; then
  green "  [OK] Using existing workspace: $GAME_PATH"
else
  mkdir -p "$GAME_PATH"
  green "  [OK] Created workspace: $GAME_PATH"
fi
if [ ! -d "$GAME_PATH/.git" ]; then
  ( cd "$GAME_PATH" && git init >/dev/null 2>&1 )
  green "  [OK] git initialized (so you can undo changes)"
fi

# --- Done ------------------------------------------------------------
green ""
green "============================================================"
green "  Done! Your workspace is always here:"
echo  "    $GAME_PATH"
green "============================================================"