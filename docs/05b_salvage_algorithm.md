# 5b. 노획 알고리즘 상세 기획

> 작성: 2026-04-21
> 관계: `docs/05 §5` 노획 시스템 개요의 **구현 가이드 보조 문서**
> 참조: `docs/05 §5` (격파 방식별 노획률 기본선) · `docs/11 §4` (파츠 가치 계층·인플레 방지) · `docs/10 §3` (전투 결과 화면 UI)
>
> **목적**: 구현자가 격파 방식 판정과 파츠·차체 생존 처리를 **모호함 없이** 코드화할 수 있도록 판정 트리·확률 매트릭스·의사코드 수준 절차를 확정한다. 실제 C# 구현은 `Crux-dev` 워크트리에서 별도 수행.

---

## 0. 용어 정의

| 용어 | 영문 | 정의 |
|---|---|---|
| 격파 방식 | KillMethod | 적 전차가 사망할 때 **어떻게 무력화되었는가**의 분류. 5종(§3) |
| 파츠 생존 | Part Survival | 격파 직후 개별 파츠가 물리적으로 살아남아 노획 가능한지 여부 |
| 파츠 등급 | Part Grade | Trash / Common / Skilled / Elite / Legendary 5단계 (`docs/05 §4.3`) |
| 차체 노획 | Chassis Salvage | 적 전차의 **차체 자체**를 부대 보관 슬롯에 편입하는 행위 |
| 노획 리포트 | SalvageReport | 전투 종료 시 결과 모달에 전달되는 격파별 집계 데이터 |

---

## 1. 설계 원칙

1. **판정은 우선순위 결정형** — 동시에 성립 가능한 조건이 있을 때 **최초로 매치되는 규칙**이 채택된다. 후순위 규칙은 평가하지 않음
2. **격파 방식이 경제를 결정** — 같은 적이라도 **어떻게 죽였나**로 파츠 생존률·차체 획득이 결정 (docs/05 §5 설계 철학 재확인)
3. **모호 케이스는 문서화** — 실전에서 자주 충돌하는 케이스는 §3.3에 명시, 구현자가 임의로 해석하지 않도록
4. **확률은 단일 소스** — 모든 생존률은 본 문서의 매트릭스가 유일한 기준. 코드에 하드코드 불가, ScriptableObject로 추출 권장
5. **후속 튜닝 대비** — 수치는 **초안**. §10 튜닝 훅으로 조정 여지 명시

---

## 2. 입력·출력 스펙

### 2.1 입력 — 격파 시점에 접근 가능한 데이터

구현자는 다음 데이터에 접근할 수 있다는 전제로 판정 로직을 작성한다:

| 데이터 | 출처 | 용도 |
|---|---|---|
| 마지막 피격 결과 | `DamageOutcome` (`ammoExploded`/`moduleHit`/`damagedModule`/`fireStarted`) | 유폭·모듈 피격 직격 판정 |
| 화재 상태 | `GridTankUnit.IsOnFire` + HP 0 도달 경로 | 화재 전소 판정 (`FireKill`) |
| 모듈 상태 전체 | `ModuleManager.All` — 8종 모듈(Engine/Barrel/MachineGun/AmmoRack/Loader/CaterpillarL/CaterpillarR/TurretRing) | 다중 모듈 파괴 판정 |
| 승무원 부상 상태 | `TankCrew` 각 슬롯의 `InjuryLevel` | 승무원 전원 사살 판정 |
| HP 비율 | `currentHP / maxHP` | 차체 노획 하한 체크 |
| 적 전차 인스턴스 | `TankInstance` (장착 파츠 리스트) | 파츠별 생존 판정 대상 |

### 2.2 출력 — 전투 전체 `SalvageReport`

한 전투에서 격파된 모든 적에 대해 누적:

```
SalvageReport
├─ entries: List<SalvageEntry>
│     ├─ enemyDisplayName: string
│     ├─ method: KillMethod
│     ├─ partsSalvaged: List<PartInstance>
│     ├─ chassisSalvaged: TankInstance? (null 가능)
│     └─ cascadeDamagedAdjacents: bool  (인접 모듈 연쇄 손상 발생 여부)
└─ Summary()
      └─ 등급별 파츠 수 집계 (결과 모달 요약 표시용)
```

---

## 3. 격파 방식 분류 (KillMethod)

### 3.1 5종 정의

| 값 | 한국어 표기 | 트리거 조건 (간결) | 노획 우위 |
|---|---|---|---|
| `AmmoRackDetonation` | 탄약고 유폭 | `DamageOutcome.ammoExploded == true` | **최저** — 내부 파편 피해 극심 |
| `FireKill` | 화재 전소 | 화재 DoT로 HP 0 도달 | 낮음 — 탄약고 미유폭이지만 열손상 |
| `ModuleKill` | 모듈 집중 파괴 | HP 0 시점에 주요 모듈 **2개 이상** Destroyed (Engine/Barrel/TurretRing 중) | 중상 — 주요 파츠 일부만 손상 |
| `CrewKill` | 승무원 사살 | 피격 시점에 **모든 배치 승무원이 `Severe` 이상** 부상 + 차체 HP 잔량 | **최상** — 차체 온전 |
| `MainGunPenetration` | 주포 관통 직사 | 위 4조건 모두 불충족하고 HP 0 (일반 누적 관통) | 기본값 — 차체 훼손 중간 |

### 3.2 판정 순서 (Decision Tree)

```
ClassifyKill(GridTankUnit unit):
  1. IF unit.LastDamageOutcome.ammoExploded == true
     → return AmmoRackDetonation

  2. IF unit 사망 원인이 화재 DoT
     (= OnFireKilled 이벤트로 사망한 경우)
     → return FireKill

  3. destroyedMajorModules =
        { Engine, Barrel, TurretRing } 중 state == Destroyed 카운트
     IF destroyedMajorModules ≥ 2
     → return ModuleKill

  4. allCrewIncapacitated =
        배치된(공석 아닌) 승무원 전원이 InjuryLevel >= Severe
     IF allCrewIncapacitated
     → return CrewKill

  5. return MainGunPenetration  (폴백)
```

### 3.3 모호 케이스 규칙

| 케이스 | 판정 | 이유 |
|---|---|---|
| 유폭 + 주포 관통 직격 동시 (같은 피격) | `AmmoRackDetonation` | outcome 플래그가 기록되면 1번 규칙 우선 |
| 화재 중 피격으로 HP 0 | `MainGunPenetration` 또는 `ModuleKill` | 피격이 최후 타격. 화재는 기여했어도 직접 원인 아님. `FireKill`은 **DoT 단독**으로 사망한 경우만 |
| 모듈 3개 이상 파괴되었지만 HP 1 남음 | 사망 아님 — 판정 보류 | 격파 판정은 HP 0 도달이 필수 전제 |
| 캐터필러 양쪽 모두 파괴 + HP 0 | 주요 모듈에 미포함 → `MainGunPenetration` (폴백) | Caterpillar는 기동 모듈이며 화력 축 아님. 별도 `MobilityKill` 이후 확장 슬롯 |
| 승무원 전원 Severe인데 주포 관통으로 HP 0이 **같은 샷**에 발생 | `CrewKill` 우선 | 4번 규칙이 5번 폴백보다 먼저 평가되며, 승무원 전원 행동불능은 강한 신호 |
| 기총사수만 공석 + 나머지 4명 Severe | `CrewKill` | "배치된 승무원 전원" 기준이므로 공석은 제외 |
| 완전 공석 (승무원 0명) 적 전차 | `CrewKill` 판정 안 됨 | `allCrewIncapacitated` 판정 시 배치 승무원이 0이면 false (공허한 참 방지) |

### 3.4 로깅 의무

구현 시 각 판정 결과를 `[CRUX] [SALVAGE]` 프리픽스로 Debug.Log 남긴다. 모호 케이스 재현·밸런스 튜닝에 필수.

