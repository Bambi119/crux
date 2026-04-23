using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Crux.Core;
using Crux.Data;
using Crux.Grid;
using Crux.Combat;

namespace Crux.Unit
{
    /// <summary>그리드 기반 전차 유닛 — 턴제 전투용</summary>
    public class GridTankUnit : MonoBehaviour, IDamageable
    {
        [Header("데이터")]
        public TankDataSO tankData;
        public AmmoDataSO currentAmmo;
        public PlayerSide side;

        // 상태
        private Vector2Int gridPosition;
        private float currentHP;
        private int currentAP;
        private float hullAngle;
        private bool isMoving;

        // 상태이상
        private bool isOnFire;
        private int fireTurnsLeft = 0;
        private int consecutiveMisses = 0;
        private int remainingSmokeCharges;

        // 오버워치 (반응 사격) — 활성화 시 FireCost 선지불, 이동 적에게 즉시 반격 1회
        private bool isOverwatching;

        // 반격 상태 관리 (Task #15·#16·#17)
        private bool isCounterImmune;           // true면 반격 불가 (오버워치 중인 유닛 등)
        private bool hasCounteredThisExchange;  // 이번 교환에서 이미 반격 실행했으면 true (연쇄 반격 차단)
        private bool counterConfirmed = true;   // Task #17: 플레이어 반격 확정 (기본 true, 프롬프트에서 설정)

        // 탄약 잔량
        private int mainGunAmmoCount;
        private int mgAmmoLoaded;
        private int mgAmmoTotal;

        // 모듈 매니저
        private ModuleManager moduleManager = new();

        // 스프라이트 기본 방향: 항상 →(90°)
        private const float spriteBaseAngle = 90f;

        // 컴포넌트
        private SpriteRenderer hullRenderer;
        private Color baseHullColor = Color.white; // Initialize 시점의 원본 색 (HP별 암전 기준)
        private GridManager grid;
        private TankCrew crew;  // 승무원 시스템 캐시

        // ===== 승무원 바인딩 =====
        /// <summary>승무원 컴포넌트 연결 (BattleController · 통합 테스트가 호출)</summary>
        public void BindCrew(TankCrew tankCrew)
        {
            crew = tankCrew;
        }

        // ===== 회전 유틸 =====
        private Quaternion CompassToRotation(float compass)
        {
            return Quaternion.Euler(0, 0, AngleUtil.ToUnity(compass) - AngleUtil.ToUnity(spriteBaseAngle));
        }

        // ===== 프로퍼티 =====
        public Vector2Int GridPosition => gridPosition;
        public float CurrentHP => currentHP;
        public int CurrentAP => currentAP;
        public int MaxAP => tankData != null ? tankData.maxAP : 6;
        public bool IsDestroyed => currentHP <= 0;
        public bool IsMoving => isMoving;
        public float HullAngle => hullAngle;
        public TankDataSO Data => tankData;
        public ModuleManager Modules => moduleManager;
        public bool IsOnFire => isOnFire;
        public int FireTurnsLeft => fireTurnsLeft;
        public int ConsecutiveMisses => consecutiveMisses;
        public int RemainingSmokeCharges => remainingSmokeCharges;
        public bool IsOverwatching => isOverwatching;
        public int MainGunAmmoCount => mainGunAmmoCount;
        public int MaxMainGunAmmo => tankData != null ? tankData.maxMainGunAmmo : 0;
        public int MGAmmoLoaded => mgAmmoLoaded;
        public int MGAmmoTotal => mgAmmoTotal;
        public TankCrew Crew => crew;
        public bool IsCounterImmune => isCounterImmune;
        public bool HasCounteredThisExchange => hasCounteredThisExchange;
        public bool CounterConfirmed => counterConfirmed;

        public System.Action<GridTankUnit> OnDeath;
        public System.Action<GridTankUnit> OnFireKilled; // 화재 누적 사망 전용
        public System.Action OnAPChanged;
        public System.Action OnMoveComplete;
        /// <summary>한 셀 이동 완료 시점에 발행 — 오버워치 트리거 체크용</summary>
        public System.Action<GridTankUnit, Vector2Int> OnMoveStepComplete;
        /// <summary>피격 후 데미지 적용 완료 시점 — 사기 이벤트 라우팅용</summary>
        public System.Action<GridTankUnit, Combat.DamageInfo, Unit.DamageOutcome> OnDamageApplied;

