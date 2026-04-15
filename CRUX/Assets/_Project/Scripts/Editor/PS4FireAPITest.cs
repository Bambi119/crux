using UnityEngine;
using Crux.Core;
using Crux.Unit;

public static class PS4FireAPITest
{
    public static void Execute()
    {
        var ctrl = Object.FindFirstObjectByType<BattleController>();
        if (ctrl == null)
        {
            Debug.LogError("[PS4-TEST] BattleController not found");
            return;
        }

        void Log(string msg) => Debug.Log($"[PS4-TEST] {msg}");

        Log($"Grid={(ctrl.Grid != null ? $"{ctrl.Grid.Width}x{ctrl.Grid.Height}" : "null")}");
        Log($"SelectedUnit={(ctrl.SelectedUnit != null ? ctrl.SelectedUnit.Data.tankName : "null")}");

        var attacker = ctrl.SelectedUnit;
        if (attacker == null)
        {
            Debug.LogError("[PS4-TEST] no attacker unit");
            return;
        }

        // 적 찾기
        GridTankUnit target = null;
        var enemies = Object.FindObjectsByType<GridTankUnit>(FindObjectsSortMode.None);
        foreach (var u in enemies)
        {
            if (u != null && !u.IsDestroyed && u.side == PlayerSide.Enemy)
            {
                target = u;
                break;
            }
        }
        if (target == null)
        {
            Debug.LogError("[PS4-TEST] no enemy target found");
            return;
        }
        Log($"Target found: {target.Data.tankName} at {target.GridPosition}");

        // 1. CalculateHitChance (BattleController에 유지)
        int distance = ctrl.Grid.GetDistance(attacker.GridPosition, target.GridPosition);
        float basicHit = ctrl.CalculateHitChance(distance, target);
        Log($"1.CalculateHitChance(d={distance}) => {basicHit:P0}");

        // 2. Fire 모드 진입
        ctrl.CancelToSelect();
        ctrl.TryEnterFireMode();
        Log($"2.TryEnterFireMode => {ctrl.CurrentInputMode}");

        // 3. SelectWeapon(MainGun)
        ctrl.SelectWeapon(WeaponType.MainGun);
        Log($"3.SelectWeapon(MainGun) => {ctrl.SelectedWeapon}");

        // 4. HoveredTarget 설정 경로 검증 (Fire 모드 클릭 → TrySelectTarget)
        ctrl.HandleClickAt(target.GridPosition);
        Log($"4.HandleClickAt(target) => mode={ctrl.CurrentInputMode} pendingTarget={(ctrl.PendingTarget != null ? ctrl.PendingTarget.Data.tankName : "null")}");

        // 5. CommitWeaponSelection은 CommitFire → FireExecutor.Execute → SceneManager.LoadScene을 호출함
        //    Scene 전환은 실행하지 않는 것이 안전 (Play 세션 유지). 대신 상태만 확인.
        Log($"5.FireExecutor chain READY (fire commit would trigger scene transition)");

        // 복귀
        ctrl.CancelToSelect();
        Log($"6.CancelToSelect => {ctrl.CurrentInputMode}");

        Log("=== PS4 FIRE API TEST DONE (no fire commit — scene transition avoided) ===");
    }
}
