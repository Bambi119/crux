using UnityEngine;
using UnityEditor;
using Crux.Data;
using System.Collections.Generic;

/// <summary>
/// P4-D Convoy Inventory Smoke Test — 부대 공용 파츠 재고 관리 검증.
/// docs/05 §4~5 기반. ConvoyInventory + TankInstance 간 파츠 이동·회수 통합.
/// 메뉴 Crux/Test/P4D Convoy Inventory 실행.
///
/// 시나리오:
/// 1. 빈 재고 생성 — TotalCount == 0, 각 카테고리 CountOf == 0
/// 2. Add 기본 — Engine 2개·Armor 3개 추가 → TotalCount=5, CountOf(Engine)=2
/// 3. Add null·중복 — null Add → false. 동일 instanceId 재Add → false
/// 4. Remove 성공 — 특정 instanceId Remove → 반환 non-null, TotalCount 감소, FindById null
/// 5. Remove 미존재 — 잘못된 id → null
/// 6. GetByCategory — 리스트 크기 확인, read-only 반환 확인
/// 7. EquipTo 성공 경로 — Scout TankInstance 생성, 재고에 최소 필수 파츠 세트 추가 후 순차 EquipTo → isValid=true, Tank.Validate PASS
/// 8. EquipTo 실패 원복 — 하중 초과 파츠 EquipTo → 실패 시 재고에 되돌아옴(FindById non-null)
/// 9. ReturnFrom — 장착된 Engine ReturnFrom → 재고로 회수, Tank.engine == null, 재고 CountOf(Engine) 증가
/// </summary>
public static class P4D_ConvoyInventoryTest
{
    [MenuItem("Crux/Test/P4D Convoy Inventory")]
    public static void Execute()
    {
        void Log(string msg) => Debug.Log($"[P4D] {msg}");
        void Fail(string msg) => Debug.LogError($"[P4D] FAIL — {msg}");

        int passed = 0;
        int failed = 0;

        void Assert(bool cond, string name)
        {
            if (cond) { passed++; Log($"OK {name}"); }
            else      { failed++; Fail(name); }
        }

        // ===== Helper: PartsDataSO 인메모리 생성 =====
        EnginePartSO CreateEnginePartSO(float weight = 12f, float powerOutput = 100f)
        {
            var so = ScriptableObject.CreateInstance<EnginePartSO>();
            so.weight = weight;
            so.powerOutput = powerOutput;
            so.powerDraw = 0f;
            return so;
        }

        TurretPartSO CreateTurretPartSO(float weight = 5f)
        {
            var so = ScriptableObject.CreateInstance<TurretPartSO>();
            so.weight = weight;
            so.powerDraw = 1f;
            so.caliberLimit = 50;
            return so;
        }

        MainGunPartSO CreateMainGunPartSO(float weight = 6f, int caliber = 45)
        {
            var so = ScriptableObject.CreateInstance<MainGunPartSO>();
            so.weight = weight;
            so.powerDraw = 1f;
            so.caliber = caliber;
            return so;
        }

        AmmoRackPartSO CreateAmmoRackPartSO(float weight = 5f)
        {
            var so = ScriptableObject.CreateInstance<AmmoRackPartSO>();
            so.weight = weight;
            so.powerDraw = 1f;
            return so;
        }

        ArmorPartSO CreateArmorPartSO(float weight = 8f)
        {
            var so = ScriptableObject.CreateInstance<ArmorPartSO>();
            so.weight = weight;
            so.powerDraw = 0f;
            return so;
        }

        // ===== 1. 빈 재고 생성 =====
        var convoy = new ConvoyInventory();
        Assert(convoy.TotalCount == 0, "Empty convoy TotalCount == 0");
        Assert(convoy.CountOf(PartCategory.Engine) == 0, "Empty convoy CountOf(Engine) == 0");
        Assert(convoy.CountOf(PartCategory.Armor) == 0, "Empty convoy CountOf(Armor) == 0");

        // ===== 2. Add 기본 =====
        var engineSO1 = CreateEnginePartSO();
        var engineSO2 = CreateEnginePartSO();
        var armorSO1 = CreateArmorPartSO();
        var armorSO2 = CreateArmorPartSO();
        var armorSO3 = CreateArmorPartSO();

        var engineInst1 = new PartInstance(engineSO1);
        var engineInst2 = new PartInstance(engineSO2);
        var armorInst1 = new PartInstance(armorSO1);
        var armorInst2 = new PartInstance(armorSO2);
        var armorInst3 = new PartInstance(armorSO3);

        Assert(convoy.Add(engineInst1), "Add engine 1 succeeds");
        Assert(convoy.Add(engineInst2), "Add engine 2 succeeds");
        Assert(convoy.Add(armorInst1), "Add armor 1 succeeds");
        Assert(convoy.Add(armorInst2), "Add armor 2 succeeds");
        Assert(convoy.Add(armorInst3), "Add armor 3 succeeds");

        Assert(convoy.TotalCount == 5, "TotalCount == 5 after 5 adds");
        Assert(convoy.CountOf(PartCategory.Engine) == 2, "CountOf(Engine) == 2");
        Assert(convoy.CountOf(PartCategory.Armor) == 3, "CountOf(Armor) == 3");

        // ===== 3. Add null·중복 =====
        Assert(!convoy.Add(null), "Add null returns false");
        var duplicateResult = convoy.Add(engineInst1);  // 이미 추가됨
        Assert(!duplicateResult, "Add duplicate instanceId returns false");

        // ===== 4. Remove 성공 =====
        var removedEngine = convoy.Remove(engineInst1.instanceId);
        Assert(removedEngine == engineInst1, "Remove returns correct instance");
        Assert(convoy.TotalCount == 4, "TotalCount == 4 after remove");
        Assert(convoy.CountOf(PartCategory.Engine) == 1, "CountOf(Engine) == 1 after remove");

        // ===== 5. Remove 미존재 =====
        var notFound = convoy.Remove("invalid-id");
        Assert(notFound == null, "Remove invalid id returns null");

        // ===== 6. GetByCategory =====
        var armorList = convoy.GetByCategory(PartCategory.Armor);
        Assert(armorList.Count == 3, "GetByCategory(Armor) count == 3");

        var engineList = convoy.GetByCategory(PartCategory.Engine);
        Assert(engineList.Count == 1, "GetByCategory(Engine) count == 1");

        // ===== 7. EquipTo 성공 경로 =====
        // Scout 전차 + 최소 필수 세트 (Engine, Turret, MainGun, AmmoRack)
        var scoutTank = new TankInstance("Scout-02", HullClass.Scout);

        var turretSO = CreateTurretPartSO();
        var gunSO = CreateMainGunPartSO();
        var ammoSO = CreateAmmoRackPartSO();

        var turretInst = new PartInstance(turretSO);
        var gunInst = new PartInstance(gunSO);
        var ammoInst = new PartInstance(ammoSO);

        // 재고에 필수 파츠 세트 추가 (engineInst2는 line 103에서 이미 추가됨)
        Assert(convoy.Add(turretInst), "Add turret for EquipTo test");
        Assert(convoy.Add(gunInst), "Add gun for EquipTo test");
        Assert(convoy.Add(ammoInst), "Add ammo for EquipTo test");

        // 순차 EquipTo
        var equipEngine = convoy.EquipTo(scoutTank, engineInst2.instanceId, PartCategory.Engine);
        Assert(equipEngine.isValid, "EquipTo Engine succeeds");
        Assert(scoutTank.engine == engineInst2, "Tank.engine assigned");
        Assert(convoy.FindById(engineInst2.instanceId) == null, "Equipped engine not in inventory");

        var equipTurret = convoy.EquipTo(scoutTank, turretInst.instanceId, PartCategory.Turret);
        Assert(equipTurret.isValid, "EquipTo Turret succeeds");
        Assert(scoutTank.turret == turretInst, "Tank.turret assigned");

        var equipGun = convoy.EquipTo(scoutTank, gunInst.instanceId, PartCategory.MainGun);
        Assert(equipGun.isValid, "EquipTo MainGun succeeds");
        Assert(scoutTank.mainGun == gunInst, "Tank.mainGun assigned");

        var equipAmmo = convoy.EquipTo(scoutTank, ammoInst.instanceId, PartCategory.AmmoRack);
        Assert(equipAmmo.isValid, "EquipTo AmmoRack succeeds");
        Assert(scoutTank.ammoRack == ammoInst, "Tank.ammoRack assigned");

        // Validate() 통과 확인
        var validateResult = scoutTank.Validate();
        Assert(validateResult.isValid, "Tank.Validate PASS after equipping all required");

        // ===== 8. EquipTo 실패 원복 =====
        var heavyArmorSO = ScriptableObject.CreateInstance<ArmorPartSO>();
        heavyArmorSO.weight = 200f;  // Scout 용량 60kg 초과
        heavyArmorSO.powerDraw = 0f;

        var heavyArmorInst = new PartInstance(heavyArmorSO);
        Assert(convoy.Add(heavyArmorInst), "Add heavy armor to inventory");

        var equipHeavy = convoy.EquipTo(scoutTank, heavyArmorInst.instanceId, PartCategory.Armor, 0);
        Assert(!equipHeavy.isValid, "EquipTo overweight armor fails");
        Assert(scoutTank.armor[0] == null, "Tank.armor[0] still null (rollback)");
        Assert(convoy.FindById(heavyArmorInst.instanceId) != null, "Heavy armor returned to inventory after failed equip");

        // ===== 9. ReturnFrom =====
        var returnedEngine = convoy.ReturnFrom(scoutTank, PartCategory.Engine);
        Assert(returnedEngine == engineInst2, "ReturnFrom returns correct instance");
        Assert(scoutTank.engine == null, "Tank.engine null after return");
        Assert(convoy.FindById(engineInst2.instanceId) != null, "Returned engine back in inventory");
        Assert(convoy.CountOf(PartCategory.Engine) == 1, "CountOf(Engine) == 1 after return");

        // ===== 정리 =====
        Object.DestroyImmediate(engineSO1);
        Object.DestroyImmediate(engineSO2);
        Object.DestroyImmediate(armorSO1);
        Object.DestroyImmediate(armorSO2);
        Object.DestroyImmediate(armorSO3);
        Object.DestroyImmediate(turretSO);
        Object.DestroyImmediate(gunSO);
        Object.DestroyImmediate(ammoSO);
        Object.DestroyImmediate(heavyArmorSO);

        // ===== 결과 =====
        if (failed == 0)
            Log($"=== ALL PASS ({passed}/{passed}) ===");
        else
            Debug.LogError($"[P4D] === FAILED {failed} / {passed + failed} ===");
    }
}
