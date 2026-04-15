using UnityEngine;
using UnityEditor;
using Crux.Data;
using Crux.Unit;

/// <summary>
/// P2-A Batch Smoke Test — CrewMemberRuntime / MoraleSystem / TankCrew 로직 검증.
/// Editor 메뉴 Crux/Test/P2A Crew Runtime 실행 또는 execute_script(methodName=Execute).
/// 성공 조건: 전 단계 "OK" 로그, 실패 시 "FAIL" 로그 + 상세.
/// </summary>
public static class P2A_CrewRuntimeTest
{
    [MenuItem("Crux/Test/P2A Crew Runtime")]
    public static void Execute()
    {
        void Log(string msg) => Debug.Log($"[P2A] {msg}");
        void Fail(string msg) => Debug.LogError($"[P2A] FAIL — {msg}");

        int passed = 0;
        int failed = 0;

        void Assert(bool cond, string name)
        {
            if (cond) { passed++; Log($"OK {name}"); }
            else      { failed++; Fail(name); }
        }

        // ===== 1. MoraleSystem 밴드 경계 =====
        Assert(MoraleSystem.GetBand(100) == MoraleBand.High, "band(100)=High");
        Assert(MoraleSystem.GetBand(80)  == MoraleBand.High, "band(80)=High");
        Assert(MoraleSystem.GetBand(79)  == MoraleBand.Normal, "band(79)=Normal");
        Assert(MoraleSystem.GetBand(50)  == MoraleBand.Normal, "band(50)=Normal");
        Assert(MoraleSystem.GetBand(49)  == MoraleBand.Shaken, "band(49)=Shaken");
        Assert(MoraleSystem.GetBand(25)  == MoraleBand.Shaken, "band(25)=Shaken");
        Assert(MoraleSystem.GetBand(24)  == MoraleBand.Panic, "band(24)=Panic");
        Assert(MoraleSystem.GetBand(0)   == MoraleBand.Panic, "band(0)=Panic");

        // ===== 2. MoraleSystem 델타 테이블 =====
        Assert(MoraleSystem.DefaultDelta(MoraleEvent.EnemyKilled) == +5, "delta.EnemyKilled=+5");
        Assert(MoraleSystem.DefaultDelta(MoraleEvent.AmmoRackNear) == -20, "delta.AmmoRackNear=-20");
        Assert(MoraleSystem.DefaultDelta(MoraleEvent.CrewInjured) == -15, "delta.CrewInjured=-15");
        Assert(MoraleSystem.DefaultDelta(MoraleEvent.SideRearHit) == -10, "delta.SideRearHit=-10");

        // ===== 3. MoraleSystem 페널티 조회 =====
        Assert(MoraleSystem.AimModifier(MoraleBand.High) == +5, "aimMod.High=+5");
        Assert(MoraleSystem.AimModifier(MoraleBand.Panic) == -15, "aimMod.Panic=-15");
        Assert(MoraleSystem.TurnApPenalty(MoraleBand.Panic) == 1, "apPenalty.Panic=1");
        Assert(MoraleSystem.TurnApPenalty(MoraleBand.Normal) == 0, "apPenalty.Normal=0");
        Assert(MoraleSystem.ForbidsActiveSkills(MoraleBand.Panic) == true, "forbidActives.Panic");
        Assert(MoraleSystem.ForbidsActiveSkills(MoraleBand.Shaken) == false, "forbidActives.Shaken");
        Assert(Mathf.Approximately(MoraleSystem.ReactionFailChance(MoraleBand.Shaken), 0.2f), "reactFail.Shaken=0.2");

        // ===== 4. CrewMemberRuntime 기본값 =====
        var astra = AssetDatabase.LoadAssetAtPath<CrewMemberSO>("Assets/_Project/Data/Crew/Members/Crew_astra.asset");
        Assert(astra != null, "load Crew_astra.asset");
        if (astra == null)
        {
            Debug.LogError($"[P2A] test aborted — assets not found. passed={passed} failed={failed + 1}");
            return;
        }

        var rt = new CrewMemberRuntime(astra);
        Assert(rt.data == astra, "runtime.data ref");
        Assert(rt.Class == CrewClass.Commander, "astra.Class=Commander");
        Assert(rt.BaseAim == astra.aim, "runtime.BaseAim maps to SO");
        Assert(rt.injuryState == InjuryLevel.None, "injury default None");
        Assert(rt.IsCombatReady, "default IsCombatReady");

        // 부상 → 공석 취급
        rt.injuryState = InjuryLevel.Severe;
        Assert(!rt.IsCombatReady, "severe → not combat ready");
        rt.injuryState = InjuryLevel.None;

        // ===== 5. 마크 보너스 테이블 =====
        Assert(CrewMemberRuntime.MarkBonus(0) == 0, "markBonus(0)=0");
        Assert(CrewMemberRuntime.MarkBonus(1) == 3, "markBonus(1)=3");
        Assert(CrewMemberRuntime.MarkBonus(3) == 10, "markBonus(3)=10");
        Assert(CrewMemberRuntime.MarkBonus(5) == 20, "markBonus(5)=20");

        // ===== 6. 쿨다운 =====
        rt.SetCooldown("test_skill", 3);
        Assert(rt.IsOnCooldown("test_skill"), "cd set");
        Assert(rt.GetCooldown("test_skill") == 3, "cd=3");
        rt.TickCooldowns();
        Assert(rt.GetCooldown("test_skill") == 2, "cd→2 after tick");
        rt.TickCooldowns();
        rt.TickCooldowns();
        Assert(!rt.IsOnCooldown("test_skill"), "cd→0 removed");

        // ===== 7. TankCrew 생성 + 사기 초기값 =====
        var crewGO = new GameObject("P2A_TankCrew_Test");
        try
        {
            var tc = crewGO.AddComponent<TankCrew>();

            var ririd = AssetDatabase.LoadAssetAtPath<CrewMemberSO>("Assets/_Project/Data/Crew/Members/Crew_ririd.asset");
            var grin = AssetDatabase.LoadAssetAtPath<CrewMemberSO>("Assets/_Project/Data/Crew/Members/Crew_grin.asset");
            var pretena = AssetDatabase.LoadAssetAtPath<CrewMemberSO>("Assets/_Project/Data/Crew/Members/Crew_pretena.asset");
            var iris = AssetDatabase.LoadAssetAtPath<CrewMemberSO>("Assets/_Project/Data/Crew/Members/Crew_iris.asset");

            tc.Initialize(astra, ririd, grin, pretena, iris, commanderHullClassAxis: null);
            Assert(tc.Morale == 50, "init morale=50 (no commander bonus axis)");
            Assert(tc.Band == MoraleBand.Normal, "init band=Normal");
            Assert(!tc.PanicSafetyUsed, "init panicSafety=false");
            Assert(tc.commander != null && tc.commander.Class == CrewClass.Commander, "commander slot");
            Assert(tc.gunner != null && tc.gunner.Class == CrewClass.Gunner, "gunner slot");

            // 공석 판정 — 없는 슬롯 (astra만 있는 가상 전차 시나리오)
            tc.Initialize(astra, null, null, null, null);
            Assert(tc.IsVacant(CrewClass.Gunner), "vacant gunner");
            Assert(!tc.IsVacant(CrewClass.Commander), "commander not vacant");

            // ===== 8. 사기 이벤트 적용 =====
            tc.Initialize(astra, ririd, grin, pretena, iris);
            int initial = tc.Morale;
            tc.ApplyMoraleEvent(MoraleEvent.EnemyKilled);
            Assert(tc.Morale == initial + 5, "morale +5 after kill");

            tc.ApplyMoraleEvent(MoraleEvent.AmmoRackNear);
            Assert(tc.Morale == initial + 5 - 20, "morale after ammo rack near");

            // ===== 9. 공황 안전장치 — 정상에서 공황 진입 시 +15 =====
            tc.Initialize(astra, ririd, grin, pretena, iris);
            Assert(tc.Morale == 50, "reset morale=50");
            tc.SetMorale(15); // 정상 → 공황 진입
            // 안전장치 발동 → 15 + 15 = 30 (흔들림 구간)
            Assert(tc.Morale == 30, $"panic safety kick to 30 (got {tc.Morale})");
            Assert(tc.PanicSafetyUsed, "panic safety used");
            Assert(tc.Band == MoraleBand.Shaken, "safety recover band=Shaken");

            // 두 번째 공황 진입 — 더 이상 안전장치 안 됨
            tc.SetMorale(10);
            Assert(tc.Morale == 10, "second panic no safety (10)");
            Assert(tc.Band == MoraleBand.Panic, "band=Panic second");

            // ResetForNextBattle
            tc.ResetForNextBattle();
            Assert(!tc.PanicSafetyUsed, "safety reset for next battle");

            // ===== 10. TickTurnStart — 쿨다운 감소 =====
            tc.Initialize(astra, ririd, grin, pretena, iris);
            tc.gunner.SetCooldown("precision", 2);
            tc.TickTurnStart();
            Assert(tc.gunner.GetCooldown("precision") == 1, "cd tick on turn start");
        }
        finally
        {
            Object.DestroyImmediate(crewGO);
        }

        // ===== 결과 =====
        if (failed == 0)
            Log($"=== ALL PASS ({passed}/{passed}) ===");
        else
            Debug.LogError($"[P2A] === FAILED {failed} / {passed + failed} ===");
    }
}
