# 4b. 스킬 시스템 상세 기획 (v2)

> 작성: 2026-04-22 (v1) · 개정: 2026-04-22 (v2 — 분류 체계 재설계)
> 관계: `docs/04 §1~3` 직책·스탯·마크 위에 얹히는 **스킬 체계 통합 권위 문서**
> 참조: `docs/04 §1` (직책 5종) · `docs/04 §3` (마크 숙련도) · `docs/04 §6.1` (사기) · `docs/06 §3.4~3.6` (반격 코어 메커니즘) · `docs/03b §4.1` (정비 탭 각성 카드 UX) · `docs/10b` (격납고 모듈 경계)
>
> **목적**: 캐릭터(승무원) 하위로 흩어져 있던 스킬 정의·획득·장착·발동 규칙을 한 문서에 모은다. 직책별 풀, 전차장 명령 커맨더, 반격 관련 발동형 스킬, 마크 스케일링, 각성 카드 파이프라인을 통합 관리.
>
> **v2 핵심 변경**: 스킬 타입을 **패시브 / 발동형 / 액티브** 3계층으로 재설계. v1의 ActiveInstant·ActiveReactive를 통합 → 발동형으로, 능동 명령형은 액티브(전차장·수리병 한정)로 분리. 마크 스케일링 직책별 분기 도입. 자세한 사유는 §14 변경 이력.

---

## 0. 용어 정의

| 용어 | 영문 | 정의 |
|---|---|---|
| 스킬 | Skill | 캐릭터(승무원)에 귀속되는 능력. 직책 종속, 마크 각성으로 획득 |
| 패시브 | Passive | 장착 즉시 항상 적용. 조건부 자동 활성/비활성 가능 |
| 발동형 | Triggered | 보유 스킬이 탑승 전차 호환성 통과 시 활성. 트리거 조건 충족 시 확률 자동 발동. 마크업으로 발동률·범위 강화 |
| 액티브 | Active (Command) | 명령 커맨더 슬롯에 장착. 플레이어가 자기 턴에 명시적으로 발동. 전차장·수리병만 보유 가능 |
| 명령 커맨더 슬롯 | Command Slot | 전차장·수리병 전용 액티브 1슬롯. AP·쿨다운 직접 소모 |
| 사격 토글 | Fire Toggle | 사격 액션 모달 내에서 패시브가 부여한 변형 모드 (예: 정밀 사격) |
| 각성 카드 | Awakening Card | 마크 레벨업 시 획득 후보 스킬 또는 강화 스택 |
| 마크 강화 | Mark Reinforcement | 발동형 스킬이 마크업 시 발동률·범위가 한 단계 상승하는 경로 |
| 호환성 상태 | Compatibility State | 캐릭터 보유 스킬이 탑승 전차에 적용되는지 3상태 표시 (미습득/비활성/활성) |
| 요구조건 | Requirement Predicate | 스킬 활성 조건. 전차 파츠·직책 매칭 검사 |
| 사기 보정 | Morale Modifier | 발동형 스킬 발동률에 사기를 곱하는 공식 |

---

## 1. 설계 원칙

1. **스킬 = 캐릭터 귀속** — 스킬은 승무원 개인이 보유하고 장착한다. 전차에 귀속되지 않는다. 승무원이 전차를 옮기면 스킬도 함께 이동
2. **직책 종속** — 모든 스킬은 `targetClass` 1개를 가진다
3. **3계층 분류** — 패시브(항시) · 발동형(조건+확률) · 액티브(능동 명령) — 의도와 실행 모델이 다르므로 데이터·UI도 분리
4. **액티브는 전차장·수리병만** — 능동 행동 결정은 지휘관(전차장)과 보존 결정자(수리병)의 역할. 다른 직책의 능동성은 발동형으로 흡수
5. **마크 스케일링 직책별 분기** — 발동형은 마크업 시 **강화** (발동률·범위 상승), 패시브·액티브는 마크업 시 **신규 카드 획득**
6. **블라인드 규율** — 다음 마크까지 진행도 공개, 카드 내용 비공개
7. **확률 발동에는 사기 보정** — 발동형 스킬의 발동률에 사기 계수를 곱한다. 패닉(<20)에서 강제 0
8. **호환성 = 데이터 검증** — 보유 스킬은 탑승 전차에 따라 3상태(미습득/비활성/활성). 자동 검증 + UI 경고
9. **슬롯 제한** — 직책당 4슬롯 고정. 단 액티브 슬롯 보유 여부는 직책별로 분기 (§3.1)

---

## 2. 스킬 타입 3계층

### 2.1 패시브 (Passive)

장착 즉시 항상 적용. 조건부 활성화 가능.

**실행 모델**:
- 전투 시작 시 효과 등록
- 조건 만족 시 자동 활성 (예: "HP ≤50% 시 명중 +10")
- 플레이어 입력 불필요

**특수 변형 — 사격 토글 패시브**:
일부 패시브는 효과 자체가 "능동 모드 잠금 해제". 사격 액션 모달 내에 토글 버튼으로 노출되며, 플레이어가 사격마다 ON/OFF 선택. 액티브 슬롯·추가 AP 소모 없음 (사격 자체의 AP만).
- 예: **정밀 사격** 패시브 — 사격 모달에 [정밀 사격 모드] 토글 추가. ON 시 모듈 선택 UI 펼침 + 명중 −15%, OFF 시 일반 사격
- 데이터: `CrewSkillSO.fireToggleKey: string?` — 토글 식별자. 사격 UI가 이 키로 모달 옵션 구성

