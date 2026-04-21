# 10b. 격납고 개요·모듈 연결 지도

> 작성: 2026-04-21
> 관계: `docs/10 §2` 격납고 와이어프레임의 **모듈 경계·연결 허브 문서**
> 참조: `CRUX/CLAUDE.md §1 모듈 아키텍처`·`docs/03 §1 편성 씬`·`docs/03b_maintenance.md` (정비 탭)·`docs/05 §3~4` (호환성·파츠 경제)·`docs/04 §10` (승무원 스키마)
>
> **목적**: 격납고(Hangar) UI·로직을 **5개 독립 모듈**로 분할하고, 각 모듈의 책임·입출력·의존 방향을 확정한다. 세부 UX는 개별 보조 문서(10c~10f, 03b)가 담당하며 본 문서는 **허브·경계·연결**만 다룬다. 실제 C# 구현은 `Crux-dev` 워크트리에서 별도 수행.

---

## 0. 용어 정의

| 용어 | 영문 | 정의 |
|---|---|---|
| 격납고 | Hangar | 편성·정비·상점·회식 등 전투 외 모든 부대 관리 기능이 모인 UI 공간. Unity Scene 1개 (`Hangar.unity`) |
| 탭 | Tab | 격납고 좌측 메뉴의 세로 항목 하나. 선택 시 중앙 작업공간 콘텐츠 전환 |
| 허브 | Hub | 본 문서. 탭·모듈 간 경계·연결 규칙을 정의하는 상위 명세 |
| 공유 상태 | Shared State | 탭 간에 걸쳐 유지되는 상태(선택 전차·탭 인덱스 등). 탭 전환에도 보존 |
| 연결 지점 | Boundary | 모듈 간 호출·데이터 교환이 허용된 유일한 경로 (직접 참조 금지) |

---

## 1. 설계 원칙 — 모듈화 우선

1. **한 탭 = 한 모듈** — 편성·정비·파츠·승무원·Trait은 각각 독립 책임. 내부 로직은 모듈 바깥에 노출 안 함
2. **모듈 간 직접 참조 금지** — 탭 컴포넌트끼리 서로의 내부 타입·메서드를 호출하지 않는다. 통신은 **공유 상태 스토어 + 이벤트**만 경유
3. **의존 방향 단방향** — 허브(10b) → 각 모듈(10c~10f, 03b). 모듈에서 허브로의 역참조 금지. 모듈 간 횡단 참조 금지
4. **각 모듈은 입력·출력이 명시적** — 읽기 소스와 쓰기 대상이 본 문서 §3.2 표에 고정. 표 밖 접근은 리팩토링 신호
5. **UI와 상태 분리** — UI 컴포넌트(Binder)는 표현만. 상태 변경은 공유 상태 스토어에 요청 → 이벤트로 전 탭에 전파
6. **순수 계산은 별도** — Trait 계산·호환성 체크 등 순수 함수는 서비스 계층(Crux.Core 또는 Crux.Data 유틸)에 두고 모듈은 호출만

**철학 근거**: `CRUX/CLAUDE.md §1·§5` 모듈 경계 강제·반(反)스파게티 원칙. 격납고는 BattleController급으로 비대해질 위험이 가장 큰 영역이라 선제 분할.

---

## 2. 격납고 탭 구성

### 2.1 탭 목록 (docs/10 §2.5 갱신판)

| 탭 | 첫 빌드 상태 | 담당 문서 | 약칭 |
|---|---|---|---|
| 편성 | ✅ 활성 | `10c_hangar_composition.md` (예정) | COMP |
| 정비 | ✅ 활성 | `03b_maintenance.md` | MAINT |
| 상점 | 🔒 잠금 | (후속) | SHOP |
| 회식 | 🔒 잠금 | (후속) | MESS |
| 인물 | 🔒 잠금 | (후속) | PEOPLE |
| ~~스킬~~ | 🔒 **폐지** | 각성 카드는 정비 탭(03b §4.1)에 흡수 | — |

### 2.2 좌측 메뉴 레이아웃

`docs/10 §2.1` 3분할 레이아웃 유지. 좌측 메뉴 160px 고정.

