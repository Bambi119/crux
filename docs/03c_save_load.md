# 3c. 세이브·로드 기획

> 작성: 2026-04-21
> 관계: `docs/03` 루프의 **한 판 완결성**을 담보하는 데이터 영속화 기획
> 참조: `docs/03 §6.3 맵 해금`·`docs/09 §7 자원 정산 흐름`·`docs/10 §7.1 타이틀`·`docs/03b §8 오픈 이슈 #6 (campVisits 필드)`·`docs/04 §10 데이터 스키마`·`docs/05 §8 데이터 스키마`·`docs/11 §9 데이터 스키마`
>
> **목적**: "한 판 처음부터 끝까지 돌아가는 빌드"(`docs/03 §7.1`)가 성립하려면 **세션 간 상태 보존**이 필수. 본 문서는 세이브 대상·시점·형식·이어하기 UX·실패 복구 규칙을 확정한다. 실제 C# 구현은 `Crux-dev` 워크트리에서 별도 수행.

---

## 0. 용어 정의

| 용어 | 영문 | 정의 |
|---|---|---|
| 세이브 스냅샷 | Save Snapshot | 캠페인 특정 시점의 부대·승무원·맵·큐 상태 전체를 묶은 단일 직렬화 단위 |
| 자동 세이브 | Autosave | 특정 이벤트(캠프 도착·전투 결과 확인 등)에 자동으로 쓰여지는 세이브 |
| 슬롯 | Save Slot | 세이브 스냅샷이 저장되는 독립 단위. MVP는 **단일 슬롯** |
| 스키마 버전 | Schema Version | 세이브 파일 포맷 버전. 로드 시 호환성 판정 |
| 이어하기 | Continue | 기존 세이브에서 마지막 자동 세이브 시점으로 재개하는 메뉴 항목 |

---

## 1. 설계 원칙

1. **단일 슬롯 MVP** — 스팀 싱글 플레이·스토리 확정형이므로 다중 슬롯은 QA 복잡도 대비 가치가 낮다. Phase 2에서 재검토
2. **자동 세이브만** (수동 없음) — 체크포인트 지점을 기획이 통제. 플레이어가 "어디서 저장되었는지"를 고민할 필요 없게 함
3. **이벤트 기반 트리거** — 일정 간격(5분 등) 타이머 세이브는 쓰지 않는다. 의미 없는 저장·파일 잠금 리스크 회피
4. **영속화 경로 일원화** — 기존 PlayerPrefs(자금·사기 b98ad83)는 JSON 도입 시 **폐기**. PlayerPrefs는 옵션 설정 같은 범용 스토리지로만 사용
5. **실패는 조용히 복구, 손상은 노출** — 쓰기 실패는 재시도 후 에러 모달만(게임 진행은 막지 않음). 파싱 실패는 사용자에게 명확히 안내
6. **스키마 버전은 명시적** — 초판 `schemaVersion = 1`. 마이그레이션 정책은 Phase 2, MVP는 불일치 시 **로드 거부 + 안내**

---

## 2. 세이브 대상 데이터

### 2.1 최상위 구조

```
CampaignSave {
  schemaVersion: int         # 현재 1
  savedAt: string            # ISO8601
  buildVersion: string       # 프로젝트 빌드 버전 (감사용)
  convoy: ConvoyState        # 부대 집합
  mapProgress: MapState      # 월드맵 진행도
  maintenance: MaintenanceState  # 정비 대기 큐 (03b §7.1 MaintenanceState)
  storyFlags: Map<string, bool>  # 스토리 분기·해금 플래그
  campaignStats: RunStats    # 실행 통계 (누적 전투 수·격파 수 등, 표시용)
}
```

### 2.2 `ConvoyState` (부대)

`docs/05 §8.4 ConvoyInventory` + `docs/11 §9.1 ConvoyEconomy` + `docs/04 §10.4 TankCrew` 집약:

```
ConvoyState {
  funds: int                     # 자금 (docs/11)
  tanks: List<TankState>         # 1~6대
  partStash: List<PartState>     # 공용 인벤토리
  availableCrew: List<CrewState> # 승무원 풀 (미배치 포함)
  maxTankSlots: int              # 보관 한도 (docs/09 §4)
  launchSlotCount: int           # 출격 슬롯 수
}
```

### 2.3 `TankState`

```
TankState {
  tankDataId: string             # TankDataSO id 참조
  instanceId: string             # 고유 식별
  isRocinante: bool              # 폐기 불가 플래그
  assignedLaunchSlot: int?       # 출격 슬롯 인덱스 (미배치 null)
  isInStorage: bool              # 보관고 여부
  currentHP: float
  moduleStates: Dictionary<ModuleType, ModuleStateSnapshot>
  installedParts: Dictionary<PartCategory, List<string>>  # instanceId 리스트
  morale: int                    # 0~100 (docs/04 §6.1)
  panicSafetyUsed: bool
  crewAssignment: Dictionary<CrewClass, string>  # crewId
}

ModuleStateSnapshot {
  state: ModuleState             # Functional/Damaged/Destroyed
  currentHP: float
}
```

### 2.4 `PartState`

```
PartState {
  partDataId: string             # PartDataSO id
  instanceId: string             # 고유
  category: PartCategory
  durability: float              # 0.0~1.0 (docs/05b §5.3, docs/03b §3.1)
  grade: PartGrade               # Trash~Legendary
  chargesRemaining: int          # Auxiliary 전용, -1은 비적용
}
```

### 2.5 `CrewState`

```
CrewState {
  crewDataId: string             # CrewMemberSO id
  currentMarks: Dictionary<AxisKey, int>  # 축별 현재 마크
  killCounters: Dictionary<AxisKey, float>  # 0.5 기여 포함 (docs/04 §3.3)
  battleCounters: Dictionary<AxisKey, int>
  ownedSkillIds: List<string>
  equippedPassiveIds: string[2]  # 2슬롯
  equippedActiveIds: string[2]
  injuryState: InjuryLevel       # None/Minor/Severe/Fatal
  injuryBattlesRemaining: int    # Severe 3 → Minor 1 카운트 (docs/03b §3.3)
  cooldowns: Dictionary<string, int>  # skillId → 남은 턴 (전투 간 이월 없음, 로드 시 0)
}
```

### 2.6 `MapState`

```
MapState {
  currentNodeId: string          # 현재 노드 (docs/09 §1.1)
  visitedNodeIds: List<string>   # 클리어한 노드 이력
  campVisitCount: int            # 캠프 방문 누적 (docs/03b §8 오픈 이슈 #6)
  unlockedFacilities: List<string>  # 캠프 시설 해금 (docs/09 §3)
}
```

### 2.7 `MaintenanceState` (docs/03b §7.1 직렬화)

```
MaintenanceState {
  awakeningQueue: List<AwakeningEntry>
  # 각성 카드 큐 (전투 종료 후 캠프에서 처리 대기)
}

AwakeningEntry {
  crewId: string
  axis: AxisKey
  newMarkLevel: int
  generatedAt: string            # ISO8601
}
```

### 2.8 직렬화 제외 항목

런타임에 재생성 가능하므로 저장하지 않음:
- 카메라 위치·줌 상태
- UI 패널 열림/닫힘 상태
- 일시 시각 이펙트 (화재·연막 파티클)
- 오버워치 예약 (전투 세션 내에서만 유효 — 저장은 전투 외부에서만 발생)
- 사기 `MoraleBand` (계산 파생치)
- 호환성 플래그 (`weightOk` 등 — 파츠·전차 조합에서 재계산)
- Module의 런타임 시각 오프셋

---

## 3. 세이브 시점 (자동 트리거)

### 3.1 트리거 이벤트 4종