**예시**: 정밀 조준, 침착함, 안정 주행, 정밀 사격(토글), 화력 집중(HE), 관통 강화(AP), 신속 교체

### 2.2 발동형 (Triggered)

보유 스킬이 탑승 전차 호환성 통과 시 활성. 트리거 조건 충족 시 확률 자동 발동.

**실행 모델**:

```
전투 시작:
  보유 발동형 스킬 → 호환성 검사 → 활성 풀 등록
턴 진행 중:
  트리거 조건 충족 (피격·이동·시야·사격 후 등)
    → 발동률 = baseProcRate × 마크 스케일 × 사기 계수
    → Random < 발동률 → 효과 실행
    → UI: 발동 알림 토스트
```

**트리거 타입**:

| 트리거 | 조건 |
|---|---|
| MoveTrigger | 적이 지정 범위 내 이동 |
| FireTrigger | 적이 아군에 사격 |
| SightTrigger | 적이 시야 진입 |
| HitTrigger | 자기 전차 피격 직전·직후 |
| SelfFireTrigger | 자기 전차 사격 후 |
| LowMoraleTrigger | 사기 ≤ N 상태 |

**제약**:
- 1턴에 발동형 발동 **누적 1회만** (중첩 방지). 단 트리거 종류가 다르면 별도 카운터 (예: HitTrigger 1 + SelfFireTrigger 1 = 2회 가능)
- AP 비용 없음 (자동 발동). 다만 효과 자체가 "추가 사격"이면 그 사격 AP·탄약은 별도 소모
- 자동 반격 코어(`docs/06 §3.4`)와 직교 — 코어는 스킬 없이도 작동, 발동형은 코어 위 강화

### 2.3 액티브 (Active — 명령 커맨더)

전차장·수리병만 보유. 명령 커맨더 슬롯에 1개 장착. 플레이어가 자기 턴에 명시적으로 발동.

**실행 모델**:

```
유닛 선택 → [E] 액티브 패널 열기
  슬롯 hover → AP·쿨다운·효과 미리보기
  클릭 → (조건부) 타겟팅 → 실행
```

**툴팁 예**:
```
격려 (전차장)
AP: 4   쿨다운: 3턴
효과: 모든 승무원 사기 +15
```

**전차장 액티브** (지휘·대응·정보·반격 보강 4 카테고리, §5.5):
오버워치 / 격려 / 전선 고수 / 약점 지적 / 전장 파악 / T 타임 등

**수리병 액티브** (모듈 보존):
응급 수리 / 화재 진압 / 구급 처치 등

**제약**:
- 한 전차당 액티브 발동은 **턴당 최대 2회** (전차장 1 + 수리병 1)
- 전차장 또는 수리병이 사망·기절 시 해당 슬롯 액티브 사용 불가
- 쿨다운은 스킬 단위로 독립 추적

---

## 3. 슬롯 구성 & 호환성

### 3.1 직책별 슬롯 분배

직책당 총 4슬롯 고정. 액티브 슬롯 보유 여부는 직책별:

| 직책 | 패시브 | 발동형 | 액티브 (명령 커맨더) | 합계 |
|---|---|---|---|---|
| 포수 Gunner | 2 | 2 | 0 | 4 |
| 탄약수 Loader | 2 | 2 | 0 | 4 |
| 조종수 Driver | 2 | 2 | 0 | 4 |
| 기총사수/수리병 GunnerMech | 2 | 1 | 1 | 4 |
| 전차장 Commander | 2 | 1 | 1 | 4 |

→ 한 전차(승무원 5인) 최대 **20슬롯**. 그 중 액티브 슬롯은 **최대 2개** (전차장+수리병).

미장착 스킬은 `ownedSkills`에만 보관. 정비 탭 또는 편성 탭의 승무원 상세에서 교체 가능.

### 3.2 호환성 3상태

캐릭터 보유 스킬은 탑승 전차에 따라 3상태 중 하나:

| 상태 | 의미 | 표시 |
|---|---|---|
| **미습득** Unowned | 캐릭터가 이 스킬을 아직 안 가짐 | 회색 자물쇠 아이콘 |
| **비활성** Inactive | 보유했으나 현재 탑승 전차가 요구조건 미충족 | 어두운 아이콘 + 빨간 테두리 |
| **활성** Active | 보유 + 요구조건 충족 + 슬롯 장착 | 풀컬러 아이콘 |

**상태 전이 트리거**:
- 승무원 전차 이동 → 보유 스킬 전체 재검증
- 전차 파츠 교체 → 해당 전차 탑승 승무원 보유 스킬 재검증
- 마크업 카드 선택 → 미습득 → 비활성/활성 (호환성에 따라)

**UI 위치**:
- 캐릭터 상세 패널 — 보유 스킬 그리드 (3상태 모두 표시)
- 격납고 편성 탭 슬롯 — 장착 슬롯 4개에만 활성/비활성 색상 적용 (미습득은 빈 슬롯)
- 격납고 10f LoadoutEffect 재계산 이벤트(`LoadoutEffectsRecalculated`)에 스킬 호환성 갱신 묶음

### 3.3 요구조건 Predicate

스킬마다 활성 가능 요구조건을 가진다.

**요구조건 축**:

| 축 | 값 |
|---|---|
| MainGunCaliber (주포 구경) | 소 / 중 / 대 |
| MainGunMechanism (주포 메커니즘) | 수동 / 반자동 / 자동 / 다연장 |
| AmmoType (탄종) | AP / HE / 로켓 / 소이 |
| MGType (기총 종류) | 경기총 / 중기총 / 개틀링 |
| HullClass (차체 등급) | 경 / 중 / 중 / 초중 |

**조합 연산자** (`SkillRequirement.operator`):
- `Any` — values 중 하나라도 일치 (OR)
- `All` — values 모두 일치 (AND)
- `None` — values 중 어떤 것도 일치하지 않음 (제외 조건)

**예시**:
```
즉시 장전
  targetClass: 탄약수
  requires: MainGunCaliber=Any[대]  OR  MainGunMechanism=Any[다연장]
```

### 3.4 범용 vs 특화 2 티어

| 티어 | 요구조건 | 효과 강도 | 예시 |
|---|---|---|---|
| **범용** General | 없음 | 보통 | 침착함, 약점 저격, 긴급 후진 |
| **특화** Specialized | 1+ 축 | 강함 | 정밀 조준(대구경), 즉시 장전(대구경 OR 다연장) |

특화 스킬이 요구조건 잃으면 **자동 비활성화** (장착 유지, 효과 0). UI 빨간 테두리 경고. 자동 대체 없음.

### 3.5 슬롯 검증 시점

| 시점 | 동작 |
|---|---|
| 장착 시도 | 직책 일치 + 요구조건 검사 → 불만족 시 거부 (단 요구조건은 허용 + 비활성 표시) |
| 파츠 교체 직후 | 활성 스킬 재검사 → 불만족 슬롯 비활성화 |
| 승무원 이동 직후 | 보유 스킬 전체 재검사 → 호환성 3상태 갱신 |
| 전투 시작 직전 | 활성 슬롯 효과 등록 |

---

## 4. 마크 스케일링 — 직책별 분기

마크 1→5 진행 시 스킬 변화 경로는 타입별로 다르다 (직책별 분기 (C)).

### 4.1 발동형 — 마크 강화 스택

발동형 스킬은 마크업 시 **새 스킬을 받지 않고 기존 발동형 스킬 1개를 강화**한다.

**강화 축**:

| 축 | 강화 방식 |
|---|---|
| 발동률 | baseProcRate +5%p~+15%p |
| 범위 | 트리거 조건 확장 (예: 측면→측·후면) |
| 효과 강도 | 효과량 ±N (예: 도탄 확률 +5%) |

**예시 — 직감 (반격 상황 선제 사격)**:
| 마크 | 발동률 | 범위 |
|---|---|---|
| Mark 1 | 15% | 정면 피격 |
| Mark 2 | 25% | 정면 피격 |
| Mark 3 | 35% | 정면·측면 피격 |
| Mark 4 | 45% | 정면·측면 피격 |
| Mark 5 | 55% | 전 방위 피격 |

**예시 — 예측 기동 (반격 불가 각도 피격에서도 반격 가능)**:
| 마크 | 측면 발동률 | 후면 발동률 |
|---|---|---|
| Mark 1 | 15% | 0% |
| Mark 2 | 25% | 15% |
| Mark 3 | 35% | 25% |
| Mark 4 | 45% | 35% |
| Mark 5 | 55% | 45% |

**강화 데이터**:
- `CrewSkillSO.markScaleTable: List<MarkScale>` — 마크별 효과 변형 테이블 (발동률·범위·효과량)
- 강화는 누적이 아닌 **테이블 룩업** — 마크 N에 도달하면 N행의 모든 값 적용

**카드 분기**: 발동형 스킬을 가진 직책의 마크업 카드는 "신규 발동형 획득" vs "기존 발동형 강화" 중 1장 선택 형태 (`docs/03b §4.1.3` UX 분기).

### 4.2 패시브 — 카드 획득 (스택 강화 없음)

패시브는 마크업 시 **신규 카드 획득**만. 강화 스택 없음.

**이유**: 패시브 +3% 명중을 마크업으로 +5%, +7%, +10% 늘리면 최종 +25% 인플레가 발생. 직책별 풀에서 다른 패시브를 받는 다양성 우선.

**카드 풀**: §5 직책별 풀의 패시브 항목.

### 4.3 액티브 — 카드 획득 (스택 강화 없음)

액티브는 마크업 시 **신규 카드 획득**만. 강화 스택 없음.

**이유**: 액티브는 효과량보다 **선택지 다양성**이 핵심. 격려/전선 고수/T 타임 중 어느 것을 명령 커맨더 슬롯에 넣을지가 전략. 같은 격려가 마크업으로 +15→+25 사기 부여로 강화되면 다른 액티브를 받을 이유가 줄어듦.

**카드 풀**: §5.5 전차장 4 카테고리 + §5.4 수리병 액티브.

### 4.4 마크 진행과 카드 트리거

`docs/04 §3.3` 이중 게이트(킬·전투 카운트) 기반. 진행 흐름:

```
전투 종료 시:
  for each crew in participated:
    for each axis in class.axes:
      update killCounter / battleCounter
      if markThreshold 달성:
        AwakeningQueue.Add(MarkLevelUpEvent)

정비 탭 진입 시:
  AwakeningQueue 처리:
    각 이벤트 → SkillPoolQuery.Query(crew, axis)
    카드 분기:
      발동형 강화 후보 + 신규 카드 후보 모두 추출
      §6 카드 수 분기 적용
```

