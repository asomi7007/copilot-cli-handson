#!/usr/bin/env bash
# 다음 행사 전: 앱을 다시 켭니다. 이전 행사 게임이 그대로 있는 갤러리로 부팅됩니다.
set -e
RESOURCE_GROUP="lab502-rg"
APP_NAME="lab502hub"
az webapp start --name "$APP_NAME" --resource-group "$RESOURCE_GROUP"
echo "재시작 완료. 갤러리: https://${APP_NAME}.azurewebsites.net/gallery"