| 트리거 | 시점 | 빈도 |
|---|---|---|
| **CampArrival** | 거점 노드 도착, 추모식·정비 처리 **완료 직후** | 캠페인당 8회 (docs/09 §2.3) |
| **BattleResultConfirmed** | 전투 결과 모달에서 "확인" 버튼 클릭 직후 (노획·마크 큐 적재 후, 월드맵 복귀 직전) | 캠페인당 22회 (docs/09 §2.1 전투 노드 수) |
| **DialogNodeCompleted** | 대화 노드 컷씬 종료 직후 | 캠페인당 15회 |
| **RestNodeCompleted** | 휴식 노드 짧은 이벤트 종료 직후 | 캠페인당 6회 |

**합계**: 캠페인 1회분 ≈ 51회 자동 세이브. 모든 노드 1회씩 = 노드 단위 완료 보장.

### 3.2 트리거 안 하는 경우

- **전투 중** — 턴·AP·오버워치 예약 등 런타임 상태 과다. MVP는 **전투 진입 전 상태**에서 재개. 전투 도중 이탈 시 해당 전투 재시작
- **편성/정비 탭 조작 중** — 조작 완료가 아닌 중간 상태 저장 지양. 탭 전환이나 탭 이탈 시에는 세이브 트리거 없음(이탈 후 다음 노드 이동 시 해당 노드 종료 트리거에서 저장)
- **타이틀 화면** — 저장할 신규 상태 없음
- **옵션 메뉴 변경** — PlayerPrefs로 별도 처리, 본 세이브 파일 외부

### 3.3 트리거 호출 규약

```
SaveManager.RequestSave(trigger: SaveTrigger, onComplete: Action<SaveResult>?):
  1. 스냅샷 빌드 (메인 스레드)
  2. JSON 직렬화
  3. 원자적 쓰기 (temp 파일 → rename)
  4. 결과 콜백
```

**원자적 쓰기 필수**: 중간에 프로세스 종료되어도 기존 세이브 손상 방지. `Application.persistentDataPath/save.json` 대상에 쓸 때 `save.json.tmp` 먼저 작성 → `File.Replace` 또는 `File.Move`로 원자 갱신.

---

## 4. 슬롯 정책

### 4.1 MVP — 단일 슬롯

- 파일 1개: `{persistentDataPath}/save.json`
- "뉴 게임" 선택 시 **덮어쓰기 경고 모달** 필수 (§5.2)
- 백업 없음: 구버전 파일이 있으면 로드 시도, 실패하면 "손상된 세이브" 안내

### 4.2 Phase 2 확장 시 후보

| 옵션 | 메모 |
|---|---|
| 다중 슬롯 3~5개 | 오버라이트·메타 표시 UI 추가 필요. 스팀 실적 연동 복잡 |
| 1 슬롯 + 자동 백업 3회차 | 단순 + 복구 여지. 가성비 높음 — 유력 |
| 클라우드 동기화 (Steam Cloud) | 플랫폼별 어댑터. 베타 이후 |

MVP는 1슬롯 고정, Phase 2 결정은 첫 빌드 외부 피드백 후.

---

## 5. 이어하기 UX

### 5.1 타이틀 화면 (docs/10 §7.1 갱신 제안)

기존 메뉴 4항목 상태 분기:

```
▸ 새 여정      (항상 활성)
  이어하기     (세이브 존재 시 활성, 없으면 회색)
  옵션         (항상 활성)
  종료         (항상 활성)
```

**이어하기 세이브 메타 표시**:
- 세이브 존재 시 `이어하기` 우측에 회색 1줄로 표시
- `이어하기  — 캠프: 광산 입구  ·  캠페인 12시간 4분  ·  4월 21일`
- 노드 이름 · 플레이 누적 시간 · 저장 시각

### 5.2 뉴 게임 덮어쓰기 경고

`새 여정` 클릭 시:

- **세이브 없음**: 바로 캠페인 인트로 진입
- **세이브 있음**: 모달 경고
  ```
  ⚠ 기존 진행 데이터가 있습니다

  새 여정을 시작하면 저장된 진행이 삭제됩니다.
  계속하시겠습니까?

  [취소]  [새로 시작]
  ```
