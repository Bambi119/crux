using UnityEngine;
using Crux.Core;

public static class PS3InputAPITest
{
    public static void Execute()
    {
        var ctrl = Object.FindFirstObjectByType<BattleController>();
        if (ctrl == null)
        {
            Debug.LogError("[PS3-TEST] BattleController not found — Play mode 상태여야 합니다");
            return;
        }

        void Log(string msg) => Debug.Log($"[PS3-TEST] {msg}");

        // 0. 초기 상태 확인
        Log($"Grid={(ctrl.Grid != null ? $"{ctrl.Grid.Width}x{ctrl.Grid.Height}" : "null")}");
        Log($"SelectedUnit={(ctrl.SelectedUnit != null ? "OK" : "null")}");
        Log($"MainCam={(ctrl.MainCam != null ? "OK" : "null")}");
        Log($"InitialInputMode={ctrl.CurrentInputMode} weapon={ctrl.SelectedWeapon}");
        Log($"CanHandleInput={ctrl.CanHandleInput}");

        // 1. CancelToSelect — 항상 Select로
        ctrl.CancelToSelect();
        Log($"1.CancelToSelect => {ctrl.CurrentInputMode}");

        // 2. TryEnterMoveMode — Select에서 Move로
        ctrl.TryEnterMoveMode();
        Log($"2.TryEnterMoveMode => {ctrl.CurrentInputMode}");

        // 3. CancelToSelect — 복귀
        ctrl.CancelToSelect();
        Log($"3.CancelToSelect => {ctrl.CurrentInputMode}");

        // 4. TryEnterFireMode — Select에서 Fire로, weapon=MainGun 리셋
        ctrl.TryEnterFireMode();
        Log($"4.TryEnterFireMode => {ctrl.CurrentInputMode} weapon={ctrl.SelectedWeapon}");

        // 5. SelectWeapon — MainGun(항상 OK)
        ctrl.SelectWeapon(WeaponType.MainGun);
        Log($"5.SelectWeapon(MainGun) => weapon={ctrl.SelectedWeapon}");

        // 6. SelectWeapon — CoaxialMG (null 가드 작동 여부)
        ctrl.SelectWeapon(WeaponType.CoaxialMG);
        Log($"6.SelectWeapon(CoaxialMG) => weapon={ctrl.SelectedWeapon}");

        // 7. SelectWeapon — MountedMG
        ctrl.SelectWeapon(WeaponType.MountedMG);
        Log($"7.SelectWeapon(MountedMG) => weapon={ctrl.SelectedWeapon}");

        // 8. UpdateHoveredTarget — Fire 모드 호버 갱신 (crash 없이)
        ctrl.UpdateHoveredTarget();
        Log($"8.UpdateHoveredTarget => hover={(ctrl.HoveredTarget != null ? ctrl.HoveredTarget.name : "null")}");

        // 9. HandleClickAt — OOB 좌표 무시 (crash 없이)
        ctrl.HandleClickAt(new Vector2Int(-1, -1));
        Log("9.HandleClickAt(OOB) => no crash");

        // 10. HandleClickAt — valid 좌표 (Fire 모드 → TrySelectTarget 분기)
        if (ctrl.Grid != null)
        {
            ctrl.HandleClickAt(new Vector2Int(0, 0));
            Log("10.HandleClickAt(0,0) => no crash");
        }

        // 11. CancelToSelect → TryExtinguishAction (플레이어 안 타고 있으면 no-op)
        ctrl.CancelToSelect();
        ctrl.TryExtinguishAction();
        Log("11.TryExtinguishAction => no crash");

        // 12. TryUseSmokeAction — 연막 장전 없으면 no-op
        ctrl.TryUseSmokeAction();
        Log("12.TryUseSmokeAction => no crash");

        // 13. TryActivateOverwatchAction
        ctrl.TryActivateOverwatchAction();
        Log("13.TryActivateOverwatchAction => no crash");

        // 14. SetPendingFacingAngle + CommitMoveDirection (방향 선택 API no-op 가드)
        ctrl.SetPendingFacingAngle(60f);
        Log($"14.SetPendingFacingAngle(60) => pending={ctrl.PendingFacingAngle}");

        // 15. Final state
        Log($"15.FinalInputMode={ctrl.CurrentInputMode} weapon={ctrl.SelectedWeapon}");
        Log("=== PS3 API TEST DONE ===");
    }
}
