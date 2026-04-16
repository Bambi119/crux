# CRUX — 프로젝트 하네스 규칙

> 전역 `~/.claude/CLAUDE.md`에 대한 **프로젝트 추가 규칙**. 충돌 시 이 파일이 우선.
> 목적: 1인 개발 Unity 프로젝트의 **스파게티화 예방** + 모듈 경계 강제 + 점진적 리팩토링 스케줄 관리.

## 0. 프로젝트 스냅샷

- **엔진**: Unity 6.3 LTS · 2D 탑뷰 · flat-top hex (odd-q offset)
- **장르**: 턴제 전술 전차 전투 + 모듈 장갑 + 파츠 조합
- **씬**: `StrategyScene`(8×10 표준) · `TerrainTestScene`(12×12 지형 테스트) · `FireActionScene`(연출)
- **1인 개발**: 에이전트 위임 가능, 리뷰어 모나미가 리팩토링 주도
- **권위 있는 기획 문서**: `docs/00_index.md` · 적 AI는 `docs/12_enemy_ai.md`

## 1. 모듈 아키텍처 (namespace ↔ 폴더 ↔ 책임)

```
Crux.Data       /Scripts/Data        ScriptableObject 정의 (TankDataSO·AmmoDataSO·CoverDataSO 등)
Crux.Grid       /Scripts/Grid        hex 좌표·셀·경로·LOS
Crux.Unit       /Scripts/Unit        GridTankUnit·모듈 시스템·상태 오버레이
Crux.Combat     /Scripts/Combat      관통 계산·VFX·피격 효과
Crux.AI         /Scripts/AI          적 의사결정 (Context/Role/Scoring/Decision/Controller)
Crux.Camera     /Scripts/Camera      BattleCamera — 줌·팬·프레이밍·반응 시퀀스 제어권 양보
Crux.Input      /Scripts/Input       PlayerInputHandler — 키 입력·상태 전이
Crux.UI         /Scripts/UI          BattleHUD + 패널 분리(UnitPanel/FirePreview/TerrainOverlay 등)
Crux.Cinematic  /Scripts/Cinematic   연출 씬·DamagePopup
Crux.Core       /Scripts/Core        전투 상태·턴 오케스트레이션만. HUD/입력/카메라 포함 금지
```

### 1.1 경계 강제 규칙

- **한 폴더 안쪽만 자기 namespace 사용**. 다른 폴더에 코드를 놓고 namespace만 다른 건 금지.
- **`Crux.Core`는 순수 오케스트레이션**. `OnGUI`·`Input.Get*`·카메라 조작·스프라이트 생성 금지.
- **ScriptableObject 데이터 정의는 `Crux.Data`만**. 다른 namespace에서 `[CreateAssetMenu]` 금지.
- `Editor/` 코드는 런타임 코드를 절대 참조하지 않음 (반대는 OK).

## 2. 파일·메서드 크기 예산

| 대상 | 소프트 | 하드 | 예외 |
|---|---|---|---|
| 클래스 파일 | 500 LOC | **800 LOC** | 데이터 테이블·스프라이트 제너레이터 (주석 명시) |
| 메서드 | 60 LOC | **120 LOC** | 상태기계 switch·OnGUI 드로어 (주석 명시) |
| 한 커밋 파일 수 | 5개 | **8개** | 리팩토링 PR (스케줄 진행 시) |

- **800 LOC 초과 시 분할 전에는 새 기능 추가 금지**.
- 120 LOC 초과 메서드는 일부 추출 필수.
- 새 파일 생성 시 이 예산을 **감안하고 미래 성장 여지 20%** 남기기.

## 3. 의존성 방향 (layering)

```
Data ──┐
       ├──→ Grid ──→ Unit ──→ Combat ──→ AI
       │                                  ↓
       └────→ Camera → Input → UI → Core (오케스트레이션)
                                     ↓
                               Cinematic (리프, 순환 금지)
```

- **상위가 하위를 참조**. 역방향 금지 (`Grid`가 `Core`를 참조하면 즉시 에러).
- **순환 의존 금지**. `Combat`에서 `AI`를 건드리는 순간 잘못된 방향.
- `Crux.Core`의 `BattleStateStorage` 같은 static 저장소는 레이어 경계를 우회하므로 **신중하게**.

## 4. 리팩토링 트리거 (자동 발동 조건)

다음 중 하나라도 참이면 **즉시 리팩토링 우선**, 새 기능 대기:

1. 어느 파일이 600 LOC 초과
2. 한 메서드가 80 LOC 초과
3. 한 클래스가 3개 이상의 **명확히 다른 책임**을 가짐
4. 한 파일에 5개 이상의 `Draw*` / `Handle*` / `Execute*` 메서드
5. `public static` 필드가 3개 이상 같은 클래스에 존재
6. `partial class`를 쓰려는 유혹을 느낌 (→ 분할이 신호)
7. 같은 상수를 2곳 이상에서 하드코딩

