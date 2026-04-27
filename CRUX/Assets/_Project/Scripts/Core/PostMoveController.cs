using UnityEngine;
using Crux.Grid;
using Crux.Unit;

namespace Crux.Core
{
    /// <summary>
    /// BattleController에서 분리된 post-move 흐름 제어.
    /// 이동 완료 후 방향 선택·실행·취소(Undo)·셀 클릭(이동/사격/조회) 책임.
    /// BattleCommandRouter와 동일 패턴 — MonoBehaviour 아님, BattleController internal 접근자 사용.
    /// </summary>
    internal class PostMoveController
    {
        private readonly BattleController bc;

        // post-move 흐름 필드 (BattleController에서 이전)
        private bool postMovePendingDirection;
        private bool isPostMoveContext;
        private Vector2Int preMoveGridPos;
        private float preMoveHullAngle;
        private int preMoveAP;

        // BattleController가 1줄 위임으로 읽는 getter/setter
        internal bool IsPostMoveContext { get => isPostMoveContext; set => isPostMoveContext = value; }
        internal bool PostMovePendingDirection => postMovePendingDirection;

        internal PostMoveController(BattleController owner)
        {
            bc = owner;
        }

        // ===== Update 감시 — BattleController.Update에서 매 프레임 호출 =====

        /// <summary>이동 완료 감시 + 방향 선택 진입 — BattleController.Update에서 호출</summary>
        internal void Tick()
        {
            if (postMovePendingDirection && bc.SelectedUnitInternal != null && !bc.SelectedUnitInternal.IsMoving)
            {
                postMovePendingDirection = false;
                EnterPostMoveDirectionSelect();
            }
        }

        // ===== 이동 취소 =====

        /// <summary>이동 취소 — preMoveSnapshot으로 유닛 원위치 복귀. RotationWheel·CommandBox·InputMode 모두 정리.</summary>
        internal void UndoMoveSnapshot()
        {
            var unit = bc.SelectedUnitInternal;
            if (unit == null) return;
            unit.UndoMove(preMoveGridPos, preMoveHullAngle, preMoveAP, bc.GridRef);
            isPostMoveContext = false;
            postMovePendingDirection = false;
            bc.VisualizerRef.ClearHighlights();
            bc.CommandRouterRef.HideRotationWheel();
            bc.CommandRouterRef.HideCommandBox();
            bc.InputModeInternal = BattleController.InputModeEnum.Select;
            Debug.Log($"[CRUX] 이동 취소 — {unit.Data?.tankName} 원위치 복귀 ({preMoveGridPos})");
        }

        // ===== post-move 방향 선택 =====

        /// <summary>이동 완료 후 방향 선택 모드 진입 — Tick의 postMovePendingDirection 감시에서 호출</summary>
        internal void EnterPostMoveDirectionSelect()
        {
            var unit = bc.SelectedUnitInternal;
            if (unit == null) return;
            isPostMoveContext = true;
            bc.PendingFacingAngleInternal = unit.HullAngle;
            bc.InputModeInternal = BattleController.InputModeEnum.MoveDirectionSelect;
            bc.VisualizerRef.HighlightCell(bc.PendingMoveTargetInternal, Color.cyan);
            bc.CommandRouterRef.ShowRotationWheel();
        }

        // ===== 셀 클릭 핸들러 =====

        /// <summary>Move 모드에서 셀 클릭 — 이동 실행</summary>
        internal void TryMoveToCell(Vector2Int pos)
        {
            var unit = bc.SelectedUnitInternal;
            if (unit == null) return;

            var path = bc.GridRef.FindPath(unit.GridPosition, pos);
            if (path == null || path.Count <= 1) return;

            int cost = (path.Count - 1) * unit.GetMoveCostPerCell();
            if (cost > unit.CurrentAP) return;

            // 이동 직전 스냅샷 저장
            preMoveGridPos = unit.GridPosition;
            preMoveHullAngle = unit.HullAngle;
            preMoveAP = unit.CurrentAP;

            bc.PendingMoveTargetInternal = pos;
            bc.PendingMoveCostInternal = cost;
            bool moved = unit.MoveToWithFacing(pos, unit.HullAngle);
            if (!moved) return;

            // Update에서 IsMoving=false 감지 시 EnterPostMoveDirectionSelect
            postMovePendingDirection = true;
            bc.VisualizerRef.ClearHighlights();
        }

        /// <summary>Fire 모드에서 셀 클릭 — 대상 선택</summary>
        internal void TrySelectTarget(Vector2Int pos)
        {
            var cell = bc.GridRef.GetCell(pos);
            if (cell == null || cell.Occupant == null) return;

            var target = cell.Occupant.GetComponent<GridTankUnit>();
            if (target == null || target.IsDestroyed || target.side == PlayerSide.Player) return;

            bc.TargetUnitInternal = target;
            bc.PendingTargetInternal = target;
            bc.InputModeInternal = BattleController.InputModeEnum.WeaponSelect;
            bc.VisualizerRef.ClearHighlights();
        }

        /// <summary>Select 모드에서 셀 클릭 — 유닛 정보 조회</summary>
        internal void InspectCell(Vector2Int pos)
        {
            var cell = bc.GridRef.GetCell(pos);
            if (cell == null || cell.Occupant == null)
            {
                bc.InspectedUnitInternal = null;
                return;
            }
            var unit = cell.Occupant.GetComponent<GridTankUnit>();
            if (unit == null || unit.IsDestroyed)
            {
                bc.InspectedUnitInternal = null;
                return;
            }
            bc.InspectedUnitInternal = unit.side == PlayerSide.Player ? null : unit;
            if (unit.side == PlayerSide.Player)
            {
                bc.SelectedUnitInternal = unit;
                bc.ShowCommandBoxInternal();
            }
        }
    }
}
