using UnityEngine;
using UnityEditor;
using Crux.Data;
using System.Collections.Generic;

/// <summary>
/// P4-C Tank Instance Smoke Test — 편성 모델 검증.
/// docs/05 §1 기반. 런타임 TankInstance + PartInstance 슬롯 관리·호환성 통합.
/// 메뉴 Crux/Test/P4C Tank Instance 실행.
///
/// 시나리오:
/// 1. 생성 + 슬롯 초기화 — Scout hull, slotTable 유효성, armor/auxiliary 리스트 크기 확인
/// 2. 정상 장착 흐름 — Engine 장착 → TryEquip 성공, TotalPowerSupply 증가
/// 3. 카테고리 불일치 — Engine SO를 Turret 슬롯에 TryEquip → Fail
/// 4. 하중 초과 — 매우 무거운 파츠 장착 시도 → TryEquip Fail + 원복 확인
/// 5. Armor 슬롯 인덱스 — armor[0], armor[1]에 별도 장착 → TryEquip 성공
/// 6. 슬롯 인덱스 범위 초과 — armor[99] → Fail
/// 7. Unequip — 장착된 Engine Unequip → 반환값 non-null, 해당 슬롯 null
/// 8. Validate — 필수 슬롯 중 하나라도 null → violation 존재
/// </summary>
public static class P4C_TankInstanceTest
{
    [MenuItem("Crux/Test/P4C Tank Instance")]
    public static void Execute()
    {
        void Log(string msg) => Debug.Log($"[P4C] {msg}");
        void Fail(string msg) => Debug.LogError($"[P4C] FAIL — {msg}");

        int passed = 0;
        int failed = 0;

        void Assert(bool cond, string name)
        {
            if (cond) { passed++; Log($"OK {name}"); }
            else      { failed++; Fail(name); }
        }

        // ===== 1. 생성 + 슬롯 초기화 =====
        var scoutTank = new TankInstance("Scout-01", HullClass.Scout);
        Assert(scoutTank.hullClass == HullClass.Scout, "TankInstance creation hull");
        Assert(scoutTank.slotTable.armor == 3, "Scout armor slots = 3");
        Assert(scoutTank.slotTable.auxiliary == 1, "Scout auxiliary slots = 1");
        Assert(scoutTank.armor.Count == 3, "armor list size = 3");
        Assert(scoutTank.auxiliary.Count == 1, "auxiliary list size = 1");
        Assert(scoutTank.engine == null && scoutTank.turret == null, "initial slots null");

        // ===== 2. 정상 장착 흐름 — Engine =====
        var scoutEngine = ScriptableObject.CreateInstance<EnginePartSO>();
        scoutEngine.weight = 12f;
        scoutEngine.powerOutput = 100f;  // Scout 요구 80, 충분
        scoutEngine.powerDraw = 0f;

        var engineInstance = new PartInstance(scoutEngine);
        var engineResult = scoutTank.TryEquip(PartCategory.Engine, engineInstance);
        Assert(engineResult.isValid, "TryEquip Engine succeeds");
        Assert(scoutTank.engine == engineInstance, "Engine slot assigned");
        Assert(scoutTank.TotalPowerSupply == 100f, "TotalPowerSupply = 100");

        // ===== 2.5. 필수 슬롯 최소 장착 (Validate 테스트를 위해) =====
        var minimalTurret = ScriptableObject.CreateInstance<TurretPartSO>();
        minimalTurret.weight = 5f;
        minimalTurret.powerDraw = 1f;
        minimalTurret.caliberLimit = 50;

        var minimalGun = ScriptableObject.CreateInstance<MainGunPartSO>();
        minimalGun.weight = 6f;
        minimalGun.powerDraw = 1f;
        minimalGun.caliber = 45;

        var minimalAmmo = ScriptableObject.CreateInstance<AmmoRackPartSO>();
        minimalAmmo.weight = 5f;
        minimalAmmo.powerDraw = 1f;

        scoutTank.TryEquip(PartCategory.Turret, new PartInstance(minimalTurret));
        scoutTank.TryEquip(PartCategory.MainGun, new PartInstance(minimalGun));
        scoutTank.TryEquip(PartCategory.AmmoRack, new PartInstance(minimalAmmo));
        // Now Scout has: 12+5+6+5=28kg < 60 ✓, power 100 >= 80+1+1+1 = 83 ✓

        // ===== 3. 카테고리 불일치 =====
        var wrongResult = scoutTank.TryEquip(PartCategory.Turret, engineInstance);
        Assert(!wrongResult.isValid && wrongResult.violations.Length > 0, "TryEquip wrong category fails");
        Assert(scoutTank.turret != null, "Turret slot preserved after wrong category attempt");

        // ===== 4. 하중 초과 =====
        var heavyPart = ScriptableObject.CreateInstance<ArmorPartSO>();
        heavyPart.weight = 200f;  // Scout 한도 60kg, 기존 28kg + 200kg = 228kg > 60 실패
        heavyPart.powerDraw = 0f;

        var heavyInstance = new PartInstance(heavyPart);
        var overweightResult = scoutTank.TryEquip(PartCategory.Armor, heavyInstance, 0);
        Assert(!overweightResult.isValid, "TryEquip overweight fails");
        Assert(scoutTank.armor[0] == null, "Armor[0] still null after overweight (rollback)");
        Assert(scoutTank.engine == engineInstance, "Engine still equipped after failed armor");

        // ===== 5. Armor 슬롯 인덱스 =====
        var lightArmor = ScriptableObject.CreateInstance<ArmorPartSO>();
        lightArmor.weight = 8f;
        lightArmor.powerDraw = 0f;

        var lightArmor2 = ScriptableObject.CreateInstance<ArmorPartSO>();
        lightArmor2.weight = 7f;
        lightArmor2.powerDraw = 0f;

        var armorInst1 = new PartInstance(lightArmor);
        var armorInst2 = new PartInstance(lightArmor2);

        var armorResult1 = scoutTank.TryEquip(PartCategory.Armor, armorInst1, 0);
        var armorResult2 = scoutTank.TryEquip(PartCategory.Armor, armorInst2, 1);
        Assert(armorResult1.isValid && armorResult2.isValid, "TryEquip armor [0] and [1] succeed");
        Assert(scoutTank.armor[0] == armorInst1 && scoutTank.armor[1] == armorInst2, "Armor slots assigned");

        // ===== 6. 슬롯 인덱스 범위 초과 =====
        var excessResult = scoutTank.TryEquip(PartCategory.Armor, new PartInstance(lightArmor), 99);
        Assert(!excessResult.isValid && excessResult.violations[0].Contains("범위"), "TryEquip excess index fails");

        // ===== 7. Unequip =====
        var removedEngine = scoutTank.Unequip(PartCategory.Engine);
        Assert(removedEngine == engineInstance, "Unequip returns correct instance");
        Assert(scoutTank.engine == null, "Engine slot null after unequip");
        Assert(scoutTank.armor[0] == armorInst1, "Other slots unaffected");

        // ===== 8. Validate — 필수 슬롯 체크 =====
        var validateResult = scoutTank.Validate();
        Assert(!validateResult.isValid && validateResult.violations.Length > 0, "Validate fails with null Engine");
        Assert(validateResult.violations[0].Contains("Engine"), "Violation mentions Engine");

        // ===== 정리 =====
        Object.DestroyImmediate(scoutEngine);
        Object.DestroyImmediate(minimalTurret);
        Object.DestroyImmediate(minimalGun);
        Object.DestroyImmediate(minimalAmmo);
        Object.DestroyImmediate(heavyPart);
        Object.DestroyImmediate(lightArmor);
        Object.DestroyImmediate(lightArmor2);

        // ===== 결과 =====
        if (failed == 0)
            Log($"=== ALL PASS ({passed}/{passed}) ===");
        else
            Debug.LogError($"[P4C] === FAILED {failed} / {passed + failed} ===");
    }
}
