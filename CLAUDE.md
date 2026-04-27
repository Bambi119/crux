# CRUX — 프로젝트 하네스 규칙

> 전역 `~/.claude/CLAUDE.md`에 대한 **프로젝트 추가 규칙**. 충돌 시 이 파일이 우선.
> 목적: 1인 개발 Unity 프로젝트의 **스파게티화 예방** + 모듈 경계 강제 + 자율 검증 루프.

## 0. 프로젝트 스냅샷

- **엔진**: Unity 6.3 LTS · 2D 탑뷰 · flat-top hex (odd-q offset)
- **장르**: 턴제 전술 전차 전투 + 모듈 장갑 + 파츠 조합
- **씬**: `StrategyScene`(8×10 표준) · `TerrainTestScene`(12×12 기능 검증) · `FireActionScene`(연출)
- **1인 개발**: 메인(Opus) + 4 에이전트(Sonnet) 팀 자율 루프
- **권위 있는 기획 문서**: `docs/00_index.md` · 적 AI `docs/12_enemy_ai.md`

## 1. 모듈 아키텍처 (namespace ↔ 폴더 ↔ 책임)

```
Crux.Data       /Scripts/Data        ScriptableObject 정의 (TankDataSO·AmmoDataSO·CoverDataSO)
Crux.Grid       /Scripts/Grid        hex 좌표·셀·경로·LOS
Crux.Unit       /Scripts/Unit        GridTankUnit·모듈 시스템·상태 오버레이
Crux.Combat     /Scripts/Combat      관통 계산·VFX·피격 효과
Crux.AI         /Scripts/AI          적 의사결정 (Context/Role/Scoring/Decision/Controller)
Crux.Camera     /Scripts/Camera      BattleCamera — 줌·팬·프레이밍·반응 시퀀스 제어권 양보
Crux.Input      /Scripts/Input       PlayerInputHandler — 키 입력·상태 전이
Crux.UI         /Scripts/UI          BattleHUD + 패널 분리 (uGUI 이관 진행)
Crux.Cinematic  /Scripts/Cinematic   연출 씬·DamagePopup
Crux.Core       /Scripts/Core        전투 상태·턴 오케스트레이션만. HUD/입력/카메라 포함 금지
```

### 1.1 경계 강제 규칙
- **한 폴더 안쪽만 자기 namespace 사용**. 폴더 밖에 같은 namespace 코드 금지
- **`Crux.Core`는 순수 오케스트레이션**. `OnGUI`·`Input.Get*`·카메라 조작·스프라이트 생성 금지
- **ScriptableObject 데이터 정의는 `Crux.Data`만**. 다른 namespace `[CreateAssetMenu]` 금지
- 런타임 코드는 `Editor/` 코드를 참조하지 않음 (`Editor/` → 런타임은 OK)

## 2. 파일·메서드 크기 예산

| 대상 | 소프트 | 하드 | 예외 |
|---|---|---|---|
| 클래스 파일 | 500 LOC | **800 LOC** | 데이터 테이블·스프라이트 제너레이터 (주석 명시) |
| 메서드 | 60 LOC | **120 LOC** | 상태기계 switch·OnGUI 드로어 (주석 명시) |
| 한 커밋 파일 수 | 5개 | **8개** | 리팩토링 PR |

- **800 LOC 초과 시 분할 전에는 새 기능 추가 금지**
- 120 LOC 초과 메서드는 일부 추출 필수
- 새 파일은 미래 성장 여지 20% 여유 확보

## 3. 의존성 방향 (layering)

```
Data ──┐
       ├──→ Grid ──→ Unit ──→ Combat ──→ AI
       │                                  ↓
       └────→ Camera → Input → UI → Core (오케스트레이션)
                                     ↓
                               Cinematic (리프, 순환 금지)
```

- **상위가 하위를 참조**. 역방향 금지 (`Grid`가 `Core` 참조 시 즉시 에러)
- **순환 의존 금지**. `Combat`에서 `AI` 건드리는 순간 잘못된 방향
- `Crux.Core`의 `BattleStateStorage` 등 static 저장소는 레이어 경계 우회 — 신중하게

