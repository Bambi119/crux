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

        // 모듈 매니저
        private ModuleManager moduleManager = new();

        // 스프라이트 기본 방향: 항상 →(90°)
        private const float spriteBaseAngle = 90f;

        // 컴포넌트
        private SpriteRenderer hullRenderer;
        private GridManager grid;

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

        public System.Action<GridTankUnit> OnDeath;
        public System.Action OnAPChanged;
        public System.Action OnMoveComplete;

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
                moduleSaves = moduleManager.SaveAll()
            };
        }

        /// <summary>저장된 상태로 복원</summary>
        public void RestoreState(GridManager grid, UnitSaveData state)
        {
            var oldCell = grid.GetCell(gridPosition);
            if (oldCell != null && oldCell.Occupant == gameObject)
                oldCell.Occupant = null;

            gridPosition = state.gridPosition;
            currentHP = state.currentHP;
            currentAP = state.currentAP;
            hullAngle = state.hullAngle;

            var newCell = grid.GetCell(gridPosition);
            if (newCell != null) newCell.Occupant = gameObject;

            transform.position = grid.GridToWorld(gridPosition);
            transform.rotation = CompassToRotation(hullAngle);

            // 모듈 상태 복원
            moduleManager.RestoreAll(state.moduleSaves);

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

            // 모듈 초기화 — moduleHP가 0이면 기본값 사용
            if (data.moduleHP.engine <= 0)
                data.moduleHP = ModuleHPProfile.Default;
            moduleManager.Initialize(data);

            transform.position = grid.GridToWorld(pos);
            transform.rotation = CompassToRotation(hullAngle);

            var cell = grid.GetCell(pos);
            if (cell != null) cell.Occupant = gameObject;

            hullRenderer = GetComponentInChildren<SpriteRenderer>();
            UpdateVisual();
        }

        // ===== 턴 관리 =====

        public void OnTurnStart()
        {
            currentAP = tankData != null ? tankData.maxAP : 6;
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
            int cost = GameConstants.FireCost + moduleManager.GetFireAPPenalty();
            return currentAP >= cost;
        }

        /// <summary>기총 사격 가능 여부</summary>
        public bool CanFireMG()
        {
            if (isMoving) return false;
            return moduleManager.CanFireMG();
        }

        /// <summary>차체 회전 가능 여부</summary>
        public bool CanRotate() => moduleManager.CanRotate();

        /// <summary>실효 이동 AP 비용 (1셀당)</summary>
        public int GetMoveCostPerCell() => GameConstants.MoveCostPerCell + moduleManager.GetMoveAPPenalty();

        /// <summary>실효 사격 AP 비용</summary>
        public int GetFireCost() => GameConstants.FireCost + moduleManager.GetFireAPPenalty();

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
            }

            gridPosition = path[^1];
            var newCell = grid.GetCell(gridPosition);
            if (newCell != null) newCell.Occupant = gameObject;

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

        // ===== 데미지 =====

        public void TakeDamage(DamageInfo info)
        {
            currentHP -= info.damage;

            // 모듈 피격 확률: 관통 40%, 일반 Hit 15%
            float moduleHitChance = info.outcome == ShotOutcome.Penetration ? 0.4f : 0.15f;
            if (info.outcome >= ShotOutcome.Hit && Random.value < moduleHitChance)
            {
                bool ammoExplode = moduleManager.DamageRandomModule(
                    info.damage * 0.5f, info.hitZone,
                    tankData != null ? tankData.tankName : gameObject.name);

                if (ammoExplode)
                {
                    currentHP = 0;
                    OnDeath?.Invoke(this);
                    UpdateVisual();
                    return;
                }
            }

            if (currentHP <= 0)
            {
                currentHP = 0;
                OnDeath?.Invoke(this);
            }

            UpdateVisual();
        }

        // ===== 시각 =====

        private void UpdateVisual()
        {
            if (hullRenderer != null)
            {
                float hpRatio = currentHP / (tankData != null ? tankData.maxHP : 100);
                float brightness = Mathf.Lerp(0.3f, 1f, hpRatio);
                var baseColor = hullRenderer.color;
                hullRenderer.color = new Color(
                    baseColor.r * brightness,
                    baseColor.g * brightness,
                    baseColor.b * brightness
                );
            }
        }
    }
}