- `새로 시작` 확인 시: `save.json` 삭제 → 캠페인 인트로

### 5.3 이어하기 플로우

```
이어하기 클릭
  ↓
로딩 인디케이터 (0.5~2초 예상)
  ↓
JSON 파싱 → 스키마 버전 체크 → 복원
  ↓
마지막 트리거가 CampArrival / DialogNodeCompleted / RestNodeCompleted:
  → 해당 노드 진입 상태로 WorldMap 씬 로드
마지막 트리거가 BattleResultConfirmed:
  → WorldMap 씬 진입 (다음 노드 하이라이트)
```

### 5.4 타이틀 → 월드맵 전환 시 로드

- 로딩 화면 필요성은 씬 로드 시간 실측 후 결정 (docs/10 §10)
- 스냅샷 크기 목표 ≤100KB → 파싱 <0.2초 예상 (MVP 규모)

---

## 6. 실패 처리

### 6.1 케이스 매트릭스

| 케이스 | 대응 |
|---|---|
| 파일 없음 | 이어하기 버튼 비활성. 정상 플로우 (에러 아님) |
| 쓰기 실패 (디스크 가득·권한) | 재시도 1회 → 여전히 실패 시 에러 토스트 + 로그, 게임 진행 허용 |
| JSON 파싱 실패 (파일 손상) | 모달 경고 "저장 파일을 읽을 수 없습니다. 새 여정을 시작하시겠습니까?" → 확인 시 삭제, 취소 시 타이틀 유지 (수동 복구 시도 여지) |
| 스키마 버전 > 현재 빌드 | 로드 거부 모달 "이 저장은 최신 버전으로 만들어졌습니다. 빌드를 업데이트하세요" |
| 스키마 버전 < 현재 빌드 | MVP: 로드 거부 모달 "이 저장은 구버전입니다. 새 여정을 시작해주세요" · Phase 2: 마이그레이션 |
| 참조 ID 끊김 (예: 삭제된 PartDataSO) | 해당 파츠 제거 후 경고 로그 + 게임 진행. `partStash`에서 누락된 엔트리만 스킵 |
| 승무원 참조 끊김 | 승무원 제거 + 직책 공석 처리 (docs/04 §8.1 페널티 적용) |

### 6.2 원자적 쓰기 검증

```
Save 쓰기 순서:
  1. Serialize → tempPath = save.json.tmp
  2. 쓰기 성공 시:
     File.Replace(tempPath, save.json, save.json.bak)  # 기존을 .bak로 옮기고 temp를 본체로
  3. 성공 시 save.json.bak 삭제
  4. 쓰기 실패 시 tempPath 정리, 기존 save.json 유지
```

**MVP 단순화**: .bak 보존은 선택사항 (1슬롯 + 다음 자동 세이브 전까지만 의미 있음). 최소 rename 원자성만 확보.

### 6.3 로그 규약

모든 세이브/로드 이벤트는 `[CRUX] [SAVE]` 프리픽스:

```
[CRUX] [SAVE] RequestSave(trigger=BattleResultConfirmed)
[CRUX] [SAVE] Serialize OK (size=42KB, schemaV=1)
[CRUX] [SAVE] Write OK (elapsed=18ms)
[CRUX] [SAVE] Load OK (nodeId=camp_mines, playTimeMinutes=724)
[CRUX] [SAVE] ParseFailed: <exception>
```

---

## 7. 파일 포맷 상세

### 7.1 직렬화 방식

- **JSON** (텍스트). 이유: 디버그 가능·수동 검사 용이·플랫폼 독립
- Unity `JsonUtility` 대신 **Newtonsoft.Json (Json.NET)** 권장
  - JsonUtility는 Dictionary·polymorphism 미지원
  - 본 스키마는 Dictionary 빈번
- 패키지: `com.unity.nuget.newtonsoft-json` (Unity 공식 권장)

### 7.2 경로·파일명