## 4. 리팩토링 트리거 (자동 발동 조건)

다음 중 하나라도 참이면 **즉시 리팩토링 우선**, 새 기능 대기:

1. 파일 600 LOC 초과
2. 메서드 80 LOC 초과
3. 한 클래스가 3개 이상의 **명확히 다른 책임** 보유
4. 한 파일에 5개 이상의 `Draw*`/`Handle*`/`Execute*` 메서드
5. `public static` 필드가 3개 이상 같은 클래스에 존재
6. `partial class` 사용 유혹 (→ 분할이 신호)
7. 같은 상수를 2곳 이상에서 하드코딩

## 5. 안티패턴 금지 목록

### 🚫 절대 금지
- **`BattleController` 증축 금지** — 새 기능은 폴더 결정 → 해당 폴더에 신규 파일 → Core는 조립만
- `partial class` 남발 — 한 클래스를 쪼개도 여전히 한 클래스
- 전역 `public static` 신규 필드 — 필요하면 Storage 클래스
- `Core/` 안에서 `OnGUI()`·`Input.Get*`·`Camera.main.*`
- 한 커밋에서 `BattleController.cs` + 4개 이상 다른 파일 동시 수정

### ⚠️ 경고 (재고 필요)
- 새 `MonoBehaviour`를 기존 거대 파일에 `private class`로 박아넣기
- 같은 세션에서 3개 이상 기능 동시 진행
- 사용하지 않는 `using` 방치
- 의도 없는 `TODO` 코멘트

## 6. 새 기능 추가 체크리스트

```
□ 이 기능이 속하는 namespace/폴더는 명확한가?
□ 기존 파일 중 책임이 맞는 곳이 있는가?
   있음 → 그 파일에 추가 (§2 예산 확인)
   없음 → 신규 파일 이름·폴더 결정
□ BattleController를 건드려야 하는가?
   "오케스트레이션 연결" 이외의 이유면 재고
□ 800 LOC 근접 시 분할 먼저
□ 새 public static 필드 필요한가? 대안 검토
□ 컴파일 체크(check_compile_errors) 후 커밋
```

## 7. 하네스 엔지니어링 원칙

### 7.0 작업 시작 전 필수 확인

- **세션 시작 시 `.claude/rules/mistake-log.md` 반드시 열람**. 활성 섹션 전량 1회 통독 후 작업 착수
- 동일 영역(예: Unity·워크플로우) 작업 시 해당 섹션 항목을 암기·반영
- 새로운 실수 발생 시 **즉시** 해당 파일의 형식(`- [날짜] [영역] 실수 내용 → 교훈/예방책`)으로 추가. 나중에 몰아서 기록 금지
- 동일 실수 반복 감지 시 빈도 표기([3회] 등)로 업데이트

### 7.1 편집→검증 루프
- **편집 직후 재읽기 금지** — Edit/Write는 성공 시 상태 보장
- **비트 단위 편집 후 즉시 `check_compile_errors`** — 쌓아놓지 말 것
- **검증 전 완료 선언 금지**

### 7.2 Unity 씬·에셋 특수성
- 신규 씬은 `.unity` 복제 후 `.meta` guid 새 발급
- `GameConstants.GridWidth/Height` 하드코드 금지 — `grid.Width/Height` 사용
- `EditorBuildSettings.asset` 편집 시 scene guid 일치 필수
- `.meta` 수동 삭제 금지, 이동 시 동행
- **씬 파일 저장 경로 = `Assets/_Project/Scenes/<name>.unity` 전용**. `Assets/` 루트 저장 금지 (GUID 분리·BuildSettings 참조 깨짐)
- **다중 워크트리** (`C:/01_Project/03_Crux` 메인 · `Crux-dev` · `Crux-planning`): 각 워크트리에 독립 `CRUX/` 프로젝트. 에이전트 작업 시작 시 `list_unity_project_roots`로 Unity가 연 프로젝트가 현재 워크트리인지 1회 확인 후 진행
- 에셋 경로 규칙: 프리팹 런타임 로드 `Assets/_Project/Resources/Prefabs/<domain>/` · 일반 프리팹 `Assets/_Project/Prefabs/<domain>/` · SO `Assets/_Project/ScriptableObjects/<category>/`

