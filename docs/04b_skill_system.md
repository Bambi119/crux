# 4b. 스킬 시스템 상세 기획

> 작성: 2026-04-22
> 관계: `docs/04 §1~3` 직책·스탯·마크 위에 얹히는 **스킬 체계 통합 권위 문서**
> 참조: `docs/04 §1` (직책 5종) · `docs/04 §3` (마크 숙련도) · `docs/04 §6.1` (사기) · `docs/06 §3.4~3.6` (반격 코어 메커니즘) · `docs/03b §4.1` (정비 탭 각성 카드 UX) · `docs/10b` (격납고 모듈 경계)
>
> **목적**: 캐릭터(승무원) 하위로 흩어져 있던 스킬 정의·획득·장착·발동 규칙을 한 문서에 모은다. 직책별 풀, 전차장 특화, 반격 관련 스킬, 각성 카드 파이프라인을 통합 관리. 실제 C# 구현은 `Crux-dev` 워크트리에서 별도 수행.

---

## 0. 용어 정의

| 용어 | 영문 | 정의 |
|---|---|---|
| 스킬 | Skill | 캐릭터(승무원)에 귀속되는 능동·수동 능력. 직책 종속, 마크 각성으로 획득 |
| 패시브 | Passive | 장착 즉시 항상 적용되는 스킬. 조건부 활성화 가능 |
| 즉시형 액티브 | ActiveInstant | 플레이어가 자기 턴에 명시적으로 발동하는 스킬 |
| 반응형 액티브 | ActiveReactive | 턴 종료 시 예약 → 적 턴에 트리거 조건 충족 시 자동 실행 |
| 듀얼 | DUAL | 일부 액티브 스킬에 붙는 강화 옵션. 추가 AP로 효과 확장 |
| 각성 카드 | Awakening Card | 마크 레벨업 시 획득 후보 스킬. 블라인드 원칙 적용 |
| 요구조건 | Requirement Predicate | 스킬 장착 가능 조건. 전차 파츠·직책 매칭 검사 |
| 사기 보정 | Morale Modifier | 반응형·확률형 스킬의 발동률에 사기를 곱하는 공식 |

---

## 1. 설계 원칙

1. **스킬 = 캐릭터 귀속** — 스킬은 승무원 개인이 보유하고 장착한다. 전차에 귀속되지 않는다. 승무원이 전차를 옮기면 스킬도 함께 이동
2. **직책 종속** — 모든 스킬은 `targetClass` 1개를 가진다. 포수 스킬을 조종수가 장착할 수 없음
3. **마크 각성으로 획득** — 스킬 풀은 디자이너 수작업. 플레이어는 마크 레벨업 시 후보 카드에서 선택. 상점·전리품으로는 획득 불가 (MVP)
4. **블라인드 규율** — 다음 마크까지의 진행도는 공개, 다음 카드의 내용은 비공개. "언제 각성할지"는 알되 "무엇이 나올지"는 모른다
5. **슬롯 제한** — 직책당 패시브 2 + 액티브 2 = 4 슬롯. 보유 스킬 ≠ 장착 스킬. 미장착 스킬은 보유만 됨
6. **확률 발동에는 사기 보정** — 반응형·조건형 스킬은 모두 사기에 비례한 발동률 보정을 받는다. 패닉 상태에서 강제 0
7. **요구조건은 데이터 검증** — 장착 시 자동 검증. 요구조건 상실 시(파츠 교체 등) 자동 비활성화 + UI 경고

---

## 2. 스킬 타입 3종

### 2.1 패시브 (Passive)

장착 즉시 항상 적용. 조건부 활성화 가능 (예: "HP ≤50% 시 명중 +10").

**실행 모델**:
- 전투 시작 시 효과 등록
- 조건 만족 시 자동 활성, 미만족 시 자동 비활성
- 플레이어 입력 불필요

**예시**: 정밀 조준, 침착함, 안정 주행, 기민한 반격, 연쇄 반격, 측후면 경계, 위기 대응

### 2.2 즉시형 액티브 (ActiveInstant)

플레이어가 자기 턴에 명시적으로 발동.

**실행 모델**:

```
유닛 선택 → [E] 스킬 패널 열기
  스킬 hover → AP·쿨다운·효과 미리보기
  클릭 → (조건부) 무기 선택 → 타겟팅 → 실행
```

**툴팁 예**:
```
정밀 사격
AP: 5 / 8   쿨다운: 2턴
효과: 다음 주포 사격 명중 +25%, 1회
────── DUAL (+5 AP) ──────
효과: 상동 + 2회 사격 [가능]
```

**예시**: 정밀 사격, 즉시 장전, 약점 저격, 긴급 후진, 전탄 발사, 격려, 전선 고수

### 2.3 반응형 액티브 (ActiveReactive)

턴 종료 시 예약 → 적 턴 중 트리거 조건 충족 시 자동 실행.

**트리거 타입**:

| 트리거 | 조건 | 예시 스킬 |
|---|---|---|
| **MoveTrigger** | 적이 지정 범위 내 이동 | 기본 오버워치 (포수) |
| **FireTrigger** | 적이 아군에 사격 | 카운터 리액션 (전차장) |
| **SightTrigger** | 적이 시야 진입 | 레디 샷 (전차장) |

**제약**:
- 1턴에 반응형 발동 **1회만** (중첩 방지)
- 예약 시점에 AP 선지불 (오버워치는 Fire AP × 2)
- 자동 반격(`docs/06 §3.4`)은 반응형 액티브가 아님 — 별도 코어 메커니즘. 둘은 공존 가능 (단 같은 턴 발동 1회 제한 별도 적용)

### 2.4 DUAL — 액티브 강화 옵션

특정 액티브 스킬에 붙는 추가 효과. 기본 AP에 `dualExtraCost`(보통 +5)를 더 지불해 효과 확장.

**제어**:
- 스킬 데이터에 `supportsDual: bool` 플래그
- DUAL 미지원 스킬은 강화 옵션 자체가 표시되지 않음
- 사용 여부는 발동 직전 토글로 선택

**예시**: 정밀 사격(2회 사격), 연속 사격(추가 명중 보너스), 격려(범위 확장)

---

## 3. 슬롯 구성 & 요구조건

### 3.1 슬롯 (직책당 4)

직책당 장착 상한:

- **패시브 2 슬롯**
- **액티브 2 슬롯** (즉시형·반응형 혼합 가능)

→ 직책당 총 **4 슬롯**. 한 전차(승무원 5인) 기준 최대 **20 슬롯**.

미장착 스킬은 `ownedSkills`에만 보관. 정비 탭(또는 편성 탭의 승무원 상세)에서 교체 가능.

### 3.2 요구조건 Predicate

스킬마다 장착 가능 요구조건을 가진다. 전차 파츠·직책이 조건을 만족해야 장착.

**요구조건 축**:

| 축 | 값 |
|---|---|
| MainGunCaliber (주포 구경) | 소 / 중 / 대 |
| MainGunMechanism (주포 메커니즘) | 수동 / 반자동 / 자동 / 다연장 |
| AmmoType (탄종) | AP / HE / 로켓 / 소이 |
| MGType (기총 종류) | 경기총 / 중기총 / 개틀링 |
| HullClass (차체 등급) | 경 / 중 / 중 / 초중 |

**AND / OR 조합**:

```
즉시 장전
  targetClass: 탄약수
  requires: MainGunCaliber IN [대]
         OR MainGunMechanism IN [다연장]
```

**조합 연산자** (`SkillRequirement.operator`):
- `Any` — values 중 하나라도 일치 (OR)
- `All` — values 모두 일치 (드물게 사용)
- `None` — values 중 어떤 것도 일치하지 않음 (제외 조건)

### 3.3 범용 vs 특화 2 티어

| 티어 | 요구조건 | 효과 강도 | 예시 |
|---|---|---|---|
| **범용** General | 없음 | 보통 | 침착함, 약점 저격, 긴급 후진 |
| **특화** Specialized | 1+ 축 | 강함 | 정밀 조준(대구경), 즉시 장전(대구경 OR 다연장) |

정비/편성 시 전차 파츠가 바뀌어 특화 스킬이 요구조건을 잃으면 **자동 비활성화**. UI 경고 노출. 자동 대체 없음 — 플레이어 판단에 맡김.

