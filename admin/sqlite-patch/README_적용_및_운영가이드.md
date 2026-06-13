# Community Hub — SQLite 모드 적용 & 운영 가이드

> 목적: Azure **SQL을 SQLite 파일 DB로 교체**해 비용을 줄이고, **게임·스크린샷은 그대로 Azure Blob에 저장**해 행사 사이에도 보존. 행사 후 App Service를 **정지**하고 다음 행사 때 **재시작**한다.
>
> ⚠️ **빌드/테스트는 .NET 10 SDK가 있는 당신 PC에서** 해야 합니다. 이 코드는 작성·정적 검토는 마쳤지만, 작업 환경에 .NET이 없어 **컴파일까진 못 했습니다.** 아래 "로컬 검증" 단계에서 반드시 한 번 빌드·실행해 확인하세요.

---

## 무엇이 바뀌나 (핵심)

원본 Community Hub는 모드가 `local` / `cloud` 둘뿐이고, `cloud`는 **Azure SQL + Azure Blob** 전용입니다. 여기에 **세 번째 모드 `sqlite`** 를 추가했습니다:

| 모드 | 활동/갤러리 인덱스 | 게임·스크린샷 파일 | 용도 |
|------|-----------------|------------------|------|
| local | 메모리 + JSON 파일 | 로컬 파일 | 개발용 |
| cloud | Azure SQL | Azure Blob | 원래 프로덕션 |
| **sqlite (신규)** | **SQLite 파일** | **Azure Blob** | **이번 행사용 — SQL 비용 X, 데이터 보존 O** |

기존 local/cloud 모드는 **건드리지 않았습니다.** sqlite 모드만 새로 추가했으므로 안전합니다.

추가된 파일 (`new-files/Services/`):
- `SqliteDb.cs` — 스키마 생성 + 연결 (SQLite)
- `SqliteMetrics.cs` — 활동 카운터 (SqlMetrics의 SQLite 포팅)
- `SqliteGalleryIndex.cs` — 갤러리 인덱스
- `AzureBlobStoreSqlite.cs` — Blob 업로드(동일) + 스크린샷 인덱스만 SQLite
- `SqliteDebugDataCleaner.cs` — 데이터 정리용

수정된 파일 (`changed-files/`):
- `Config/AppConfig.cs` — `APP_MODE=sqlite` 인식, `SQLITE_DB_PATH` 추가
- `Extensions/ServiceCollectionExtensions.cs` — sqlite 모드 DI 연결
- `CommunityHub.csproj` — `Microsoft.Data.Sqlite` 패키지 추가

---

## 1. 코드 적용하기

repo를 clone한 폴더에서, 두 가지 방법 중 하나:

**방법 A — 패치 적용 (권장)**
```bash
cd <repo 루트>
git apply /경로/community-hub-sqlite-patch/sqlite-mode.patch
```

**방법 B — 파일 직접 복사**
- `new-files/Services/*.cs` 5개 → `src/community-hub/CommunityHub/Services/` 에 복사
- `changed-files/Config/AppConfig.cs` → 같은 위치 덮어쓰기
- `changed-files/Extensions/ServiceCollectionExtensions.cs` → 덮어쓰기
- `changed-files/CommunityHub.csproj` → 덮어쓰기

> 패키지 버전 주의: csproj에 `Microsoft.Data.Sqlite` **9.0.0** 을 넣었습니다. .NET 10 프리뷰와 충돌하면 `dotnet add package Microsoft.Data.Sqlite` 로 호환 버전을 받으세요.

---

## 2. 로컬 검증 (배포 전 필수)

먼저 빌드되는지 확인:
```bash
cd src/community-hub
dotnet build CommunityHub.slnx
```

빌드되면, **Blob 없이** 스키마/DB 동작만 빠르게 보고 싶을 때는 기존 `local` 모드로 충분합니다. sqlite 모드는 Blob(Azure) 설정이 필요하므로, 완전한 테스트는 아래 Azure 배포 후에 합니다. (로컬에서 sqlite 모드를 굳이 돌리려면 Azure Storage 계정 + `azurite` 에뮬레이터가 필요해 번거롭습니다.)

> 컴파일 에러가 나면 거의 대부분 ① 패키지 버전 ② `Microsoft.Data.Sqlite` 미설치 둘 중 하나입니다.

---

## 3. Azure 배포 (sqlite 모드)

sqlite 모드는 **Azure Blob Storage는 필요하지만 Azure SQL은 필요 없습니다.** 따라서 cloud-mode 배포 스크립트를 그대로 쓰면 SQL까지 만들어져 낭비입니다. 두 가지 선택:

### 권장: App Service + Storage만 수동 구성

원리만 알면 간단합니다. sqlite 모드가 요구하는 환경변수는:

| 환경변수 | 값 | 설명 |
|----------|-----|------|
| `APP_MODE` | `sqlite` | 새 모드 |
| `APP_TENANT` | 예: `build2026` | 영숫자/하이픈, 최대 31자 |
| `AZURE_STORAGE_ACCOUNT` | 스토리지 계정명 | Blob 저장 |
| `AZURE_BLOB_PUBLIC_BASE` | `https://<계정>.blob.core.windows.net` | Blob 공개 URL 베이스 |
| `SQLITE_DB_PATH` | `/home/data/communityhub.db` | ★ 영구 디스크 경로 (중요!) |
| `LAB502_DASHBOARD_URL` | 배포된 앱 URL | OpenAPI/공유에 사용 |
| `PORT` | `8080` (App Service 기본) | |

**★ 가장 중요한 포인트:** `SQLITE_DB_PATH`를 반드시 **`/home`** 하위(예: `/home/data/communityhub.db`)에 두세요. Azure App Service에서 `/home`은 **영구 저장소**라 앱 재시작·정지·재배포에도 파일이 보존됩니다. `/tmp` 등 다른 경로는 날아갑니다.

스토리지 컨테이너 2개(`screenshots`, `gallery`)가 필요하고, 앱의 관리 ID(Managed Identity)에 **Storage Blob Data Contributor** 역할을 부여해야 합니다(코드가 `DefaultAzureCredential` 사용). Blob을 공개로 서빙하려면 컨테이너를 공개 읽기로 두거나 정적 웹/공개 액세스를 설정하세요.

### 대안: 기존 setup 스크립트 재활용

`src/community-hub/setup`의 cloud 배포로 App Service + Storage를 만든 뒤(만들어지는 SQL은 무시하거나 삭제), App Service 구성에서 위 환경변수로 **`APP_MODE=sqlite`** 와 `SQLITE_DB_PATH=/home/data/communityhub.db`만 덮어쓰면 SQL을 안 쓰고 SQLite로 동작합니다. (가장 빠르지만 안 쓰는 SQL 리소스가 남으니 수동 삭제 권장.)

배포는 cloud-mode와 동일하게 `dotnet publish` → zip → `az webapp deploy`.

---

## 4. 행사 사이 정지 / 재시작 (비용 절감 + 데이터 보존)

게임·스크린샷(Blob)과 SQLite 파일(`/home`)은 **정지해도 보존**됩니다. App Service의 컴퓨팅만 끄면 됩니다.

**행사 종료 후 — 정지:**
```bash
az webapp stop --name <앱이름> --resource-group <리소스그룹>
```
→ App Service 컴퓨팅 비용 청구 중지. Blob·SQLite 데이터는 그대로.

**다음 행사 전 — 재시작:**
```bash
az webapp start --name <앱이름> --resource-group <리소스그룹>
```
→ 이전 행사 게임이 그대로 있는 갤러리로 부팅. 새 참가자가 바로 구경 가능.

> 정지 중에도 남는 비용: Storage(게임·스크린샷, 매우 저렴) + App Service Plan 자체. 비용을 더 줄이려면 정지 대신 **App Service Plan을 F1(무료)로 다운**하거나, 정 안 쓰면 Plan을 삭제했다가 다음에 재생성(단 이때 `/home` 보존을 위해 같은 앱을 유지해야 함). 가장 간단·안전한 건 `stop`입니다.

> ⚠️ **destroy/삭제는 하지 마세요.** App Service나 Storage를 삭제하면 누적된 게임이 사라집니다. 이번 운영의 목적(이전 게임 보존)과 정반대입니다.

---

## 5. 참가자 안내 (매 행사 공통)

참가자는 `copilot` 실행 **전에** 자기 터미널에서:

**Windows (PowerShell):**
```powershell
$env:LAB502_DASHBOARD_URL = "https://<배포된_앱URL>"
```
**macOS/Linux:**
```bash
export LAB502_DASHBOARD_URL="https://<배포된_앱URL>"
```

진행자 화면엔 `https://<배포URL>/gallery` 와 `/activity` 를 띄워두면 됩니다.

---

## 6. 동작 점검 체크리스트

- [ ] `dotnet build` 성공
- [ ] Azure에 App Service + Storage(컨테이너 screenshots/gallery) 준비
- [ ] App Service 환경변수에 `APP_MODE=sqlite`, `SQLITE_DB_PATH=/home/data/communityhub.db`, Storage 변수들 설정
- [ ] 관리 ID에 Storage Blob Data Contributor 부여
- [ ] `/activity`, `/gallery`, `/api/openapi.json` 정상
- [ ] 진행자 게임 1개 업로드 → 갤러리 표시 확인
- [ ] App Service `stop` → `start` 후에도 그 게임이 갤러리에 남아있는지 확인 (보존 검증)

---

## 참고: 무엇을 보존하고 무엇이 초기화되나

- **보존 (Blob + SQLite/home):** 업로드된 게임 HTML, 스크린샷, 갤러리 목록, 활동 카운터(세션·도구 사용 수 등) — 전부 재시작에도 유지.
- 즉 이전 행사 참가자들의 게임·스크린샷이 새 행사 참가자에게 그대로 보입니다. (원하던 그 동작)