## 5. 안티패턴 금지 목록

### 🚫 절대 금지
- **`BattleController` 증축 금지** — 기본값 착륙 지점으로 쓰지 말 것. 새 기능은 **어느 폴더에 속하는지 먼저 결정** → 해당 폴더에 신규 파일 생성 → Core는 조립만
- `partial class` 남발 — 한 클래스를 여러 파일에 쪼개도 여전히 한 클래스
- 전역 `public static` 신규 필드 — 필요하면 별도 Storage 클래스로
- `Core/` 안에서 `OnGUI()` / `Input.Get*` / `Camera.main.*`
- 한 커밋에서 `BattleController.cs` + 4개 이상 다른 파일 동시 수정 (범위 재검토 신호)

### ⚠️ 경고 (재고 필요)
- 새 `MonoBehaviour`를 기존 거대 파일에 `private class`로 박아넣기
- 같은 세션에서 3개 이상의 기능 동시 진행
- 사용하지 않는 `using`을 방치
- 의도 없는 `TODO` 코멘트 (TODO는 파일 말미 섹션 또는 이슈로)

## 6. 새 기능 추가 체크리스트 (세션 시작 자문)

```
□ 이 기능이 속하는 namespace/폴더는 명확한가?
□ 기존 파일 중 책임이 맞는 곳이 있는가?
   있음 → 그 파일에 추가 (단, §2 예산 확인)
   없음 → 신규 파일 이름·폴더 결정
□ BattleController를 건드려야 하는가?
   "오케스트레이션 연결" 이외의 이유면 재고
□ 800 LOC 근접하는가? 근접 시 분할 먼저
□ 새 public static 필드가 필요한가? 대안 먼저 검토
□ 컴파일 체크(mcp__coplay-mcp__check_compile_errors) 후 커밋
```

## 7. 하네스 엔지니어링 원칙 (CRUX 특화)

### 7.1 편집→검증 루프
- **파일 편집 직후 재읽기 금지** (context 낭비). Edit/Write는 성공 시 상태를 보장함
- **비트 단위 편집 후 즉시 `check_compile_errors`** — 쌓아놓고 한 번에 검증하지 말 것
- **검증 전 완료 선언 금지** — 전역 규칙과 일치

### 7.2 Unity 씬·에셋 특수성
- **Scene .unity 파일은 YAML이지만 GUID 연결 복잡** — 신규 씬은 복제(cp) 후 `.meta` guid 새 발급
- **`GameConstants.GridWidth/Height` 하드코드 금지** — `grid.Width/grid.Height` 사용 (테스트 맵 대응)
- **EditorBuildSettings.asset** 편집 시 scene guid 일치 필수
- **`.meta` 파일 수동 삭제 금지** — Unity가 자동 관리, 이미지/에셋 이동 시에도 .meta 동행

### 7.3 기능 검증 경로
- **새 기능 → TerrainTestScene에서 먼저** (12×12 + 지형 + 디버그 오버레이 F1)
- StrategyScene은 회귀 확인용, 기능 개발에 쓰지 말 것
- 오버워치/반응 시퀀스 같은 시각 기능 → Unity MCP `play_game` + `capture_scene_object`로 실제 시각 검증 시도. MCP로 입력 시뮬이 어려울 때만 사용자 플레이 요청

### 7.4 커밋 규율
- **커밋 하나에 한 가지 관심사** — 리팩토링과 기능 추가 섞지 말 것
- **Conventional Commits**: `feat:` · `fix:` · `refactor:` · `docs:` · `chore:` · `tune:`
- 파일 수 > 8 커밋은 재고 신호
- `BattleController.cs` 단독 커밋이 반복되면 분할 신호

### 7.5 디버깅·관찰성
- 시각 기능 버그는 **Unity MCP로 먼저 확인** (play → capture)
- 로그 레이블: `[CRUX]` (게임 이벤트) / `[AI]` (의사결정) / `[FIRE]` (사격)
- 프로덕션 로그는 `Debug.Log` 허용, 단 튜닝용 verbose는 `#if UNITY_EDITOR` 가드 고려

### 7.6 자율 검증 루프 (사용자 개입 없는 커밋)

사용자는 하루 대부분 외부 활동 중. 수동 플레이 테스트로 개발 루프가 병목되면 안 됨.
**모든 코드 변경 커밋 전 `crux-test` 스킬로 자체 검증**을 수행한다.

**의무 검증 3단계**:

1. **컴파일 체크** — `mcp__unity__execute_menu_item(menuPath="Assets/Refresh")` → Unity 재컴파일 완료 대기. 또는 Coplay MCP 정상 시 `mcp__coplay-mcp__check_compile_errors` 직접 호출
2. **정적 테스트** — `mcp__unity__execute_menu_item(menuPath="Crux/Test/Run All Static")` → `CRUX/Temp/crux-tests.log` 에서 `failed=0` 확인
3. **플레이 스모크** — `mcp__unity__execute_menu_item(menuPath="Crux/Test/PlaySmoke TerrainTest (3s)")` → `CRUX/Temp/crux-playsmoke.log` 에서 Exception/Error 없음 + 필요한 `[CRUX]` 로그 출현 확인

**실패 시**: 즉시 구현 에이전트(시그마/픽셀)에 재작업 위임. 루프는 최대 2회.

**자동 검증 불가 항목** (시각·입력·감각): 별도 목록에 누적. 사용자 귀가 시 일괄 요청.
보고서 말미에 `## 사용자 수동 확인 요청` 섹션 필수.

**하네스 소스**:
- `CRUX/Assets/_Project/Scripts/Editor/CruxPlaySmoke.cs` — PlayMode 스모크
- `CRUX/Assets/_Project/Scripts/Editor/CruxTestRunner.cs` — 정적 테스트 오케스트레이션
- `.claude/skills/crux-test/SKILL.md` — 에이전트 실행 가이드

**MCP 연결 실패 시**:
- `claude mcp list` 로 상태 확인
- 실패 시 파일 감사·Read/Grep 기반 정적 검증까지만 수행
- 런타임 검증이 필수인 항목은 "MCP 복구 대기" 로 기록, 커밋은 사용자 지시 때까지 보류

## 8. 에이전트 사용 정책

| 에이전트 | 언제 |
|---|---|
| **시타 (planner)** | 사용자 요청 진입점. 탐색→계획→위임→검증 루프 주도 |
| **시그마 (backend)** | Grid/Unit/Combat/AI/Data/Core 구현 (리팩토링 포함) |
| **픽셀 (frontend)** | OnGUI HUD/UI/Camera/Cinematic/스프라이트/VFX 구현 (리팩토링 포함) |
| **모나미 (reviewer)** | 검증 전담 — 컴파일·테스트·구조 감사·성능. **직접 구현/편집 금지** |

### 8.1 위임 원칙
- **탐색(Explore)은 subagent** — 3개 이상의 Grep/Read가 예상되면 Explore 에이전트에 넘김
- **병렬 가능한 작업은 동시 호출** — 시그마 + 픽셀 동시 실행 가능
- **구현과 검증의 분리** — 리팩토링도 도메인에 따라 시그마(백엔드)/픽셀(프론트엔드)가 실행. 모나미는 DoD 검증만. 시타가 검증 불합격 시 구현 에이전트에 재작업 지시 루프
- **모호하면 사용자에게 질문** — 특히 밸런스·게임 디자인 관련

## 9. BattleController 분할 로드맵 (P-S1 ~ P-S7) — ✅ 완료

`BattleController.cs`: 2,532 LOC → **671 LOC** (2026-04-15 시점). 목표치 500 LOC에는 미달이지만 P-S7까지 전 스케줄 완료. 후속 잔여 정리는 Tech Debt로 추적.

아래 스케줄 기록은 **이력 보존용**. 신규 리팩토링 필요 시 §4 리팩토링 트리거에 따라 새 항목 생성.

### P-S1 — HUD 추출 (예상 감소: −900 LOC)
- **추출 대상**: `OnGUI`·`DrawBanner`·`DrawTurnInfo`·`DrawUnitInfo`·`DrawUnitInfoPanel`·`DrawFireTargetPreview`·`DrawInputModeInfo`·`DrawMoveDirectionUI`·`DrawWeaponSelectUI`·`DrawModuleStatus`·`DrawControls`·`DrawGameResult`·`DrawReactionAlert`·`DrawTerrainOverlay`·`DrawTerrainHoverInfo`
- **신규 위치**: `Scripts/UI/BattleHUD.cs` (메인 orchestrator) + 필요 시 패널별 partial class 지양하고 별도 클래스로
- **의존성**: BattleHUD는 BattleController를 참조(상태 읽기)하고, BattleController는 BattleHUD에 위임
- **DoD**: BattleController 줄 수 ≤ 1,700 · 컴파일 0 · TerrainTestScene Play 정상 · HUD 시각 동일

### P-S2 — Camera 추출 (예상 감소: −100 LOC)
- **추출 대상**: `HandleCamera`·카메라 필드(`camTargetPos`/`camTargetSize`/`camMinSize`/`camMaxSize`/`camPanSpeed`/`edgePanMargin`)·초기 프레이밍 로직
- **신규 위치**: `Scripts/Camera/BattleCamera.cs` MonoBehaviour
- **의존성**: `IsReactionPlaying` 플래그로 제어권 양보. 반응 시퀀스는 BattleCamera API로 지시
- **DoD**: 줌/팬 입력 동작 동일 · 반응 사격 카메라 점프 정상