        /// <summary>현재 상태를 스냅샷으로 저장</summary>
        public UnitSaveData SaveState()
        {
            return new UnitSaveData
            {
                gridPosition = gridPosition,
                currentHP = currentHP,
                currentAP = currentAP,
                hullAngle = hullAngle,
                isDestroyed = IsDestroyed,
                moduleSaves = moduleManager.SaveAll(),
                isOnFire = isOnFire,
                fireTurnsLeft = fireTurnsLeft,
                consecutiveMisses = consecutiveMisses,
                remainingSmokeCharges = remainingSmokeCharges,
                mainGunAmmoCount = mainGunAmmoCount,
                mgAmmoLoaded = mgAmmoLoaded,
                mgAmmoTotal = mgAmmoTotal,
                isOverwatching = isOverwatching,
                isCounterImmune = isCounterImmune,
                hasCounteredThisExchange = hasCounteredThisExchange,
                counterConfirmed = counterConfirmed
            };
        }

        /// <summary>저장된 상태로 복원</summary>
        public void RestoreState(GridManager grid, UnitSaveData state)
        {
            this.grid = grid; // ClearCellOccupancy 등에서 사용되도록 보장

            // 승무원 컴포넌트 캐싱
            TryGetComponent(out TankCrew existingCrew);
            crew = existingCrew;

            var oldCell = grid.GetCell(gridPosition);
            if (oldCell != null && oldCell.Occupant == gameObject)
                oldCell.Occupant = null;

            gridPosition = state.gridPosition;
            currentHP = state.currentHP;
            currentAP = state.currentAP;
            hullAngle = state.hullAngle;

            // 격파 유닛은 셀을 점유해서는 안 됨 — 점유 시 ShowFireRange/TrySelectTarget이
            // 죽은 유닛을 살아있는 것으로 오인. SetActive(false) 만으로는 부족.
            if (!state.isDestroyed)
            {
                var newCell = grid.GetCell(gridPosition);
                if (newCell != null) newCell.Occupant = gameObject;
            }

            transform.position = grid.GridToWorld(gridPosition);
            transform.rotation = CompassToRotation(hullAngle);

            // 모듈 상태 복원
            moduleManager.RestoreAll(state.moduleSaves);
            isOnFire = state.isOnFire;
            fireTurnsLeft = state.fireTurnsLeft;
            consecutiveMisses = state.consecutiveMisses;
            remainingSmokeCharges = state.remainingSmokeCharges;
            mainGunAmmoCount = state.mainGunAmmoCount;
            mgAmmoLoaded = state.mgAmmoLoaded;
            mgAmmoTotal = state.mgAmmoTotal;
            isOverwatching = state.isOverwatching;
            isCounterImmune = state.isCounterImmune;
            hasCounteredThisExchange = state.hasCounteredThisExchange;
            counterConfirmed = state.counterConfirmed;

            UpdateVisual();

            if (state.isDestroyed)
            {
                currentHP = 0;
                gameObject.SetActive(false);
            }
        }

        public void Initialize(GridManager grid, Vector2Int pos, TankDataSO data, AmmoDataSO ammo,
                               PlayerSide side)
        {
            this.grid = grid;
            this.tankData = data;
            this.currentAmmo = ammo;
            this.side = side;
            gridPosition = pos;
            currentHP = data.maxHP;
            currentAP = data.maxAP;
            hullAngle = side == PlayerSide.Player ? 0f : 180f;
            remainingSmokeCharges = data.smokeCharges;
            mainGunAmmoCount = data.maxMainGunAmmo;
            mgAmmoLoaded = data.mgLoadedAmmo;
            mgAmmoTotal = data.maxMGAmmo;

            // 모듈 초기화 — moduleHP가 0이면 기본값 사용
            if (data.moduleHP.engine <= 0)
                data.moduleHP = ModuleHPProfile.Default;
            moduleManager.Initialize(data);

            transform.position = grid.GridToWorld(pos);
            transform.rotation = CompassToRotation(hullAngle);

            var cell = grid.GetCell(pos);
            if (cell != null) cell.Occupant = gameObject;

            hullRenderer = GetComponentInChildren<SpriteRenderer>();
            if (hullRenderer != null) baseHullColor = hullRenderer.color;
            UpdateVisual();

            // 승무원 컴포넌트 캐싱 (BattleController가 이후 부착할 예정)
            TryGetComponent(out TankCrew existingCrew);
            crew = existingCrew;
        }

