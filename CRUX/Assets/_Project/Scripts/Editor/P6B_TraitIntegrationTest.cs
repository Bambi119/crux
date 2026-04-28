using UnityEngine;
using UnityEditor;
using Crux.Data;
using Crux.Unit;
using Crux.Core;

/// <summary>
/// P6B Batch Smoke Test — Trait 통합 (moraleFloor + InitiativeSetup, 누적 카운트 기반 모델).
/// 목적: P6A의 TraitEffects 단위 테스트에서 놓친 통합 회로 검증.
/// (1) TankCrew.Initialize 에서 5인 traits[] 배열 moraleFloor 합산 반영
/// (2) InitiativeSetup.BuildForTest 가 traits[] 을 실제로 반영
/// Editor 메뉴 Crux/Test/P6B Trait Integration 실행.
/// </summary>
public static class P6B_TraitIntegrationTest
{
    [MenuItem("Crux/Test/P6B Trait Integration")]
    public static void Execute()
    {
        void Log(string msg) => Debug.Log($"[P6B] {msg}");
        void Fail(string msg) => Debug.LogError($"[P6B] FAIL — {msg}");

        int passed = 0;
        int failed = 0;

        void Assert(bool cond, string name)
        {
            if (cond) { passed++; Log($"OK {name}"); }
            else      { failed++; Fail(name); }
        }

        // ===== 1. TankCrew 시작 사기에 traits[] 배열 moraleFloor 반영 =====

        // 시나리오 1-1: 모든 crew null → moraleFloor 0, morale=50
        var crewGO1 = new GameObject("P6B_TankCrew1");
        try
        {
            var tc1 = crewGO1.AddComponent<TankCrew>();
            tc1.Initialize(null, null, null, null, null);
            Assert(tc1.Morale == 50, $"empty crew morale=50 (got {tc1.Morale})");
        }
        finally { Object.DestroyImmediate(crewGO1); }

        // 시나리오 1-2: commander 가 wordless_comrade(+5) 가짐
        var crewGO2 = new GameObject("P6B_TankCrew2");
        try
        {
            var tc2 = crewGO2.AddComponent<TankCrew>();
            var cmdrWordless = MakeCrew("commander_wordless", CrewClass.Commander,
                traits: new[] { MakeTrait("wordless_comrade") });
            tc2.Initialize(cmdrWordless, null, null, null, null);
            // commanderMark=0 (no axis), traitFloor=+5 → 50+0+5=55
            Assert(tc2.Morale == 55, $"commander wordless_comrade morale=55 (got {tc2.Morale})");
            CleanupCrew(cmdrWordless);
        }
        finally { Object.DestroyImmediate(crewGO2); }

        // 시나리오 1-3: 5인 모두 traits[0]에 단일 trait 가짐. 현재 TraitModifier 테이블에 음수 지원 안 함.
        // 대신 hermit_eye (aim +5) 사용 — morale 영향 없음 (예시용)
        var crewGO3 = new GameObject("P6B_TankCrew3");
        try
        {
            var tc3 = crewGO3.AddComponent<TankCrew>();
            var hermitTrait = MakeTrait("hermit_eye");

            var c1 = MakeCrew("hermit1", CrewClass.Commander, traits: new[] { hermitTrait });
            var c2 = MakeCrew("hermit2", CrewClass.Gunner, traits: new[] { hermitTrait });
            var c3 = MakeCrew("hermit3", CrewClass.Loader, traits: new[] { hermitTrait });
            var c4 = MakeCrew("hermit4", CrewClass.Driver, traits: new[] { hermitTrait });
            var c5 = MakeCrew("hermit5", CrewClass.GunnerMech, traits: new[] { hermitTrait });

            tc3.Initialize(c1, c2, c3, c4, c5);
            // commanderMark=0, traitFloor=0 (hermit_eye 무 morale) → 50+0=50
            Assert(tc3.Morale == 50, $"all hermit morale=50 (got {tc3.Morale})");

            CleanupCrew(c1); CleanupCrew(c2); CleanupCrew(c3); CleanupCrew(c4); CleanupCrew(c5);
            // hermitTrait는 재사용되므로 마지막만 cleanup
        }
        finally { Object.DestroyImmediate(crewGO3); }

        // 시나리오 1-4: 혼합 — commander rocinante_owner(+5 morale) + gunner silent_worker(0 morale) → +5
        var crewGO4 = new GameObject("P6B_TankCrew4");
        try
        {
            var tc4 = crewGO4.AddComponent<TankCrew>();
            var rocinante = MakeTrait("rocinante_owner");
            var silent = MakeTrait("silent_worker");

            var cmdr = MakeCrew("roc_cmdr", CrewClass.Commander, traits: new[] { rocinante });
            var gun = MakeCrew("silent_gun", CrewClass.Gunner, traits: new[] { silent });

            tc4.Initialize(cmdr, gun, null, null, null);
            // commanderMark=0, traitFloor=+5+0=+5 → 50+5=55
            Assert(tc4.Morale == 55, $"rocinante+silent morale=55 (got {tc4.Morale})");

            CleanupCrew(cmdr); CleanupCrew(gun);
        }
        finally { Object.DestroyImmediate(crewGO4); }

        // ===== 2. InitiativeSetup.BuildForTest 가 traits[] 을 react/traitBonus 에 반영 =====

        // 시나리오 2-1: commander 없음 → traitBonus=0, react=0
        var unitGO1 = new GameObject("P6B_Unit1");
        try
        {
            var tank1 = unitGO1.AddComponent<GridTankUnit>();
            var data1 = ScriptableObject.CreateInstance<TankDataSO>();
            data1.tankName = "P6B_NoCommander";
            data1.hullClass = HullClass.Scout;
            tank1.tankData = data1;
            tank1.side = PlayerSide.Player;

            var crew1 = unitGO1.AddComponent<TankCrew>();
            crew1.Initialize(null, null, null, null, null);
            tank1.BindCrew(crew1);

            // 모두 null이므로 traitBonus=0, react=0 (기본)
            var input1 = InitiativeSetup.BuildForTest(tank1);
            Assert(input1.traitBonus == 0, $"no commander traitBonus=0 (got {input1.traitBonus})");
            Assert(input1.react == 0, $"no commander react=0 (got {input1.react})");

            ScriptableObject.DestroyImmediate(data1);
        }
        finally { Object.DestroyImmediate(unitGO1); }

        // 시나리오 2-2: commander 가 donquixote_dream(initiativeBonus +2) → traitBonus=+2, react=base
        var unitGO2 = new GameObject("P6B_Unit2");
        try
        {
            var tank2 = unitGO2.AddComponent<GridTankUnit>();
            var data2 = ScriptableObject.CreateInstance<TankDataSO>();
            data2.tankName = "P6B_Donquixote";
            data2.hullClass = HullClass.Scout;
            tank2.tankData = data2;
            tank2.side = PlayerSide.Player;

            var donqSO = MakeCrew("donq_cmdr", CrewClass.Commander,
                traits: new[] { MakeTrait("donquixote_dream") });
            donqSO.react = 50; // 베이스 react

            var crew2 = unitGO2.AddComponent<TankCrew>();
            crew2.Initialize(donqSO, null, null, null, null);
            tank2.BindCrew(crew2);

            var input2 = InitiativeSetup.BuildForTest(tank2);
            Assert(input2.traitBonus == 2, $"donquixote_dream traitBonus=+2 (got {input2.traitBonus})");
            Assert(input2.react == 50, $"react base 50, no reactBonus (got {input2.react})");

            CleanupCrew(donqSO);
            ScriptableObject.DestroyImmediate(data2);
        }
        finally { Object.DestroyImmediate(unitGO2); }

        // 시나리오 2-3: commander 가 little_hand_prodigy(reactBonus +2) → react=base+2, traitBonus=0
        var unitGO3 = new GameObject("P6B_Unit3");
        try
        {
            var tank3 = unitGO3.AddComponent<GridTankUnit>();
            var data3 = ScriptableObject.CreateInstance<TankDataSO>();
            data3.tankName = "P6B_Prodigy";
            data3.hullClass = HullClass.Scout;
            tank3.tankData = data3;
            tank3.side = PlayerSide.Player;

            var prodigySO = MakeCrew("prodigy_cmdr", CrewClass.Commander,
                traits: new[] { MakeTrait("little_hand_prodigy") });
            prodigySO.react = 60; // 베이스 react

            var crew3 = unitGO3.AddComponent<TankCrew>();
            crew3.Initialize(prodigySO, null, null, null, null);
            tank3.BindCrew(crew3);

            var input3 = InitiativeSetup.BuildForTest(tank3);
            Assert(input3.react == 62, $"little_hand_prodigy react=62 (60+2) (got {input3.react})");
            Assert(input3.traitBonus == 0, $"little_hand_prodigy no init bonus (got {input3.traitBonus})");

            CleanupCrew(prodigySO);
            ScriptableObject.DestroyImmediate(data3);
        }
        finally { Object.DestroyImmediate(unitGO3); }

        // 시나리오 2-4: traits[0]=donquixote_dream + traits[1]=wordless_comrade (다중 trait 누적)
        // → traitBonus=+2, moraleFloor=+5 → morale=55
        var unitGO4 = new GameObject("P6B_Unit4");
        try
        {
            var tank4 = unitGO4.AddComponent<GridTankUnit>();
            var data4 = ScriptableObject.CreateInstance<TankDataSO>();
            data4.tankName = "P6B_Mixed";
            data4.hullClass = HullClass.Scout;
            tank4.tankData = data4;
            tank4.side = PlayerSide.Player;

            var multiTraitSO = MakeCrew("multi_cmdr", CrewClass.Commander,
                traits: new[] { MakeTrait("donquixote_dream"), MakeTrait("wordless_comrade") });
            multiTraitSO.react = 50;

            var crew4 = unitGO4.AddComponent<TankCrew>();
            crew4.Initialize(multiTraitSO, null, null, null, null);
            tank4.BindCrew(crew4);

            var input4 = InitiativeSetup.BuildForTest(tank4);
            Assert(input4.traitBonus == 2, $"donquixote_dream traitBonus=+2 (got {input4.traitBonus})");
            // TankCrew morale: 50 + commanderMark(0)*3 + moraleFloor(donq=0, wordless=+5) = 55
            Assert(crew4.Morale == 55, $"donquixote+wordless TankCrew morale=55 (got {crew4.Morale})");

            CleanupCrew(multiTraitSO);
            ScriptableObject.DestroyImmediate(data4);
        }
        finally { Object.DestroyImmediate(unitGO4); }

        // ===== 결과 =====
        if (failed == 0)
            Log($"=== ALL PASS ({passed}/{passed}) ===");
        else
            Fail($"=== FAILED {failed}/{passed + failed} ===");
    }

    static TraitSO MakeTrait(string id)
    {
        var t = ScriptableObject.CreateInstance<TraitSO>();
        t.id = id;
        t.displayName = id;
        t.axisType = TraitAxis.None;
        t.axisThreshold = 10;
        return t;
    }

    static CrewMemberSO MakeCrew(string name, CrewClass kls, TraitSO[] traits)
    {
        var c = ScriptableObject.CreateInstance<CrewMemberSO>();
        c.displayName = name;
        c.klass = kls;
        c.traits = traits ?? new TraitSO[0];
        c.react = 50; // 기본값
        return c;
    }

    static void CleanupCrew(CrewMemberSO c)   { if (c != null) ScriptableObject.DestroyImmediate(c); }
    static void CleanupTrait(TraitSO t)       { if (t != null) ScriptableObject.DestroyImmediate(t); }
}
