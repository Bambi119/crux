using UnityEngine;
using UnityEditor;
using Crux.Data;
using Crux.UI;

/// <summary>
/// P2-B Batch Smoke Test — HullClass enum / HullSlotTable / HullClassDefaults / UI label 매핑 검증.
/// 메뉴 Crux/Test/P2B Hull Data 실행.
/// </summary>
public static class P2B_HullDataTest
{
    [MenuItem("Crux/Test/P2B Hull Data")]
    public static void Execute()
    {
        void Log(string msg) => Debug.Log($"[P2B] {msg}");
        void Fail(string msg) => Debug.LogError($"[P2B] FAIL — {msg}");

        int passed = 0;
        int failed = 0;

        void Assert(bool cond, string name)
        {
            if (cond) { passed++; Log($"OK {name}"); }
            else      { failed++; Fail(name); }
        }

        // ===== 1. HullClass enum 값 확인 =====
        Assert(System.Enum.IsDefined(typeof(HullClass), HullClass.Scout), "HullClass.Scout defined");
        Assert(System.Enum.IsDefined(typeof(HullClass), HullClass.Assault), "HullClass.Assault defined");
        Assert(System.Enum.IsDefined(typeof(HullClass), HullClass.Support), "HullClass.Support defined");
        Assert(System.Enum.IsDefined(typeof(HullClass), HullClass.Heavy), "HullClass.Heavy defined");
        Assert(System.Enum.IsDefined(typeof(HullClass), HullClass.Siege), "HullClass.Siege defined");
        Assert(System.Enum.GetValues(typeof(HullClass)).Length == 5, "HullClass has 5 values");

        // ===== 2. HullClassDefaults — 하중 (docs/05 §7.1) =====
        Assert(HullClassDefaults.WeightCapacityFor(HullClass.Scout) == 60, "weight.Scout=60");
        Assert(HullClassDefaults.WeightCapacityFor(HullClass.Assault) == 100, "weight.Assault=100");
        Assert(HullClassDefaults.WeightCapacityFor(HullClass.Support) == 110, "weight.Support=110");
        Assert(HullClassDefaults.WeightCapacityFor(HullClass.Heavy) == 160, "weight.Heavy=160");
        Assert(HullClassDefaults.WeightCapacityFor(HullClass.Siege) == 220, "weight.Siege=220");

        // ===== 3. HullClassDefaults — 출력 (docs/05 §7.1) =====
        Assert(HullClassDefaults.PowerRequirementFor(HullClass.Scout) == 80, "power.Scout=80");
        Assert(HullClassDefaults.PowerRequirementFor(HullClass.Assault) == 100, "power.Assault=100");
        Assert(HullClassDefaults.PowerRequirementFor(HullClass.Support) == 95, "power.Support=95");
        Assert(HullClassDefaults.PowerRequirementFor(HullClass.Heavy) == 120, "power.Heavy=120");
        Assert(HullClassDefaults.PowerRequirementFor(HullClass.Siege) == 150, "power.Siege=150");

        // ===== 4. HullSlotTable — 차체별 슬롯 수 (docs/05 §1.1) =====
        var scoutT = HullSlotTable.ForHull(HullClass.Scout);
        Assert(scoutT.armor == 3 && scoutT.auxiliary == 1 && scoutT.mainGun == 1, "slot.Scout 3/1/1");

        var assaultT = HullSlotTable.ForHull(HullClass.Assault);
        Assert(assaultT.armor == 4 && assaultT.auxiliary == 2 && assaultT.mainGun == 1, "slot.Assault 4/2/1");

        var supportT = HullSlotTable.ForHull(HullClass.Support);
        Assert(supportT.armor == 4 && supportT.auxiliary == 4 && supportT.mainGun == 1, "slot.Support 4/4/1");

        var heavyT = HullSlotTable.ForHull(HullClass.Heavy);
        Assert(heavyT.armor == 6 && heavyT.auxiliary == 3 && heavyT.mainGun == 1, "slot.Heavy 6/3/1");

        var siegeT = HullSlotTable.ForHull(HullClass.Siege);
        Assert(siegeT.armor == 6 && siegeT.mainGun == 2 && siegeT.ammoRack == 2, "slot.Siege 6/2/2");

        // ===== 5. IsEmpty 검증 =====
        Assert(new HullSlotTable().IsEmpty(), "empty struct IsEmpty");
        Assert(!scoutT.IsEmpty(), "populated !IsEmpty");

        // ===== 6. TankDataSO OnValidate — 기본값 자동 채움 =====
        var tank = ScriptableObject.CreateInstance<TankDataSO>();
        try
        {
            tank.hullClass = HullClass.Heavy;
            tank.weightCapacity = 0;
            tank.powerRequirement = 0;
            tank.slotTable = new HullSlotTable(); // empty

            // OnValidate 강제 호출
            var mi = typeof(TankDataSO).GetMethod("OnValidate",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (mi != null)
            {
                mi.Invoke(tank, null);
                Assert(tank.weightCapacity == 160, $"OnValidate Heavy weight=160 (got {tank.weightCapacity})");
                Assert(tank.powerRequirement == 120, $"OnValidate Heavy power=120 (got {tank.powerRequirement})");
                Assert(tank.slotTable.armor == 6, $"OnValidate Heavy armor slot=6 (got {tank.slotTable.armor})");
                Assert(tank.isRocinante == false, "isRocinante default false");
            }
            else
            {
                Log("skip OnValidate reflection (not editor)");
            }

            // hullClass 변경 + weight 명시 설정 시 명시값 유지 (0이 아님)
            tank.hullClass = HullClass.Scout;
            tank.weightCapacity = 999;   // 명시값
            tank.powerRequirement = 0;   // 미설정
            if (mi != null)
            {
                mi.Invoke(tank, null);
                Assert(tank.weightCapacity == 999, "OnValidate preserves explicit weight");
                Assert(tank.powerRequirement == 80, "OnValidate fills Scout power=80 when 0");
            }
        }
        finally
        {
            Object.DestroyImmediate(tank);
        }

        // ===== 7. BattleHUD 라벨 매핑 =====
        Assert(BattleHUD.GetHullClassLabelStatic(HullClass.Scout) == "경전차", "label.Scout=경전차");
        Assert(BattleHUD.GetHullClassLabelStatic(HullClass.Assault) == "중형전차", "label.Assault=중형전차");
        Assert(BattleHUD.GetHullClassLabelStatic(HullClass.Support) == "지원전차", "label.Support=지원전차");
        Assert(BattleHUD.GetHullClassLabelStatic(HullClass.Heavy) == "중전차", "label.Heavy=중전차");
        Assert(BattleHUD.GetHullClassLabelStatic(HullClass.Siege) == "초중전차", "label.Siege=초중전차");

        // ===== 결과 =====
        if (failed == 0)
            Log($"=== ALL PASS ({passed}/{passed}) ===");
        else
            Debug.LogError($"[P2B] === FAILED {failed} / {passed + failed} ===");
    }
}