### 2.3 탭 진입 기본값

```
격납고 진입 시 기본 선택 탭:
  IF 각성 카드 큐 > 0     → MAINT 탭 (정비 우선)
  ELSE IF 파손 파츠 존재  → MAINT 탭
  ELSE                     → COMP 탭 (편성 기본)
```

### 2.4 탭 간 네비게이션

- 탭 클릭 시 중앙 작업공간만 교체. 우 패널(선택 전차 상세)은 **유지**
- 탭 전환 중 **자동 세이브 없음** (03c §3.2). 편성 변경 후 다음 노드로 이동할 때만 세이브 트리거
- 탭 진입·이탈 시 훅 `OnTabEnter(tab)`·`OnTabLeave(tab)` 제공 — 탭별 바인딩 초기화·정리용

---

## 3. 모듈 분할 지도

### 3.1 책임 경계 (1줄 요약)

| 모듈 | 문서 | 책임 |
|---|---|---|
| **10c 편성** | `10c_hangar_composition.md` | "어떤 전차를 데려갈지" 결정. 출격·보관 슬롯·출격 확정 |
| **10d 파츠 인벤토리** | `10d_hangar_parts_inventory.md` | "어떤 파츠를 어디에 꽂을지". 카테고리·필터·호환성 피드백·합성 |
| **10e 승무원 배치** | `10e_hangar_crew_assignment.md` | "어떤 승무원을 어느 직책에". 5직책·선호·공석 처리 |
| **10f Trait** | `10f_hangar_traits.md` | "이 파츠·승무원 조합이 만드는 고정 효과". 계산 엔진 |
| **03b 정비** | `03b_maintenance.md` | "다음 전투 전 무엇이 회복돼야 하는지". 수리·부상·각성 카드 |

### 3.2 데이터 입출력 표

각 모듈이 읽기·쓰기하는 데이터 및 의존 관계:

| 모듈 | 읽기 | 쓰기 | 다른 모듈 의존 |
|---|---|---|---|
| 10c 편성 | `ConvoyInventory.tanks`·`launchSlots`·`storageSlots` | `launchSlots` 할당, `SharedState.selectedTank` | (없음 — 최상위 진입점) |
| 10d 파츠 | `ConvoyInventory.partStash`·`SharedState.selectedTank` | 선택 전차 `installedParts` 변경 | docs/05 §3 호환성, 10f (계산 트리거) |
| 10e 승무원 | `ConvoyInventory.availableCrew`·`SharedState.selectedTank.crew` | 직책 슬롯 할당 | docs/04 §1 선호, 10f (계산 트리거) |
| 10f Trait | 선택 전차의 파츠·승무원 스냅샷 | `SharedState.selectedTankTraits` (파생) | (읽기 전용 — 역참조 없음) |
| 03b 정비 | `ConvoyInventory` 전체·`MaintenanceState` | 파츠 내구도·모듈 HP·부상·스킬 소유 | 10c (선택 전차) |

**엄격 규칙**: 위 표 밖의 접근은 **아키텍처 위반**. 리뷰어(모나미)가 Pull Request에서 차단한다.

### 3.3 의존 방향 그래프

```
                   HangarController (10b 허브)
                          │
         ┌────────┬───────┼───────┬────────┐
         ▼        ▼       ▼       ▼        ▼
       10c     10d     10e     10f       03b
     편성    파츠     승무원   Trait      정비
         │      │       │       ▲         │
         │      │       │       │         │
         │      └───────┴───────┤         │
         │           (읽기만)    │         │
         │                       │         │
         └──────────────────────┼─────────┘
                                │
                       SharedState (읽기 노출)
                       HangarBus  (이벤트 발행/구독)
```

- 10f는 **계산만**. 10d·10e 변경 이벤트를 구독해 재계산하고 결과를 SharedState에 쓴다
- 10c는 다른 탭을 알지 못함. 선택 전차를 바꿀 뿐
- 순환 금지. 10d가 10e를 직접 참조하면 모듈화 원칙 위반

---

## 4. 공유 상태 & 이벤트 버스

### 4.1 공유 상태 스토어 (`HangarSharedState`)

