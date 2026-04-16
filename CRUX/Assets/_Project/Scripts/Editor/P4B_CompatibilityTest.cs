using UnityEngine;
using UnityEditor;
using Crux.Data;
using System.Collections.Generic;

/// <summary>
/// P4-B Batch Smoke Test — 호환성 3중 체계 검사기 검증.
/// docs/05 §3 기반. 하중·출력·규격 3축 검사 로직 확인.
/// 메뉴 Crux/Test/P4B Compatibility 실행.
///
/// 시나리오:
/// 1. 가벼운 파츠 → CheckWeight OK
/// 2. 초과 중량 파츠 → CheckWeight FAIL
/// 3. 엔진 없음 → CheckPower FAIL
/// 4. 출력 부족 → CheckPower FAIL
/// 5. 주포 구경 초과 → CheckSpec FAIL
/// 6. AmmoRack 차체 제약 → CheckSpec FAIL
/// </summary>
public static class P4B_CompatibilityTest
{
    [MenuItem("Crux/Test/P4B Compatibility")]
    public static void Execute()
    {
        void Log(string msg) => Debug.Log($"[P4B] {msg}");
        void Fail(string msg) => Debug.LogError($"[P4B] FAIL — {msg}");

        int passed = 0;
        int failed = 0;

        void Assert(bool cond, string name)
        {
            if (cond) { passed++; Log($"OK {name}"); }
            else      { failed++; Fail(name); }
        }

        // ===== 1. CheckWeight — 경량 파츠 조합 (Scout, 60kg 한도) =====
        var scoutEngine = ScriptableObject.CreateInstance<EnginePartSO>();
        scoutEngine.weight = 15f;
        scoutEngine.powerDraw = 0f;
        scoutEngine.powerOutput = 80f;

        var scoutTurret = ScriptableObject.CreateInstance<TurretPartSO>();
        scoutTurret.weight = 10f;
        scoutTurret.powerDraw = 2f;
        scoutTurret.caliberLimit = 45;

        var lightGun = ScriptableObject.CreateInstance<MainGunPartSO>();
        lightGun.weight = 8f;
        lightGun.powerDraw = 1f;
        lightGun.caliber = 45;

        var lightParts = new List<PartDataSO> { scoutEngine, scoutTurret, lightGun };
        var weightResult1 = CompatibilityChecker.CheckWeight(HullClass.Scout, lightParts);
        Assert(weightResult1.isValid, "CheckWeight Scout light parts (33kg < 60kg)");

        // ===== 2. CheckWeight — 초과 중량 (Scout에 80kg 파츠) =====
        var heavyArmor = ScriptableObject.CreateInstance<ArmorPartSO>();
        heavyArmor.weight = 50f;
        heavyArmor.powerDraw = 0f;

        var overweightParts = new List<PartDataSO> { lightParts[0], lightParts[1], lightParts[2], heavyArmor };
        var weightResult2 = CompatibilityChecker.CheckWeight(HullClass.Scout, overweightParts);
        Assert(!weightResult2.isValid && weightResult2.violations.Length > 0, "CheckWeight Scout overweight fails");
        Assert(!weightResult2.isValid && weightResult2.violations[0].Contains("초과"), "CheckWeight message contains 초과");

        // ===== 3. CheckPower — 엔진 없음 (Scout 요구 80) =====
        var noEngineList = new List<PartDataSO> { scoutTurret, lightGun };
        var powerResult1 = CompatibilityChecker.CheckPower(HullClass.Scout, noEngineList);
        Assert(!powerResult1.isValid, "CheckPower Scout no engine fails");

        // ===== 4. CheckPower — 출력 부족 (Scout 엔진 80 공급, 포탑 2 + 주포 1 + 차체 80 = 83 요구) =====
        var weakEngine = ScriptableObject.CreateInstance<EnginePartSO>();
        weakEngine.weight = 10f;
        weakEngine.powerOutput = 80f;  // Scout 요구
        weakEngine.powerDraw = 0f;

        var demandingTurret = ScriptableObject.CreateInstance<TurretPartSO>();
        demandingTurret.weight = 8f;
        demandingTurret.powerDraw = 5f;  // 높은 수요

        var demandingGun = ScriptableObject.CreateInstance<MainGunPartSO>();
        demandingGun.weight = 5f;
        demandingGun.powerDraw = 10f;  // 높은 수요
        demandingGun.caliber = 45;

        var underpoweredList = new List<PartDataSO> { weakEngine, demandingTurret, demandingGun };
        var powerResult2 = CompatibilityChecker.CheckPower(HullClass.Scout, underpoweredList);
        // Scout 요구 80 + turret 5 + gun 10 = 95 필요, 엔진 80 공급 → 부족
        Assert(!powerResult2.isValid, "CheckPower Scout underpowered fails");
        Assert(!powerResult2.isValid && powerResult2.violations[0].Contains("부족"), "CheckPower message contains 부족");

        // ===== 5. CheckSpec — 주포 구경 초과 (turret caliberLimit 45, mainGun caliber 75) =====
        var smallTurret = ScriptableObject.CreateInstance<TurretPartSO>();
        smallTurret.caliberLimit = 45;

        var largeCaliber = ScriptableObject.CreateInstance<MainGunPartSO>();
        largeCaliber.caliber = 75;

        var specResult1 = CompatibilityChecker.CheckSpec(HullClass.Scout, smallTurret, largeCaliber, null);
        Assert(!specResult1.isValid && specResult1.violations.Length > 0, "CheckSpec caliber mismatch fails");
        Assert(!specResult1.isValid && specResult1.violations[0].Contains("구경"), "CheckSpec message contains 구경");

        // ===== 6. CheckSpec — AmmoRack 차체 제약 (hullClassRestrictions=[Heavy] but hull=Scout) =====
        var heavyAmmoRack = ScriptableObject.CreateInstance<AmmoRackPartSO>();
        heavyAmmoRack.hullClassRestrictions = new string[] { "Heavy" };

        var specResult2 = CompatibilityChecker.CheckSpec(HullClass.Scout, null, null, heavyAmmoRack);
        Assert(!specResult2.isValid && specResult2.violations.Length > 0, "CheckSpec ammo restriction fails");
        Assert(!specResult2.isValid && specResult2.violations[0].Contains("탄약고"), "CheckSpec message contains 탄약고");

        // ===== 7. CheckAll — 모든 조건 통과 =====
        var validEngine = ScriptableObject.CreateInstance<EnginePartSO>();
        validEngine.weight = 12f;
        validEngine.powerOutput = 100f;
        validEngine.powerDraw = 0f;

        var validTurret = ScriptableObject.CreateInstance<TurretPartSO>();
        validTurret.weight = 10f;
        validTurret.powerDraw = 2f;
        validTurret.caliberLimit = 100;

        var validGun = ScriptableObject.CreateInstance<MainGunPartSO>();
        validGun.weight = 15f;
        validGun.powerDraw = 3f;
        validGun.caliber = 75;

        var validAmmo = ScriptableObject.CreateInstance<AmmoRackPartSO>();
        validAmmo.weight = 8f;
        validAmmo.powerDraw = 1f;
        validAmmo.hullClassRestrictions = System.Array.Empty<string>(); // 제약 없음

        var allValidParts = new List<PartDataSO> { validEngine, validTurret, validGun, validAmmo };
        var allResult = CompatibilityChecker.CheckAll(
            HullClass.Assault,
            validTurret,
            validGun,
            validAmmo,
            allValidParts);
        // Assault: 하중 100, 출력 100
        // 총중량: 12+10+15+8 = 45kg < 100 ✓
        // 출력: 100 >= 100 (hull) + 2+3+1 = 100 >= 106 ✗ 부족할 수도
        // 실제로는 (powerm sum = 6, hull req = 100) → 100 >= 106은 false
        // 그래서 이 케이스는 실패할 것. 재작성.
        // 더 간단한 통과 케이스를 만들자.

        var powerfulEngine = ScriptableObject.CreateInstance<EnginePartSO>();
        powerfulEngine.weight = 14f;
        powerfulEngine.powerOutput = 150f;  // Assault 요구 100 충분
        powerfulEngine.powerDraw = 0f;

        var safeTurret = ScriptableObject.CreateInstance<TurretPartSO>();
        safeTurret.weight = 8f;
        safeTurret.powerDraw = 1f;
        safeTurret.caliberLimit = 100;

        var safeGun = ScriptableObject.CreateInstance<MainGunPartSO>();
        safeGun.weight = 12f;
        safeGun.powerDraw = 2f;
        safeGun.caliber = 75;

        var safeAmmo = ScriptableObject.CreateInstance<AmmoRackPartSO>();
        safeAmmo.weight = 6f;
        safeAmmo.powerDraw = 0f;
        safeAmmo.hullClassRestrictions = System.Array.Empty<string>();

        var goodParts = new List<PartDataSO> { powerfulEngine, safeTurret, safeGun, safeAmmo };
        var checkAllPass = CompatibilityChecker.CheckAll(
            HullClass.Assault,
            safeTurret,
            safeGun,
            safeAmmo,
            goodParts);
        // Assault: 하중 100, 출력 100
        // 총중량: 14+8+12+6 = 40kg < 100 ✓
        // 출력: 150 >= 100 + 1+2+0 = 103 ✓
        // 규격: 75 <= 100 ✓, ammo 제약 없음 ✓
        Assert(checkAllPass.isValid, "CheckAll valid combo passes");

        // ===== 정리 =====
        Object.DestroyImmediate(scoutEngine);
        Object.DestroyImmediate(scoutTurret);
        Object.DestroyImmediate(lightGun);
        Object.DestroyImmediate(heavyArmor);
        Object.DestroyImmediate(weakEngine);
        Object.DestroyImmediate(demandingTurret);
        Object.DestroyImmediate(demandingGun);
        Object.DestroyImmediate(smallTurret);
        Object.DestroyImmediate(largeCaliber);
        Object.DestroyImmediate(heavyAmmoRack);
        Object.DestroyImmediate(validEngine);
        Object.DestroyImmediate(validTurret);
        Object.DestroyImmediate(validGun);
        Object.DestroyImmediate(validAmmo);
        Object.DestroyImmediate(powerfulEngine);
        Object.DestroyImmediate(safeTurret);
        Object.DestroyImmediate(safeGun);
        Object.DestroyImmediate(safeAmmo);

        // ===== 결과 =====
        if (failed == 0)
            Log($"=== ALL PASS ({passed}/{passed}) ===");
        else
            Debug.LogError($"[P4B] === FAILED {failed} / {passed + failed} ===");
    }
}
