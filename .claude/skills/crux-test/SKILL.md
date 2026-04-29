---
name: crux-test
description: CRUX 프로젝트 자율 플레이·정적 테스트 실행. Unity 에디터가 running 상태에서 Claude/에이전트가 직접 검증까지 수행. 사용자 개입 없이 커밋 → 검증 → 다음 단계 루프 유지.
allowed-tools: Read Bash Glob Grep mcp__unity__execute_menu_item mcp__unity__run_tests mcp__unity__notify_message
---

# CRUX 자율 테스트 실행 가이드

## 핵심 원칙

CRUX 프로젝트의 모든 코드 변경은 **사용자 개입 없이 검증까지 끝내고 커밋**한다. 사용자는 하루 대부분 외부 활동 중이며, 수동 플레이 테스트로 병목을 만들지 않는다.

자율 검증 경로 3가지:
1. **정적 어설션 테스트** — `Crux/Test/Run All Static` (P2A+P2B+P2C 실행, ~1초)
2. **플레이 스모크 테스트** — `Crux/Test/PlaySmoke TerrainTest (3s)` (씬 로드 + PlayMode 3초, ~5초)
3. **Unity Test Runner** — `mcp__unity__run_tests` (EditMode/PlayMode nunit, 설정 시 사용)

## 환경 전제 조건

- Unity Editor 6000.3.13f1 **이미 실행 중** (CRUX 프로젝트 열림)
- Unity MCP 서버(`mcp__unity__*`) 연결 정상 — `claude mcp list` 로 확인
- 자동 Asset 리프레시 활성 (기본값). 스크립트 편집 후 에디터가 자동 재컴파일

## 검증 프로토콜

### 1단계 — 컴파일 체크 (필수, 자율)

**MCP 기반 (우선)**:
```
mcp__coplay-mcp__check_compile_errors
```

**파일 기반 fallback** (MCP 끊김 시):
1. 편집 완료 후 Unity 자동 refresh 대기 (5~15초)
2. 필요 시 `mcp__unity__execute_menu_item(menuPath="Crux/Test/Force Recompile")` 로 강제 재컴파일 유도
3. `Read("CRUX/Temp/crux-compile-status.txt")` 결과 확인
   - 첫 줄 `[STATUS] OK (0 errors, N warnings)` → 통과
   - `[STATUS] FAIL N errors M warnings` → 이어지는 `[ERROR] file:line message` 라인 파싱
4. 파일이 없으면 아직 Unity가 재컴파일 안 한 상태 — 대기 연장 또는 Force Recompile

파일은 `CruxCompileLog.cs`(`[InitializeOnLoad]`)가 `CompilationPipeline` 이벤트 훅으로 자동 작성. 매 컴파일마다 덮어쓰기.

### 2단계 — 정적 테스트 (기본 회귀 방지)

```
mcp__unity__execute_menu_item(menuPath="Crux/Test/Run All Static")
```
결과 파일을 읽어 PASS/FAIL 확인:
```
Read("CRUX/Temp/crux-tests.log")
```

파일 끝에서 `[RUNNER] ===== TOTAL passed=N failed=N =====` 라인 확인.
`failed=0` 이면 PASS.

### 3단계 — 플레이 스모크 (런타임 로그·초기화 검증)

**중요: PlaySmoke MCP 응답 처리 규칙**
`mcp__unity__execute_menu_item(menuPath="Crux/Test/PlaySmoke TerrainTest (3s)")` 호출 시
**`Connection closed` 응답은 정상으로 간주** — 재시도 금지, 에러 보고 금지.
원인: PlayMode 진입 시 Unity 도메인 리로드가 WebSocket 세션을 끊는 정상 동작.
PlaySmoke 자체는 정상 실행되며 결과는 로그 파일에 기록된다.

