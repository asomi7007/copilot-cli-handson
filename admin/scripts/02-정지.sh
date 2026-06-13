#!/usr/bin/env bash
# 행사 종료 후: 앱을 정지해 비용을 줄입니다. (게임/갤러리 데이터는 보존됩니다)
set -e
RESOURCE_GROUP="lab502-rg"
APP_NAME="lab502hub"
az webapp stop --name "$APP_NAME" --resource-group "$RESOURCE_GROUP"
echo "정지 완료. 다음 행사 전에 03-재시작.sh 를 실행하세요."
echo "※ 절대 삭제(delete)하지 마세요 — 삭제하면 누적된 게임이 사라집니다."
