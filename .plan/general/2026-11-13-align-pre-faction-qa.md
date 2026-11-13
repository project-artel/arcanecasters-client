# 2026-11-13 — 세력 선택 이전 클라이언트·서버 QA 버전 정렬

- Date: 2026-11-13
- GitHub Issue: None (샘플 QA 환경 정렬)
- Status: Implemented; player replacement pending

## Goal
세력 선택이 없던 클라이언트와 맞도록 로컬 QA 로비·게임 서버를 진영 도입 직전 소스로 빌드한다. 첫 로그인 기본 마법·덱 자동 지급을 유지하며 최신 ARTEL SDK와 기존 QA 전용 기능도 사용할 수 있게 한다.

## Non-goals
운영 서비스 배포나 롤백, 데이터베이스 마이그레이션 되돌리기, 기존 데이터·체크아웃 초기화, 진행 중인 QA 임의 중단은 하지 않는다.

## Context / Constraints
- 현재 클라이언트는 진영 선택 코드가 없는 a3dded5a 기반이며 QA 치트 추가 커밋을 포함한다.
- 로비의 94dfb10은 초기 사용자 생성에서 initUserMagic/initUserDeck 호출을 제거한다. 그 직전 7c3b50454f0a4c71b4eb3debb62e2abcc013c4db는 진영 선택이 없고 초기 지급을 수행한다.
- 게임의 세력별 튜토리얼 도입 199ba1fb 직전은 ebfb74cf65ca9c85906b5ed96780e98ffea724b7이다.
- 로컬 arcanecasters-qa Compose 스택은 현재 정상 실행 중이다. 현재 실행한 클라이언트는 운영 도메인에 연결돼 있었으므로 새 클라이언트는 ARCANECASTERS_LOCAL_QA=1로 로컬 Account 6210/Lobby 6209에 연결한다.

## Approach (Checklist)
- [x] **Step 0: Recon**: 세력 도입 diff, 이전 커밋, 클라이언트 카드 프로토콜, 로컬 Compose 상태 확인.
- [x] **Step 1: Implementation**: 원본 서버 체크아웃을 변경하지 않고 Library/PreFactionQaSources 아래 분리 worktree를 만든다. 로비 7c3b504 + 기존 QA API 커밋 60533f9, 게임 ebfb74cf + 기존 QA API 커밋 f6e840ed를 적용한다. 충돌과 컴파일 결과를 검토한다.
- [x] **Step 2: Tests**: 로컬 서버 빌드·관련 테스트, QA 마법 목록·매치 API와 기본 카드/15장 덱 준비 확인. 기존 QA 계정은 seed-deck.sql로 채워져 있어 기본 지급의 증거로 쓰지 않는다. 로컬 Account의 공개 게스트 시작 API로 신규 테스트 사용자를 만들고, seed 없이 Lobby의 정상 GET /api/users/mine 초기화를 실행한다. 그 계정의 DEFAULT·PLAYER 카드 지급, 해금 상태, user_id=0 템플릿에서 복제된 비어 있지 않은 15장 덱과 selectedDeckId를 API로 확인한다. 이 검증이 실패하면 기본 지급 복구를 주장하지 않는다. Dockerfile의 -x test 빌드 성공과 별도 관련 테스트 결과도 구분한다. 서버/클라이언트 magicId 카드 프로토콜과 빌드 출처를 기록한다.
- [ ] **Step 3: Rollout / Rollback**: 전용 Compose override로 lobby/game만 새 로컬 이미지로 교체한다. account/postgres/redis와 기존 볼륨·마이그레이션은 유지한다. 새 SDK 클라이언트 빌드를 준비하고 실행 중 QA 상태를 확인한 뒤 교체를 결정한다.

## Validation
- **Commands to run:** Docker Compose build/up의 lobby/game 범위 실행, 컨테이너 health와 소스 SHA 확인, 문서화된 로컬 QA 계정의 정상 로그인·카드 목록·선택 덱 조회, 새 개발 빌드 컴파일와 ARTEL 프로젝트 53 연결 확인.
- **Expected output:** 세력 이전 소스와 QA 전용 API를 포함한 두 healthy 서버, seed하지 않은 신규 사용자의 기본 마법과 자동 생성된 15장 덱, 최신 SDK를 포함한 클라이언트 빌드. 신규 사용자의 덱 생성 결과로 템플릿 존재를 확인하고 필요하면 기존 저장소의 관련 테스트로 DEFAULT·PLAYER 필터를 검증한다. 비밀번호·JWT·키는 출력하거나 새 파일에 저장하지 않는다.

## Risks & Rollback
- **Risks:** 과거 서버와 현재 DB의 호환성 및 QA 커밋 적용 충돌. 기존 QA 서버 이미지를 보존하고 컨테이너·볼륨 삭제 없이 롤백한다. 다른 QA 프로젝트나 진행 중인 게임 실행은 중단하지 않는다.
- **Rollback steps:** 원래 Compose 파일의 기존 lobby/game 이미지로 재생성한다. 사용자 DB 볼륨은 그대로 둔다. 기존 클라이언트 실행 파일도 보존한다.

## Open Questions
- 새 클라이언트 교체 시 현재 실행 중인 순서형 QA의 종료 여부를 다시 확인해야 한다.

## Verification results
- 서버 이미지 빌드와 개별 QaCheatControllerTest/QaMatchControllerTest 통과.
- 적용한 로비·게임 컨테이너 모두 healthy.
- seed하지 않은 신규 게스트 25: 카드 76종 보유·해금, 15장 덱 1개, selectedDeckId 62.
- 클라이언트 최신 SDK 개발 빌드 완료. 기존 ARTEL QA가 RUNNING이라 플레이어 교체는 보류.