**3단계 절차**:
1. **메뉴 호출**: `mcp__unity__execute_menu_item(menuPath="Crux/Test/PlaySmoke TerrainTest (3s)")` 실행. `Connection closed` 포함 모든 응답을 무시하고 다음 단계로 진행
2. **5초 대기**: `Bash sleep 5` — Unity PlayMode 진입 → 3초 스모크 → 종료 → 로그 기록 완료까지의 시간
3. **로그 Read**: `Read("CRUX/Temp/crux-playsmoke.log")` 로 결과 확인

합격 조건:
- (a) 파일 mtime이 호출 이후로 갱신됨
- (b) `[Exception]` 또는 `[Error]` 부재
- (c) 아래 기대 패턴 출현

기대 로그 패턴(DoD별 grep):
- `[SMOKE] start` + `[SMOKE] finished reason=exited` — 정상 종료
- `[Log] [CRUX] TankCrew 초기화` — 커밋 1(P3-a) 회귀 확인
- `[Log] [CRUX] morale` — 커밋 2(P3-b) 회귀 (사격 발생 시만)
- `[Log] [CRUX] 이니셔티브:` 또는 `[Log] [CRUX] 선공 판정` — 커밋 3(P3-c) 회귀

실패 판정 방법:
```bash
grep -E "\[Exception\]|\[Error\]" CRUX/Temp/crux-playsmoke.log
```

### 4단계 — 긴 스모크 (AI·반응 사격 등 복잡 흐름)

3초로 부족한 시나리오는 8초:
```
mcp__unity__execute_menu_item(menuPath="Crux/Test/PlaySmoke TerrainTest (8s)")
```

### 5단계 — 긴급 중단

하네스가 PlayMode에 갇히면:
```
mcp__unity__execute_menu_item(menuPath="Crux/Test/PlaySmoke Abort")
```

## 자동 검증 불가 항목 (사용자 요청 목록)

아래 항목은 현재 자동 검증 불가능 — 모나미/시그마/픽셀 검증 보고서 말미에
**"사용자 수동 확인 필요"** 섹션을 만들어 일괄 축적 → 사용자 귀가 시 한 번에 요청.

### 시각·UX 관련 (사용자 수동)
- 스프라이트 틴팅/색조 비교 (HP별 암전, 파괴 상태)
- HUD 레이아웃 깨짐 (해상도별, 가변 UI)
- 연출 시퀀스 타이밍 감각 (발사→명중→폭발 간격)
- 카메라 줌/팬/프레이밍 자연스러움
- 반응 사격 점프 카메라 복귀

### 인풋 의존 플로우 (사용자 수동)
- 키/마우스 조합 실제 조작 (방향 선택, 무기 선택)
- 드래그/휠 줌 감도
- 예: "사격 키를 눌렀을 때 WeaponSelect 모드로 전환 후 1/2키 반응"

### 튜닝·감각 (사용자 수동)
- 밸런스 수치 체감 (명중률·관통률·사기 델타)
- 난이도 곡선
- 사운드 타이밍 (구현 시)

### 대규모 통합 (자동화 후보, 아직 미구현)
- End-to-end 전투 시나리오 (턴 10회+)
- 승리/패배 조건 검증
- 세이브/로드 라운드트립

## 에이전트 보고 형식에 추가

모나미/시그마 검증 보고서 말미에 다음 섹션 추가:

```
## 사용자 수동 확인 요청 (자동화 불가)

- [항목 1] 이유: 시각 감각적 판단 필요
- [항목 2] 이유: 실제 키보드 입력 시뮬레이션 불가
- ...

요청 시점: 다음 귀가 후 일괄
```

부모 세션은 이 목록을 누적해 두었다가 사용자 세션 복귀 시 한 번에 제시.

## 실패 모드

### Unity MCP 연결 끊김
증상: `mcp__unity__execute_menu_item` → `Connection failed: Unknown error`