        // ===== 턴 관리 =====

        public void OnTurnStart()
        {
            consecutiveMisses = 0;
            currentAP = tankData != null ? tankData.maxAP : 6;

            // 오버워치는 다음 턴 시작 시 해제 — 트리거되지 않았다면 사전 지불 AP는 손실
            isOverwatching = false;

            // 반격 상태 초기화 — 새 턴마다 리셋 (해제는 교환 단위로 진행)
            hasCounteredThisExchange = false;
            isCounterImmune = false;
            counterConfirmed = true;  // Task #17: 턴 시작 시 반격 확정 기본값으로 리셋

            // 화재 처리 — TickFire가 사망 처리까지 담당
            TickFire();

            OnAPChanged?.Invoke();
        }

        /// <summary>이동 가능 여부 (AP + 모듈 상태)</summary>
        public bool CanMove()
        {
            if (isMoving) return false;
            if (!moduleManager.CanMove()) return false;
            int cost = GameConstants.MoveCostPerCell + moduleManager.GetMoveAPPenalty();
            return currentAP >= cost;
        }

        /// <summary>주포 사격 가능 여부 (AP + 모듈 상태)</summary>
        public bool CanFire()
        {
            if (isMoving) return false;
            if (!moduleManager.CanFireMainGun()) return false;
            return currentAP >= GetFireCost();
        }

        /// <summary>기총 사격 가능 여부</summary>
        public bool CanFireMG()
        {
            if (isMoving) return false;
            return moduleManager.CanFireMG();
        }

        /// <summary>차체 회전 가능 여부</summary>
        public bool CanRotate() => moduleManager.CanRotate();

        /// <summary>실효 이동 AP 비용 (1셀당) — 엔진 출력/궤도 기동성 포함</summary>
        public int GetMoveCostPerCell()
        {
            int cost = GameConstants.MoveCostPerCell;
            cost += moduleManager.GetMoveAPPenalty();
            if (isOnFire) cost += 1;

            // 편성 엔진 출력 여유 → 이동 AP 보정 (partsEnginePowerOutput==0 이면 미적용)
            if (tankData != null && tankData.partsEnginePowerOutput > 0f)
            {
                float delta = tankData.partsEnginePowerOutput - tankData.powerRequirement;
                if (delta < 0f) cost += 1;          // 출력 부족: +1
                else if (delta >= 50f) cost -= 1;   // 여유 많음: -1
            }

            // 편성 궤도 기동성 보너스 (mob+1 = cost-1)
            if (tankData != null)
                cost -= tankData.partsTrackMobilityBonus;

            // 과적 패널티 — 파츠 중량 초과 시 이동 AP +1
            if (tankData != null && tankData.partsIsOverweight) cost += 1;

            return Mathf.Max(1, cost);  // 최소 1 보장
        }

        /// <summary>실효 사격 AP 비용 — tankData.fireCost 기준</summary>
        public int GetFireCost()
        {
            int baseCost = tankData != null ? tankData.fireCost : 3;
            return baseCost + moduleManager.GetFireAPPenalty() + (isOnFire ? 1 : 0);
        }

        // ===== 오버워치 (반응 사격) =====

        /// <summary>오버워치 설정 가능 여부 — 주포 발사 가능 + 잔탄 + AP 충분 + 아직 설정 안됨</summary>
        public bool CanActivateOverwatch()
        {
            if (isMoving || isOverwatching) return false;
            if (!moduleManager.CanFireMainGun()) return false;
            if (mainGunAmmoCount <= 0) return false;
            int owCost = GameConstants.GetOverwatchCost(GetFireCost());
            return currentAP >= owCost;
        }