```
[CRUX] [SALVAGE] 적 전차 Panzer-3 격파 → CrewKill
                 (ammoExploded=false, fireDeath=false,
                  modulesDestroyed=[Barrel], allCrewDown=true)
```

---

## 4. 파츠 생존 확률 매트릭스

### 4.1 기본 매트릭스 (PartCategory × KillMethod)

표기: 개별 파츠의 **기본 생존 확률** (연쇄 손상 보정 전).

| KillMethod ＼ Category | Engine | Turret | MainGun | Armor | AmmoRack | Track | Auxiliary |
|---|---|---|---|---|---|---|---|
| AmmoRackDetonation | **5%** | 10% | 5% | 20% | **0%** | 15% | 10% |
| FireKill | 15% | 25% | 30% | 40% | 10% | 35% | 25% |
| MainGunPenetration | 35% | 40% | 50% | 40% | 30% | 55% | 50% |
| ModuleKill | 40% | 45% | 55% | 65% | 50% | 60% | 60% |
| CrewKill | **75%** | 80% | 85% | 70% | 70% | 85% | **80%** |

### 4.2 설계 의도 주석

- **AmmoRack 행**: 유폭이면 탄약고 자체는 **확정 소실**(0%), 인접 파츠(Engine·MainGun) 생존률 큰 폭 하락. Armor는 외피라 상대적 보존
- **FireKill 행**: 열 피해로 Engine·AmmoRack처럼 내부 가연 모듈 하락. Track은 지상 접촉부라 상대적 보존
- **CrewKill 행**: docs/05 §5.1 원문의 "70%" 기준선에서 카테고리별 ±10~15% 편차. **플레이어가 의도적으로 도달할 경로**이므로 보상이 뚜렷해야 함
- **Armor 행**: 피격면 외 3면은 상대적 보존. 단 `ModuleKill`에선 장갑면 피해 누적으로 생존률 상승(65%)

### 4.3 연쇄 손상 보정

파츠별 생존 판정 후 다음 보정을 **순차 적용**:

```
AdjustSurvival(part, baseRate, context):
  adjusted = baseRate

  # (1) 유폭 연쇄
  IF context.method == AmmoRackDetonation
     AND part.category IN {Engine, MainGun}
     → adjusted *= 0.5

  # (2) 인접 모듈 파괴
  FOREACH adjacent IN AdjacencyMap[part.category]
     IF adjacent.state == Destroyed
        → adjusted -= 0.10  (최대 -0.30 누적)

  # (3) 전차 HP 비율
  IF context.hpRatioAtDeath < 0.10
     → adjusted -= 0.05     (관통 누적 과다)

  return clamp(adjusted, 0.0, 0.95)
```

### 4.4 인접 관계 (AdjacencyMap)

```
Engine     ↔ AmmoRack   (엔진 구획 내 탄약고 공간 공유)
Engine     ↔ Turret     (동력 전달축)
AmmoRack   ↔ MainGun    (급탄 연결)
Turret     ↔ MainGun    (마운트)
Armor면    은 면별 독립 (인접 없음)
Track L    과 Track R   은 독립 (구동부 분리)
Auxiliary  는 인접 없음 (선택적 부가)
```

**주의**: 현실의 전차 레이아웃과는 단순화됐다. **게임 밸런스를 위한 추상 관계**로 취급.

---

## 5. 파츠 등급 결정 알고리즘

### 5.1 파이프라인

```
SalvagePartsFrom(enemyTank, killMethod, context):
  result = []
  FOREACH part IN enemyTank.installedParts:
    survivalRate = BaseMatrix[part.category, killMethod]
    survivalRate = AdjustSurvival(part, survivalRate, context)

    IF random01() >= survivalRate:
       continue   # 소실

    newGrade = DetermineGrade(part.originalGrade, killMethod, context)

    salvaged = new PartInstance(
        data: part.data,
        durability: CalcDurability(killMethod, context),
        grade: newGrade
    )
    result.Add(salvaged)

  return result
```

