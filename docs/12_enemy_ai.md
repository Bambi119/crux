# CRUX 적 AI 전술 기획서 v1.1

> 작성일: 2026-04-14
> 기준 브랜치: `feature/hex-grid`
> 상태: **기획 승인 완료 — 구현 대기 (v1.1 세부 결정 반영)**

## 0. 설계 원칙

- 모든 적은 **Role**(역할)을 갖는다. Role이 행동의 의도를 정의한다.
- 전술 판단은 **상태기계(State Machine) × 유틸리티 스코어링(Utility AI)** 하이브리드로 구성한다.
  - 상태기계: Role × 상황 → State 결정 (의도 선택)
  - 유틸리티: 선택된 State 안에서 이동·사격 후보를 가중 점수로 평가 (최적 행동 선택)
- 참조 설계: XCOM 2, Jagged Alliance 3, Fire Emblem, Wargroove, Gears Tactics.
- 현 프로젝트 구조(6방향 flat-top hex, 모듈 장갑, AP 시스템)를 전제로 한다.

## 1. 사용자 확정 결정 (Decision Log)

| # | 결정 | 영향 범위 |
|---|---|---|
| D1 | **자폭 드론은 셀 단위 HE 블라스트로 피해** | 드론 자폭이 인접 엄폐물/건물에도 데미지 적용. 동적 지형 파괴 연계 가능 |
| D2 | **보병은 개별 병사(캐릭터) 단위** | 한 병사 = 1 GameObject. 분대 단위 아님. 동시 다수 스폰 전제 |
| D3 | **고도는 타일 수치값으로만 표현** | 언덕 = +3 같은 스칼라. 실제 3D 고저 렌더링 없음. 명중률/LOS 판정에만 사용 |
| D4 | **웨이브당 적 수는 현실적 범위** (5~10) | AI 스코어링 예산 상한 결정 |
| D5 | **문서로 저장** | 본 파일이 공식 기획 레퍼런스 |
| D6 | **연막 = 2턴 유지 + 턴당 효과 감쇠 + 3턴 쿨다운** | 차량·플레이어 공통 규칙. 항 §3.1·§7.4 참조 |
| D7 | **자폭 드론 = 후면 장갑 HE / 엄폐물 동시 피해 / 기총 최고 카운터** | 밸런스 프레임 확정. 기총 카운터는 후순위 구체화 |
| D8 | **고도 시각화는 셀 틴트 1차 시도** | Unity Tilemap `SetColor()` — O(1) 비용. 복잡도 상승 시 높이 인디케이터로 폴백 |
| D9 | **건물 진입 폐기 → "고지 건물 타일"** | 보병/드론만 점유, 전차 불가. 사거리+1·명중+5%·엄폐 50%. 내부 모델링 없음 |
| D10 | **P1 지형 타일 톤 기준** = `Sprites/Tile_01-removebg-preview.png` | 다크톤 파편·금속판 계열 베이스. 변형 타일 프로시저럴/생성 MCP로 확보 |

---

## 2. 아키텍처

### 2.1 의사결정 파이프라인

```
매 적 턴 (유닛별):
  1) SituationEval   ─ 사실 수집
       ├─ 플레이어/아군 위치·HP·모듈 상태·자세각
       ├─ 자신의 HP·모듈·AP·탄
       ├─ LOS 가능 대상 집합
       ├─ 현재 셀의 지형·엄폐·은엄폐·고도
       └─ 지형 그래프 캐시 (이동 가능 셀)

  2) StateSelect     ─ Role 전이 테이블 → 이번 턴 State 결정
       { Engage, Flank, Reposition, Retreat, Guard,
         Suppress, Charge, Ambush, ModuleHunt, Bombard }

  3) CandidateGen    ─ 이동 후보 × 사격 후보 열거
       ├─ 이동 후보: GetReachableCells() 결과 → 지형 필터
       └─ 각 이동 후보에서 가능한 사격 조합

  4) UtilityScore    ─ Role 가중치 × 공통 팩터
       score = Σ wᵢ × factorᵢ + stateBonus

  5) Execute         ─ MoveTo → Fire (기존 시스템 재사용)
```

### 2.2 공통 스코어 팩터 (Factor Vocabulary)