        /// <summary>오버워치 설정 — AP 선지불, 트리거 시 반격 1회</summary>
        public bool ActivateOverwatch()
        {
            if (!CanActivateOverwatch()) return false;
            int owCost = GameConstants.GetOverwatchCost(GetFireCost());
            currentAP -= owCost;
            isOverwatching = true;
            OnAPChanged?.Invoke();
            Debug.Log($"[CRUX] {tankData?.tankName} 오버워치 설정 (AP -{owCost})");
            return true;
        }

        /// <summary>오버워치 반격 소진 — BattleController 트리거 처리 후 호출</summary>
        public void ConsumeOverwatchShot()
        {
            isOverwatching = false;
        }

        // ===== 반격 상태 관리 (Task #15-#16-#17) =====

        /// <summary>반격 면역 설정 — 오버워치 또는 기타 조건으로 인해 반격 불가</summary>
        public void SetCounterImmune(bool value)
        {
            isCounterImmune = value;
        }

        /// <summary>이번 교환에서 반격 실행 표시 — 연쇄 반격 차단용</summary>
        public void SetCountered(bool value)
        {
            hasCounteredThisExchange = value;
        }

        /// <summary>Task #17: 플레이어 반격 확정 여부 — 프롬프트에서 Y/N 선택 후 설정</summary>
        public void SetCounterConfirmed(bool value)
        {
            counterConfirmed = value;
        }

        // ===== 이동 (애니메이션) =====

        public bool MoveTo(Vector2Int target)
        {
            if (isMoving) return false;

            var path = grid.FindPath(gridPosition, target);
            if (path == null || path.Count <= 1) return false;

            int steps = path.Count - 1;
            int cost = steps * GetMoveCostPerCell();
            if (cost > currentAP) return false;

            StartCoroutine(MoveAlongPath(path, cost));
            return true;
        }

        private IEnumerator MoveAlongPath(List<Vector2Int> path, int totalCost)
        {
            isMoving = true;

            var oldCell = grid.GetCell(gridPosition);
            if (oldCell != null) oldCell.Occupant = null;

            float moveSpeed = tankData != null ? tankData.moveSpeed : 3f;
            float rotSpeed = moduleManager.CanRotate() ? 180f : 0f;

            int lastStepIdx = 0;
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 from = grid.GridToWorld(path[i - 1]);
                Vector3 to = grid.GridToWorld(path[i]);

                // 차체 회전 (회전 가능할 때만)
                if (rotSpeed > 0)
                {
                    Vector3 dir = to - from;
                    float targetAngle = AngleUtil.FromDir(new Vector2(dir.x, dir.y));

                    while (Mathf.Abs(Mathf.DeltaAngle(hullAngle, targetAngle)) > 1f)
                    {
                        hullAngle = Mathf.MoveTowardsAngle(hullAngle, targetAngle, rotSpeed * Time.deltaTime);
                        transform.rotation = CompassToRotation(hullAngle);
                        yield return null;
                    }
                    hullAngle = targetAngle;
                    transform.rotation = CompassToRotation(hullAngle);
                }

                // 전진
                float distance = Vector3.Distance(from, to);
                float duration = distance / moveSpeed;
                float t = 0;

                while (t < 1f)
                {
                    t += Time.deltaTime / duration;
                    transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(t));
                    yield return null;
                }
                transform.position = to;

                // 스텝 완료 — 논리 좌표를 즉시 반영 (오버워치 트리거가 현재 셀을 정확히 알도록)
                gridPosition = path[i];
                lastStepIdx = i;

                // 구독자(BattleController)가 오버워치 체크 → 반격 → 이 유닛 사망 가능
                OnMoveStepComplete?.Invoke(this, gridPosition);
                if (IsDestroyed) break;

                // 반응 사격 연출이 진행 중이면 종료까지 대기 (카메라 팬/트레이서 시간 확보)
                while (Crux.Combat.ReactionFireSequence.IsPlaying)
                    yield return null;
                if (IsDestroyed) break;
            }

            var newCell = grid.GetCell(gridPosition);
            if (newCell != null && !IsDestroyed) newCell.Occupant = gameObject;