---

## 5. 직책별 스킬 풀

> 모든 표 효과 수치는 **초안**. 플레이테스트 후 §11 튜닝 훅으로 조정.

### 5.1 포수 (Gunner) — 패시브 2 + 발동형 2

| 스킬 | 타입 | 요구조건 | 효과 |
|---|---|---|---|
| 정밀 조준 | Passive | 대구경 | 정밀 사격 명중 +15 |
| **정밀 사격** | Passive (사격 토글) | 범용 | 사격 모달에 모듈 선택 토글 추가. ON: 명중 −15%, 모듈 확정 타격. OFF: 일반 사격 |
| 침착함 | Passive | 범용 | HP ≤50%에서도 명중 감소 없음 |
| 화염 사수 | Passive | AmmoType=소이 | 발화 확률 +20%p |
| **속사** | Triggered | 소구경 자동 | 사격 후 SelfFireTrigger 30% — 추가 1회 사격 (탄약 별도) |
| **연속 사격** | Triggered | 중·대구경 | 사격 명중 후 SelfFireTrigger 25% — AP 환급 1 |
| **약점 저격** | Triggered | 범용 | 사격 명중 후 30% — 모듈 타격 변환 |
| **직감** Intuition | Triggered | 범용 | 피격 직전 HitTrigger — AP 비교 무시, 강제 선제 반격 |
| **냉정 조준** Cold Aim | Passive | 범용 | 반격 실행 시 −15% 명중 페널티 상쇄 |
| **연쇄 반격** Follow-Up | Triggered | 범용 | 반격 직후 재피격 시 추가 반격 1회 (AP·탄약 별도) |

### 5.2 탄약수 (Loader) — 패시브 2 + 발동형 2

| 스킬 | 타입 | 요구조건 | 효과 |
|---|---|---|---|
| 화력 집중 (HE) | Passive | AmmoType=HE | HE 폭발 범위 +1셀 |
| 관통 강화 (AP) | Passive | AmmoType=AP | AP 관통력 +20 |
| 신속 교체 | Passive | 범용 | 탄종 교환 AP −1 |
| **즉시 장전** | Triggered | 대구경 OR 다연장 | 사격 후 SelfFireTrigger 25% — 장전 턴 1회 무효 |
| **속장** Quick Reload | Triggered | 범용 | 반격 실행 시 — 반격 Fire AP −1 (최소 1) |
| **측후면 경계** Peripheral Watch | Triggered | 범용 | 측·후면 피격 HitTrigger — 전방 호 제한 1회 무시 |

### 5.3 조종수 (Driver) — 패시브 2 + 발동형 2

| 스킬 | 타입 | 요구조건 | 효과 |
|---|---|---|---|
| 안정 주행 | Passive | HullClass=중·초중 | 이동 후 사격 명중 페널티 제거 |
| 추진 효율 | Passive | 범용 | 이동 AP −1 (1턴 1회) |
| **긴급 후진** | Triggered | 범용 | 피격 직전 HitTrigger 20% — 공짜 1셀 후진 (피격 회피 시도) |
| **드리프트** | Triggered | HullClass=경 | 대각 이동 중 25% — 회전 1회 무료 |
| **질주** | Triggered | HullClass=경 | 적 시야 진입 SightTrigger 30% — 이동거리 +2 |

### 5.4 기총사수/수리병 (GunnerMech) — 패시브 2 + 발동형 1 + 액티브 1

| 스킬 | 타입 | 요구조건 | 효과 |
|---|---|---|---|
| 버스트 연장 | Passive | 경기총·중기총 | 점사 발사 수 +2 |
| 대 드론 타격 | Passive | 범용 | 소프트타겟(드론·보병) 명중 +15 |
| **전탄 발사** | Triggered | 중기총·개틀링 | 적 근접 SightTrigger 25% — AP 전량 소모, 사거리 내 연사 |
| **제압 사격** | Triggered | 중기총 | 적 사격 직전 FireTrigger 20% — 적 명중 −15 |
| **응급 수리** | Active (Command) | 범용 | AP 4, 쿨다운 4턴 — 모듈 1개 즉시 복구 |
| **화재 진압** | Active (Command) | 범용 | AP 3, 쿨다운 3턴 — 자기 전차 발화 상태 즉시 제거 |
| **구급 처치** | Active (Command) | 범용 | AP 5, 쿨다운 5턴 — 부상 승무원 1명 즉시 회복 |

### 5.5 전차장 (Commander) — 패시브 2 + 발동형 1 + 액티브 1 (명령 커맨더)

전차장 스킬은 4 카테고리로 분류. 발동자=전차장, 수혜자=타 승무원 또는 전차 전체.

#### 5.5.1 (A) 지휘 (사기)

| 스킬 | 타입 | 효과 | AP / 쿨다운 |
|---|---|---|---|
| 격려 | Active | 즉시 사기 +15 | 4 / 3턴 |
| 전선 고수 | Active | 이번 턴 사기 하락 전부 무효 + 피격 시 +5 | 5 / 4턴 |
| 승리의 함성 | Active (조건부) | 격파 직후 즉시 발동. 사기 +20 | 0 / 조건부 |

#### 5.5.2 (B) 대응