모든 Role은 동일한 팩터 집합을 공유하고, 가중치만 다르다.

| 팩터 | 의미 | 범위 | 비고 |
|---|---|---|---|
| `dist` | 목표까지 hex 거리 | 0~8 | 음수 가중 시 접근, 양수 시 회피 |
| `losTo` | 목표까지 LOS 유효 | 0 or 1 | 0이면 사격 후보에서 배제 |
| `cover` | 공격 벡터 기준 방호면 엄폐율 | 0~1 | hex 방향별 방호면 판정 |
| `concealment` | 해당 셀의 은엄폐 (수풀 등) | 0~1 | 피격 확률 감소 |
| `flank` | 목표 측/후면에 대한 공격 각도 적합도 | 0~1 | hit-zone 계산 연계 |
| `exposure` | 해당 셀을 LOS로 보는 플레이어측 유닛 수 | 0~N | |
| `proxAlly` | 가장 가까운 아군과의 hex 거리 | 0~M | Role별 부호 |
| `modulePriority` | 목표의 파괴 가능 모듈 가치 총합 | ≥0 | 보병 전용 |
| `elev` | 공격자 고도 − 목표 고도 | 정수 | +1당 +5% 명중, LOS 보너스 |
| `facingHold` | 내 전면이 목표를 향하는지 | 0~1 | 구축전차 가중 |
| `reachAP` | 자폭 목표까지 필요 AP | ≥0 | 드론 전용, 음수 가중 |
| `kcs` | Kill Confidence Score = P(hit)·expDmg/targetHP | 0~1 | 사격 기대효과 |

### 2.3 State 사전

| State | 의미 | 기본 행동 규칙 |
|---|---|---|
| **Engage** | 사거리 확보 후 사격 | 가장 높은 kcs 후보 선택 |
| **Flank** | 목표 측/후면으로 우회 | flank·cover 가중↑, dist 무시 가능 |
| **Reposition** | 불리한 위치 이탈 | exposure·cover 중심 |
| **Retreat** | 저HP 후퇴 | exposure 최소 + 엄폐 최대 |
| **Guard** | 정지 방어 | 이동 0, 포탑·포신 회전만 |
| **Suppress** | 플랭커 견제 사격 | 우회 중인 플레이어 유닛 우선 타겟 |
| **Charge** | 직진 돌진 | dist만 고려 |
| **Ambush** | LOS 선점 고정 | losTo·facingHold 극대화 |
| **ModuleHunt** | 모듈 파괴 사격 | modulePriority 중심 타겟/벡터 선택 |
| **Bombard** | 간접 사격 | 최원거리 안전 셀 선호, LOS 불필요 |

---

## 3. 적 아카이타입 카탈로그

**v1 구현 대상**: 핵심 5종 + 보병(신규 캐릭터 시스템).
**Phase 2 확장**: 구축전차·자주포·지원차.

### 3.1 차량 — Reconnaissance Vehicle

| 항목 | 값 |
|---|---|
| Class | Vehicle |
| Role | Scout Flanker |
| 기본 State | `Flank` |
| HP | 낮음 (30~40) |
| 장갑 | 전면 20 / 측면 10 / 후면 5 |
| AP | 7 (이동 고속) |
| 주무장 | 20mm 기관포 (저관통 고연사) |
| 특수 | **연막 차단기 2회 (쿨다운 3턴)** |

**State 전이**
- 기본: `Flank`
- 공격각 확보 (측/후면 사격 가능) → `Engage` 1턴
- HP ≤ 40% → `Retreat`
- 개활지에서 `exposure ≥ 2` AND 인접 엄폐 없음 AND 연막 잔량 > 0 AND 쿨다운 만료 → `Reposition` + 연막 투척

**연막 규칙 (D6, 차량·플레이어 공통)**
- 지속: 2턴 (투척 턴 포함). 턴 경과마다 효과 감쇠
  - 1턴차: 명중률 −40%
  - 2턴차: 명중률 −20%
  - 3턴차: 효과 소멸
- 쿨다운: 투척 후 **최소 3턴** (차량 유닛별 독립 카운터)
- 잔량: 차량 기본 2발, 쿨다운과 독립. 둘 다 충족해야 재투척 가능