### 7.3 기능 검증 경로
- **새 기능 → TerrainTestScene에서 먼저** (12×12 + 지형 + F1 디버그 오버레이)
- StrategyScene은 회귀 확인용
- 시각 기능은 Unity MCP `play_game` + `capture_scene_object`로 검증 시도

### 7.4 커밋 규율
- **커밋 하나에 한 가지 관심사** — 리팩토링과 기능 추가 섞지 말 것
- **Conventional Commits**: `feat:`·`fix:`·`refactor:`·`docs:`·`chore:`·`tune:`
- 파일 수 > 8 커밋은 재고 신호
- `BattleController.cs` 단독 커밋 반복 시 분할 신호

### 7.5 디버깅·관찰성
- 시각 기능 버그는 Unity MCP로 먼저 (play → capture)
- 로그 레이블: `[CRUX]`(게임)·`[AI]`(의사결정)·`[FIRE]`(사격)
- 튜닝 verbose는 `#if UNITY_EDITOR` 가드 고려

### 7.6 자율 검증 3단계 (커밋 전 필수)

사용자 부재 상황 기본값. 모든 코드 변경 커밋 전 `crux-test` 스킬로 자체 검증.

1. **컴파일** — `mcp__coplay-mcp__check_compile_errors` 또는 `mcp__unity__execute_menu_item(menuPath="Assets/Refresh")`
2. **정적 테스트** — `mcp__unity__execute_menu_item(menuPath="Crux/Test/Run All Static")` → `CRUX/Temp/crux-tests.log` `failed=0`
3. **플레이 스모크** — `mcp__unity__execute_menu_item(menuPath="Crux/Test/PlaySmoke TerrainTest (3s)")` → `CRUX/Temp/crux-playsmoke.log` Exception/Error 부재 + 필요한 `[CRUX]` 로그 출현

**실패 시**: 즉시 구현 에이전트에 재작업 위임. 루프 최대 2회. 3회차는 사용자 지시 대기.

**MCP 1회 실패 시 즉시 정적 감사 전환** (재시도·추측 금지). `claude mcp list`로 상태 확인 후 "MCP 복구 대기" 기록, 런타임 검증 필수 항목은 사용자 지시까지 커밋 보류.

**자동 검증 불가 항목**(시각·입력·감각)은 `## 사용자 수동 확인 요청` 섹션에 누적.

**하네스 소스**: `Scripts/Editor/CruxPlaySmoke.cs`·`Scripts/Editor/CruxTestRunner.cs`·`.claude/skills/crux-test/SKILL.md`

## 8. 에이전트 자율 루프

### 8.1 4-역할 자율 루프 (기획→개발→검토→수정)

| 에이전트 | 색상 | 역할 | 주 도구 |
|---|---|---|---|
| **시타** (planner) | 🟣 | 사용자 요청 진입점·탐색·계획·위임·통합 보고 | Read·Glob·Grep (편집 금지) |
| **시그마** (backend) | 🔵 | Grid·Unit·Combat·AI·Data·Core 구현 | Edit·Write·check_compile_errors·execute_script |
| **픽셀** (frontend) | 🟡 | UI·Camera·Cinematic·스프라이트·씬/프리팹 조립 | Edit·Write·씬 MCP 전체·capture·uGUI MCP |
| **모나미** (reviewer) | 🔴 | 컴파일·테스트·구조 감사·성능 검증 (편집 금지) | run_tests·play/stop_game·capture·query MCP |

**루프 표준**: 시타 계획 → 시그마·픽셀 병렬 구현 → 모나미 DoD 검증 → 불합격 시 구현 재위임 (최대 2회) → 합격 시 메인(Opus) 통합·커밋.

### 8.2 MCP 도구 경계 (중복 제거·역할 고정)