| 스킬 | 타입 | 트리거/조건 | 효과 |
|---|---|---|---|
| 매복 | Active | 발동 후 1턴 은폐 (LOS 0.5×) | AP 6, 쿨다운 5턴. 적 진입 사격 시 크리 +30 |
| 약점 지적 | Active | 적 1대 모듈 1개 지정 | AP 3, 쿨다운 2턴. 다음 아군 사격이 그 모듈 타격 |
| 경고 사격 | Active | 적 1대 지정 | AP 4, 쿨다운 3턴. 그 적 다음 사격 명중 −20 |
| **기민한 반격** | Triggered | 오버워치 발동 시 | 30% — AP 1 환급 (1턴 1회) |

#### 5.5.3 (C) 정보

| 스킬 | 타입 | 효과 | AP / 쿨다운 |
|---|---|---|---|
| 전장 파악 | Active | 이번 턴 시야 +2 | 3 / 2턴 |
| 적의 의도 | Active | 근거리 적 1대의 다음 턴 AP·행동 타입 예측 UI 표시 | 4 / 3턴 |

#### 5.5.4 (D) 반격 보강 (전차장 특화)

| 스킬 | 타입 | 조건 | 효과 |
|---|---|---|---|
| **직감** Intuition | Triggered | 피격 직전 HitTrigger | AP 비교 무시, 강제 선제 반격 (마크 스케일 §4.1) |
| **예측 기동** Predictive Move | Triggered | 측·후면 피격 HitTrigger | 반격 불가 각도에서도 반격 가능 (마크별 측→후면 확장) |
| **대응 사격** Counter-Chain | Triggered | 반격 직후 재피격 | 추가 반격 1회 (마크업 시 발동률 상승) |
| **측후면 경계** Peripheral Watch | Triggered | 측·후면 피격 HitTrigger | 전방 호 제한 1회 무시 (탄약수와 공유) |
| **위기 대응** Crisis Reflex | Passive | Morale ≤ 40 피격 | "직감" 발동률 +50%p (이 턴 한정) |
| **T 타임** T-Time | Active | — | AP 3, 쿨다운 5턴 — 다음 피격 1회 5% 무피해 도탄 (HE 폭발 제외) |

### 5.6 발동자·수혜자 구조

지휘·정보 카테고리 등 **전차 전체에 영향을 주는 액티브**는 발동자 = 전차장, 수혜자 = 타 승무원으로 분리.

```
예: 약점 지적 (전차장 액티브)
  발동: 전차장 AP 3, 쿨다운 2턴
  수혜: 포수 다음 주포 사격이 지정 모듈 타격
  UI:   포수 사격 시 버프 아이콘 표시
```

전차장 AP·쿨다운만 소모, 수혜자는 자동으로 혜택. UI는 수혜자 슬롯에도 임시 버프 아이콘.

---

## 6. 반격 풀 통합 (자동 반격 보강)

자동 반격 코어 메커니즘은 `docs/06 §3.4~3.6`에 정의 (7조건·이니셔티브·오버워치 면역). 본 절은 그 위에 얹히는 **승무원 발동형 스킬 풀**을 모은 권위표.

> v1에서는 별도 "반격 풀 6종" 절로 분리했으나, v2에서는 모두 §5 직책별 풀의 발동형 항목으로 통합. 본 절은 사기 보정 공식과 발동 우선순위만 담당.

### 6.1 사기 보정 공식

**모든 발동형 스킬에 공통 적용**:

```
실제 발동률 = (baseProcRate × 마크 스케일) × (0.5 + Morale / 200)
```

| Morale | 계수 | 비고 |
|---|---|---|
| 0 | 0.5× | 절반 |
| 50 (정상 하한) | 0.75× | |
| 80 (사기충천) | 0.9× | |
| 100 (만점) | 1.0× | 기본률 그대로 |
| **< 20 (패닉)** | **강제 0** | 패닉 상태에서 발동형 전면 차단 |

**적용 범위**: 발동형 스킬 전체 + Passive 중 확률 명시 항목 (예: 위기 대응의 +50%p 부여 자체는 패시브, 그 결과 직감 발동률에 사기 곱). 액티브는 사기 영향 없음 (능동 명령이므로).

### 6.2 반격 발동형 풀 (요약 — §5에서 통합 관리)

| 스킬 | 보유 직책 | 트리거 | 기본 발동률 (Mark 1) |
|---|---|---|---|
| 직감 | 전차장/포수 | HitTrigger 정면 | 15% |
| 예측 기동 | 전차장 | HitTrigger 측·후면 | 15% (측면) |
| 대응 사격 | 전차장 | 반격 직후 재피격 | 15% |
| 측후면 경계 | 전차장/탄약수 | HitTrigger 측·후면 | 20% |
| 연쇄 반격 | 포수 | 반격 직후 재피격 | 15% |
| 냉정 조준 | 포수 | 반격 실행 시 (Passive) | 항시 |
| 속장 | 탄약수 | 반격 실행 시 | 20% |
| 위기 대응 | 전차장 | Morale ≤ 40 피격 (Passive) | 항시 |

상세 효과는 §5.1·§5.2·§5.5.4 참조.

### 6.3 반격 발동 우선순위

같은 피격 이벤트에 여러 발동형 트리거 동시 충족 시:

1. Passive 효과 먼저 적용 (위기 대응의 발동률 +50%p 등)
2. 발동률 높은 발동형 우선 판정
3. 동률은 직책 우선순위 — **전차장 > 포수 > 탄약수 > 조종수 > 기총사수**
4. 1턴 1회 한도 (HitTrigger 카운터)

### 6.4 사기 필드 전제