**가중치**
```
flank ×3.0  cover ×2.5  exposure ×−2.5
dist ×−0.5  kcs ×1.5    concealment ×2.0
```

**판별 규칙**
- 이동 후보 필터: 엄폐 or 은엄폐 셀만 허용 (개활지 이동 금지, 단 징검다리 마지막 셀 예외)
- 연막 발동: 본 유닛 턴 시작 시 체크, 상기 조건 만족 시 최우선 행동
- "징검다리 이동": `BFS(AP)`의 결과를 엄폐 체인(각 셀이 이전 셀 인접 엄폐) 순열로 정렬해 score 최대값 선택

### 3.2 자폭 드론 — Kamikaze Drone

| 항목 | 값 |
|---|---|
| Class | **Drone (신규)** |
| Role | Suicide Runner |
| 기본 State | `Charge` (항상) |
| HP | 1~2샷 |
| 장갑 | 없음 |
| AP | 8 (이동 극고속) |
| 플래그 | `isFlying = true` |
| 무장 | 없음 (자폭) |
| 자폭 | 인접 셀 → **셀 단위 HE 블라스트 (D1)** |

**자폭 피해 모델 (D1·D7 반영)**
- 폭발 중심: 자폭 시점 드론 위치
- 반경: 1 hex (본 셀 + 6방향 인접 = 최대 7셀)
- 데미지: 중심 100%, 인접 50%
- 적용 대상:
  - 전차 유닛: **후면 장갑 기준 HE 판정** (상방 낙하 가정)
  - 해당 셀·인접 셀의 엄폐물 HP (동시 피해로 동적 지형 변화)
  - 고지 건물 타일 (§4.1 참조)
- 탄두 타입: HE 고정 (관통은 낮고 블라스트 데미지 높음)

**이동 규칙**
- `isFlying` 플래그로 수풀·물·파편지대·벽 통과 (건물 외벽 통과 불가, 고지 건물 타일 위로 비행 통과 가능)
- A*는 통과 가능 셀만 고려
- 이동 후 플레이어와 거리 1이면 즉시 자폭 (다음 턴 대기 없음)

**가중치**: `reachAP ×−5.0` 단일. 기타 팩터 전부 0.

**카운터 (D7)**
- 기관총 사격이 **가장 효과적** — HP 극저라 1~2 버스트 처리
- 플레이어 오버워치(주포)는 구경 오버킬이나 여전히 유효
- 밸런싱: 기관총이 드론에 받는 데미지 보정(예: ×1.5 배율) 적용 후순위 구체화
- 고연사 보조 무기가 드론 러시의 주 해법이 되도록 설계

### 3.3 중형 전차 — Main Battle Tank

| 항목 | 값 |
|---|---|
| Class | Medium |
| Role | Balanced Opportunist |
| 기본 State | `Engage` |
| HP | 중 (70~90) |
| 장갑 | 전면 60 / 측면 30 / 후면 20 |
| AP | 5 |
| 주무장 | 76mm 주포 (균형형) + 기관총 |

**State 전이 (기본 → 기회 공격)**
- 기본: `Engage`
- 트리거 `Flank`:
  - 플레이어 유닛 중 하나가 중형 전차의 공격각에 측/후면을 노출한 경우, OR
  - 아군 중전차(Anvil)가 이미 플레이어와 교전 중이고 플레이어 주의가 쏠려 있을 때 (전선 고착 판정: 플레이어 AP가 아군 중전차 사격에 소모되는 패턴)
- 트리거 `Reposition`: 자신이 플레이어 시야에 측면 노출된 채 걸림

**가중치 (State별)**
```
Engage:      cover ×2.0  kcs ×2.5  dist ×−1.0  exposure ×−1.5  facingHold ×1.0
Flank:       flank ×3.0  dist ×−1.0  cover ×1.0  kcs ×2.0
Reposition:  exposure ×−3.0  cover ×2.5  dist ×0
```

**특징**: v1에서 "망치" 역할. 중전차의 모루와 함께 망치-모루 전술 성립.

### 3.4 보병 — Infantry Character (신규 시스템)