탭 간에 유지되는 상태. 읽기는 자유, 쓰기는 `HangarController` 또는 각 탭의 **자기 책임 필드만** 허용.

```
HangarSharedState {
  # 탭 공통
  selectedTab: HangarTab
  selectedTank: TankInstance?           # 우 패널 대상

  # 편성 탭 (10c 전용 쓰기)
  launchSlotAssignment: TankInstance[]  # 0~4 인덱스 = 출격 슬롯

  # Trait 파생 (10f 전용 쓰기)
  selectedTankTraits: IReadOnlyList<TraitEffect>

  # 필터 상태 (10d 전용 쓰기)
  partsFilterCategory: PartCategory?
  partsFilterCompatibleOnly: bool

  # 각성 큐 뱃지 (03b 읽기 · MaintenanceTicker 쓰기)
  awakeningQueueCount: int
}
```

**원칙**: 쓰기 주체는 **한 모듈로 고정**. 다른 모듈이 필드 쓰기 시도하면 예외. SharedState는 **POCO + readonly interface 2세트**로 제공.

### 4.2 이벤트 정의 (`HangarBus`)

탭 간 통신 유일 경로. 퍼블리셔·서브스크라이버 패턴.

| 이벤트 | 발행자 | 구독자 | 의미 |
|---|---|---|---|
| `TankSelected(TankInstance)` | 10c | 10d, 10e, 10f, 03b | 우 패널·관련 탭이 대상 전차 교체 |
| `PartEquipped(TankInstance, PartCategory, slotIndex, PartInstance)` | 10d | 10f, (10c 표시 갱신) | Trait 재계산 트리거 |
| `PartUnequipped(TankInstance, PartCategory, slotIndex, PartInstance)` | 10d | 10f, 10c | 상동 |
| `CrewAssigned(TankInstance, CrewClass, CrewMemberRuntime)` | 10e | 10f, 10c | 선호 매칭·Trait 재계산 |
| `CrewUnassigned(TankInstance, CrewClass, CrewMemberRuntime)` | 10e | 10f, 10c | 상동 |
| `TraitsRecalculated(TankInstance, IReadOnlyList<TraitEffect>)` | 10f | 10c (우 패널 Trait 섹션 갱신) | 계산 완료 통지 |
| `TabChanged(HangarTab)` | 10b 허브 | 전 탭 | `OnTabEnter`·`OnTabLeave` 호출 |
| `AwakeningQueueChanged(int newCount)` | MaintenanceTicker | 10b 허브 (뱃지 갱신) | 탭 뱃지 숫자 변경 |
| `LaunchConfirmed(IReadOnlyList<TankInstance>)` | 10c | 10b 허브 (씬 전환) | 출격 확정 → WorldMap 복귀 |

### 4.3 이벤트 전파 규칙

1. **동기 전파** — 이벤트는 동기로 전파. 구독자 전부 처리 후 발행 함수 반환. 비동기·지연 없음 (UI 반응성)
2. **순서 불확정** — 구독자 호출 순서는 보장 안 함. 구독자 간 순서 의존 금지
3. **재진입 금지** — 이벤트 핸들러 안에서 같은 이벤트 재발행 금지 (무한 루프 방지). 다른 이벤트 발행은 OK
4. **실패 격리** — 한 구독자가 예외를 던져도 다른 구독자는 계속. 예외는 `[CRUX] [HANGAR]` 로그로만 기록

---

## 5. 전체 흐름

### 5.1 전투 복귀 → 격납고 진입

```
WorldMap 상태:
  · 전투 결과 모달 "확인" → 03c §3.1 BattleResultConfirmed 세이브
  · 다음 노드가 거점이면:
      ▼
  · 캠프 진입 이벤트 → 추모식 있으면 먼저 처리
      ▼
  · MaintenanceTicker.OnCampVisit() — 자동 정비 처리
      ▼
  · Hangar 씬 로드 → HangarController 초기화
      ▼
  · 탭 기본값 선택 (§2.3)
      ▼
  · 03c §3.1 CampArrival 세이브
```

### 5.2 편성 → 출격