### 5.2 등급 변환 규칙

```
DetermineGrade(originalGrade, method, context):
  grade = originalGrade

  IF method == AmmoRackDetonation
     grade -= 2
  ELSE IF method == FireKill
     grade -= 1
  ELSE IF method == MainGunPenetration
     grade -= 1   (관통 직격으로 손상)
  ELSE IF method == ModuleKill
     grade -= 0   (모듈 피해지만 특정 파츠는 온전)
  ELSE IF method == CrewKill
     grade += 0   (변동 없음, 원본 등급 유지)
  # CrewKill은 **등급 상승 없음** — 노획 보상은 수량·확률로 표현

  return clamp(grade, Trash, Legendary)
```

### 5.3 내구도 결정

```
CalcDurability(method, context):
  base = 1.0
  IF method == AmmoRackDetonation → base = 0.25
  IF method == FireKill           → base = 0.40
  IF method == MainGunPenetration → base = 0.60
  IF method == ModuleKill         → base = 0.75
  IF method == CrewKill           → base = 0.90

  # 랜덤 편차 ±10%
  return clamp(base + rand(-0.1, +0.1), 0.1, 1.0)
```

내구도는 장착 가능하지만 추가 정비 시간이 필요한 상태를 표현. **정비 씬**(docs/03 §5)에서 수리 대상.

---

## 6. 차체 노획 (Chassis Salvage)

### 6.1 획득 후보 필터

다음을 **모두 만족**해야 차체 노획 판정으로 진입:

- `killMethod ∈ { CrewKill, ModuleKill }`
- `DamageOutcome.ammoExploded == false`
- `killMethod != FireKill`
- `hpRatioAtDeath >= 0.20`  (HP 20% 이상 남은 상태로 사망 — 크게 누적 피해 없음)

### 6.2 차체 획득 확률

| KillMethod | 차체 노획 확률 |
|---|---|
| CrewKill | **60%** |
| ModuleKill | 20% |
| (그 외) | 0% (§6.1 필터에서 탈락) |

### 6.3 획득 처리 분기

```
IF chassisRolled == true:
  IF convoy.HasStorageSlot():
     convoy.AddTank(new TankInstance(enemyTank.data))
     → 결과 모달에 "차체 노획 — 보관고로 이동" 표시
  ELSE:
     → 결과 모달에 "차체 노획 가능 — 기존 전차 폐기 선택 UI" 팝업
        플레이어 선택:
          · 신규 차체 폐기 (포기)
          · 기존 전차 1대 선택 → 폐기 + 해당 파츠 공용 인벤토리 환원
             + 폐기 사기 페널티 (-5)  ※ docs/05 §5.4 참조, 수치 후속 확정
```

**로시난테 예외**: 로시난테는 `isRocinante=true` 플래그로 폐기 불가 (docs/05 §1.3). UI에서 선택 불가로 표시.

### 6.4 차체 초기 상태

- 장착 파츠: **비어 있음** — 적이 쓰던 파츠는 §4·§5 경로로 별도 노획
- 기본 내구도: 0.50 (정비 필요 상태)
- 장착된 승무원: 없음

---

## 7. 결과 모달 표시 규칙

### 7.1 섹션 구성 (docs/10 §3 보강)

현재 docs/10 §3 전투 결과 화면의 **유닛 목록** 섹션 아래에 **노획 섹션**을 추가:

```
┌────────────────────────────────────────────┐
│  ▼ 작전 완료: 섬멸                          │
├────────────────────────────────────────────┤
│  격파: 적 전차 4                            │
│  아군 손실: 0 (부상 1)                      │
│  사기 변화: +8  (85 → 93)                   │
├────────────────────────────────────────────┤
│  격파 상세                                  │
│  · Panzer-3    [승무원 사살]  파츠 5 / 차체 ○│
│  · Panzer-4    [탄약고 유폭]  파츠 1 / 차체 ×│
│  · Recon-Alpha [관통 직사]    파츠 3 / 차체 ×│
│  · Drone-7     [화재 전소]    파츠 0 / 차체 ×│
├────────────────────────────────────────────┤
│  노획 파츠 요약                             │
│  쓰레기 ×2  일반 ×5  숙련 ×1  정예 ×1       │
├────────────────────────────────────────────┤
│  자금 보상: +₩2,500                         │
│             [확인]                          │
└────────────────────────────────────────────┘
```