`CrewMemberRuntime`에 `morale` 접근 경로가 있어야 함. 현재 사기는 **전차 단위**(`TankCrew.morale`, docs/04 §6.1)이므로 승무원 스킬 발동 시 자기 전차의 사기를 참조 (`crew.tank.morale`).

---

## 7. 스킬 획득 파이프라인 — 각성 카드

### 7.1 획득 트리거

전투 종료 시 `MaintenanceTicker.OnBattleEnd`가 호출되어 다음 흐름 실행 (`docs/03b §7.2` 정합):

```
for each crew in participatedCrew:
  for each axis in crew.class.axes:
    update killCounter / battleCounter
    if markThreshold 달성 (이중 게이트, docs/04 §3.3):
      AwakeningQueue.Add(MarkLevelUpEvent { crew, axis, newMarkLevel })
```

큐는 `Crux.Core.Maintenance.AwakeningQueue` 컨테이너에 누적. 정비 탭 진입 시 처리.

### 7.2 카드 분기 — v2 신규

마크업 카드는 두 종류가 후보 풀에 함께 들어간다:

- **신규 카드** — 미습득 패시브·발동형·액티브 중에서 추출 (직책 매칭 + 마크 임계 통과)
- **강화 카드** — 보유 발동형 스킬 중 마크 강화 가능 항목 (마크 N → N+1 행으로 갱신)

```
SkillPoolQuery.Query(crew, axis):
  newCards = AllSkills
    .Where(s => s.targetClass == crew.klass)
    .Where(s => crew.markLevel(axis) >= s.requiredMarkLevel(axis))
    .Where(s => !crew.ownedSkills.Contains(s))
    .Where(s => CompatCheck(s, crew.tank))      // 비활성도 후보 가능, 단 UI 경고

  reinforceCards = crew.ownedSkills
    .Where(s => s.type == Triggered)
    .Where(s => s.markScaleTable.HasNextLevel(s.currentMarkInScale))

  candidates = newCards ∪ reinforceCards
  IF candidates.Count > 3:
    candidates = candidates.Shuffle().Take(3)

  return candidates
```

**비율 가이드** (튜닝 훅):
- 직책당 발동형 보유 1~2개 시 강화 카드 1장 보장 시도
- 직책당 발동형 0개 시 신규 카드만 추출

### 7.3 카드 수 분기

| 후보 수 | 처리 |
|---|---|
| 0개 | 스킵 (로그만) |
| 1개 | 자동 획득 — 각성 팝업 표시 후 즉시 장착 제안 |
| 2~3개 | 카드 선택 UI — 가로 배치, 호버 설명 |
| 4+ | 3장 랜덤 추출 후 카드 선택 |

### 7.4 정비 탭 UX

상세 동선은 `docs/03b §4.1.3`. 본 문서는 데이터 측 책임만:

- 큐 항목은 순차 처리 — 한 번에 한 승무원
- 스킵·뒤로가기 없음
- 후보 4+ 랜덤 3장도 재뽑기 없음
- 강화 카드 선택 시 즉시 마크 스케일 행 갱신 + UI 강화 효과 표시
- 신규 카드 선택 시 즉시 장착 여부 다이얼로그 → 편성 탭 슬롯 UI 호출

### 7.5 가시성 — 블라인드 규율

- **공개**: 다음 마크까지 진행도 (예: `⊕ 대구경 14/15`)
- **공개**: 큐 대기 수량 (정비 탭 뱃지 `🔧 ●3`)
- **블라인드**: 다음 카드 내용 — 신규/강화 분기 자체도 카드 모달 진입 직전까지 비공개

---

## 8. 데이터 스키마

### 8.1 `CrewSkillSO` (Crux.Data)

```
id                  string
displayName         string
description         string
type                SkillType        // Passive / Triggered / Active
targetClass         CrewClass        // Commander / Gunner / Loader / Driver / GunnerMech
requires            SkillRequirement[]
requiredMarkLevel   Dictionary<AxisKey, int>

# Passive 전용
fireToggleKey       string?          // 사격 토글 식별자 (정밀 사격 등). null이면 일반 패시브

# Triggered 전용
trigger             TriggerType      // Move / Fire / Sight / Hit / SelfFire / LowMorale
baseProcRate        float            // 0.0~1.0, Mark 1 기준
markScaleTable      MarkScale[]      // 마크별 강화 행
moraleAffected      bool             // 사기 보정 공식 적용 여부 (보통 true)

# Active 전용
apCost              int
cooldown            int
targetingMode       TargetingMode    // None / Self / SingleEnemy / SingleAlly / Tile

effectKey           string           // effect 실행 훅
```

### 8.2 `MarkScale` — 발동형 강화 행

```
markLevel           int              // 1~5
procRate            float            // 이 마크에서의 발동률 (덮어쓰기)
rangeExtension      string?          // 범위 확장 키 (예: "FrontalOnly" → "FrontalAndSide")
effectMagnitude     float?           // 효과량 (도탄 +5% 등)
```

### 8.3 `SkillRequirement`

```
axis                RequirementAxis
operator            Op               // Any / All / None
values              string[]
```

### 8.4 `MarkLevelUpEvent` (Crux.Data.Crew)

```
crew                CrewMemberRuntime
axis                AxisKey
newMarkLevel        int              // 1~5
queuedAt            DateTime         // 디버그용
```

### 8.5 `CrewMemberRuntime` 스킬 필드 (`docs/04 §10.5` 발췌·확장)

