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

            // ESC/Tab 취소 — 최우선
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) || UnityEngine.Input.GetKeyDown(KeyCode.Tab))
            {
                controller.CancelToSelect();
                return;
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

            // 회전 모드
            if (controller.CurrentInputMode == BattleController.InputModeEnum.RotateMode)
            {
                HandleRotateMode();
                return;
            }

            // Select 모드: Q/M 이동 진입, E/F 사격 진입
            HandleSelectModeKeys();

            // Fire 모드: 호버 갱신 + 무기 전환
            if (controller.CurrentInputMode == BattleController.InputModeEnum.Fire)
            {
                controller.UpdateHoveredTarget();
                HandleWeaponSwitch();
            }

            // C/V/O/Space 액션 키
            HandleActionKeys();

            // 좌클릭: 모드별 처리
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                HandleLeftClick();
            }
        }

        /// <summary>무기 선택 모드 입력 처리 (1/2/3 + Space/Enter/Click)</summary>
        private void HandleWeaponSelectMode()
        {
            // 1/2/3: 무기 선택만 (확정 아님)
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

        /// <summary>회전 모드 입력 처리 (Q/E 각도 조정 + Space/Enter/Click 확정)</summary>
        private void HandleRotateMode()
        {
            // Q: −60°, E: +60°
            if (UnityEngine.Input.GetKeyDown(KeyCode.Q))
                controller.AccumulateRotation(-60f);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.E))
                controller.AccumulateRotation(60f);

            // Space / Enter: 회전 확정
            if (UnityEngine.Input.GetKeyDown(KeyCode.Space) || UnityEngine.Input.GetKeyDown(KeyCode.Return))
            {
                controller.CommitRotation();
            }

            // 좌클릭: 회전 확정
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                controller.CommitRotation();
            }

            // ESC/Tab: 회전 취소
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) || UnityEngine.Input.GetKeyDown(KeyCode.Tab))
            {
                controller.CancelRotateMode();
            }
        }

        /// <summary>Select 모드: Q/M 이동 진입, E/F 사격 진입</summary>
        private void HandleSelectModeKeys()
        {
            if (controller.CurrentInputMode != BattleController.InputModeEnum.Select) return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Q) || UnityEngine.Input.GetKeyDown(KeyCode.M))
            {
                controller.TryEnterMoveMode();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.E) || UnityEngine.Input.GetKeyDown(KeyCode.F))
            {
                controller.TryEnterFireMode();
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