### 3.4 슬롯 검증 규칙

| 시점 | 검증 동작 |
|---|---|
| 장착 시도 | 요구조건 검사 → 불만족 시 거부 |
| 파츠 교체 직후 | 장착된 특화 스킬 재검사 → 불만족 슬롯 비활성화 (장착은 유지, 효과 0) |
| 승무원 이동 직후 | 직책 일치 검사 → 불일치는 발생 불가 (직책 불변, §1.1 docs/04) |
| 전투 시작 직전 | 모든 활성 슬롯 효과 등록 |

---

## 4. 직책별 스킬 풀

> 모든 표 효과 수치는 **초안**. 플레이테스트 후 §9 튜닝 훅으로 조정.

### 4.1 포수 (Gunner)

| 스킬 | 타입 | 요구조건 | 효과 |
|---|---|---|---|
| 정밀 조준 | Passive | 대구경 | 조준 사격 명중 +15 |
| 속사 | ActiveInstant | 소구경 자동 | 2회 사격, 각 -5 명중 |
| 약점 저격 | ActiveInstant | 범용 | 모듈 타격 확률 +30 |
| 침착함 | Passive | 범용 | HP ≤50%에서도 명중 감소 없음 |
| 기본 오버워치 | Passive (자동) | 범용 | 턴 종료 시 AP 3 잔여면 MoveTrigger 예약 |
| 연속 사격 | ActiveInstant | 중·대구경 | 명중 시 AP 환급 1 (1턴 1회), DUAL 지원 |
| **냉정 조준** Cold Aim | Passive | 범용 | 반격 실행 시 −15% 명중 페널티 상쇄 (§5) |
| **연쇄 반격** Follow-Up | Passive | 범용 | 반격 직후 재피격 시 추가 반격 1회 (AP·탄약 별도, §5) |

### 4.2 탄약수 (Loader)

| 스킬 | 타입 | 요구조건 | 효과 |
|---|---|---|---|
| 즉시 장전 | ActiveInstant | 대구경 OR 다연장 | 장전 턴 1회 무효 |
| 화력 집중 (HE) | Passive | AmmoType=HE | HE 폭발 범위 +1셀 |
| 관통 강화 (AP) | Passive | AmmoType=AP | AP 관통력 +20 |
| 신속 교체 | Passive | 범용 | 탄종 교환 AP -1 |
| **속장** Quick Reload | Passive | 범용 | 반격 실행 시 반격 Fire AP −1 (최소 1, §5) |
| **측후면 경계** Peripheral Watch | Passive | 범용 | 측·후면 피격 시 전방 호 제한 1회 무시 (§5, 전차장과 공유) |

### 4.3 조종수 (Driver)

| 스킬 | 타입 | 요구조건 | 효과 |
|---|---|---|---|
| 긴급 후진 | ActiveInstant | 범용 | 공짜 1셀 후진, 방향 유지 |
| 드리프트 | ActiveInstant | HullClass=경 | 대각 이동 중 회전 1회 무료 |
| 안정 주행 | Passive | HullClass=중·초중 | 이동 후 사격 명중 페널티 제거 |
| 질주 | ActiveInstant | HullClass=경 | 이번 턴 이동거리 +2 |

### 4.4 기총사수/수리병 (Gunner-Mech)

| 스킬 | 타입 | 요구조건 | 효과 |
|---|---|---|---|
| 버스트 연장 | Passive | 경기총·중기총 | 점사 발사 수 +2 |
| 전탄 발사 | ActiveInstant | 중기총·개틀링 | AP 전량 소모, 사거리 내 연사 |
| 대 드론 타격 | Passive | 범용 | 소프트타겟(드론·보병) 명중 +15 |
| 제압 사격 | ActiveInstant | 중기총 | 적 1 셀 다음 턴 AP -1 |
| 응급 수리 | ActiveInstant | 범용 | 모듈 1개 즉시 복구 (쿨다운 4) |

### 4.5 전차장 (Commander)

전차장 스킬은 3 카테고리. 발동자=전차장, 수혜자=타 승무원 또는 전차 전체.

#### 4.5.1 (A) 지휘 (사기)