```
ownedSkills         Dictionary<SkillId, OwnedSkillData>
                    # 발동형은 currentMarkInScale 추적 필요

equippedPassives    CrewSkillSO[2]
equippedTriggered   CrewSkillSO[N]   // 직책별 1 또는 2
equippedActive      CrewSkillSO[N]   // 직책별 0 또는 1 (전차장·수리병만)

cooldowns           Dictionary<SkillId, int>
disabledSlots       HashSet<SkillId> // 호환성 비활성
```

`OwnedSkillData`:
```
skill               CrewSkillSO
currentMarkInScale  int              // 발동형 강화 누적. 패시브·액티브는 항상 1
acquiredAt          DateTime
```

### 8.6 `AwakeningQueue` (Crux.Core.Maintenance)

```
List<MarkLevelUpEvent> events
int Count → events.Count
void Add(MarkLevelUpEvent)
MarkLevelUpEvent? PeekNext()
void PopHead()
event Action<int> OnCountChanged          // 03b 뱃지 갱신
```

---

## 9. 호환성 UI 상태 머신 (격납고)

§3.2 3상태(미습득/비활성/활성) 전이를 격납고 모듈에서 어떻게 갱신하는지.

### 9.1 갱신 트리거

| 이벤트 | 영향 범위 |
|---|---|
| 승무원 → 전차 배치 | 해당 승무원 보유 스킬 전체 재검증 |
| 전차 → 파츠 교체 | 해당 전차 탑승 승무원 전원 보유 스킬 재검증 |
| 마크업 카드 선택 | 해당 카드 1개 상태 갱신 (미습득→활성/비활성) |
| 슬롯 장착 변경 | 슬롯 표시만 갱신, 보유 상태는 불변 |

### 9.2 이벤트 버스 연결

격납고 10b 이벤트 버스(`HangarBus`)에 다음 이벤트가 흘러들어옴:
- `LoadoutEffectsRecalculated` — 파츠 교체 후 LoadoutEffect 재계산 완료 → 스킬 호환성도 함께 갱신
- `CrewAssigned` — 승무원 전차 배치 완료 → 해당 승무원 호환성 갱신
- `SkillAcquired` — 신규 카드 선택 후 보유 스킬 추가
- `SkillReinforced` — 강화 카드 선택 후 마크 스케일 갱신

### 9.3 UI 상태 표시

캐릭터 상세 패널 — 보유 스킬 그리드:
```
[정밀 조준]  [침착함]   [속사]      [직감]
 풀컬러      풀컬러      어두움+빨강  자물쇠
 (활성)     (활성)     (비활성)    (미습득)
```

격납고 편성 탭 슬롯 — 장착 4슬롯:
```
[Passive 1]  [Passive 2]  [Triggered 1]  [Active]
 정밀 조준   (빈 슬롯)    직감          격려
 활성        —            비활성        활성
```

---

## 10. 구현 책임 분리 (Dev 가이드)

> 본 절은 `Crux-dev` 워크트리 구현자 참조용. 네임스페이스·클래스명은 제안.

### 10.1 신규/이관 타입

| 타입 | 위치 | 비고 |
|---|---|---|
| `CrewSkillSO` | `Crux.Data.Crew` | v2 스키마로 갱신 — `type` 3계층, `markScaleTable` 신규 |
| `SkillRequirement` | `Crux.Data.Crew` | 변경 없음 |
| `MarkScale` | `Crux.Data.Crew` | 신규 |
| `OwnedSkillData` | `Crux.Data.Crew` | 신규 — 발동형 currentMarkInScale 추적 |
| `SkillPoolQuery` | `Crux.Core.Crew` | v2 신규/강화 분기 로직 |
| `AwakeningQueue` | `Crux.Core.Maintenance` | 변경 없음 |
| `SkillEffectRegistry` | `Crux.Combat.Skills` | `effectKey` → 실행 함수 매핑 |
| `MoraleModifier` | `Crux.Combat.Skills` | §6.1 사기 보정 단일 소스 |
| `SkillCompatibilityChecker` | `Crux.Core.Crew` | 호환성 3상태 판정 단일 소스 |
| `FireToggleRegistry` | `Crux.UI.FireTargeting` | 사격 모달이 활성 패시브의 fireToggleKey 수집 |

### 10.2 통합 지점

- **`BattleController`** — 전투 시작 시 활성 슬롯을 `SkillEffectRegistry`에 등록. 종료 시 `MaintenanceTicker.OnBattleEnd` 호출
- **`FireExecutor` / `ReactionFireSequence`** — 발동형 스킬 트리거. 사기 보정 `MoraleModifier` 경유
- **`FireTargetingPanel`** — 패시브의 `fireToggleKey` 수집해서 모달에 토글 버튼 추가
- **`HangarController` (편성 탭)** — 슬롯 장착·교체 + 호환성 3상태 색상
- **`HangarController` (정비 탭)** — 큐 처리 모달 (신규/강화 카드 분기)
- **`HangarBus`** — `LoadoutEffectsRecalculated` 핸들러에서 호환성 재검사

### 10.3 테스트 가능성

- `SkillPoolQuery.Query` — 순수 함수, 단위 테스트 권장
- `MoraleModifier.Apply(baseRate, morale)` — 경계값 테스트 (0, 19, 20, 50, 100) 필수
- `SkillCompatibilityChecker.Check(skill, tank)` — 요구조건 매트릭스 단위 테스트
- 발동형 시퀀스는 `ReactionFireSequence` / `FireExecutor` 통합 테스트
- 마크 스케일은 `MarkScale` 룩업 단위 테스트