**중요**: D2 결정으로 **개별 병사 단위**. 분대 아님. 각 병사가 독립된 GameObject/AIController.

| 항목 | 값 |
|---|---|
| Class | **Infantry (신규)** |
| Role | Anti-Module Specialist |
| 기본 State | `ModuleHunt` |
| HP | 극저 (5~10) — 즉사 가능 |
| 장갑 | 없음 |
| AP | 4 (도보) |
| 무장 (개별 병사가 1종) | RPG / SMG / 저격소총 / 폭발물 中 택1 |
| 특수 | 건물 garrison 가능 (D3 고도 +3 적용) |

**병사 병종 (무장별)**
| 병종 | 무장 | 특성 |
|---|---|---|
| RPG병 | 고관통 저연사 | 3턴 재장전, 측/후면 관통 가능 |
| SMG병 | 저관통 연사 | 2셀 이내 근접, 모듈 다수 히트 |
| 저격수 | 중관통 1발 | 장거리 (6셀), 승무원 부상 우선 |
| 폭파병 | 인접 폭발물 설치 | 인접 시 자폭 유사 피해, 1회용 |

**타겟 선정 (ModuleHunt 규칙)**
```
moduleValue 테이블:
  Barrel         = 10   (주포 무력화 최우선)
  Engine         = 8    (기동 봉쇄)
  CaterpillarL/R = 6    (회전·이동 제한)
  TurretRing     = 5    (포탑 회전 봉쇄)
  AmmoRack       = 4    (대폭발 유도)
  MachineGun     = 3    (기총 무력화)
  Loader         = 3    (사격 AP +2)

targetScore = Σ (moduleValue × (1 − currentHP/maxHP) × shotVectorBonus)
shotVectorBonus: 현재 공격 각도에서 해당 모듈이 hit-zone 가중치를 받는지
```

**State 전이**
- 기본: `ModuleHunt`
- 사거리 밖 + 이동 가능한 엄폐 경로 있음 → `Flank` (엄폐 체인)
- HP ≤ 50% OR 사거리 이내 플레이어의 주포 조준 내 → `Retreat`
- RPG병 재장전 턴: `Reposition` (엄폐 유지)

**가중치**
```
cover ×4.0  modulePriority ×3.0  exposure ×−3.0
dist ×0.5   concealment ×3.0    elev ×1.5
```

**고지 건물 점유 (D9)**
- "건물 내부 진입" 개념 폐기. 대신 **전차가 접근 불가한 고지 건물 타일** 위에 보병(또는 드론)만 올라갈 수 있음
- 점유 효과:
  - 고도 +3 (명중률 보너스, LOS 단계 무시)
  - 사거리 +1
  - 엄폐 50% (방향 무관, 고정 부여)
- 해당 타일의 HP가 0이 되면 위에 있던 보병 피해/추락 처리 (구체화 후순위)

### 3.5 중전차 — Anvil (Heavy Tank)

| 항목 | 값 |
|---|---|
| Class | Heavy |
| Role | Line Holder · Flanker Suppression |
| 기본 State | `Guard` |
| HP | 매우 높음 (120~150) |
| 장갑 | 전면 120 / 측면 50 / 후면 30 |
| AP | 3 (이동 느림) |
| 주무장 | 대구경 주포 + 기관총 |
| 특수 | **오버워치 상시** (턴 시작 시 자동 설정) |

**State 전이**
- 기본: `Guard` — 이동 0, 포신·차체 회전만. 오버워치 자동 설정.
- 트리거 `Suppress`: 플레이어측 유닛이 아군 전선 측면으로 우회를 시작함 (플레이어 유닛의 이동 궤적이 아군 중간선을 교차)
- 트리거 `Reposition`: 현재 전면이 주요 위협 반대 방향이 된 경우 → 차체 180° 회전 (이동 없음)

**가중치**
```
Guard:     cover ×3.0  facingHold ×5.0  proxAlly ×2.0  dist ×0.1
Suppress:  kcs ×3.0 (단, 타겟은 플랭커 한정)  flank ×−2.0 (자신이 플랭크 받지 않게)
```