| 용도 | 도구 | 시타 | 시그마 | 픽셀 | 모나미 |
|---|---|:-:|:-:|:-:|:-:|
| 컴파일 체크 | coplay `check_compile_errors` | — | ✓ | ✓ | ✓ |
| Unity 로그 조회 | coplay `get_unity_logs` | — | ✓ | ✓ | ✓ |
| 메뉴 실행 | unity `execute_menu_item` | — | ✓ | ✓ | ✓ |
| 에디터 테스트 러너 | unity `run_tests` | — | — | — | ✓ |
| 에디터 상태 조회 | coplay `get_unity_editor_state` | — | — | ✓ | ✓ |
| Hierarchy 조회 | coplay `list_game_objects_in_hierarchy`·`get_game_object_info` | — | — | ✓ | ✓ |
| 씬 열기/저장 | coplay `open_scene`·`save_scene` | — | — | ✓ | 읽기만 |
| 씬 GO 생성/조작 | coplay `create_game_object`·`parent_game_object`·`duplicate_game_object`·`delete_game_object`·`set_layer`·`set_tag`·`set_transform` | — | — | ✓ | — |
| uGUI 생성/조작 | coplay `create_ui_element`·`set_rect_transform`·`set_ui_layout`·`set_ui_text` | — | — | ✓ | — |
| 컴포넌트 | coplay `add_component`·`set_property`·unity `update_component` | — | — | ✓ | — |
| 프리팹 | coplay `create_prefab`·`create_prefab_variant` | — | — | ✓ | — |
| 스크린샷 | coplay `capture_scene_object`·`capture_ui_canvas` | — | — | ✓ | ✓ |
| 플레이 제어 | coplay `play_game`·`stop_game` | — | — | — | ✓ |
| 에디터 스크립트 실행 | coplay `execute_script` | — | ✓ | — | — |

- 시타는 **관찰만** — 직접 Unity 건드리지 않음 (계획 단계에서 Read/Glob/Grep으로 코드 읽기)
- **편집**=픽셀 (씬·UI·프리팹) / 시그마 (코드·에디터 자동화) / **플레이 제어**=모나미 전담
- 모나미는 `save_scene` 금지 — 검증 중 자동 저장 금지 (상태 오염 방지)

### 8.3 위임 원칙
- **탐색 3회 이상 예상 시 Explore subagent** — 메인이 반복 Grep/Read 금지
- **병렬 호출** — 독립 작업은 한 메시지에 묶어 시그마·픽셀 동시 기동
- **구현↔검증 분리** — 리팩토링도 도메인별로 시그마/픽셀이. 모나미는 DoD만
- **재작업 루프 2회 초과** 시 시타가 루프 중단 → 사용자 보고
- **모호하면 사용자 질문** — 밸런스·게임 디자인은 자율 금지

### 8.4 모델 지정 (전역 §8 일치)

- **메인(부모) = Opus 4.7** — 계획·통합·코드 리뷰·에이전트 지시. 직접 구현 최소화
- **시타·시그마·픽셀·모나미 = Sonnet 기본** — `Agent` 호출 시 **`model: "sonnet"` 파라미터 필수**
- 파라미터 누락 = Opus 상속 → 비용 폭증 (자동 다운그레이드 없음)
- 에이전트 `.md` frontmatter `model:`은 호출 파라미터보다 우선순위 낮음 — **호출 시점 파라미터가 신뢰원**
- **Opus 서브 허용 예외**: 핵심 아키텍처 결정·다중 가설 디버깅. **사용자 사전 고지 후** `model: "opus"` 명시

### 8.5 보고 규율
- **성공은 조용히, 실패만 보고** (전역 §3 일치)
- 완료 보고는 결론 1~2줄 + 다음 결정 1줄
- 표·섹션 재정리 금지 — 이미 수행된 작업 장황하게 풀지 말 것
- Read/Grep 원문 인용 시 요약 없이 그대로 복붙 (허위 보고 방지)

## 9. 기술 부채 원장 (Tech Debt Ledger)