| 플랫폼 | 경로 |
|---|---|
| Windows | `%USERPROFILE%\AppData\LocalLow\<Company>\CRUX\save.json` |
| macOS | `~/Library/Application Support/<Company>/CRUX/save.json` |
| Linux | `~/.config/unity3d/<Company>/CRUX/save.json` |

Unity의 `Application.persistentDataPath`가 자동 처리.

### 7.3 크기 예산

MVP 캠페인 후반 추정:
- 전차 6대 × (파츠 12~20개 + 모듈 8개 + 승무원 5) ≈ 각 3KB → 18KB
- 파츠 스태시 50~100개 × 200B → 20KB
- 승무원 풀 20명 × 1KB → 20KB
- 노드·플래그·큐 → 5KB
- **총 ≈ 63KB**. 100KB 여유.

암호화·압축 없음 (MVP). 필요 시 Phase 2.

---

## 8. 기존 PlayerPrefs 영속화 마이그레이션

`b98ad83 save-minimal 자금/사기 PlayerPrefs 영속화` 커밋 존재. MVP JSON 도입 시 경로:

1. **신규 빌드**: PlayerPrefs 키(`funds`/`morale`) **무시**. `save.json` 기준으로만 동작
2. **구버전 데이터 승계**: 1차 로드 시 `save.json` 없음 + PlayerPrefs에 값 있음 → **1회 임시 승계 로직** (선택)
   - MVP: 승계하지 않음. "기존 테스트 데이터 소실" 감수 (테스트 단계이므로 허용)
3. **PlayerPrefs 키 삭제**: JSON 마이그레이션 커밋에서 `PlayerPrefs.DeleteKey("funds")`·`DeleteKey("morale")` 수행. 이후 혼란 방지

**용도 전환**: PlayerPrefs는 **옵션 값** 저장으로만 쓴다 (마스터 볼륨·전체화면). 캠페인 데이터와 분리.

---

## 9. 구현 책임 분리 (Dev 가이드)

> 본 절은 `Crux-dev` 워크트리 구현자 참조용.

### 9.1 신규 타입

| 타입 | 네임스페이스 제안 | 성격 |
|---|---|---|
| `SaveTrigger` | `Crux.Core.Save` | enum: CampArrival / BattleResultConfirmed / DialogNodeCompleted / RestNodeCompleted |
| `CampaignSave` + 하위 DTO | `Crux.Core.Save` | JSON 직렬화 대상 POCO |
| `SaveManager` | `Crux.Core.Save` | static, `RequestSave`·`TryLoad`·`HasSave`·`DeleteSave`·`ReadMeta` API |
| `SaveMigrator` (Phase 2) | `Crux.Core.Save` | 버전 마이그레이션. MVP는 `schemaVersion == 1` 검사만 |

### 9.2 핵심 API

```
SaveManager.HasSave() → bool
SaveManager.ReadMeta() → SaveMeta? (schemaVersion, savedAt, nodeName, playMinutes)
SaveManager.RequestSave(SaveTrigger, Action<SaveResult>?)
SaveManager.TryLoad() → (success, CampaignSave?, LoadFailReason?)
SaveManager.DeleteSave()
```

### 9.3 통합 지점

- **`WorldMapController`** — 노드 종료 이벤트에서 `RequestSave` 호출. 트리거 종류 구분해 전달
- **`BattleController`** — 전투 결과 모달 Return 버튼 → `MaintenanceTicker.OnBattleEnd` → `RequestSave(BattleResultConfirmed)`
- **`MaintenanceTicker.OnCampVisit`** — 처리 완료 후 `RequestSave(CampArrival)` (§3.3 순서)
- **`TitleSceneController`** — `HasSave` 체크로 버튼 활성/회색, `ReadMeta`로 메타 표시
- **`ConvoyInventory`·`TankInstance`·`CrewMemberRuntime`** — Serialize/Deserialize 헬퍼 노출 (또는 SaveManager가 리플렉션 회피 직접 매핑)

### 9.4 빌드·복원 헬퍼