```
Hangar COMP 탭:
  · 출격 슬롯에 전차 배치
  · 각 전차의 파츠·승무원 확인 (필요 시 10d·10e 이동)
      ▼
  · "출격" 버튼 활성 조건: 출격 슬롯 ≥1대 + 선택된 전차가 스테이지 요구 충족
      ▼
  · 출격 버튼 클릭 → LaunchConfirmed 이벤트
      ▼
  · HangarController: 씬 전환 (WorldMap 복귀 → 전투 노드 진입)
      ▼
  · BattleEntryData.PlayerTanks 주입
```

### 5.3 탭 간 네비게이션 일관성

탭 전환 시 **보존되는 것**:
- `selectedTank` (우 패널 유지)
- 파츠 필터 상태 (10d 재진입 시 이전 필터 복원)
- 스크롤 위치 (각 탭 자체 책임)

**초기화되는 것**:
- 드래그 진행 중 상태 (미완료 드래그는 탭 전환 시 취소)
- 모달 오버레이 (파츠 상세·호환성 경고 등은 탭 전환 시 닫힘)

---

## 6. 문서 맵

| 문서 | 상태 | 범위 |
|---|---|---|
| `10b_hangar_overview.md` | ✅ 본 문서 | 허브·모듈 경계·연결 |
| `10c_hangar_composition.md` | ⏸ 예정 | 편성 탭 UX·슬롯 상호작용·출격 확정 |
| `10d_hangar_parts_inventory.md` | ⏸ 예정 | 파츠 인벤토리·필터·호환성 피드백·합성 진입점 |
| `10e_hangar_crew_assignment.md` | ⏸ 예정 | 승무원 배치·직책 매칭·선호·공석 경고 |
| `10f_hangar_traits.md` | ⏸ 예정 | Trait 계산 규칙·획득 경로·표시 |
| `03b_maintenance.md` | ✅ 완료 | 정비 탭 전체 |
| `hangar_prefab_build_guide.md` | ✅ 완료 | 프리팹 수동 조립 절차 (구현 보조) |

**작성 순서 제안**: 10c → 10d → 10e → 10f. 이유: 편성(10c)이 모든 탭의 대상 전차를 결정하므로 먼저. Trait(10f)은 10d·10e 이후.

---

## 7. 구현 책임 분리 (Dev 가이드)

### 7.1 핵심 타입

| 타입 | 네임스페이스 제안 | 성격 |
|---|---|---|
| `HangarController` | `Crux.UI.Hangar` | MonoBehaviour. 씬 진입점·탭 라우터·LaunchConfirmed 처리 |
| `HangarSharedState` | `Crux.UI.Hangar` | POCO + `IHangarStateReadOnly` 인터페이스 |
| `HangarBus` | `Crux.UI.Hangar` | 이벤트 버스. 탭 간 통신 유일 경로 |
| `HangarTab` | `Crux.UI.Hangar` | enum (Composition / Maintenance / Shop / Mess / People) |
| `ITabModule` | `Crux.UI.Hangar` | 각 탭 바인더가 구현할 인터페이스 |

### 7.2 `ITabModule` 인터페이스

```
ITabModule {
  HangarTab Tab { get; }
  void Initialize(IHangarStateReadOnly state, IHangarBus bus);
  void OnEnter();         # 탭 진입 시
  void OnLeave();         # 탭 이탈 시
  void Tick(float dt);    # 활성 탭일 때만 호출 (선택)
}
```

각 탭 바인더(`CompositionTabBinder` 등)는 본 인터페이스를 구현하고 `HangarController`에 등록된다.

### 7.3 경계 강제 방법

- **namespace 분리**: `Crux.UI.Hangar.Composition` / `Crux.UI.Hangar.Parts` / `Crux.UI.Hangar.Crew` / `Crux.UI.Hangar.Traits` — 한 namespace가 다른 namespace의 internal 타입을 참조할 때 경고
- **internal 접근자 활용**: 탭별 MonoBehaviour·헬퍼는 `internal` 기본. `public`은 `ITabModule`·이벤트·DTO 한정
- **assembly definition 분리** (선택, Phase 2): 탭별 asmdef로 컴파일 단계에서 경계 강제

### 7.4 통합 지점