| 스킬 | 타입 | 효과 | 쿨다운 |
|---|---|---|---|
| 격려 | ActiveInstant | 즉시 사기 +15 | 3턴 |
| 전선 고수 | ActiveInstant | 이번 턴 사기 하락 전부 무효 + 피격 시 +5 | 4턴 |
| 승리의 함성 | ActiveInstant | 격파 직후 즉시 발동. 사기 +20 | 조건부 |

#### 4.5.2 (B) 대응

| 스킬 | 타입 | 트리거 | 효과 |
|---|---|---|---|
| 매복 | ActiveReactive | MoveTrigger | 턴 종료 시 은폐(LOS 0.5×). 적 진입 사격 시 크리 +30 |
| 약점 지적 | ActiveInstant | — | 적 1대의 모듈 1개 지정. 다음 아군 사격이 그 모듈 타격 |
| 기민한 반격 | Passive | — | 오버워치 발동 시 AP 1 환급 (1턴 1회) |
| 경고 사격 | ActiveInstant | — | 적 1대 지정. 그 적 다음 사격 명중 -20 |

#### 4.5.3 (C) 정보

| 스킬 | 타입 | 효과 |
|---|---|---|
| 전장 파악 | ActiveInstant | 이번 턴 시야 +2 |
| 적의 의도 | ActiveInstant | 근거리 적 1대의 다음 턴 AP·행동 타입 예측 UI 표시 |

#### 4.5.4 (D) 반격 보강 (전차장 특화)

| 스킬 | 타입 | 조건 | 효과 |
|---|---|---|---|
| **직감** Intuition | ActiveReactive | 피격 직전 | AP 비교 무시, 강제 선제 반격 (§5) |
| **측후면 경계** Peripheral Watch | Passive | 측·후면 피격 | 전방 호 제한 1회 무시 (§5, 탄약수와 공유) |
| **위기 대응** Crisis Reflex | Passive | Morale ≤ 40 피격 | "직감" 발동률 +50%p (이 턴 한정, §5) |

### 4.6 발동자·수혜자 구조

지휘·정보 카테고리 등 **전차 전체에 영향을 주는 스킬**은 발동자 = 전차장, 수혜자 = 타 승무원으로 분리.

```
예: 집중 사격
  발동: 전차장 쿨다운 3턴 소모
  수혜: 포수 다음 주포 사격 명중 +15
  UI:   포수 사격 시 버프 아이콘 표시
```

전차장 AP·쿨다운만 소모, 수혜자는 자동으로 혜택을 받는다. UI는 수혜자 슬롯에도 임시 버프 아이콘 표시.

---

## 5. 반격 관련 스킬 풀 (자동 반격 보강)

자동 반격 코어 메커니즘은 `docs/06 §3.4~3.6`에 정의 (7조건·이니셔티브·오버워치 면역). 본 절은 그 위에 얹히는 **승무원 스킬 6종**을 모은 권위표.

### 5.1 사기 보정 공식

**모든 확률 발동 스킬에 공통 적용**:

```
실제 발동률 = 기본률 × (0.5 + Morale / 200)
```

| Morale | 계수 | 비고 |
|---|---|---|
| 0 | 0.5× | 절반 |
| 50 (정상 하한) | 0.75× | |
| 80 (사기충천) | 0.9× | |
| 100 (만점) | 1.0× | 기본률 그대로 |
| 200 (이론적 극한) | 1.5× | 현재 미사용 (상한 100) |
| **< 20 (패닉)** | **강제 0** | 패닉 상태에서 확률 스킬 전면 차단 |

**적용 범위**: 본 §5 반격 스킬 6종 + §2.3 반응형 액티브 중 확률 명시된 항목 + §4 직책별 풀의 확률 명시 패시브.

### 5.2 반격 스킬 6종