### 7.2 표시 원칙

| 요소 | 규칙 |
|---|---|
| 격파 방식 뱃지 | 한국어 고정 표기 (§3.1 한국어 컬럼 그대로) |
| 방식별 색상 | 유폭=적색 / 화재=주황 / 관통=회색 / 모듈=노랑 / 승무원=녹색 (긍정 강조) |
| 파츠 수량 | 카테고리 무관 총합 (상세는 별도 탭) |
| 차체 노획 여부 | ○ (성공) / × (실패) 아이콘 |
| 등급 요약 | 5등급 전부 표기. 수량 0은 **회색 처리** (완전 생략 아님) |
| 상세 파츠 리스트 | 결과 모달 내 "상세" 탭으로 이관. 파츠명·카테고리·등급·내구도 표시. **첫 빌드는 미구현**, 요약만 |

### 7.3 첫 빌드 범위 (docs/10 §1.3 정합)

첫 빌드에서는 **격파 상세 행** + **노획 파츠 요약 1줄**까지만. 상세 탭·차체 노획 후속 UI는 이후. `docs/10 §3` 전투 결과 와이어프레임에 이 섹션 정식 반영.

---

## 8. 구현 책임 분리 (Dev 가이드)

> 본 절은 `Crux-dev` 워크트리에서 구현자가 참조할 목적.
> 네임스페이스·클래스명은 제안이며 구현자 재량으로 조정 가능.

### 8.1 신규 타입

| 타입 | 네임스페이스 제안 | 성격 |
|---|---|---|
| `KillMethod` | `Crux.Combat.Salvage` | enum 5종 |
| `SalvageEntry` | `Crux.Combat.Salvage` | 격파별 결과 struct/class |
| `SalvageReport` | `Crux.Combat.Salvage` | 전투 누적 컨테이너 |
| `SalvagePartMatrixSO` | `Crux.Data.Salvage` | ScriptableObject, §4.1 매트릭스 데이터 |
| `SalvageChassisTableSO` | `Crux.Data.Salvage` | ScriptableObject, §6.2 표 |

### 8.2 서비스 계층 (static 가능)

| 함수 | 책임 |
|---|---|
| `KillMethodClassifier.Classify(GridTankUnit) → KillMethod` | §3.2 판정 트리 실행 |
| `SalvageCalculator.CalculateParts(TankInstance, KillMethod, Context) → List<PartInstance>` | §5 파이프라인 |
| `SalvageCalculator.TryClaimChassis(GridTankUnit, KillMethod, Context) → TankInstance?` | §6 차체 판정 |

### 8.3 통합 지점

- **`BattleController`** — `OnDeath` 구독 시 `Classify` → `SalvageCalculator` → `SalvageReport.Add`
- **`GridTankUnit`** — `LastDamageOutcome` 속성 노출 필요 (현재 미확인, 구현자 확인 후 필요 시 추가)
- **`BattleEntryData`** — `SalvageReport` 정적 필드 추가 → 결과 모달이 접근
- **`MissionCompleteModalBinder`** — §7.1 UI 섹션 렌더
- **`ConvoyInventory`** — 기존 `Add(PartInstance)` 사용. 다수 파츠 귀속 시 반복 호출

### 8.4 GridTankUnit 최소 변경 (구현자 확인 필요)

판정에 필요한 상태 중 **현재 노출되지 않았을 가능성이 있는 항목**:

- `LastDamageOutcome`: 마지막 피격의 `DamageOutcome` 스냅샷. `ApplyPrerolledDamage` 끝에서 필드 저장
- `DeathByFire`: HP 0 도달 원인이 화재 DoT인지 플래그. 현재 `OnFireKilled` 이벤트 존재 → 구독만으로도 판정 가능(별도 플래그 불필요)

