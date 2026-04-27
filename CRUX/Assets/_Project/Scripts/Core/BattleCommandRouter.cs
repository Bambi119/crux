using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Crux.Data;
using Crux.Unit;
using Crux.UI;
using Crux.Grid;

namespace Crux.Core
{
    /// <summary>
    /// BattleController에서 분리된 Phase 3 UI 명령 라우팅.
    /// CommandBox 표시/숨김 · TargetCycler 관리 · RotateMode 상태 · 무기 프리뷰.
    /// PlayerInputHandler → BattleController → BattleCommandRouter 흐름으로 위임.
    /// </summary>
    internal class BattleCommandRouter
    {
        private readonly BattleController controller;

        // Phase 3 UI 컴포넌트
        private CommandBoxController commandBox;
        private TargetCycler targetCycler;

        // RotateMode 누적 회전각
        private float pendingRotationDelta;

        // 활성 RotationWheel 인스턴스 (post-move 또는 일반 Rotate 진입 중)
        private GameObject activeWheelObj;

        internal BattleCommandRouter(BattleController controller)
        {
            this.controller = controller;
        }

        // ===== 초기화 (BattleController.InitializeBattle에서 호출) =====

        /// <summary>CommandBox prefab 인스턴스 연결 및 이벤트 구독</summary>
        internal void SetupCommandBox()
        {
            var cmdBoxPrefab = Resources.Load<GameObject>("Prefabs/UI/CommandBox");
            if (cmdBoxPrefab == null)
            {
                Debug.LogWarning("[BattleController] CommandBox prefab not found");
                return;
            }

            // Canvas 부모 탐색: HUDContainer → Canvas → world root 순
            Transform hudParent = null;
            var hudContainer = GameObject.Find("Canvas/HUDContainer");
            if (hudContainer != null)
            {
                hudParent = hudContainer.transform;
            }
            else
            {
                var canvas = Object.FindFirstObjectByType<Canvas>();
                if (canvas != null)
                    hudParent = canvas.transform;
                else
                    Debug.LogWarning("[BattleController] Canvas/HUDContainer not found — CommandBox left at world root");
            }

            var cmdBoxObj = hudParent != null
                ? Object.Instantiate(cmdBoxPrefab, hudParent, worldPositionStays: false)
                : Object.Instantiate(cmdBoxPrefab);

            commandBox = cmdBoxObj.GetComponent<CommandBoxController>();
            if (commandBox != null)
            {
                // Instantiate 직후 SetActive(false) — HideMenu() 전에 한 프레임도 렌더되지 않도록 보장
                cmdBoxObj.SetActive(false);
                commandBox.OnMenuSelected += HandleCommandBoxMenuSelected;
                commandBox.OnMenuCanceled += HandleCommandBoxCanceled;
            }
        }

        /// <summary>TargetCycler GameObject 생성 및 이벤트 구독</summary>
        internal void SetupTargetCycler()
        {
            var cyclerObj = new GameObject("TargetCycler");
            targetCycler = cyclerObj.AddComponent<TargetCycler>();
            targetCycler.OnTargetChanged += HandleTargetCycled;
        }

        // ===== CommandBox API =====

        /// <summary>커맨드 박스 표시 — Select 모드에서 호출</summary>
        internal void ShowCommandBox()
        {
            if (commandBox == null) return;
            // SetActive(true) — SetupCommandBox에서 false로 초기화된 GameObject를 활성화
            commandBox.gameObject.SetActive(true);
            var selected = controller.SelectedUnit;
            if (selected != null)
                commandBox.ShowMenuAt(selected.transform.position, controller.MainCam);
            else
                commandBox.ShowMenu();
        }

        /// <summary>커맨드 박스 숨김</summary>
        internal void HideCommandBox()
        {
            if (commandBox != null)
                commandBox.HideMenu(); // HideMenu 내부에서 gameObject.SetActive(false) 처리
        }