| 스킬 | 보유 직책 | 조건 | 효과 | 기본 발동률 |
|---|---|---|---|---|
| **직감** Intuition | 전차장/포수 | 피격 직전 | AP 비교 무시, 강제 선제 반격 | 25% |
| **측후면 경계** Peripheral Watch | 전차장/탄약수 | 측·후면 피격 | 전방 호 제한 1회 무시 | 20% |
| **연쇄 반격** Follow-Up | 포수 | 반격 직후 재피격 | 추가 반격 1회 (AP·탄약 별도) | 15% |
| **냉정 조준** Cold Aim | 포수 | 반격 실행 시 | −15% 명중 페널티 상쇄 | 30% |
| **속장** Quick Reload | 탄약수 | 반격 실행 시 | 반격 Fire AP −1 (최소 1) | 20% |
| **위기 대응** Crisis Reflex | 전차장 | Morale ≤ 40 피격 | "직감" 발동률 +50%p (이 턴 한정) | 패시브 (조건부 자동) |

### 5.3 반격 코어와의 관계

- 반격 코어 메커니즘(7조건·AP 비교·오버워치 면역)은 스킬 없이도 작동
- 본 §5 스킬은 코어 위에 **확률적 강화·예외**로 동작
- "직감" 발동 시 §3.5 이니셔티브 AP 비교를 우회 — 즉 메커니즘 분기점에 끼어드는 형태
- 1단계 구현(반격 코어)은 스킬 없이 검증 가능. 스킬은 별도 단계

### 5.4 사기 필드 전제

`CrewMemberRuntime`에 `morale` 접근 경로가 있어야 함. 현재 사기는 **전차 단위**(`TankCrew.morale`, docs/04 §6.1)이므로 승무원 스킬 발동 시 자기 전차의 사기를 참조한다 (`crew.tank.morale`).

---

## 6. 스킬 획득 파이프라인 — 각성 카드

### 6.1 획득 트리거

전투 종료 시 `MaintenanceTicker.OnBattleEnd`가 호출되어 다음 흐름 실행 (`docs/03b §7.2` 정합):

```
for each crew in participatedCrew:
  for each axis in crew.class.axes:
    update killCounter / battleCounter
    if markThreshold 달성 (이중 게이트, docs/04 §3.3):
      AwakeningQueue.Add(MarkLevelUpEvent { crew, axis, newMarkLevel })
```

큐는 `Crux.Core.Maintenance.AwakeningQueue` 컨테이너에 누적. 정비 탭 진입 시 처리.

### 6.2 카드 수 분기

| 쿼리 결과 후보 수 | 처리 |
|---|---|
| 0개 | 스킵 (로그만 출력) |
| 1개 | **자동 획득** — 각성 팝업 표시 후 즉시 장착 제안 |
| 2~3개 | **카드 선택 UI** — 2~3장 가로 배치, 호버 설명 |
| 4개 이상 | 3장 랜덤 추출 후 카드 선택 (UI 폭 제약, 밸런싱) |

**중복 제거**: 이미 보유 중인 스킬·요구조건 불만족 스킬은 쿼리 결과에서 배제.

### 6.3 SkillPoolQuery 의사 코드

```
SkillPoolQuery.Query(crew, axis):
  candidates = AllSkills
    .Where(s => s.targetClass == crew.klass)
    .Where(s => crew.markLevel(axis) >= s.requiredMarkLevel(axis))
    .Where(s => !crew.ownedSkills.Contains(s))
    .Where(s => CheckRequires(s.requires, crew.tank))
  
  IF candidates.Count > 3:
    candidates = candidates.Shuffle().Take(3)
  
  return candidates
```

`CheckRequires`는 §3.2 요구조건 Predicate를 이번 전차 파츠·직책에 대해 평가.

### 6.4 정비 탭에서의 UX

상세 동선은 `docs/03b §4.1.3`. 본 문서는 데이터 측 책임만 정의:

- 큐 항목은 **순차 처리** — 한 번에 한 승무원
- 스킵·뒤로가기 없음 — 진입한 카드는 반드시 하나 선택
- 후보 4+개의 랜덤 3장 추출도 재뽑기 없음
- 선택 후 즉시 장착 여부 다이얼로그 → 편성 탭 슬롯 UI 호출

### 6.5 가시성 — 블라인드 규율

- **공개**: 다음 마크까지 남은 진행도 (예: `⊕ 대구경 14/15`)
- **공개**: 큐 대기 수량 (정비 탭 뱃지 `🔧 ●3`)
- **블라인드**: 다음 획득 스킬의 이름·효과 (카드 선택 모달 진입 직전까지)