---

## 9. 경제 경로와 연계

### 9.1 docs/11 §4 파츠 가치 계층과 정합

| 등급 | 분포 (기획 상 기대) | 획득 경로 |
|---|---|---|
| 쓰레기 | 60~70% | AmmoRackDetonation·FireKill 중심 |
| 일반 | 20~30% | MainGunPenetration 중심 |
| 숙련 | 5~10% | ModuleKill 중심 |
| 정예 | 1~3% | CrewKill 중심 |
| 전설 | <1% | 스토리 한정 (알고리즘 외) |

§5.2의 **등급 하락 규칙**을 적용하면 위 분포가 자연 형성된다. 구현 후 플레이테스트에서 실측 분포가 기획 분포와 5% 이상 어긋나면 매트릭스 또는 등급 변환 규칙 재조정.

### 9.2 매각가 (docs/11 §2.3) 재확인

노획 파츠는 **매각 시 등급 고정가**. 본 알고리즘이 생성하는 등급이 그대로 경제 입력값이 된다. 매각가 튜닝은 docs/11에서 담당.

---

## 10. 튜닝 훅 (후속 결정 항목)

| 항목 | 초안 | 튜닝 사유 발생 시 |
|---|---|---|
| §4.1 생존 확률 매트릭스 35개 칸 | 표의 현재 값 | 플레이테스트에서 분포 편향 확인 시 |
| §4.3 HP 비율 하한(`< 0.10`) 보정(-5%) | 5% | 과관통 페널티가 약하다 / 강하다 의견 시 |
| §5.2 등급 변환 +/- 폭 | -2/-1/0/+0 | 특정 방식이 과보상이면 감산 강화 |
| §6.2 차체 노획 확률 | CrewKill 60% / ModuleKill 20% | 차체 획득이 루프에서 몇 번 일어나는지 측정 |
| §6.3 기존 전차 폐기 사기 페널티 | -5 | 구체 플레이 압박 관찰 후 |
| `SalvagePartMatrixSO` 에셋화 여부 | 권장 | 런타임 하드코드 시 튜닝 비용 증가 |

---

## 11. 기획 범위 외

- **분해 시스템** — `docs/05 §4.4` 분해(환원) 로직. 본 문서는 **노획까지만** 책임
- **스토리 고정 보상** — `docs/11 §2.4` Act 경계 보상은 알고리즘 대상 아님
- **전설 등급 획득 경로** — 스토리 이벤트 한정, 알고리즘 바깥
- **절차 생성 적 전차 파츠 인벤토리** — 적 `TankInstance` 구조 확장 필요. 현재는 프리셋 파츠 가정

---

## 12. 오픈 이슈 / 결정 필요

1. **적 전차의 `TankInstance` 보유 여부** — 현재 적은 `TankDataSO`만 참조할 가능성. 노획 대상 파츠 리스트가 없으면 알고리즘 가동 불가. 구현자가 기존 코드 확인 후 **프리셋 PartInstance 생성기**가 필요한지 피드백 필요
2. **`AdjacencyMap` 확정** — §4.4는 게임 밸런스용 추상. 사용자 디자인 의도와 맞는지 재검토
3. **차체 폐기 UX** — §6.3 "기존 전차 폐기 선택" 흐름을 Hangar 편성 탭과 연계할지, 결과 모달 내 즉시 결정인지 미정
4. **캐터필러 양쪽 파손 케이스** — 현재 `MainGunPenetration` 폴백. 별도 `MobilityKill` 신설 가치 있는지 후속 검토

---

## 변경 이력

| 날짜 | 변경 |
|---|---|
| 2026-04-21 | 초판. docs/05 §5 개요를 바탕으로 판정 트리·확률 매트릭스·등급 변환·차체 노획·모달 표시 규칙 확정. 구현자 가이드 §8 포함 |
