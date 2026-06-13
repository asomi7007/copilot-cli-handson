#!/usr/bin/env bash
# =====================================================================
#  Community Hub(공용 갤러리) - Azure 배포 (SQLite 모드, App Service만)
# =====================================================================
#  사전: az login 완료, LAB502 저장소(SQLite 패치 적용본)를 clone 한 폴더에서 실행
#  하는 일: 리소스 그룹 + 스토리지 + App Service 생성 후 앱 배포
#  ※ 비개발자도 따라할 수 있게 변수만 채우면 됩니다.
# =====================================================================
set -e

# ---- 여기만 본인 값으로 바꾸세요 ------------------------------------
RESOURCE_GROUP="sangseng-communityhub-rg"
LOCATION="koreacentral"
BASE_NAME="sangseng-hub-0614"            # 전역 고유해야 함 (영문+숫자). URL에 쓰임
APP_TENANT="build-20260614"           # 갤러리 구분 이름 (영문/숫자/하이픈)
STORAGE_ACCOUNT="sangsenghub0614"   # 전역 고유. 소문자+숫자, 24자 이하
PLAN_SKU="B1"
# --------------------------------------------------------------------

APP_NAME="$BASE_NAME"

echo "==> 1) 리소스 그룹 생성"
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" -o none

echo "==> 2) 스토리지 계정 + 컨테이너 생성 (게임/스크린샷 보관)"
az storage account create --name "$STORAGE_ACCOUNT" --resource-group "$RESOURCE_GROUP" \
  --location "$LOCATION" --sku Standard_LRS --kind StorageV2 --allow-blob-public-access true -o none
az storage container create --account-name "$STORAGE_ACCOUNT" --name screenshots --public-access blob -o none || true
az storage container create --account-name "$STORAGE_ACCOUNT" --name gallery --public-access blob -o none || true

echo "==> 3) App Service 요금제 + 웹앱 생성 (.NET 10)"
az appservice plan create --name "${BASE_NAME}-plan" --resource-group "$RESOURCE_GROUP" \
  --sku "$PLAN_SKU" --is-linux -o none
az webapp create --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" \
  --plan "${BASE_NAME}-plan" --runtime "DOTNETCORE:10.0" -o none

echo "==> 4) 웹앱에 관리 ID 부여 + 스토리지 접근 권한"
az webapp identity assign --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" -o none
PRINCIPAL_ID=$(az webapp identity show --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --query principalId -o tsv)
STORAGE_ID=$(az storage account show --name "$STORAGE_ACCOUNT" --resource-group "$RESOURCE_GROUP" --query id -o tsv)
az role assignment create --assignee "$PRINCIPAL_ID" --role "Storage Blob Data Contributor" --scope "$STORAGE_ID" -o none

APP_URL="https://${APP_NAME}.azurewebsites.net"
BLOB_BASE="https://${STORAGE_ACCOUNT}.blob.core.windows.net"

echo "==> 5) 앱 환경변수 설정 (SQLite 모드)"
az webapp config appsettings set --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --settings \
  APP_MODE=sqlite \
  APP_TENANT="$APP_TENANT" \
  SQLITE_DB_PATH="/home/data/communityhub.db" \
  AZURE_STORAGE_ACCOUNT="$STORAGE_ACCOUNT" \
  AZURE_BLOB_PUBLIC_BASE="$BLOB_BASE" \
  LAB502_DASHBOARD_URL="$APP_URL" \
  PORT="8080" \
  WEBSITES_PORT="8080" -o none

echo "==> 6) 앱 빌드 & 배포"
cd src/community-hub
dotnet publish CommunityHub/CommunityHub.csproj -c Release -o /tmp/publish
( cd /tmp/publish && zip -r /tmp/app.zip . >/dev/null )
az webapp deploy --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --type zip --src-path /tmp/app.zip -o none

echo ""
echo "============================================================"
echo "  배포 완료!"
echo "  갤러리 주소(참가자에게 안내):  $APP_URL"
echo "    - 활동 보드:  $APP_URL/activity"
echo "    - 갤러리:     $APP_URL/gallery"
echo ""
echo "  참가자는 copilot 켜기 전에 이 줄을 입력해야 합니다:"
echo "    (Windows) \$env:LAB502_DASHBOARD_URL = \"$APP_URL\""
echo "    (Mac)     export LAB502_DASHBOARD_URL=\"$APP_URL\""
echo "============================================================"
echo "  ※ 위 RESOURCE_GROUP / APP_NAME 값을 02-정지.sh, 03-재시작.sh 에도 동일하게 쓰세요."