            currentAP -= totalCost;
            OnAPChanged?.Invoke();
            isMoving = false;
            OnMoveComplete?.Invoke();
        }

        public void SetFacing(float newAngle)
        {
            hullAngle = newAngle;
            transform.rotation = CompassToRotation(hullAngle);
        }

        public void FaceToward(Vector2Int targetPos)
        {
            Vector2 dir = new Vector2(targetPos.x - gridPosition.x, targetPos.y - gridPosition.y);
            if (dir.sqrMagnitude > 0.01f)
                SetFacing(AngleUtil.FromDir(dir));
        }

        public bool MoveToWithFacing(Vector2Int target, float facingAngle)
        {
            if (isMoving) return false;

            var path = grid.FindPath(gridPosition, target);
            if (path == null || path.Count <= 1) return false;

            int steps = path.Count - 1;
            int cost = steps * GetMoveCostPerCell();
            if (cost > currentAP) return false;

            StartCoroutine(MoveAlongPathWithFacing(path, cost, facingAngle));
            return true;
        }

        private IEnumerator MoveAlongPathWithFacing(List<Vector2Int> path, int totalCost, float finalAngle)
        {
            yield return MoveAlongPath(path, totalCost);

            if (moduleManager.CanRotate())
            {
                isMoving = true;
                float rotSpeed = 180f;
                while (Mathf.Abs(Mathf.DeltaAngle(hullAngle, finalAngle)) > 1f)
                {
                    hullAngle = Mathf.MoveTowardsAngle(hullAngle, finalAngle, rotSpeed * Time.deltaTime);
                    transform.rotation = CompassToRotation(hullAngle);
                    yield return null;
                }
                hullAngle = finalAngle;
                transform.rotation = CompassToRotation(hullAngle);
                isMoving = false;
            }
        }

        /// <summary>사격 — AP 소모 (패널티 포함)</summary>
        public void ConsumeFireAP()
        {
            currentAP -= GetFireCost();
            OnAPChanged?.Invoke();
        }

        /// <summary>AP 감소 (사기 페널티 등). OnAPChanged 발행.</summary>
        public void DeductAP(int amount)
        {
            currentAP = Mathf.Max(0, currentAP - amount);
            OnAPChanged?.Invoke();
        }

        /// <summary>주포 1발 소모 — 잔탄 없으면 false</summary>
        public bool ConsumeMainGunRound()
        {
            if (mainGunAmmoCount <= 0) return false;
            mainGunAmmoCount--;
            return true;
        }

        /// <summary>기관총 burstCount 발 소모 — 장전량 우선, 부족분은 전체에서 충전</summary>
        public void ConsumeMGBurst(int burst)
        {
            int spent = Mathf.Min(burst, mgAmmoLoaded + Mathf.Max(0, mgAmmoTotal - mgAmmoLoaded));
            // 장전량에서 우선 차감
            int fromLoaded = Mathf.Min(spent, mgAmmoLoaded);
            mgAmmoLoaded -= fromLoaded;
            mgAmmoTotal -= spent;
            if (mgAmmoTotal < 0) mgAmmoTotal = 0;
            // 장전량 0이면 남은 총량에서 재장전 (간단 구현)
            if (mgAmmoLoaded <= 0 && mgAmmoTotal > 0 && tankData != null)
            {
                mgAmmoLoaded = Mathf.Min(tankData.mgLoadedAmmo, mgAmmoTotal);
            }
        }

        // ===== 화재/연막 =====

        /// <summary>화재 발생 — fireTurnsLeft를 최대값으로 설정</summary>
        public void SetOnFire()
        {
            if (isOnFire) return;
            isOnFire = true;
            fireTurnsLeft = FireConstants.MaxFireTurns;
            Debug.Log($"[CRUX] {tankData?.tankName} 화재 발생!");
        }

        /// <summary>수동 소화 (AP 2 소모)</summary>
        public bool TryExtinguish()
        {
            if (!isOnFire || currentAP < 2) return false;
            currentAP -= 2;
            isOnFire = false;
            OnAPChanged?.Invoke();
            Debug.Log($"[CRUX] {tankData?.tankName} 수동 소화! AP -2");
            return true;
        }

        /// <summary>연막 사용 가능 여부</summary>
        public bool CanUseSmoke() => remainingSmokeCharges > 0 && currentAP >= 1 && !isMoving;

        /// <summary>연막 사용 (AP 1, 수량 -1)</summary>
        public bool UseSmoke()
        {
            if (!CanUseSmoke()) return false;
            remainingSmokeCharges--;
            currentAP -= 1;
            // 현재 셀에 연막 배치 (3턴 유지)
            var cell = grid?.GetCell(gridPosition);
            if (cell != null)
            {
                cell.SmokeTurnsLeft = 3;
                // 연막 시각 효과 스폰
                grid.GetVisualizer()?.ShowSmoke(gridPosition);
            }
            OnAPChanged?.Invoke();
            Debug.Log($"[CRUX] {tankData?.tankName} 연막 발사! 잔여: {remainingSmokeCharges}");
            return true;
        }

        // ===== 데미지 =====

        /// <summary>즉시 데미지 — 롤 포함 (전략맵에서 직접 호출용, 기존 호환)</summary>
        public void TakeDamage(DamageInfo info)
        {
            var outcome = PreRollDamage(info);
            ApplyPrerolledDamage(info, outcome);
        }

        /// <summary>데미지 + 모듈 + 화재 + 격파 + 승무원 부상 사전 롤 — 실제 적용 없음</summary>
        public DamageOutcome PreRollDamage(DamageInfo info)
        {
            var outcome = new DamageOutcome();

            // 모듈 피격 확률: 관통 40%, 일반 Hit 15%
            float moduleHitChance = info.outcome == ShotOutcome.Penetration ? 0.4f : 0.15f;
            if (info.outcome >= ShotOutcome.Hit && Random.value < moduleHitChance)
            {
                outcome = moduleManager.RollModuleHit(info.damage * 0.5f, info.hitZone);
            }

            // 유폭이면 격파 보장
            if (outcome.ammoExploded)
            {
                outcome.killed = true;
                return outcome;
            }

            // 화재 발생 확률 (관통 시)
            if (info.outcome == ShotOutcome.Penetration && !isOnFire)
            {
                float fireChance = 0.25f;
                var ammo = moduleManager.Get(ModuleType.AmmoRack);
                if (ammo.state >= ModuleState.Damaged) fireChance += 0.20f;
                // 탄약고 피격 사전 롤이 있으면 예상 상태로 판정
                if (outcome.moduleHit && outcome.damagedModule == ModuleType.AmmoRack
                    && outcome.newState >= ModuleState.Damaged) fireChance += 0.20f;
                var engine = moduleManager.Get(ModuleType.Engine);
                if (engine.state >= ModuleState.Damaged) fireChance += 0.15f;

                if (Random.value < fireChance)
                    outcome.fireStarted = true;
            }

            // 승무원 부상 롤 — 모듈 피격 시 관련 승무원에게 부상 기회
            if (outcome.moduleHit && crew != null)
            {
                RollCrewInjury(outcome.damagedModule);
            }

            // 화재 발생 시도 (관통 + 모듈 피격 시)
            if (info.outcome == ShotOutcome.Penetration && outcome.moduleHit)
            {
                TryStartFire(outcome.damagedModule);
            }

            // 체력 0 예측 (피해만으로 격파)
            if (currentHP - info.damage <= 0)
                outcome.killed = true;

            return outcome;
        }

        /// <summary>모듈 피격 시 관련 승무원에게 부상 확률 롤</summary>
        private void RollCrewInjury(ModuleType module)
        {
            if (crew == null) return;

            CrewMemberRuntime target = null;
            float injuryChance = 0f;

            switch (module)
            {
                case ModuleType.Engine:
                    // 엔진 피격 → Driver 10% 부상 기회
                    target = crew.driver;
                    injuryChance = 0.10f;
                    break;
                case ModuleType.Barrel:
                case ModuleType.TurretRing:
                    // 포신/포탑 피격 → Gunner 15% 부상 기회
                    target = crew.gunner;
                    injuryChance = 0.15f;
                    break;
                case ModuleType.AmmoRack:
                    // 탄약고 피격 → Loader 20% 부상 기회
                    target = crew.loader;
                    injuryChance = 0.20f;
                    break;
                case ModuleType.CaterpillarLeft:
                case ModuleType.CaterpillarRight:
                    // 궤도는 특정 승무원 영향 없음 (분산 처리)
                    break;
                default:
                    break;
            }

            if (target != null && Random.value < injuryChance)
            {
                // 이미 부상 있으면 격상, 없으면 경상
                if (target.injuryState == InjuryLevel.None)
                {
                    target.injuryState = InjuryLevel.Minor;
                    Debug.Log($"[CRUX] {target.DisplayName}(직책: {target.data?.klass}) 경상 부상!");
                }
                else if (target.injuryState == InjuryLevel.Minor)
                {
                    target.injuryState = InjuryLevel.Severe;
                    Debug.Log($"[CRUX] {target.DisplayName}(직책: {target.data?.klass}) 중상으로 악화!");
                }
                // Fatal은 이 경로로 발생 금지 (게임 밸런스)
            }
        }

        /// <summary>모듈 피격 시 화재 발생 확률 롤 — FireConstants 기준</summary>
        public void TryStartFire(ModuleType module)
        {
            if (isOnFire) return;

            float baseChance = module switch
            {
                ModuleType.AmmoRack => FireConstants.AmmoRackFireChance,
                ModuleType.Engine   => FireConstants.EngineFireChance,
                _                   => FireConstants.OtherModuleFireChance,
            };

            float resistance = (tankData?.fireResistancePercent ?? 0f) / 100f;
            if (Random.value < Mathf.Max(0f, baseChance - resistance))
            {
                isOnFire = true;
                fireTurnsLeft = FireConstants.MaxFireTurns;
                Debug.Log($"[CRUX] {tankData?.tankName} 화재 시작! 모듈={module} 남은턴={fireTurnsLeft}");
            }
        }

        /// <summary>화재 턴 처리 — 데미지 + 자동 소화 + 만료 판정. OnTurnStart에서 호출 필수</summary>
        public void TickFire()
        {
            if (!isOnFire) return;

            float dmg = (tankData != null ? tankData.maxHP : 100) * (FireConstants.FireDamagePerTurnPercent / 100f);
            currentHP = Mathf.Max(0, currentHP - (int)dmg);
            Debug.Log($"[CRUX] {tankData?.tankName} 화재 데미지 {(int)dmg} → HP {currentHP}/{tankData?.maxHP}");

            if (currentHP <= 0)
            {
                currentHP = 0;
                ClearCellOccupancy();
                OnFireKilled?.Invoke(this);
                OnDeath?.Invoke(this);
                UpdateVisual();
                return;
            }

            if (Random.value < FireConstants.AutoExtinguishChance)
            {
                ExtinguishFire();
                return;
            }

            fireTurnsLeft--;
            if (fireTurnsLeft <= 0)
                ExtinguishFire();
        }

        /// <summary>화재 소화 — 턴 카운트 리셋</summary>
        public void ExtinguishFire()
        {
            isOnFire = false;
            fireTurnsLeft = 0;
            Debug.Log($"[CRUX] {tankData?.tankName} 화재 소화");
        }

        /// <summary>연속 실패 기록 (Pseudo-RNG 보호용)</summary>
        public void RecordMiss() => consecutiveMisses++;

        /// <summary>연속 실패 카운터 리셋</summary>
        public void ResetMisses() => consecutiveMisses = 0;

        /// <summary>사전 롤된 데미지 적용 — 결정론적</summary>
        public void ApplyPrerolledDamage(DamageInfo info, DamageOutcome outcome)
        {
            currentHP -= info.damage;

            if (outcome.moduleHit)
            {
                moduleManager.ApplyModuleHit(outcome,
                    tankData != null ? tankData.tankName : gameObject.name);
            }

            if (outcome.fireStarted && !isOnFire)
                SetOnFire();

            if (outcome.ammoExploded || outcome.killed || currentHP <= 0)
            {
                currentHP = 0;
                ClearCellOccupancy();
                OnDeath?.Invoke(this);
            }

            UpdateVisual();

            // 사기 이벤트 라우팅
            OnDamageApplied?.Invoke(this, info, outcome);
        }

        /// <summary>현재 점유 셀에서 자기 자신을 제거 — 사망 시 호출 필수</summary>
        /// <remarks>이 처리가 누락되면 죽은 유닛이 셀을 영구 점유해
        /// ShowFireRange/AI/pathfinding이 죽은 유닛을 살아있는 것으로 오인함.</remarks>
        private void ClearCellOccupancy()
        {
            if (grid == null) return;
            var cell = grid.GetCell(gridPosition);
            if (cell != null && cell.Occupant == gameObject)
                cell.Occupant = null;
        }

        // ===== 회전 & 무기 가용성 (Phase 2 UI 백엔드) =====

        /// <summary>제자리 회전 — 60° 당 1 AP 소모. 모듈 손상 시 회전 불가.</summary>
        /// <remarks>deltaDegrees는 음수(시계방향) 또는 양수(반시계방향) 각도 변위.
        /// AP 비용 = ceil(|deltaDegrees| / 60). 모듈이 회전 가능하지 않으면 false.</remarks>
        public bool RotateHullInPlace(float deltaDegrees)
        {
            if (!moduleManager.CanRotate()) return false;

            int apCost = Mathf.CeilToInt(Mathf.Abs(deltaDegrees) / 60f);
            if (apCost > currentAP) return false;

            hullAngle += deltaDegrees;
            hullAngle = (hullAngle % 360f + 360f) % 360f; // normalize to [0, 360)
            transform.rotation = CompassToRotation(hullAngle);

            currentAP -= apCost;
            OnAPChanged?.Invoke();
            return true;
        }

        /// <summary>이 유닛이 발사할 수 있는 무기 범위 내에 어떤 적도 있는지 확인.
        /// 주포(10 cells) + 기관총(6 cells) + 연막(4 cells) 범위 내 적 감지.</summary>
        public bool HasAnyEnemyInFireRange()
        {
            if (grid == null) return false;

            // 간단히 모든 그리드 셀을 순회하며 적 감지
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    var cell = grid.GetCell(new Vector2Int(x, y));
                    if (cell?.Occupant == null) continue;

                    var occupant = cell.Occupant.GetComponent<GridTankUnit>();
                    if (occupant == null || occupant.side == side) continue; // 같은 팀 또는 유닛 아님

                    int distance = grid.GetDistance(gridPosition, occupant.GridPosition);
                    if (distance <= 10) // 주포 또는 기관총 범위
                        return true;
                }
            }
            return false;
        }

        /// <summary>특정 대상에 대한 무기 가용성 (주포/기관총/연막 범위 내 여부).</summary>
        public WeaponAvailability GetWeaponAvailability(GridTankUnit target)
        {
            if (grid == null || target == null)
                return new WeaponAvailability { mainGunInRange = false, machineGunInRange = false, smokeInRange = false };

            int distance = grid.GetDistance(gridPosition, target.GridPosition);
            return new WeaponAvailability
            {
                mainGunInRange = distance <= 10,     // 주포: 10 cells
                machineGunInRange = distance <= 6,   // 기관총: 6 cells
                smokeInRange = distance <= 4         // 연막: 4 cells
            };
        }

        // ===== 시각 =====

        private void UpdateVisual()
        {
            if (hullRenderer != null)
            {
                float hpRatio = currentHP / (tankData != null ? tankData.maxHP : 100);
                float brightness = Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(hpRatio));
                // 항상 baseHullColor를 기준으로 곱셈 — 절대로 hullRenderer.color를 읽지 않는다
                // (읽으면 호출마다 곱셈이 누적되어 스프라이트가 점점 검게 변함)
                hullRenderer.color = new Color(
                    baseHullColor.r * brightness,
                    baseHullColor.g * brightness,
                    baseHullColor.b * brightness,
                    baseHullColor.a
                );
            }
        }
    }

    /// <summary>특정 대상에 대한 무기별 사거리 내 여부 (B-3 API 반환 타입)</summary>
    [System.Serializable]
    public struct WeaponAvailability
    {
        public bool mainGunInRange;
        public bool machineGunInRange;
        public bool smokeInRange;
    }
}