        // ===== CommandBox 이벤트 콜백 =====

        private void HandleCommandBoxMenuSelected(CommandBoxController.MenuItem item)
        {
            switch (item)
            {
                case CommandBoxController.MenuItem.Move:
                    controller.TryEnterMoveMode();
                    break;
                case CommandBoxController.MenuItem.Fire:
                    controller.TryEnterFireMode();
                    break;
                case CommandBoxController.MenuItem.Rotate:
                    TryEnterRotateMode();
                    break;
                case CommandBoxController.MenuItem.Skill:
                    Debug.Log("[CommandBox] 전차장스킬 선택");
                    break;
                case CommandBoxController.MenuItem.Wait:
                    controller.EndPlayerTurn();
                    break;
                case CommandBoxController.MenuItem.Cancel:
                    // Cancel은 OnMenuCanceled에서 처리
                    break;
            }
        }

        private void HandleCommandBoxCanceled()
        {
            // post-move 중 취소 → 이동 취소(원위치 복귀), 일반 취소 → Select 모드
            if (controller.IsPostMoveContext)
                controller.UndoMoveSnapshot();
            else
                controller.CancelToSelect();
        }

        // ===== post-move CommandBox + RotationWheel =====

        /// <summary>이동 완료 후 CommandBox 표시 — 컨텍스트 필터 적용 (CommitMoveDirection에서 호출)</summary>
        internal void ShowPostMoveCommandBox()
        {
            if (commandBox == null || controller.SelectedUnit == null) return;

            var unit = controller.SelectedUnit;
            var visibleItems = BuildPostMoveMenuItems(unit);

            commandBox.gameObject.SetActive(true);
            commandBox.ShowMenuFiltered(visibleItems, unit.transform.position, controller.MainCam);
        }

        /// <summary>post-move 시점 표시할 MenuItem 배열 결정</summary>
        private CommandBoxController.MenuItem[] BuildPostMoveMenuItems(GridTankUnit unit)
        {
            var items = new List<CommandBoxController.MenuItem>();

            // Fire: CanFire() + 사거리 내 생존 적 존재
            if (unit.CanFire() && HasEnemyInRange(unit))
                items.Add(CommandBoxController.MenuItem.Fire);

            // Rotate: 추가 회전 가능 시
            if (unit.CanRotate())
                items.Add(CommandBoxController.MenuItem.Rotate);

            // Skill(연막): CanUseSmoke()
            if (unit.CanUseSmoke())
                items.Add(CommandBoxController.MenuItem.Skill);

            // Wait: 항상
            items.Add(CommandBoxController.MenuItem.Wait);

            // Cancel: 항상 (이동 취소)
            items.Add(CommandBoxController.MenuItem.Cancel);

            return items.ToArray();
        }

        private bool HasEnemyInRange(GridTankUnit unit)
        {
            var allUnits = controller.GetAllUnitsForRangeCheck();
            foreach (var other in allUnits)
            {
                if (other == null || other.IsDestroyed) continue;
                if (other.side == unit.side) continue;
                if (HexCoord.Distance(unit.GridPosition, other.GridPosition) <= GameConstants.MaxFireRange)
                    return true;
            }
            return false;
        }

        /// <summary>RotationWheel UI 표시 — post-move flow와 일반 Rotate(제자리) 양쪽에서 호출</summary>
        internal void ShowRotationWheel()
        {
            if (controller.SelectedUnit == null)
            {
                controller.CancelToSelect();
                return;
            }

            var wheelPrefab = Resources.Load<GameObject>("Prefabs/UI/RotationWheel");
            if (wheelPrefab == null)
            {
                Debug.LogWarning("[BattleCommandRouter] RotationWheel prefab not found — keyboard fallback active");
                controller.SetInputModeInternal(BattleController.InputModeEnum.MoveDirectionSelect);
                return;
            }

            // Canvas 부모 탐색 (SetupCommandBox와 동일 패턴)
            Transform hudParent = null;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null) hudParent = canvas.transform;