**망치와 모루 연계**
- 중전차는 **모루**: 전면 위치 고수로 플레이어의 주의를 고정
- 중형 전차는 **망치**: 중전차가 플레이어를 고정시킨 사이 측/후면 침투
- AI 판정: 중전차가 Guard 상태로 플레이어와 교전 2턴 이상 지속 → 중형 전차의 Flank 트리거 활성

### 3.6 확장 3종 (Phase 2)

| 이름 | Role | 핵심 특징 |
|---|---|---|
| 구축전차 | Hull-Down Sniper | 전면 장갑 극강, 측/후면 극약. `Ambush` 상태 고정. 절대 측면 노출 금지 (facingHold ×10) |
| 자주포 | Indirect Fire | LOS 무시 HE 탄. 후방 고정, 재장전 2턴. 근접 시 극취약 |
| 지원차 | Aura Buffer | 아군 명중률·AP 버프. 전투 회피. 플레이어 우선 파괴 유도 (hero tank) |

---

## 4. 지형 시스템

현 `TerrainType { Normal, Mud, Road }`은 enum만 존재. AI 결정의 기반이므로 **본 설계의 P1 구현 우선순위**.

### 4.1 지형 타입 명세

| 종류 | 이동비용 | 엄폐율 | LOS 차단 | 은엄폐 | 고도 | 비고 |
|---|---|---|---|---|---|---|
| 개활지 | 1 | 0% | × | 0% | 0 | 기본 |
| 도로 | 1 (+이동 보너스) | 0% | × | 0% | 0 | 이동 AP 턴당 +1 |
| 진창 | 2 | 0% | × | 0% | 0 | 느린 이동 |
| 수풀 | 1 | 0% | × | **30%** | 0 | 은폐만, 엄폐 아님 |
| 파편지대 | 2 | 20% | × | 10% | 0 | 엄폐+은엄폐 |
| 탄흔(크레이터) | 1 | 30% | × | 0% | −1 | **HE 피격 시 동적 생성** |
| 경엄폐 | 1 | 30% | × | 0% | 0 | 모래주머니/낮은 벽 |
| 중엄폐 | 1 | 50% | 부분 | 0% | 0 | 콘크리트/벽체 |
| 언덕 | 1 | 0% | × | 0% | **+3** | 명중·LOS 보너스 (D3) |
| 건물 외벽 | ∞ | — | ○ | — | 0 | 통과 불가, 인접 셀에 엄폐 제공 |
| **고지 건물 타일** | ∞ (전차) / 1 (보병·드론) | 50% | × | 0% | **+3** | **D9**: 보병 점유 전용 옥상. 사거리 +1, 명중 +5%. 내부 진입 개념 없음 |
| 물 | ∞ (지상) | 0% | × | 0% | 0 | **비행 통과 가능** |

### 4.2 고도 시스템 (D3·D8)

- 실제 3D 렌더링 없음. 셀의 `elevation: int` 속성만.
- 가장 흔한 값: 0 (기본), +3 (언덕/고지 건물)
- 효과:
  - 공격자 고도 − 목표 고도 = Δ
  - `hitChanceBonus = Δ × 5%` (양수일 때만)
  - LOS 계산 시 Δ > 0이면 중간 블로킹 1단계 무시
  - 사거리 +1 (고지에서 저지대 사격 시)
- **시각화 (D8)**: Unity Tilemap `SetColor(position, color)` 기반 셀 틴트가 1차 안.
  - elevation > 0 셀에 약한 하이라이트 틴트 (예: warm tint 5~10%)
  - 구현 비용 O(1), 기존 렌더 파이프라인 영향 없음
  - 복잡도 급상승 시 높이 인디케이터 스프라이트(셀 중앙 작은 화살표 등)로 폴백

### 4.3 신규 셀 속성 (코드 확장)

```csharp
// GridCell에 추가할 속성
public TerrainType terrain;          // 확장 enum
public int elevation;                 // 0, +3, -1 등 정수
public int concealment;               // % (0~100)
public bool blocksLOS;                // 중엄폐/건물 벽
public bool flyingPassable;           // 수풀·물·벽을 드론이 통과
public int moveCostOverride;          // 0=terrain 기본, >0=강제값
```

### 4.4 LOS 시스템 (신규 필요)