---

## 11. 튜닝 훅

| 항목 | 초안 | 튜닝 사유 |
|---|---|---|
| 사기 보정 공식 `0.5 + M/200` | 선형 | 패닉 절벽 체감 |
| 패닉 임계 Morale < 20 | 20 | 사기충천·정상 구간 비례 |
| 발동형 기본 발동률 (15~30%) | 표 참조 | 반격·트리거 빈도 측정 후 |
| 마크 스케일 증가폭 (+5~+15%p) | 표 참조 | 마크업 체감 |
| 후보 카드 4+ 추출 상한 3장 | 3 | 빈도 측정 후 |
| 강화 카드 보장 비율 | 발동형 1+ 보유 시 강화 1장 보장 | 풀 다양성 vs 강화 만족감 |
| 정밀 사격 토글 명중 페널티 | −15% | 능동 모듈 타격 가치 vs 안정 사격 |
| 슬롯 4개 (P2+T2 또는 P2+T1+A1) | 4 | 직책당 부담·다양성 |
| 액티브 보유 직책 (전차장·수리병) | 2 | 액티브 슬롯 인플레 방지 |
| 마크 5단계 임계값 (`docs/04 §3.3`) | 표 참조 | 마크 5 도달 체감 |

---

## 12. 오픈 이슈

1. **사기 필드 접근 경로** — 승무원 스킬이 자기 전차의 `morale`을 참조해야 함. `CrewMemberRuntime`에서 `tank` 역참조 신설 또는 발동 시점 컨텍스트 주입. 결정 필요
2. **확률 발동 UI 표시** — "기본 25% × 마크 1.5 × 사기 0.9 = 33.75%" 같은 실시간 발동률 노출 여부. 노출 시 디버그 가치 vs 비노출 시 긴장감
3. **발동형 발동 우선순위** — §6.3 직책 우선순위가 게임 플레이에 직관적인지 검증 필요. 대안: 발동률 가중 랜덤 1개
4. **정밀 사격 토글 vs 약점 저격 발동형 중복** — 정밀 사격(Passive 토글, 명중 −15% 확정 모듈) vs 약점 저격(Triggered 30% 모듈 변환). 두 스킬 공존 시 시너지·중복 검증
5. **수리병 액티브 풀 규모** — 현재 3종(응급 수리·화재 진압·구급 처치). 슬롯 1개에 비해 선택지 부족할 수 있음. Phase 2 확장 후보
6. **각성 카드 후보 풀 규모** — 직책당 10~15개 목표. 현재 표본 5~10개. Phase 2 확장 시 풀 채우기
7. **강화 카드 vs 신규 카드 우선순위** — §7.2 비율 가이드는 초안. 플레이테스트 후 강화/신규 빈도 재튜닝
8. **호환성 비활성 시 자동 비활성화 정책** — "장착은 유지, 효과 0" vs "강제 해제 + 보유로 환원". v2는 전자 유지, UX 검증 필요
9. **T 타임 발동 시점** — "다음 피격 1회"가 발동 직후 첫 피격인지, 발동 다음 턴 첫 피격인지 명확화. v2 초안: 발동 직후부터 다음 턴 종료까지 첫 피격
10. **마크 스케일 초과 처리** — 발동형이 Mark 5 도달 후 추가 마크업 카드 트리거 시 어떻게? → "강화 카드 후보 0 → 신규 카드만 후보" 자연 처리

---

## 13. 범위 외

- **스킬 트리·연계** — 단일 카드 모델만 사용
- **상점·전리품 스킬 획득** — Phase 2 검토
- **스킬 책 (장비형 스킬)** — 캐릭터 귀속 원칙(§1.1) 위반 — 신중 검토
- **PvP·멀티플레이어 밸런싱** — 싱글 전제
- **액티브 직책 확장** — 현 정책은 전차장·수리병 2직책. 포수·조종수 액티브는 발동형 흡수가 우선

---

## 14. 변경 이력

| 날짜 | 버전 | 변경 |
|---|---|---|
| 2026-04-22 | v1 | 초판. `docs/04 §4·§5·§6.2·§6.3` + `docs/06 §3.7` + `docs/03b §4.1` 흩어진 정의를 캐릭터 하위 권위로 통합. 타입 3종(Passive/ActiveInstant/ActiveReactive) + DUAL + 반격 풀 6종 + 사기 보정 + 각성 파이프라인 |
| 2026-04-22 | v2 | **분류 체계 재설계** — Passive / Triggered (발동형) / Active (명령 커맨더) 3계층. ActiveInstant·ActiveReactive를 발동형으로 통합, 능동 명령형은 액티브로 분리(전차장·수리병만). 마크 스케일링 직책별 분기 (발동형=강화 스택, 패시브·액티브=카드 획득). 정밀 사격 패시브+사격 토글 모델 도입. T 타임 액티브 신규. 호환성 3상태(미습득/비활성/활성) UI 상태 머신 정의. v1 반격 풀 6종은 §5 직책별 풀의 발동형 항목으로 통합. v1 DUAL 시스템은 폐기 (마크 스케일링이 강화 축을 흡수). 사유: ActiveInstant·ActiveReactive 경계 모호, 마크가 "신규 획득"만이라 깊이 부족, 액티브 슬롯 인플레 우려 |