            var wheelObj = hudParent != null
                ? Object.Instantiate(wheelPrefab, hudParent, worldPositionStays: false)
                : Object.Instantiate(wheelPrefab);

            var wheel = wheelObj.GetComponent<RotationWheelController>();
            if (wheel == null)
            {
                Object.Destroy(wheelObj);
                Debug.LogWarning("[BattleCommandRouter] RotationWheelController component not found on prefab");
                controller.SetInputModeInternal(BattleController.InputModeEnum.MoveDirectionSelect);
                return;
            }

            activeWheelObj = wheelObj;
            var unit = controller.SelectedUnit;
            wheel.Show(unit.transform.position, unit.HullAngle, controller.MainCam);

            wheel.OnAngleSelected += (absAngle) =>
            {
                float delta = absAngle - unit.HullAngle;
                while (delta > 180f)  delta -= 360f;
                while (delta < -180f) delta += 360f;
                bool postMove = controller.IsPostMoveContext;
                bool ok = unit.RotateHullInPlace(delta);
                if (activeWheelObj == wheelObj) activeWheelObj = null;
                Object.Destroy(wheelObj);
                if (ok)
                {
                    controller.SetInputModeInternal(BattleController.InputModeEnum.Select);
                    if (postMove)
                        ShowPostMoveCommandBox();
                    else
                        ShowCommandBox();
                }
                else
                    controller.CancelToSelect();
            };

            wheel.OnCanceled += () =>
            {
                bool postMove = controller.IsPostMoveContext;
                if (activeWheelObj == wheelObj) activeWheelObj = null;
                Object.Destroy(wheelObj);
                if (postMove)
                {
                    // post-move 방향 선택 취소 → 이동 스냅샷 UNDO
                    controller.UndoMoveSnapshot();
                }
                else
                {
                    controller.CancelToSelect();
                    ShowCommandBox();
                }
            };

            controller.SetInputModeInternal(BattleController.InputModeEnum.RotateMode);
        }

        /// <summary>활성 RotationWheel 인스턴스 즉시 파괴 — 외부 취소 경로(UndoMoveSnapshot 등)용</summary>
        internal void HideRotationWheel()
        {
            if (activeWheelObj != null)
            {
                Object.Destroy(activeWheelObj);
                activeWheelObj = null;
            }
        }

        // ===== RotateMode =====

        /// <summary>방향전환 모드 진입 — RotationWheel UI 표시</summary>
        private void TryEnterRotateMode()
        {
            if (controller.SelectedUnit == null)
            {
                controller.CancelToSelect();
                return;
            }

            pendingRotationDelta = 0f;
            // CommandBox 닫고 휠 표시 (휠 내부에서 RotateMode 설정)
            HideCommandBox();
            ShowRotationWheel();
        }

        /// <summary>축적된 회전각도 적용 및 RotateMode 종료</summary>
        internal void CommitRotation()
        {
            if (controller.SelectedUnit == null
                || controller.CurrentInputMode != BattleController.InputModeEnum.RotateMode)
                return;

            if (Mathf.Abs(pendingRotationDelta) < 1f)
            {
                controller.CancelToSelect();
                return;
            }

            bool success = controller.SelectedUnit.RotateHullInPlace(pendingRotationDelta);
            if (success)
            {
                Debug.Log($"[CRUX] Rotate {pendingRotationDelta}° executed (AP cost)");
                controller.SetInputModeInternal(BattleController.InputModeEnum.Select);
                if (controller.IsPostMoveContext)
                    ShowPostMoveCommandBox();
            }
            else
            {
                Debug.Log("[CRUX] Rotate failed — insufficient AP or invalid angle");
                controller.CancelToSelect();
            }
        }