현재 hex line-of-sight 없음. AI의 `losTo`/`exposure` 팩터 선결 조건.

```
bool HasLOS(Vector2Int from, Vector2Int to)
  1. line = HexCoord.LineBetween(from, to)     // hex 보간 (Cube linear interpolation)
  2. for each mid in line[1..^1]:
       if mid.blocksLOS:
         if from.elev > mid.elev AND to.elev >= mid.elev:
           continue  // 고지 너머 저지대 목격 가능
         return false
  3. return true
```

### 4.5 동적 지형 파괴

- HE 탄 (주포·자폭 드론) 명중 셀: 엄폐물 HP 감소 → 파괴 시 `TerrainType → 탄흔`
- 건물: 벽체 HP 0 도달 시 `TerrainType → 파편지대` (garrison 중 보병 즉사)
- 플레이어도 이 변화를 활용 가능 (엄폐물을 주포로 날려 시야 확보)

---

## 5. 유닛 클래스 확장

### 5.1 TankClass enum 확장 (현 Vehicle/Light/Medium/Heavy)

```csharp
public enum UnitClass {
    Vehicle,    // 차량
    Light,      // 경전차
    Medium,     // 중형전차
    Heavy,      // 중전차
    Drone,      // 자폭/정찰 드론 (신규)
    Infantry,   // 개별 병사 (신규)
    TankDestroyer,  // Phase 2
    Artillery,      // Phase 2
    Support         // Phase 2
}
```

### 5.2 신규 플래그 (TankDataSO 확장)

```csharp
public bool isFlying;          // 수풀/물/벽 통과
public bool canGarrison;       // 건물 내부 점거 가능
public bool hasModuleHunting;  // 보병 모듈 타겟팅 활성
public AIRole defaultRole;     // AI Role enum
```

### 5.3 Infantry 시스템 신규 컴포넌트

보병은 전차와 구조가 크게 다름. 공통 인터페이스 추출 필요:
- 공통: `IBattleUnit` (HP, 위치, 턴 개념, 파괴 판정)
- 전차: `GridTankUnit` (기존)
- 보병: `GridInfantryUnit` (신규) — 모듈 없음, 단일 HP, 무장 1종, garrison 가능

AI 컨트롤러는 `EnemyAIController`가 공통 진입점이 되고 내부 Role 로직만 분화.

---

## 6. 구현 로드맵

### P1 — 지형 기반 (1~2일)
- [ ] `TerrainType` enum 확장 (10종)
- [ ] `GridCell` 속성 추가 (elevation/concealment/blocksLOS/flyingPassable)
- [ ] `GridManager.FindPath`의 이동 비용을 terrain 기반으로 교체
- [ ] `HexCoord.LineBetween` + `GridManager.HasLOS` 구현
- [ ] 기존 테스트 맵에 지형 수동 태깅 (개활지/엄폐/수풀 기본 배치)
- [ ] `CalculateHitChanceWithCover`에 concealment·elevation 팩터 통합
- 검증: 기존 플레이 흐름이 깨지지 않는지 컴파일 + 플레이테스트 1턴

### P2 — AI 골조 (2~3일)
- [ ] `AIContext` struct (Situation eval 결과)
- [ ] `IEnemyAI` 인터페이스 + `EnemyAIController` 컴포넌트
- [ ] 공통 스코어 함수 (12개 팩터 계산)
- [ ] `AIRole` enum + 가중치 테이블 ScriptableObject
- [ ] `BattleController.ProcessEnemyTurn`을 `ai.Decide(ctx)` 호출로 교체
- [ ] 첫 Role로 중형 전차(`Engage`만) 통과시키기
- 검증: 기존 2종 적(lightEnemy/heavyEnemy)이 중형 Role로 플레이 가능

### P3 — 핵심 5종 Role 구현 (3~5일)
- [ ] 중형 전차 Role 완성 (`Engage`/`Flank` 전이)
- [ ] 중전차 Role (`Guard`/`Suppress`)
- [ ] 차량 Role (`Flank`/`Reposition`, 연막 발동)
- [ ] 자폭 드론 Role (`Charge` + 셀 HE 블라스트)
  - D1 반영: `ExplodeAt(cell)` → 반경 1 hex, 셀 엄폐물·건물 HP 피해
  - `isFlying` 통과 로직