블라인드 원칙은 매 플레이의 조합 다양성을 위한 것. 풀이 좁아도 매번 다른 스킬 조합 경험.

---

## 7. 데이터 스키마

### 7.1 `CrewSkillSO` (Crux.Data)

```
id                  string
displayName         string
description         string
type                SkillType        // Passive / ActiveInstant / ActiveReactive
targetClass         CrewClass        // Commander / Gunner / Loader / Driver / GunnerMech
requires            SkillRequirement[]
requiredMarkLevel   Dictionary<AxisKey, int>   // 각 축별 최소 마크
apCost              int              // ActiveInstant·일부 ActiveReactive
cooldown            int              // 발동 후 쿨다운 턴
supportsDual        bool
dualExtraCost       int              // 보통 5
dualEffectKey       string           // DUAL 강화 효과 식별자
triggerType         ReactiveTrigger  // Reactive 전용: Move / Fire / Sight
baseProcRate        float            // 0.0~1.0, 확률 발동 스킬에만
moraleAffected      bool             // 사기 보정 공식 적용 여부
effectKey           string           // effect 실행 훅
```

### 7.2 `SkillRequirement`

```
axis                RequirementAxis  // MainGunCaliber / MainGunMechanism / AmmoType / MGType / HullClass
operator            Op               // Any / All / None
values              string[]
```

### 7.3 `MarkLevelUpEvent` (Crux.Data.Crew)

```
crew                CrewMemberRuntime
axis                AxisKey
newMarkLevel        int              // 새로 도달한 마크 레벨 (1~5)
queuedAt            DateTime         // 디버그용
```

### 7.4 `CrewMemberRuntime` 스킬 필드 (`docs/04 §10.5` 발췌·확장)

```
ownedSkills         CrewSkillSO[]              // 획득한 스킬 전체
equippedPassives    CrewSkillSO[2]             // 장착 패시브 슬롯 2
equippedActives     CrewSkillSO[2]             // 장착 액티브 슬롯 2
cooldowns           Dictionary<SkillId, int>   // 남은 쿨다운 턴
disabledSlots       HashSet<SkillId>           // 요구조건 상실로 비활성화된 슬롯
```

### 7.5 `AwakeningQueue` (Crux.Core.Maintenance)

```
List<MarkLevelUpEvent> events
int Count → events.Count
void Add(MarkLevelUpEvent)
MarkLevelUpEvent? PeekNext()
void PopHead()
event Action<int> OnCountChanged          // 03b 뱃지 갱신용
```

---

## 8. 구현 책임 분리 (Dev 가이드)

> 본 절은 `Crux-dev` 워크트리 구현자 참조용. 네임스페이스·클래스명은 제안.

### 8.1 신규/이관 타입

| 타입 | 위치 | 비고 |
|---|---|---|
| `CrewSkillSO` | `Crux.Data.Crew` | 기존 `docs/04 §10.2` 정의 — 본 문서로 권위 이관 |
| `SkillRequirement` | `Crux.Data.Crew` | 기존 `docs/04 §10.3` |
| `SkillPoolQuery` | `Crux.Core.Crew` | 후보 스킬 필터·랜덤 추출. 순수 함수 |
| `AwakeningQueue` | `Crux.Core.Maintenance` | `docs/03b §7.1` 정의 |
| `SkillEffectRegistry` | `Crux.Combat.Skills` | `effectKey` → 실행 함수 매핑. 전투 시작 시 활성 슬롯 등록 |
| `MoraleModifier` | `Crux.Combat.Skills` | §5.1 사기 보정 공식 단일 소스. 모든 확률 발동에서 호출 |

### 8.2 통합 지점

- **`BattleController`** — 전투 시작 시 모든 활성 스킬을 `SkillEffectRegistry`에 등록. 종료 시 `MaintenanceTicker.OnBattleEnd` 호출 (큐 채움)
- **`FireExecutor` / `ReactionFireSequence`** — 반격 실행 시 §5 스킬 6종 호출. 사기 보정은 `MoraleModifier` 경유
- **`HangarController` (정비 탭)** — 큐 처리 모달 호출. UI는 `docs/03b §4.1.3`
- **`HangarController` (편성 탭)** — 슬롯 장착·교체 UI. 요구조건 상실 시 `disabledSlots` 표시

