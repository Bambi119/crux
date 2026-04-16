using UnityEngine;
using UnityEditor;
using Crux.Data;

/// <summary>
/// P6 Batch Smoke Test — TraitEffects / TraitModifier 로직 검증.
/// Editor 메뉴 Crux/Test/P6 Trait Effects 실행 또는 execute_script(methodName=Execute).
/// 성공 조건: 전 단계 "OK" 로그, 실패 시 "FAIL" 로그 + 상세.
/// </summary>
public static class P6_TraitEffectsTest
{
    [MenuItem("Crux/Test/P6 Trait Effects")]
    public static void Execute()
    {
        void Log(string msg) => Debug.Log($"[P6] {msg}");
        void Fail(string msg) => Debug.LogError($"[P6] FAIL — {msg}");

        int passed = 0;
        int failed = 0;

        void Assert(bool cond, string name)
        {
            if (cond) { passed++; Log($"OK {name}"); }
            else      { failed++; Fail(name); }
        }

        // ===== 1. 빈 effectKey =====
        Assert(TraitEffects.Get((string)null).initiativeBonus == 0, "Get(null).init=0");
        Assert(TraitEffects.Get("").aimBonus == 0, "Get(\"\").aim=0");
        Assert(TraitEffects.Get(null).reactBonus == 0, "Get(null).react=0");
        Assert(TraitEffects.Get(null).moraleFloor == 0, "Get(null).morale=0");

        // ===== 2. 미등록 effectKey =====
        var unknown = TraitEffects.Get("trait.nonexistent");
        Assert(unknown.initiativeBonus == 0, "Get(nonexistent).init=0");
        Assert(unknown.aimBonus == 0, "Get(nonexistent).aim=0");
        Assert(unknown.reactBonus == 0, "Get(nonexistent).react=0");
        Assert(unknown.moraleFloor == 0, "Get(nonexistent).morale=0");

        // ===== 3. 등록된 effectKey 확인 =====
        var hermit = TraitEffects.Get("trait.hermit_eye");
        Assert(hermit.aimBonus == +5, "hermit_eye.aim=+5");
        Assert(hermit.initiativeBonus == 0, "hermit_eye.init=0");

        var donquixote = TraitEffects.Get("trait.donquixote_dream");
        Assert(donquixote.initiativeBonus == +2, "donquixote.init=+2");
        Assert(donquixote.aimBonus == -3, "donquixote.aim=-3");

        var fear = TraitEffects.Get("trait.first_battle_fear");
        Assert(fear.initiativeBonus == -3, "fear.init=-3");
        Assert(fear.moraleFloor == -10, "fear.morale=-10");

        var prodigy = TraitEffects.Get("trait.little_hand_prodigy");
        Assert(prodigy.reactBonus == +2, "prodigy.react=+2");

        // ===== 4. Get(TraitSO null) =====
        var nullTrait = TraitEffects.Get((TraitSO)null);
        Assert(nullTrait.initiativeBonus == 0, "Get(TraitSO null).init=0");
        Assert(nullTrait.aimBonus == 0, "Get(TraitSO null).aim=0");

        // ===== 5. SumForCrewMember null 2개 =====
        var sumNull = TraitEffects.SumForCrewMember(null, null);
        Assert(sumNull.initiativeBonus == 0, "SumForCrewMember(null,null).init=0");
        Assert(sumNull.aimBonus == 0, "SumForCrewMember(null,null).aim=0");
        Assert(sumNull.reactBonus == 0, "SumForCrewMember(null,null).react=0");
        Assert(sumNull.moraleFloor == 0, "SumForCrewMember(null,null).morale=0");

        // ===== 6. SumForCrewMember 장점만 =====
        var hermitSO = AssetDatabase.LoadAssetAtPath<TraitSO>("Assets/_Project/Data/Traits/Trait_hermit_eye.asset");
        if (hermitSO != null)
        {
            var sumPos = TraitEffects.SumForCrewMember(hermitSO, null);
            Assert(sumPos.aimBonus == +5, "SumForCrewMember(hermit,null).aim=+5");
            Assert(sumPos.initiativeBonus == 0, "SumForCrewMember(hermit,null).init=0");
        }

        // ===== 7. SumForCrewMember 장점 + 약점 =====
        var donqSO = AssetDatabase.LoadAssetAtPath<TraitSO>("Assets/_Project/Data/Traits/Trait_donquixote_dream.asset");
        var spoilSO = AssetDatabase.LoadAssetAtPath<TraitSO>("Assets/_Project/Data/Traits/Trait_spoiled.asset");

        if (donqSO != null && spoilSO != null)
        {
            var sumBoth = TraitEffects.SumForCrewMember(donqSO, spoilSO);
            Assert(sumBoth.initiativeBonus == 2, "SumForCrewMember(donq,spoiled).init=+2");
            Assert(sumBoth.aimBonus == -3, "SumForCrewMember(donq,spoiled).aim=-3");
            Assert(sumBoth.moraleFloor == -5, "SumForCrewMember(donq,spoiled).morale=-5");
        }

        // ===== 8. 10종 trait 모두 등록 확인 =====
        string[] effectKeys = new[]
        {
            "trait.donquixote_dream",
            "trait.first_battle_fear",
            "trait.hermit_eye",
            "trait.dialogue_phobia",
            "trait.little_hand_prodigy",
            "trait.wordless_comrade",
            "trait.rocinante_owner",
            "trait.brother_dependent",
            "trait.silent_worker",
            "trait.spoiled",
        };

        // 10개 trait 이 모두 Table 에 등록돼 있는지 확인 (0이 아닌 필드 또는 특수 케이스)
        var wordless = TraitEffects.Get("trait.wordless_comrade");
        Assert(wordless.moraleFloor == +5, "wordless_comrade.morale=+5");

        var brother = TraitEffects.Get("trait.brother_dependent");
        Assert(brother.moraleFloor == +5, "brother_dependent.morale=+5");

        var rocinante = TraitEffects.Get("trait.rocinante_owner");
        Assert(rocinante.initiativeBonus == +3, "rocinante.init=+3");
        Assert(rocinante.moraleFloor == +5, "rocinante.morale=+5");

        var phobia = TraitEffects.Get("trait.dialogue_phobia");
        Assert(phobia.initiativeBonus == -1, "phobia.init=-1");

        var silent = TraitEffects.Get("trait.silent_worker");
        Assert(silent.reactBonus == +1, "silent_worker.react=+1");

        Debug.Log($"[P6] ===== TEST COMPLETE ===== passed={passed} failed={failed}");
    }
}
