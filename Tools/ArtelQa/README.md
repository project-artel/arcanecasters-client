# 세력 선택 이전 로컬 QA 환경

`versions.json`에 클라이언트, SDK, 로비·게임 서버의 소스 커밋을 고정한다.
기본 마법·덱을 로그인 시 지급하던 서버에 기존 QA API 커밋만 적용한다.
계정 서버와 DB는 기존 로컬 QA 스택을 사용한다.

## 서버 소스 준비

아래 명령은 `samples/arcanecasters`에서 실행한다. 대상 경로가 이미 있으면
새 worktree를 만들지 말고 해당 커밋과 적용된 QA 패치를 확인한다.

```bash
git -C arcanecasters-lobby worktree add --detach ../arcanecasters-client/Library/PreFactionQaSources/lobby 7c3b50454f0a4c71b4eb3debb62e2abcc013c4db
git -C arcanecasters-client/Library/PreFactionQaSources/lobby cherry-pick 60533f967daaaaa33df6b1c72aa88ec81f9c7b4e
git -C arcanecasters-game worktree add --detach ../arcanecasters-client/Library/PreFactionQaSources/game ebfb74cf65ca9c85906b5ed96780e98ffea724b7
git -C arcanecasters-client/Library/PreFactionQaSources/game cherry-pick f6e840eda30d294ad56442e930f8cf0fd701c7d2
```

## 로컬 서버 적용

기존 스택의 `.env`, 계정 서버, PostgreSQL, Redis가 준비된 상태에서 실행한다.
`compose.pre-faction.yaml`의 상대 경로는 첫 Compose 파일이 있는 디렉터리를 기준으로 한다.

```bash
docker compose -f compose.yaml -f arcanecasters-client/Tools/ArtelQa/compose.pre-faction.yaml build lobby game
docker compose -f compose.yaml -f arcanecasters-client/Tools/ArtelQa/compose.pre-faction.yaml up -d --no-deps --wait lobby game
```

DB 초기화나 마이그레이션 되돌리기는 필요하지 않다. 기존 서버 이미지는 보존한다.
이전 로컬 서버로 돌아갈 때는 원래 Compose 파일로 `lobby game`만 재생성한다.

## 클라이언트 실행

Unity `2022.3.34f1`로 Windows Development 빌드를 만든다. 출력은 `Build/` 아래에 둔다.
실행 환경 변수 `ARCANECASTERS_LOCAL_QA=1`을 설정해야 로컬 계정 서버
`127.0.0.1:6210`, 로비 `127.0.0.1:6209`에 연결한다.
환경 변수를 빼면 기본 서버 설정을 사용하므로 주의한다.

```powershell
$env:ARCANECASTERS_LOCAL_QA = '1'
artel game start --build ./Build/ArtelQa/20261113/ArcaneCasters.exe --project 53 --timeout 60 --json
```

실행 중인 QA가 있으면 기존 플레이어를 교체하기 전에 해당 실행의 종료를 확인한다.
로그아웃·전체 카드 지급 치트는 Development 빌드에 포함되며 카드 지급은 QA 허용 계정에서 사용한다.

## 2026-11-13 검증

- 최신 SDK `465e18c885`를 Unity가 해석해 lock 파일을 갱신했다.
- Windows Development 빌드를 완료했고 빌드된 `Assembly-CSharp.dll`에서
  `LogoutToLogin`, `GrantAllMagics`를 포함한 QA 치트 10개를 확인했다.
- 두 서버를 위 이미지로 교체한 후 모두 healthy 상태를 확인했다.
- 시딩하지 않은 신규 게스트에서 정상 로그인·사용자 초기화 API를 실행했다.
  카드 76종 보유·해금, 15장 기본 덱 1개와 선택 덱 자동 생성을 확인했다.
- Dockerfile의 이미지 빌드는 테스트를 생략하므로 별도 QA API 테스트 결과와 구분한다.
- `QaCheatControllerTest`와 `QaMatchControllerTest`를 각 빌드 환경에서 별도로 실행해 통과했다.
- 실행 중인 기존 ARTEL QA가 있어 새 플레이어의 런타임 연결·치트 호출 검증은 보류했다.
