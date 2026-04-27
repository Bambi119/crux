using UnityEngine;
using Crux.Core;

namespace Crux.PlayerInput
{
    /// <summary>플레이어 키보드/마우스 입력 감지 — BattleController public API로 행동 지시</summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        private BattleController controller;

        /// <summary>BattleController 참조 설정</summary>
        public void Initialize(BattleController controller)
        {
            this.controller = controller;
        }

        /// <summary>플레이어 턴 중 매 프레임 호출 (BattleController.Update에서)</summary>
        public void Tick()
        {
            if (controller == null || !controller.CanHandleInput) return;

            // ESC 취소 — 최우선
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                controller.CancelToSelect();
                return;
            }

            // 우클릭 — 한 단계 뒤로 (이전 명령 단계 복귀)
            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                HandleRightClickStepBack();
                return;
            }

            // Select 모드: M 또는 Q로 커맨드 박스 표시
            if (controller.CurrentInputMode == BattleController.InputModeEnum.Select)
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.M) || UnityEngine.Input.GetKeyDown(KeyCode.Q))
                {
                    controller.ShowCommandBox();
                    return;
                }
            }

            // 무기 선택 모드
            if (controller.CurrentInputMode == BattleController.InputModeEnum.WeaponSelect
                && controller.PendingTarget != null)
            {
                HandleWeaponSelectMode();
                return;
            }

            // 방향 선택 모드
            if (controller.CurrentInputMode == BattleController.InputModeEnum.MoveDirectionSelect)
            {
                HandleMoveDirectionMode();
                return;
            }

            // 회전 모드 (Phase 4)
            if (controller.CurrentInputMode == BattleController.InputModeEnum.RotateMode)
            {
                HandleRotateMode();
                return;
            }

            // Fire 모드: 호버 갱신 + 무기 전환 + 목표 순환
            if (controller.CurrentInputMode == BattleController.InputModeEnum.Fire)
            {
                controller.UpdateHoveredTarget();
                HandleWeaponSwitch();
                HandleTargetCycling();
            }

            // C/V/O/Space 액션 키
            HandleActionKeys();

            // 좌클릭: 모드별 처리
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                HandleLeftClick();
            }
        }

        /// <summary>우클릭 — 현재 입력 모드에서 한 단계 뒤로</summary>
        private void HandleRightClickStepBack()
        {
            switch (controller.CurrentInputMode)
            {
                case BattleController.InputModeEnum.Select:
                    // post-move CommandBox 표시 중 우클릭 → 이동 취소(원위치 복귀)
                    if (controller.IsPostMoveContext)
                        controller.UndoMoveSnapshot();
                    else
                        controller.HideCommandBox();
                    break;
                case BattleController.InputModeEnum.Move:
                    controller.CancelToSelect();
                    controller.ShowCommandBox();
                    break;
                case BattleController.InputModeEnum.MoveDirectionSelect:
                    controller.UndoMoveSnapshot();
                    break;
                case BattleController.InputModeEnum.Fire:
                    controller.CancelToSelect();
                    controller.ShowCommandBox();
                    break;
                case BattleController.InputModeEnum.WeaponSelect:
                    controller.TryEnterFireMode();
                    break;
                case BattleController.InputModeEnum.RotateMode:
                    if (controller.IsPostMoveContext)
                    {
                        // post-move 컨텍스트: 우클릭 → 이동 취소(원위치 복귀). 휠·CommandBox 모두 정리.
                        controller.UndoMoveSnapshot();
                    }
                    else
                    {
                        controller.CancelRotateMode();
                        controller.ShowCommandBox();
                    }
                    break;
            }
        }

        /// <summary>무기 선택 모드 입력 처리 (1/2/3 + Space/Enter/Click)</summary>
        private void HandleWeaponSelectMode()
        {
            // 1/2/3: 무기 선택만 (확정 아님)
            // TODO(J-4): 무기 가용성 체크 후 회색 처리 (현재 placeholder)
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1))
                controller.SelectWeapon(WeaponType.MainGun);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2))
                controller.SelectWeapon(WeaponType.CoaxialMG);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3))
                controller.SelectWeapon(WeaponType.MountedMG);

            // Space / Enter / 좌클릭: 선택된 무기로 사격 확정
            bool commit = UnityEngine.Input.GetKeyDown(KeyCode.Space)
                          || UnityEngine.Input.GetKeyDown(KeyCode.Return)
                          || UnityEngine.Input.GetMouseButtonDown(0);
            if (commit)
            {
                controller.CommitWeaponSelection();
            }
        }

        /// <summary>회전 모드 입력 처리 — RotationWheelController가 키보드 전담. 우클릭만 PlayerInputHandler가 처리(HandleRightClickStepBack).</summary>
        private void HandleRotateMode()
        {
            // 의도적으로 비움. 휠 UI가 Q/E/W/A/S/D/Space/Esc 전부 처리.
        }

        /// <summary>방향 선택 모드 입력 처리 (QWEASD + Space/Enter/Click)</summary>
        private void HandleMoveDirectionMode()
        {
            // 6방향 육각 매핑 (QWE 상단 + ASD 하단, 2행 키보드 레이아웃)
            //   Q = NW (300°)   W = N  (0°)    E = NE (60°)
            //   A = SW (240°)   S = S  (180°)  D = SE (120°)
            if (UnityEngine.Input.GetKeyDown(KeyCode.Q))
                controller.SetPendingFacingAngle(300f);   // NW
            else if (UnityEngine.Input.GetKeyDown(KeyCode.W))
                controller.SetPendingFacingAngle(0f);     // N
            else if (UnityEngine.Input.GetKeyDown(KeyCode.E))
                controller.SetPendingFacingAngle(60f);    // NE
            else if (UnityEngine.Input.GetKeyDown(KeyCode.A))
                controller.SetPendingFacingAngle(240f);   // SW
            else if (UnityEngine.Input.GetKeyDown(KeyCode.S))
                controller.SetPendingFacingAngle(180f);   // S
            else if (UnityEngine.Input.GetKeyDown(KeyCode.D))
                controller.SetPendingFacingAngle(120f);   // SE

            // Space / Enter: 방향 확정
            if (UnityEngine.Input.GetKeyDown(KeyCode.Space) || UnityEngine.Input.GetKeyDown(KeyCode.Return))
            {
                controller.CommitMoveDirection();
            }

            // 좌클릭: 클릭 방향 스냅 + 이동 확정
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                controller.CommitMoveDirectionFromMouse();
            }
        }

        /// <summary>Fire 모드: Tab/Shift+Tab으로 목표 순환</summary>
        private void HandleTargetCycling()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab))
            {
                if (UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift))
                {
                    // Shift+Tab: 이전 목표
                    controller.CycleTargetPrevious();
                }
                else
                {
                    // Tab: 다음 목표
                    controller.CycleTargetNext();
                }
            }
        }

        /// <summary>Fire 모드에서 1/2/3으로 무기 전환</summary>
        private void HandleWeaponSwitch()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1))
                controller.SelectWeapon(WeaponType.MainGun);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2))
                controller.SelectWeapon(WeaponType.CoaxialMG);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3))
                controller.SelectWeapon(WeaponType.MountedMG);
        }

        /// <summary>Select 모드 액션 키 처리 (C/V/O/Space EndTurn)</summary>
        private void HandleActionKeys()
        {
            if (controller.CurrentInputMode != BattleController.InputModeEnum.Select) return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.C))
            {
                controller.TryExtinguishAction();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.V))
            {
                controller.TryUseSmokeAction();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.O))
            {
                controller.TryActivateOverwatchAction();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                controller.EndPlayerTurn();
            }
        }

        /// <summary>좌클릭 처리 — 월드 좌표에서 그리드 좌표로 변환 후 dispatch</summary>
        private void HandleLeftClick()
        {
            var mainCam = controller.MainCam;
            if (mainCam == null) return;

            var worldPos = mainCam.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            var gridPos = controller.Grid.WorldToGrid(worldPos);

            controller.HandleClickAt(gridPos);
        }
    }
}