**직렬화 주의점**:
- ScriptableObject 참조는 id로. SO 자체는 직렬화하지 않고 ID로 역참조 (`ScriptableObject.Find` 또는 별도 레지스트리)
- DateTime은 ISO8601 문자열로 (문화권 영향 회피)
- Dictionary는 Newtonsoft가 처리 가능 (JsonUtility 불가)
- Nullable 필드는 `null` 그대로 허용 (Newtonsoft 기본)

---

## 10. 오픈 이슈 / 결정 필요

1. **Newtonsoft 의존 추가 승인** — 현재 프로젝트에 이미 있는지 확인 필요. 없으면 `com.unity.nuget.newtonsoft-json` 패키지 등록. 용량 영향 미미
2. **PlayerPrefs 승계 여부** — §8 2번. 개발 테스트 데이터 보존 필요한지, 아니면 초기화 허용인지 사용자 결정
3. **노드 진입 시점 정확한 정의** — "노드 도달" vs "노드 처리 완료" (휴식 노드의 1초 이벤트 내부에서 세이브하면 중복 방지 복잡). 현재는 **완료 직후**로 통일 — 재확인 필요
4. **스팀 도전 과제 저장** — Steam API는 별도 저장. MVP에는 도전 과제 없음으로 가정
5. **`campaignStats`(§2.1) 필드 구체 구성** — 총 전투 수·격파 적 수·피격 관통 수 등. 세이브 표시용/통계 공개용 이중 목적. 최소 필드 정의는 후속
6. **전투 중 자동 저장 (체크포인트)** — 플레이 시간 25~30분(docs/09 §9)이면 체크포인트 없이 허용 가능. 재시작 손실이 크게 느껴지면 후속 도입
7. **스키마 버전 증가 가이드** — 필드 추가 = 마이너(호환 가능 가정, MVP는 `>v1` 거부), 필드 제거·타입 변경 = 메이저. 문서화 시점 필요
8. **다중 캠페인 구분** — Phase 2 다중 슬롯 도입 시 `save_1.json`·`save_2.json`·`save_3.json`. 확장 여유 위해 **현 단계에서도 `save.json`보다 `save_0.json`이 나을지** 검토

---

## 11. 튜닝 훅

| 항목 | 초안 | 변경 사유 |
|---|---|---|
| §3.1 자동 세이브 트리거 4종 | 현재 4개 | 중간 저장 요청 발생 시 확장 (예: 상점 거래 후) |
| §5.4 로딩 화면 표시 임계 | 실측 후 결정 | 파일 크기·디바이스 스펙 |
| §6.2 원자적 쓰기 .bak 보존 | 보존 안 함 | 복구 요청 발생 시 도입 |
| §7.1 Newtonsoft vs JsonUtility | Newtonsoft | Dictionary 제거·스키마 단순화 시 JsonUtility 회귀 가능 |
| §7.3 크기 예산 100KB | 100KB | 캠페인 확장 시 재측정 |

---

## 12. 범위 외

- **스팀 클라우드 동기화** — Phase 2, 플랫폼 어댑터 별도
- **세이브 파일 암호화** — MVP 불필요 (스피드런·치트 방지 이슈 생기면 HMAC 정도)
- **프로필 시스템** (사용자 계정별 프로필) — 싱글 디바이스 단일 사용자 전제
- **자동 백업 세대 관리 (3세대 등)** — Phase 2
- **버전 마이그레이션 엔진** — Phase 2, 현재는 버전 체크만
- **도전 과제 진행도** — 저장 대상 아님 (Steam API로 이관 예정)
- **재생 (Replay) 데이터** — 게임플레이 재현용, 저장 스코프 밖

---

## 13. 변경 이력

| 날짜 | 변경 |
|---|---|
| 2026-04-21 | 초판. 단일 슬롯 자동 세이브 정책, 4종 트리거, JSON(Newtonsoft) 포맷, 스키마 버전 1, 원자적 쓰기, 실패 대응, docs/10 §7.1 타이틀 확장 제안, docs/03b §8 오픈 이슈 #6 (campVisits) 반영. 튜닝 훅·오픈 이슈 다수 명시 |