        /// <summary>RotateMode 취소 — 휠 destroy 포함</summary>
        internal void CancelRotateMode()
        {
            if (controller.CurrentInputMode == BattleController.InputModeEnum.RotateMode)
            {
                HideRotationWheel();
                controller.CancelToSelect();
            }
        }

        /// <summary>화면 내 회전각 누적 (양수=시계, 음수=반시계)</summary>
        internal void AccumulateRotation(float deltaDegrees)
        {
            if (controller.CurrentInputMode == BattleController.InputModeEnum.RotateMode)
                pendingRotationDelta += deltaDegrees;
        }

        /// <summary>누적된 회전각 조회</summary>
        internal float GetPendingRotationDelta() => pendingRotationDelta;

        // ===== TargetCycler =====

        /// <summary>Fire 모드 진입 시 사격 범위 내 유효 목표 초기화</summary>
        internal void InitializeTargetCycler(GridTankUnit selectedUnit, List<GridTankUnit> enemyUnits)
        {
            if (selectedUnit == null || targetCycler == null)
                return;

            var validTargets = new List<GridTankUnit>();
            foreach (var enemy in enemyUnits)
            {
                if (enemy != null && !enemy.IsDestroyed)
                {
                    int dist = HexCoord.Distance(selectedUnit.GridPosition, enemy.GridPosition);
                    if (dist <= GameConstants.MaxFireRange)
                        validTargets.Add(enemy);
                }
            }

            targetCycler.SetValidTargets(validTargets);
        }

        private void HandleTargetCycled(GridTankUnit newTarget)
        {
            controller.SetTargetUnitInternal(newTarget);
            if (newTarget != null)
            {
                controller.BattleCam?.PanTo(newTarget.transform.position);
                Debug.Log($"[TargetCycler] 목표 변경: {newTarget.gameObject.name}");
            }
        }

        /// <summary>Fire 모드: 다음 목표로 순환</summary>
        internal void CycleTargetNext()
        {
            if (controller.CurrentInputMode != BattleController.InputModeEnum.Fire
                || targetCycler == null) return;
            targetCycler.CycleToNext();
        }

        /// <summary>Fire 모드: 이전 목표로 순환</summary>
        internal void CycleTargetPrevious()
        {
            if (controller.CurrentInputMode != BattleController.InputModeEnum.Fire
                || targetCycler == null) return;
            targetCycler.CycleToPrevious();
        }

        // ===== 무기 프리뷰 =====

        /// <summary>무기 선택 프리뷰 — 예상 피해 부위 바 깜빡임 (Phase 4)</summary>
        internal void UpdateWeaponPreview(WeaponType weapon, GridTankUnit target,
            MachineGunDataSO coaxialMGData, MachineGunDataSO mountedMGData)
        {
            float expectedDamage = weapon switch
            {
                WeaponType.MainGun   => 15f,                            // mainGunData 미연결 — fallback
                WeaponType.CoaxialMG => coaxialMGData?.damagePerShot ?? 3f,
                WeaponType.MountedMG => mountedMGData?.damagePerShot ?? 3f,
                _ => 0f
            };

            // TODO: HitZone 매핑 구현 (현재 간단히 Front만)
            var zone = HitZone.Front; // Placeholder
            UpdateTargetWeaponPreview(target, zone, expectedDamage);
        }

        /// <summary>목표 유닛의 부위 바 프리뷰 갱신</summary>
        internal void UpdateTargetWeaponPreview(GridTankUnit target, HitZone zone, float expectedDamage)
        {
            if (target == null) return;
            Debug.Log($"[CRUX] Weapon preview: {zone} zone, expected damage {expectedDamage:F1}");
            // PartBarFlashAnimator 연결 예정 (prefab에 배치된 상태)
        }

        /// <summary>무기 프리뷰 종료</summary>
        internal void ClearWeaponPreview()
        {
            Debug.Log("[CRUX] Weapon preview cleared");
            // PartBarFlashAnimator.StopFlash() 호출 예정
        }
    }
}
