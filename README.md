# 🎮 GitHub Copilot CLI 핸즈온 — 나만의 방식으로 만들고 공유하기

> GitHub Copilot을 **터미널(CLI)에서** 직접 써보는 실습입니다. 프로그래밍을 몰라도 괜찮습니다.
> 자연어(평소 말하듯)로 명령하면 Copilot이 게임을 만들고, 화면을 캡처해 공유하고, 나만의 규칙과 기능까지 만들어 줍니다.

이 실습은 Microsoft Build의 [LAB502](https://github.com/microsoft/Build26-LAB502-make-github-copilot-work-your-way-custom-tools-context-and-workflows)를 **한글 + 비개발자 + CLI 전용**으로 재구성한 것입니다. (Visual Studio Code 없이 터미널만으로 진행합니다.)

---

## 🧭 이 실습에서 하는 일

1. **설치** — 내 컴퓨터에 GitHub Copilot CLI를 깐다 (자동 설치 스크립트 제공)
2. **게임 만들기** — "우주 침략자(Space Invaders) 게임 만들어줘" 한마디로 게임을 생성
3. **공유하기** — 만든 게임 화면을 자동으로 캡처해서 모두의 갤러리에 올린다
4. **나만의 규칙 만들기** — Copilot이 항상 따를 규칙을 자연어로 만든다
5. **나만의 기능 만들기** — "내 게임을 갤러리에 올리는 기능"을 직접 만든다

모두 **터미널에 자연어로 말하면** 됩니다.

---

## 👥 누구를 위한 문서인가

| 역할 | 봐야 할 문서 |
|------|-------------|
| **참가자** (실습하는 사람) | 아래 "참가자 시작하기" → `docs/` 모듈 순서대로 |
| **진행자/관리자** (행사를 준비하는 사람) | [`admin/관리자_가이드.md`](admin/관리자_가이드.md) — Azure 공용 갤러리 구축, Codespaces 준비 |

---

## 🚀 참가자 시작하기

### 1단계: 준비물 자동 설치

아래 **한 줄 명령**을 붙여넣으면 GitHub에서 설치 스크립트를 받아 바로 실행합니다. (한 번만)

**🪟 Windows** — 먼저 **PowerShell을 "관리자 권한으로 실행"** 합니다.
(시작 메뉴 → `PowerShell` 검색 → 마우스 오른쪽 → **관리자 권한으로 실행**)
그다음 아래 한 줄을 붙여넣고 Enter:
```powershell
Set-ExecutionPolicy Bypass -Scope Process -Force; Invoke-RestMethod https://raw.githubusercontent.com/asomi7007/copilot-cli-handson/main/setup/install.ps1 | Invoke-Expression
```

**🍎 Mac / Linux** — **터미널**을 열고 아래 한 줄을 붙여넣습니다. (sudo 불필요)
```bash
curl -fsSL https://raw.githubusercontent.com/asomi7007/copilot-cli-handson/main/setup/install.sh | bash
```

> - Windows는 **관리자 권한**이 필요합니다(Node 설치·전역 npm 설치). 일반 PowerShell이면 스크립트가 안내 후 종료합니다.
> - 설치 스크립트(`install.ps1` / `install.sh`)는 영문입니다(콘솔 메시지 깨짐 방지). 설명 문서는 한글입니다.

자세한 방법(저장소 clone 실행, 개별 설치 링크 등)은 [`setup/설치_가이드.md`](setup/설치_가이드.md)를 참고하세요.

### 2단계: 실습 시작

설치가 끝나면 [`docs/00-시작하기.md`](docs/00-시작하기.md)부터 순서대로 따라가세요.

| 모듈 | 내용 |
|------|------|
| [00 — 시작하기](docs/00-시작하기.md) | 전체 흐름 소개 |
| [01 — 환경 준비와 로그인](docs/01-환경준비와-로그인.md) | Copilot CLI 켜고 GitHub 로그인 |
| [02 — 플러그인 설치](docs/02-플러그인-설치.md) | 게임 공유 도구 설치 |
| [03 — 게임 만들기](docs/03-게임-만들기.md) | 자연어로 게임 생성 |
| [04 — 게임 공유하기](docs/04-게임-공유하기.md) | 화면 캡처 후 갤러리에 자동 공유 |
| [05 — 커스터마이징 둘러보기](docs/05-커스터마이징-둘러보기.md) | Copilot을 구성하는 9가지 요소 익히기 |
| [06 — 나만의 지침 만들기](docs/06-나만의-규칙-만들기.md) | 지침(Instructions) 파일 생성 |
| [07 — 나만의 스킬 만들기](docs/07-나만의-기능-만들기.md) | 스킬(Skills) 직접 생성 |
| [08 — 정리](docs/08-정리.md) | 배운 내용 한눈에 |

**🎁 보너스 (시간이 남으면)**

| 보너스 | 내용 |
|--------|------|
| [보너스 1 — 클라우드 에이전트](docs/bonus-01-클라우드-에이전트.md) | GitHub에 올려 클라우드 Copilot에게 작업 위임 |
| [보너스 2 — MCP 서버 연결](docs/bonus-02-MCP-서버-연결.md) | 외부 도구를 자연어로 직접 사용 |
| [보너스 3 — 재사용 프롬프트](docs/bonus-03-재사용-프롬프트.md) | 자주 쓰는 작업을 /명령으로 저장 |

---

## 💡 핵심 메시지

이 실습이 끝나면 이런 걸 알게 됩니다:

- 코딩을 몰라도 **자연어로** 컴퓨터에게 일을 시킬 수 있다
- Copilot CLI는 게임뿐 아니라 **문서 작성, 파일 정리, 자료 조사, 업무 자동화**에도 똑같이 쓸 수 있다
- Copilot을 **내 방식대로** 길들이는 방법(규칙·기능 추가)이 있다

> 💬 GitHub Copilot은 AI라서 매번 똑같은 답을 주지 않습니다. 화면에 보이는 결과가 이 문서와 조금 달라도 정상입니다. 마음에 안 들면 그냥 다시 말로 고쳐달라고 하면 됩니다.
