# 2026-11-13 — ARTEL SDK 최신 develop 커밋 적용

- Date: 2026-11-13
- GitHub Issue: None (저장소 Issues 비활성화)
- Status: Implemented; runtime replacement pending

## Goal
ARTEL SDK를 bf69707ada3295ee3e5011ce59b29186aa68b89f에서 465e18c88512f858db83741fb9b96d64a658b2be로 고정하고, 새 SDK와 기존 QA 치트를 포함한 Windows 개발 빌드를 검증한다.

## Non-goals
운영 서버 배포, 클라이언트 버전 증가, SDK 저장소 자체 변경은 하지 않는다. 사용자가 추가 요청한 세력 이전 버전 정렬은 별도 QA 스택 계획과 함께 진행한다.

## Context / Constraints
SDK 패키지 버전 문자열은 두 커밋 모두 0.2.0이다. 최신 패키지에는 imgui 의존성이 추가됐으며 Unity 2022.3을 지원한다. Unity 실행 전에 기존 dirty 추적 파일을 Library/ArtelSdkUpdateVerification/20261113에 바이트 단위로 복사하고 실행 후 해시를 대조한다. artel/ 산출물도 보존한다. 현재 QA 실행이 확인되어 플레이어는 교체하지 않고 새 빌드부터 준비한다.

## Approach (Checklist)
- [x] **Step 0: Recon**: 원격 기본 브랜치와 커밋, 패키지 의존성, 기존 PR 병합 상태 확인.
- [x] **Step 1: Implementation**: manifest의 SDK 해시를 변경하고 Unity 2022.3.34f1로 packages-lock을 재생성한다. 필요한 일회용 Editor 빌드 코드는 만든 .cs와 .meta만 검증 후 제거한다.
- [x] **Step 2: Tests**: 기존 .qa/build를 덮지 않고 Build/ArtelQa/20261113에 Windows Development 빌드를 만든다. 새 PackageCache SHA, 빌드 컴파일 기록과 Artel.Runtime.dll을 대조하고 새 list_inputs 코드 포함도 확인한다. 실제 Editor/개발 플레이어 컴파일 및 기존 치트 코드 생성을 확인한다. 런타임 연결·호출 결과와 구분한다.
- [ ] **Step 3: Rollout / Rollback**: 활성 QA가 없다면 이번 대화에서 실행한 기존 클라이언트를 새 빌드로 교체하고 프로젝트 53 연결과 치트 카탈로그를 확인한다. 다른 세션을 중단하지 않는다. manifest/lock/계획과 로컬 QA 버전 고정 파일을 커밋하고 PR을 만든다.

## Validation
- **Commands to run:** Unity 2022.3.34f1 batchmode로 의존성 해석과 개발 빌드; git diff --check; 프로젝트 53의 게임 연결과 Game Function 카탈로그 조회.
- **Expected output:** 새 SHA와 일치하는 lock 및 PackageCache, 컴파일 오류 0개, 새 Windows 개발 빌드, 카탈로그의 logout_to_login 및 grant_all_magics.

## Risks & Rollback
- **Risks:** 입력·코드 생성·Game Function API가 변경됐을 수 있다. 원래 로컬 변경과 Unity가 만든 변경을 혼동하지 않는다. 실행 중인 QA가 있으면 플레이어 교체를 보류한다. 교체 전에는 이 대화에서 실행한 PID와 실제 실행 경로를 재확인한다.
- **Rollback steps:** 이번 변경의 manifest/lock 커밋을 되돌리고 원래 SDK 버전으로 다시 빌드한다. 원래 플레이어와 사용자 저장 상태는 보존한다.

## Open Questions
- 없음. 런타임의 카드 지급은 QA 서버 권한과 별개이며 SDK 업데이트의 성공 조건으로 계정 데이터를 변경하지 않는다.

## Verification results
- Unity 2022.3.34f1 Windows Development 빌드 완료.
- lock/PackageCache의 SHA 일치. Artel.Runtime.dll의 list_inputs 문자열과 Assembly-CSharp.dll의 QA 치트 10개 확인.
- 기존 dirty 추적 파일 17개의 작업 전 해시 보존 확인.
- 기존 QA 실행이 RUNNING이므로 새 플레이어의 ARTEL 연결과 치트 호출은 아직 검증하지 않음.