해결 시 목록에서 제거.

| ID | 부채 | 심각도 | 해결 경로 |
|---|---|---|---|
| TD-03 | `FireActionContext.cs` 4개 타입 혼재 | 🟢 P3 | 부가 정리 |
| TD-04 | 빈 의도 폴더 Enemy/Loot/Vision | 🟢 P3 | Phase 2 콘텐츠 확장 시 |
| TD-05 | `HitEffects`/`MuzzleFlash`/VFX 렌더 상수 하드코드 | 🟢 P4 | 밸런스 패스 때 Data SO로 |
| TD-06 | 지형 플로어 스프라이트 다크톤 multiply 틴트 약함 | 🟡 P2 | Pixel 타일 변형 생성 |
| TD-08 | BattleHUD 배너/경고 큐 uGUI·월드 공간 렌더링 이관 대기 | 🟢 P3 | Phase 4 UI 통합 |

**이력**: BattleController 분할 로드맵 P-S1~P-S7은 2026-04-16 완료. 상세 내역은 `git log --grep="P-S[1-7]"` 참조.

## 10. 변경 이력

| 날짜 | 변경 |
|---|---|
| 2026-04-14 | 초판. 아키텍처·예산·레이어·리팩토링 트리거·S1~S7 스케줄·부채 원장 수립 |
| 2026-04-16 | §1 Camera/Input/UI 폴더 상태 갱신 · §9 P-S1~S7 완료 표시 · §10 TD-01/TD-04 현재 상태 반영 |
| 2026-04-20 | Phase 3: BattleHUD OnGUI 제거 — ShowBanner/ShowAlert 큐 BattleController 이관 · TD-08 신규 |
| 2026-04-24 | §8 자율 루프 확장 — 4-역할 MCP 도구 경계 표·보고 규율 신설 / §9 P-S 로드맵 git 이력으로 이관 / 전반 압축 |
| 2026-04-25 | §7.2 씬 파일 경로 규칙 신설 — `Assets/_Project/Scenes/` 전용, 다중 워크트리 `list_unity_project_roots` 시작 확인 의무화 (픽셀 UI 배선 루트 저장 사고 재발 방지) |
| 2026-04-27 | §9 TD-01 해소 — `PostMoveController` 추출, `BattleController.cs` 863 → 780 LOC (`8f673dc`) |
| 2026-04-27 | §9 TD-02 해소 — `FireCinematicFX`/`FirePostImpactHandler` 추출, `FireSequenceController.cs` 1153 → 727 LOC (`23b0e11`) |
| 2026-04-27 | 공격+반격 단일 큐 시스템 복원 — `CounterFireResolver` 신규(8조건) + `FireActionContext` 큐화 + Y/N 프롬프트(1.5s 타임아웃). `feature/next-dev` 분기 중 손실분 재이식 (`171d7dc`+`fd7b199`) |
| 2026-04-27 | 반격 UX 재설계 — Y/N 프롬프트 폐기, 피격 후 무기 선택 패널 자동 진입 + `[0] 반격 취소` 행 + 3s 카운트다운(미입력 시 주포 자동 발사). `CounterFireSession` (Crux.Combat) + `CounterFireController` (Crux.Core) 분리, `CounterFireResolver` 8→7조건 (CounterConfirmed 게이트 제거). §1.1 위반 동시 해소 — 마우스 스냅 계산을 `PostMoveController`(Crux.Core)에서 `PlayerInputHandler`(Crux.Input)로 이관 (`62c42de`) |
| 2026-04-27 | 단일 FireActionScene 내 공격+반격 통합 — `CounterFireUIPanel` (Crux.Cinematic) 신규 + `FireSequenceController` 시퀀스 종료 분기에 `PendingCounterSelect` 처리 + `FireExecutor.TryEnqueueAIRetaliation` (AI 반격 자동 큐잉) + `BattleStateManager.StartCounterFireWeaponSelectInternal` 제거 (BattleScene 진입점 단일화). FireActionScene Canvas WeaponSelectPanel 배선 (`952cf56`) |
