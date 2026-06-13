#!/usr/bin/env bash
# LAB502 Codespaces 자동 셋업
# Codespace 생성 시 1회 실행됩니다. (devcontainer.json의 onCreateCommand)
set -e

echo "==> GitHub Copilot CLI 설치 (npm)"
npm install -g @github/copilot

echo "==> Playwright + 브라우저(헤드리스) 설치"
# web-screenshotter 에이전트가 사용하는 Playwright. Codespaces는 GUI가 없으므로
# 크로미움을 headless로 설치합니다. (모듈 04는 headless로 동작 — 아래 README 참고)
npx -y playwright@latest install --with-deps chromium || true

echo "==> 설치 확인"
node --version
dotnet --version || true
copilot --version || echo "(copilot은 첫 실행 시 /login 필요)"

echo ""
echo "============================================================"
echo " LAB502 Codespace 준비 완료!"
echo " 1) 터미널에서:  copilot   →  /login  으로 GitHub 인증"
echo " 2) 게임 공유를 진행자 허브로 보내려면 copilot 켜기 전에:"
echo "      export LAB502_DASHBOARD_URL=\"https://<진행자_허브URL>\""
echo " 3) 자세한 순서는 저장소의 docs/00-intro.md 부터"
echo "============================================================"
