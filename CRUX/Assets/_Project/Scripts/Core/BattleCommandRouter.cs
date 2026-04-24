using System.Collections.Generic;
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

        internal BattleCommandRouter(BattleController controller)
        {
            this.controller = controller;
        }

        // ===== 초기화 (BattleController.InitializeBattle에서 호출) =====

        /// <summary>CommandBox prefab 인스턴스 연결 및 이벤트 구독</summary>
        internal void SetupCommandBox()
        {
            var cmdBoxPrefab = Resources.Load<GameObject>("Prefabs/UI/CommandBox");
            if (cmdBoxPrefab != null)
            {
                var cmdBoxObj = Object.Instantiate(cmdBoxPrefab);
                commandBox = cmdBoxObj.GetComponent<CommandBoxController>();
                if (commandBox != null)
                {
                    commandBox.OnMenuSelected += HandleCommandBoxMenuSelected;
                    commandBox.OnMenuCanceled += HandleCommandBoxCanceled;
                    commandBox.HideMenu();
                }
            }
            else
            {
                Debug.LogWarning("[BattleController] CommandBox prefab not found");
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
            var selected = controller.SelectedUnit;
            if (selected != null)
                commandBox.ShowMenuAt(selected.transform.position);
            else
                commandBox.ShowMenu();
        }

        /// <summary>커맨드 박스 숨김</summary>
        internal void HideCommandBox()
        {
            if (commandBox != null)
                commandBox.HideMenu();
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
            controller.CancelToSelect();
        }

        // ===== RotateMode =====

        /// <summary>방향전환 모드 진입</summary>
        private void TryEnterRotateMode()
        {
            if (controller.SelectedUnit == null)
            {
                controller.CancelToSelect();
                return;
            }

            controller.SetInputModeInternal(BattleController.InputModeEnum.RotateMode);
            pendingRotationDelta = 0f;
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
                controller.EndPlayerTurn();
            }
            else
            {
                Debug.Log("[CRUX] Rotate failed — insufficient AP or invalid angle");
                controller.CancelToSelect();
            }
        }

        /// <summary>RotateMode 취소</summary>
        internal void CancelRotateMode()
        {
            if (controller.CurrentInputMode == BattleController.InputModeEnum.RotateMode)
                controller.CancelToSelect();
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
                Debug.Log($"[TargetCycler] 목표 변경: {newTarget.gameObject.name}");
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