### P-S3 — Input 추출 (예상 감소: −200 LOC)
- **추출 대상**: `HandlePlayerInput` 전체
- **신규 위치**: `Scripts/Input/PlayerInputHandler.cs`
- **의존성**: `BattleController`의 공개 메서드로 행동 지시 (`TrySelectUnit`·`TryMove`·`TryFire`·`ConfirmEndTurn`)
- **DoD**: 모든 키바인딩 동일 · 상태 전이 동일

### P-S4 — Fire Executor 추출 (예상 감소: −550 LOC)
- **추출 대상**: `ExecuteFire`·`ExecuteMGFire`·관련 hit/pen 계산 조립
- **신규 위치**: `Scripts/Combat/FireExecutor.cs`
- **의존성**: `CalculateHitChanceWithCover`는 Combat로 이동 가능 여부 검토
- **DoD**: 사격 결과 동일 · 연출 씬 전환 정상

### P-S5 — Reaction Fire Sequence 추출 (예상 감소: −250 LOC)
- **추출 대상**: `HandleEnemyMoveStep`·`ExecuteReactionFireSequence`·`AnimateReactionTracer`·`ShowAlertAt`·`DrawReactionAlert`·`IsReactionPlaying` 플래그
- **신규 위치**: `Scripts/Combat/ReactionFireSequence.cs`
- **의존성**: P-S2(Camera) 완료 후 진행 — Camera API 필요
- **DoD**: 반응 사격 시퀀스 동일 · 이동 적 pause/resume 정상

### P-S6 — State Save/Restore 추출 (예상 감소: −200 LOC)
- **추출 대상**: `SaveBattleState`·`RestoreBattleState`·`ApplyPendingResult`
- **신규 위치**: `Scripts/Core/BattleStateManager.cs` (Core 유지, 단 분리)
- **DoD**: 씬 복귀 시 상태 동일

### P-S7 — 잔여 정리 + 최종 검증 (예상 감소: −100 LOC)
- **대상**: 남은 Overwatch 로직·`TickSmoke`·`HandleFireKill`·유틸 함수 재배치
- **DoD**: BattleController ≤ 500 LOC · 전체 회귀 없음 · docs/12_enemy_ai.md §1 § 2와 코드 구조 일치 검증

### 스케줄 실행 주의사항
- **P-S1이 가장 큼 + 가장 독립적** → 첫 타깃. 시타가 한 세션 안에 완료 가능 여부 판단 (HUD 추출은 픽셀 담당)
- **각 스케줄은 PR 규모의 단독 커밋**, 메시지 예: `refactor(P-S1): HUD 추출 — BattleController 2532 → 1640`
- **중간 이탈 금지**: 스케줄 시작했으면 DoD까지 간다. 도중에 새 기능 섞지 말 것
- **각 스케줄 직후 회귀 검증**: `check_compile_errors` → Play 모드 로드 → 스크린샷

## 10. 기술 부채 원장 (Tech Debt Ledger)

현재 알려진 부채. 해결될 때마다 **이 목록에서 제거**.

| ID | 부채 | 심각도 | 해결 경로 |
|---|---|---|---|
| TD-01 | `BattleController.cs` 671 LOC — 목표 500 LOC 미달, 잔여 P-S7+ 정리 후속 | 🟢 P3 | §4 트리거 재발동 시 |
| TD-02 | `FireSequenceController.cs` 1,137 LOC 단일 파일 | 🟡 P2 | 시퀀스 단계별 분할 (후속) |
| TD-03 | `FireActionContext.cs`에 4개 타입 혼재 | 🟢 P3 | 부가 정리 |
| TD-04 | 빈 의도 폴더 Enemy/Loot/Vision (Camera/Input/UI는 채워짐) | 🟢 P3 | Phase 2 콘텐츠 확장 시 |
| TD-05 | `HitEffects`/`MuzzleFlash`/VFX 렌더 상수 하드코드 | 🟢 P4 | 밸런스 패스 때 Data SO로 |
| TD-06 | 지형 플로어 스프라이트가 다크톤 multiply라 틴트 약함 | 🟡 P2 | Pixel 에이전트 타일 변형 생성 |
| TD-07 | `BattleHUD.cs` OnGUI → uGUI 이관 | 🟢 P3 | 첫 빌드 완성 이후 리팩토링 슬롯 (docs/10 §5) |

## 11. 변경 이력

| 날짜 | 변경 |
|---|---|
| 2026-04-14 | 초판. 아키텍처·예산·레이어·리팩토링 트리거·S1~S7 스케줄·부채 원장 수립 |
| 2026-04-16 | §1 Camera/Input/UI 폴더 상태 갱신 · §9 P-S1~S7 완료 표시 · §10 TD-01/TD-04 현재 상태 반영 |