### 8.3 테스트 가능성

- `SkillPoolQuery.Query`는 순수 함수 — 단위 테스트 권장
- `MoraleModifier.Apply(baseRate, morale)`도 순수 — 경계값 테스트 (0, 19, 20, 50, 100) 필수
- 반격 시퀀스는 `ReactionFireSequence` 통합 테스트로 검증

---

## 9. 튜닝 훅

| 항목 | 초안 | 튜닝 사유 발생 시 |
|---|---|---|
| 사기 보정 공식 `0.5 + M/200` | 선형 | 패닉 절벽이 너무 부드럽거나 가파르면 비선형 검토 |
| 패닉 임계 Morale < 20 | 20 | 사기충천·정상 구간과의 체감 비례 |
| §5 6종 기본 발동률 (15~30%) | 표 참조 | 반격 빈도 측정 후 |
| 후보 카드 4+ 추출 상한 3장 | 3 | 빈도 측정 후 단순화 가능 |
| DUAL 추가 AP 5 | 5 | 듀얼 사용률 측정 |
| 슬롯 4개(P2+A2) | 4 | 직책당 너무 많거나 적으면 조정 |
| 마크 5단계 임계값 (`docs/04 §3.3`) | 표 참조 | 마크 5 도달까지 체감 |

---

## 10. 오픈 이슈

1. **사기 필드 접근 경로** — 승무원 스킬이 자기 전차의 `morale`을 참조해야 함. 현재 `CrewMemberRuntime`에서 `tank` 역참조가 없음 → 신설 필요 (또는 발동 시점에 컨텍스트 주입). 결정 필요
2. **확률 발동 UI 표시** — "기본 25% × 사기 0.9 = 22.5%" 같은 실시간 발동률을 플레이어에게 노출할지 비공개할지. 노출 시 디버그 가치 vs 비노출 시 긴장감
3. **반격 스킬 발동 우선순위** — 같은 피격 이벤트에 "직감"과 "측후면 경계"가 동시 발동 조건이면 어느 것이 먼저? → 제안: 발동률 높은 쪽 우선, 동률은 직책 우선순위(전차장 > 포수 > 탄약수)
4. **DUAL 효과의 서술 표준** — 카드 툴팁에 DUAL 효과 동시 표기 vs 별도 "DUAL 가능" 배지. UX 일관성 결정
5. **각성 카드 후보 풀 규모** — 직책당 10~15개 목표(`docs/04 §12`). 현재 표본 5~8개. Phase 2 확장 시 모집 풀에 맞춰 카드 빈도 재튜닝
6. **요구조건 상실 시 자동 비활성화 정책** — 현재 "장착은 유지, 효과 0". 대안은 "강제 해제 + 보유로 환원". UX 검증 필요
7. **반격 코어와 반응형 액티브의 1턴 1회 한도** — 둘이 같은 카운터를 공유하는지, 별도 카운터인지. 현재 별도로 명시했으나 실측 후 재검토

---

## 11. 범위 외

- **스킬 트리·연계** — 단일 카드 모델만 사용. 트리·전제 스킬 구조는 MVP 밖
- **스킬 강화/레벨업** — 한 스킬은 한 단계만. 강화 시스템 없음
- **상점·전리품 스킬 획득** — MVP에서는 마크 각성만. Phase 2 검토
- **스킬 책 (장비형 스킬)** — 도입 시 캐릭터 귀속 원칙(§1.1) 위반 — 신중 검토
- **PvP·멀티플레이어 밸런싱** — 싱글 전제

---

## 12. 변경 이력

| 날짜 | 변경 |
|---|---|
| 2026-04-22 | 초판. `docs/04 §4·§5·§6.2·§6.3` + `docs/06 §3.7` + `docs/03b §4.1` 흩어진 스킬 정의를 캐릭터 하위 권위 문서로 통합. 사기 보정 공식·반격 스킬 6종·각성 파이프라인·요구조건 Predicate·DUAL·슬롯 구성·데이터 스키마 단일 소스화. 원 문서들은 후속 PR에서 본 문서 포인터로 축약 |
