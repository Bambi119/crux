using System.IO;
using UnityEngine;
using Crux.Core;
using Crux.Unit;

public static class PSHudCaptureTest
{
    const string OutDir = "C:/01_project/03_crux/tmp";

    static BattleController Ctrl => Object.FindFirstObjectByType<BattleController>();

    static void EnsureDir()
    {
        if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);
    }

    static GridTankUnit FindEnemy()
    {
        foreach (var u in Object.FindObjectsByType<GridTankUnit>(FindObjectsSortMode.None))
            if (u != null && !u.IsDestroyed && u.side == PlayerSide.Enemy) return u;
        return null;
    }

    public static void Capture01Select()
    {
        EnsureDir();
        var ctrl = Ctrl;
        if (ctrl == null) { Debug.LogError("[HUD-CAP] no ctrl"); return; }
        ctrl.CancelToSelect();
        ScreenCapture.CaptureScreenshot(OutDir + "/hud_01_select.png");
        Debug.Log($"[HUD-CAP] 1.select mode={ctrl.CurrentInputMode}");
    }

    public static void Capture02FireEmpty()
    {
        EnsureDir();
        var ctrl = Ctrl;
        if (ctrl == null) return;
        ctrl.CancelToSelect();
        ctrl.TryEnterFireMode();
        ScreenCapture.CaptureScreenshot(OutDir + "/hud_02_fire_empty.png");
        Debug.Log($"[HUD-CAP] 2.fire_empty mode={ctrl.CurrentInputMode}");
    }

    public static void Capture03WeaponSelect()
    {
        EnsureDir();
        var ctrl = Ctrl;
        if (ctrl == null) return;
        var target = FindEnemy();
        if (target == null) { Debug.LogError("[HUD-CAP] 3.no enemy"); return; }
        ctrl.CancelToSelect();
        ctrl.TryEnterFireMode();
        ctrl.HandleClickAt(target.GridPosition);
        ScreenCapture.CaptureScreenshot(OutDir + "/hud_03_weapon_select.png");
        Debug.Log($"[HUD-CAP] 3.weapon_select target={target.GridPosition} mode={ctrl.CurrentInputMode} pendingTarget={(ctrl.PendingTarget != null)}");
    }

    public static void Capture04Move()
    {
        EnsureDir();
        var ctrl = Ctrl;
        if (ctrl == null) return;
        ctrl.CancelToSelect();
        ctrl.TryEnterMoveMode();
        ScreenCapture.CaptureScreenshot(OutDir + "/hud_04_move.png");
        Debug.Log($"[HUD-CAP] 4.move mode={ctrl.CurrentInputMode}");
    }

    public static void Capture05Final()
    {
        EnsureDir();
        var ctrl = Ctrl;
        if (ctrl == null) return;
        ctrl.CancelToSelect();
        ScreenCapture.CaptureScreenshot(OutDir + "/hud_05_final.png");
        Debug.Log($"[HUD-CAP] 5.final mode={ctrl.CurrentInputMode}");
    }
}
