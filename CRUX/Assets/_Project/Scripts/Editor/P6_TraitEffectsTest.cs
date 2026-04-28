using UnityEngine;
using UnityEditor;
using Crux.Data;

/// <summary>
/// P6 Batch Smoke Test — TraitEffects / TraitModifier 로직 검증 (누적 카운트 기반 모델).
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

        // ===== 1. 빈 id 문자열 =====
        Assert(TraitEffects.Get((string)null).initiativeBonus == 0, "Get(null).init=0");
        Assert(TraitEffects.Get("").aimBonus == 0, "Get(\"\").aim=0");
        Assert(TraitEffects.Get((string)null).reactBonus == 0, "Get(null).react=0");
        Assert(TraitEffects.Get((string)null).moraleFloor == 0, "Get(null).morale=0");

        // ===== 2. 미등록 id =====
        var unknown = TraitEffects.Get("nonexistent_trait");
        Assert(unknown.initiativeBonus == 0, "Get(nonexistent).init=0");
        Assert(unknown.aimBonus == 0, "Get(nonexistent).aim=0");
        Assert(unknown.reactBonus == 0, "Get(nonexistent).react=0");
        Assert(unknown.moraleFloor == 0, "Get(nonexistent).morale=0");

        // ===== 3. 등록된 id 확인 (5개 trait) =====
        var hermit = TraitEffects.Get("hermit_eye");
        Assert(hermit.aimBonus == +5, "hermit_eye.aim=+5");
        Assert(hermit.initiativeBonus == 0, "hermit_eye.init=0");

        var donquixote = TraitEffects.Get("donquixote_dream");
        Assert(donquixote.initiativeBonus == +2, "donquixote.init=+2");
        Assert(donquixote.aimBonus == -3, "donquixote.aim=-3");

        var prodigy = TraitEffects.Get("little_hand_prodigy");
        Assert(prodigy.reactBonus == +2, "prodigy.react=+2");

        var wordless = TraitEffects.Get("wordless_comrade");
        Assert(wordless.moraleFloor == +5, "wordless_comrade.morale=+5");

        var rocinante = TraitEffects.Get("rocinante_owner");
        Assert(rocinante.initiativeBonus == +3, "rocinante.init=+3");
        Assert(rocinante.moraleFloor == +5, "rocinante.morale=+5");

        var silent = TraitEffects.Get("silent_worker");
        Assert(silent.reactBonus == +1, "silent_worker.react=+1");

        // ===== 4. Get(TraitSO null) =====
        var nullTrait = TraitEffects.Get((TraitSO)null);
        Assert(nullTrait.initiativeBonus == 0, "Get(TraitSO null).init=0");
        Assert(nullTrait.aimBonus == 0, "Get(TraitSO null).aim=0");

        // ===== 5. SumForCrewMember null array =====
        var sumNull = TraitEffects.SumForCrewMember(null);
        Assert(sumNull.initiativeBonus == 0, "SumForCrewMember(null).init=0");
        Assert(sumNull.aimBonus == 0, "SumForCrewMember(null).aim=0");
        Assert(sumNull.reactBonus == 0, "SumForCrewMember(null).react=0");
        Assert(sumNull.moraleFloor == 0, "SumForCrewMember(null).morale=0");

        // ===== 6. SumForCrewMember empty array =====
        var sumEmpty = TraitEffects.SumForCrewMember(new TraitSO[0]);
        Assert(sumEmpty.initiativeBonus == 0, "SumForCrewMember([]).init=0");
        Assert(sumEmpty.moraleFloor == 0, "SumForCrewMember([]).morale=0");

        // ===== 7. SumForCrewMember single trait =====
        var hermitSO = AssetDatabase.LoadAssetAtPath<TraitSO>("Assets/_Project/Data/Traits/Trait_hermit_eye.asset");
        if (hermitSO != null)
        {
            var sumOne = TraitEffects.SumForCrewMember(new[] { hermitSO });
            Assert(sumOne.aimBonus == +5, "SumForCrewMember([hermit]).aim=+5");
            Assert(sumOne.initiativeBonus == 0, "SumForCrewMember([hermit]).init=0");
        }

        // ===== 8. SumForCrewMember multiple traits (accumulation) =====
        var donqSO = AssetDatabase.LoadAssetAtPath<TraitSO>("Assets/_Project/Data/Traits/Trait_donquixote_dream.asset");
        var wordlessSO = AssetDatabase.LoadAssetAtPath<TraitSO>("Assets/_Project/Data/Traits/Trait_wordless_comrade.asset");

        if (donqSO != null && wordlessSO != null)
        {
            var sumBoth = TraitEffects.SumForCrewMember(new[] { donqSO, wordlessSO });
            Assert(sumBoth.initiativeBonus == 2, "SumForCrewMember([donq,wordless]).init=+2");
            Assert(sumBoth.aimBonus == -3, "SumForCrewMember([donq,wordless]).aim=-3");
            Assert(sumBoth.moraleFloor == +5, "SumForCrewMember([donq,wordless]).morale=+5");
        }

        // ===== 9. SumForCrewMember with null in array (filtering) =====
        if (donqSO != null)
        {
            var sumWithNull = TraitEffects.SumForCrewMember(new[] { donqSO, null });
            Assert(sumWithNull.initiativeBonus == +2, "SumForCrewMember([donq,null]).init=+2");
        }

        Debug.Log($"[P6] ===== TEST COMPLETE ===== passed={passed} failed={failed}");
    }
}