- **`BattleController` → `BattleEntryData.PlayerTanks`** — 출격 시 COMP가 설정
- **`WorldMapController`** — 거점 노드 도착 시 `SceneManager.LoadScene("Hangar")`
- **`ConvoyInventory`** — 모든 탭의 기초 데이터 소스. 전역 싱글턴 (`BattleEntryData.Convoy`)
- **`MaintenanceTicker`** — 03b의 자동 처리 주체. 캠프 도착·전투 종료 훅

### 7.5 테스트 가능성

- 각 탭 바인더는 **mock `IHangarStateReadOnly`·`IHangarBus`** 주입으로 단위 테스트 가능해야 함
- Trait 계산(10f)은 **순수 함수**로 — `ComputeTraits(TankInstance) → IReadOnlyList<TraitEffect>` 단독 테스트 가능
- 호환성 체크(docs/05 §3)는 이미 순수, 재활용

---

## 8. 오픈 이슈 / 결정 필요

1. **HangarSharedState 구현 방식** — POCO vs ScriptableObject. ScriptableObject는 Inspector 디버그에 유리하지만 전역 변경 시 dirty 처리 등 주의. 현재는 POCO 제안
2. **HangarBus 타입 안전** — 제네릭 퍼블리셔(`Publish<T>(T evt)`) vs 이벤트별 전용 메서드. 후자가 컴파일 타임 안전하나 보일러플레이트 증가
3. **탭별 assembly definition 분리 시점** — 지금부터 적용 vs 격납고 완성 후 리팩토링. 결정 필요
4. **10c·10d 간 드래그앤드롭 경계** — 편성 탭에서 파츠를 드래그해 전차 슬롯에 드롭 시, 편성 탭이 파츠 인벤토리 내부를 알아야 하나? → **대안**: "파츠 장착 요청" 이벤트를 10c가 발행, 10d가 장착 실행. 경계 유지 가능
5. **로시난테 특례 처리 위치** — `isRocinante` 플래그는 `TankInstance`에 있음. 편성 탭(10c)이 폐기 불가 UI 표시. 별도 10g 문서? 아니면 10c 내부
6. **탭 전환 중 미저장 변경 경고** — 편성·파츠·승무원 변경은 즉시 반영이 원칙(드래프트 상태 없음). 따라서 경고 불필요. 재확인
7. **상점·회식·인물 탭 활성화 시점** — 09 §3 캠프 시설 단계별 해금과 연결. 플래그 소스는 `MapState.unlockedFacilities` (03c §2.6). 관련 조건 분기 위치 결정 필요

---

## 9. 튜닝 훅

| 항목 | 초안 | 변경 사유 |
|---|---|---|
| 기본 진입 탭 우선순위 (§2.3) | 정비 큐 > 파손 > 편성 | 테스터가 "탭을 바꾸는 게 귀찮다" 피드백 시 조정 |
| 선택 전차 보존 여부 | 탭 전환 시 보존 | 특정 탭에서 "전차 선택 해제"가 필요한 사용 사례 발견 시 |
| HangarBus 실패 격리 정책 | 예외 무시 + 로그만 | 버그 조기 탐지 위해 Editor 모드만 예외 재던짐 옵션 |
| 탭 모듈별 asmdef 분리 | 미적용 (후속) | 컴파일 시간 증가 또는 순환 참조 경고 시 도입 |

---

## 10. 범위 외

- **개별 탭 UX 상세** — 10c~10f·03b 각자 책임
- **프리팹 조립 절차** — `hangar_prefab_build_guide.md`
- **파츠 합성 로직** — 10d에 진입점만, 본격 합성 규칙은 Phase 2
- **상점·회식·인물 탭** — 후속 별도 문서
- **멀티플레이어·공유 격납고** — 싱글 플레이 전제, 해당 없음

---

## 11. 변경 이력

| 날짜 | 변경 |
|---|---|
| 2026-04-21 | 초판. 격납고 5모듈 분할·경계·의존 지도·공유 상태·이벤트 정의·탭 네비·구현 가이드 확정. 10c~10f는 후속 작성. 03b는 기존 문서를 정비 탭으로 흡수 |