복구:
1. `claude mcp list` 로 상태 확인 (Connected / Failed)
2. Failed 시 사용자에게 "Claude Code 재시작 또는 Unity 에디터 재기동 필요" 보고
3. 대기 상태에서 파일 편집·구조 감사는 계속 가능 (Read/Grep 기반)

### 컴파일 실패
증상: Asset Refresh 후 Unity Console에 CS###### 에러

복구:
1. `Crux/Test/PlaySmoke TerrainTest` 가 에러로 즉시 fail
2. `Temp/crux-playsmoke.log` 에 Exception/Error 기록됨
3. 시그마에게 정확한 에러 위치 + 라인 전달해 재작업

### 플레이 스모크 무한 루프
증상: `Temp/crux-playsmoke.log` 에 `finished` 마커 없음, 5초 넘게 실행

복구:
1. `Crux/Test/PlaySmoke Abort` 호출
2. 원인 조사 (Scene에 초기화 무한 루프? 스크립트 에러?)

## 확장 권고

향후 추가할 자동 검증:
1. **P3 Integration Test** — BattleController 턴 루프 통합 회귀 (현재 수동 스모크로만 확인)
2. **HUD 스크린샷 diff** — `mcp__coplay-mcp__capture_scene_object` 기반 시각 회귀 (MCP 복구 시)
3. **Performance 스모크** — `get_worst_cpu_frames` 활용 (MCP 복구 시)
4. **Scene Roundtrip** — Save/Restore 라운드트립 테스트

## Scenario Runner

자동화 시나리오 러너 — PlayMode에서 BattleController API를 순서대로 호출하고 상태를 검증한다.

### 메뉴

| 메뉴 경로 | 설명 |
|---|---|
| `Crux/Test/Run Scenario (Pick)` | 파일 선택 다이얼로그로 .asset 선택 후 실행 |
| `Crux/Test/Run Scenario (Last)` | 직전 Pick 경로로 재실행 |
| `Crux/Test/Create PostMoveFlow Scenario` | PostMoveFlow 5-step PoC 자산 자동 생성 |

### 자산 위치

- SO (ScriptableObject) 저장: `Assets/_Project/ScriptableObjects/Scenarios/`
- 생성: 우클릭 → Create → Crux → Test → Scenario

### 출력 경로 (`CRUX/Temp/` 아래)

| 파일 | 내용 |
|---|---|
| `crux-scenario-<name>.log` | 스텝별 `[PASS|FAIL] idx label - detail` + 타임스탬프 |
| `crux-scenario-<name>-summary.json` | `{ total, passed, failed, durationSec, steps:[...] }` |
| `crux-scenario-<name>/<idx>_<label>.png` | 1280×720 스크린샷 (CapturePolicy.Always 또는 실패 시) |
| `crux-scenario-<name>/<idx>_<label>.diff.png` | 골든 이미지 비교 diff (골든 있을 때만) |

### 골든 이미지 디렉토리

`Assets/_Project/Tests/Golden/<scenarioName>/` 아래에 동명 PNG를 두면 자동 비교.
없으면 비교 skip (항상 ok=true).

### 신규 ScenarioAction enum 추가 절차

1. `CruxScenarioStep.cs` — `ScenarioAction` enum에 값 추가
2. `CruxScenarioInputHelper.cs` — `ApplyAction()` switch에 case 추가
3. `check_compile_errors` 후 커밋

## 파일 경로 참조

- 플레이 스모크 결과: `CRUX/Temp/crux-playsmoke.log`
- 정적 테스트 결과: `CRUX/Temp/crux-tests.log`
- 시나리오 러너 소스: `CRUX/Assets/_Project/Scripts/Editor/Automation/CruxScenario*.cs`
- 하네스 소스: `CRUX/Assets/_Project/Scripts/Editor/CruxPlaySmoke.cs`, `CruxTestRunner.cs`
- 기존 P2 테스트: `CRUX/Assets/_Project/Scripts/Editor/P2{A,B,C}_*.cs`