- [ ] 보병 Role (`ModuleHunt`)
  - D2 반영: `GridInfantryUnit` 신규, 무장별 병종 (RPG/SMG/저격수)
  - `IBattleUnit` 공통 인터페이스 추출
- [ ] 각 Role별 `TankDataSO`/`InfantryDataSO` 에셋 생성
- 검증: 각 Role 단독 테스트 씬에서 의도대로 행동

### P4 — 통합·밸런스 (2~3일)
- [ ] 복합 조우 맵 (중전차 1 + 중형 1 + 차량 2 + 드론 2 + 보병 3) — D4 범위
- [ ] 망치-모루 연계 AI 트리거 테스트
- [ ] 가중치 튜닝 세션
- [ ] 웨이브 스폰 테이블 (`WaveSpawner`)
- 검증: 단일 플레이테스트에서 각 Role이 의도된 행동 패턴 노출

### P5 — 확장 (후순위)
- [ ] 구축전차 (`Ambush`)
- [ ] 자주포 (`Bombard`)
- [ ] 지원차 (`Buff Aura`)

---

## 7. 기술 노트

### 7.1 성능 예산

- D4: 웨이브당 적 5~10 유닛 × 평균 50 이동 후보 × 12 팩터 = 초당 수천 회 스코어 연산
- 턴제이므로 실시간 부담 없음. 적 턴 1~2초 허용 가능.
- LOS 계산이 병목 가능성 → 맵 작을 때(8×10) 캐싱 불필요, 확장 시 재검토.

### 7.2 치트시트: Role → State 기본 전이

```
Vehicle (차량):
  default → Flank
  HP<40% → Retreat
  exposure≥2 && no adj cover → Reposition + SmokeScreen

Drone (자폭 드론):
  default → Charge
  dist==1 → Detonate (셀 HE)

Medium (중형):
  default → Engage
  player has exposed flank → Flank
  self side exposed → Reposition

Infantry (보병):
  default → ModuleHunt
  HP≤50% || in player LOS → Retreat
  RPG reloading → Reposition

Heavy (중전차):
  default → Guard (overwatch auto)
  enemy flanking allied line → Suppress
  front misaligned → Reposition (rotate only)
```

### 7.3 디버그 훅

- 각 적 유닛의 AIContext를 인스펙터에 노출 (읽기 전용)
- 최종 선택 점수 + 상위 3 후보 + factor 분해를 콘솔 로그로 출력 (toggle flag)
- 맵 기즈모: 현재 턴의 best path 와 LOS 라인

---

## 8. 미정·오픈 이슈

- ~~**연막 소비 규칙**~~ → **v1.1에서 D6으로 확정**: 2턴 유지, 감쇠, 3턴 쿨다운
- **보병 승무원 부상 연계**: 저격수가 모듈 대신 승무원을 노릴 때 메커니즘? (Phase 2 전투 기능 — 보류 목록 참조)
- ~~**드론 폭발 데미지 vs 장갑 축**~~ → **v1.1에서 D7로 확정**: 후면 장갑 기준 HE
- ~~**고도 시각화**~~ → **v1.1에서 D8로 확정**: 셀 틴트 1차 시도
- ~~**Infantry garrison UI**~~ → **v1.1에서 D9로 해소**: 건물 진입 개념 폐기, 고지 타일로 단순화
- **드론 기총 카운터 배율**: 기총이 드론에 받는 데미지 보정치 구체값 (×1.5? ×2?). 밸런스 패스 때 확정
- **고지 타일 파괴 처리**: HP 0 시 위의 보병이 피해·추락 vs 즉사 — 후순위

---

## 9. 변경 이력

| 날짜 | 버전 | 변경 |
|---|---|---|
| 2026-04-14 | v1 | 초안 작성. 사용자 결정 5건(D1~D5) 반영 |
| 2026-04-14 | v1.1 | 세부 결정 5건(D6~D10) 추가 반영: 연막 규칙·드론 피해축·고도 시각화·건물 진입 폐기·타일 톤 기준. §3.1·§3.2·§3.4·§4.1·§4.2·§8 갱신 |
