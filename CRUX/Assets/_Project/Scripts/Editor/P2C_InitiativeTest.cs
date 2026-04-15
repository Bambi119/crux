using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Crux.Core;
using Crux.Data;
using Crux.Combat;

/// <summary>
/// P2-C Batch Smoke Test — EngagementResolver 이니셔티브 공식 / 선공 그룹 / 기대값.
/// 메뉴 Crux/Test/P2C Initiative 실행. 결정적 d20 큐 사용.
/// </summary>
public static class P2C_InitiativeTest
{
    [MenuItem("Crux/Test/P2C Initiative")]
    public static void Execute()
    {
        void Log(string msg) => Debug.Log($"[P2C] {msg}");
        void Fail(string msg) => Debug.LogError($"[P2C] FAIL — {msg}");

        int passed = 0;
        int failed = 0;

        void Assert(bool cond, string name)
        {
            if (cond) { passed++; Log($"OK {name}"); }
            else      { failed++; Fail(name); }
        }

        // ===== 결정적 d20 큐 팩토리 =====
        System.Func<int> MakeRoll(params int[] values)
        {
            var q = new Queue<int>(values);
            return () => q.Count > 0 ? q.Dequeue() : 10; // 고갈 시 10
        }

        // ===== 1. InitiativeSpeedFor 기본값 =====
        Assert(HullClassDefaults.InitiativeSpeedFor(HullClass.Scout) == +4, "speed.Scout=+4");
        Assert(HullClassDefaults.InitiativeSpeedFor(HullClass.Assault) == +2, "speed.Assault=+2");
        Assert(HullClassDefaults.InitiativeSpeedFor(HullClass.Support) == +1, "speed.Support=+1");
        Assert(HullClassDefaults.InitiativeSpeedFor(HullClass.Heavy) == 0, "speed.Heavy=0");
        Assert(HullClassDefaults.InitiativeSpeedFor(HullClass.Siege) == -2, "speed.Siege=-2");

        // ===== 2. 단일 유닛 공식 — React 50 + Morale 50 + Trait 0 + Assault(+2) + d20=15 =====
        var single = new InitiativeInput[]
        {
            new InitiativeInput {
                unitId = "rocinante",
                side = PlayerSide.Player,
                react = 50, morale = 50, traitBonus = 0,
                hullClass = HullClass.Assault
            }
        };
        var out1 = EngagementResolver.Resolve(single, MakeRoll(15));
        Assert(out1.perUnit.Length == 1, "single result length");
        // 50 + 10(morale/5) + 0 + 2 + 15 = 77
        Assert(out1.perUnit[0].total == 77, $"single total=77 (got {out1.perUnit[0].total})");
        Assert(out1.perUnit[0].d20 == 15, "single d20=15");
        Assert(out1.perUnit[0].moraleDiv == 10, "single moraleDiv=10");
        Assert(out1.perUnit[0].hullSpeed == 2, "single hullSpeed=2");
        Assert(out1.firstSide == PlayerSide.Player, "single firstSide=Player (solo)");

        // ===== 3. 아군 vs 적 — 아군 평균 우위 → 아군 선공 =====
        var ally_wins = new InitiativeInput[]
        {
            new InitiativeInput { unitId="p1", side=PlayerSide.Player, react=60, morale=60, hullClass=HullClass.Scout },   // 60+12+0+4 = 76
            new InitiativeInput { unitId="e1", side=PlayerSide.Enemy,  react=40, morale=40, hullClass=HullClass.Heavy },  // 40+8+0+0  = 48
        };
        // d20: p1=10, e1=10 → p1=86, e1=58. ally avg 86 > enemy 58 → Player
        var out2 = EngagementResolver.Resolve(ally_wins, MakeRoll(10, 10));
        Assert(out2.perUnit[0].total == 86, $"ally p1 total=86 (got {out2.perUnit[0].total})");
        Assert(out2.perUnit[1].total == 58, $"enemy e1 total=58 (got {out2.perUnit[1].total})");
        Assert(Mathf.Approximately(out2.allyAvg, 86f), $"allyAvg=86");
        Assert(Mathf.Approximately(out2.enemyAvg, 58f), $"enemyAvg=58");
        Assert(out2.firstSide == PlayerSide.Player, "ally wins → Player first");

        // ===== 4. 적 우위 — 적 선공 =====
        var enemy_wins = new InitiativeInput[]
        {
            new InitiativeInput { unitId="p1", side=PlayerSide.Player, react=30, morale=20, hullClass=HullClass.Siege },  // 30+4+0-2 = 32
            new InitiativeInput { unitId="e1", side=PlayerSide.Enemy,  react=70, morale=80, hullClass=HullClass.Scout },  // 70+16+0+4 = 90
        };
        // d20: p1=5, e1=5 → p1=37, e1=95 → Enemy first
        var out3 = EngagementResolver.Resolve(enemy_wins, MakeRoll(5, 5));
        Assert(out3.perUnit[0].total == 37, $"enemy wins p1 total=37 (got {out3.perUnit[0].total})");
        Assert(out3.perUnit[1].total == 95, $"enemy wins e1 total=95 (got {out3.perUnit[1].total})");
        Assert(out3.firstSide == PlayerSide.Enemy, "enemy wins → Enemy first");

        // ===== 5. 동률 → 플레이어 우위 =====
        var tie = new InitiativeInput[]
        {
            new InitiativeInput { unitId="p1", side=PlayerSide.Player, react=50, morale=50, hullClass=HullClass.Assault },
            new InitiativeInput { unitId="e1", side=PlayerSide.Enemy,  react=50, morale=50, hullClass=HullClass.Assault },
        };
        var out4 = EngagementResolver.Resolve(tie, MakeRoll(10, 10));
        Assert(Mathf.Approximately(out4.allyAvg, out4.enemyAvg), "tie averages equal");
        Assert(out4.firstSide == PlayerSide.Player, "tie → Player first (advantage)");

        // ===== 6. 비대칭 진영 크기 (아군 2 vs 적 3) — 평균으로 비교 =====
        var asymmetric = new InitiativeInput[]
        {
            new InitiativeInput { unitId="p1", side=PlayerSide.Player, react=80, morale=90, hullClass=HullClass.Scout },   // 80+18+0+4 = 102
            new InitiativeInput { unitId="p2", side=PlayerSide.Player, react=80, morale=90, hullClass=HullClass.Scout },   // 102
            new InitiativeInput { unitId="e1", side=PlayerSide.Enemy,  react=40, morale=40, hullClass=HullClass.Heavy },   // 40+8+0+0 = 48
            new InitiativeInput { unitId="e2", side=PlayerSide.Enemy,  react=40, morale=40, hullClass=HullClass.Heavy },   // 48
            new InitiativeInput { unitId="e3", side=PlayerSide.Enemy,  react=40, morale=40, hullClass=HullClass.Heavy },   // 48
        };
        // roll 10 all → p=112 each, e=58 each. ally avg 112, enemy avg 58
        var out5 = EngagementResolver.Resolve(asymmetric, MakeRoll(10, 10, 10, 10, 10));
        Assert(Mathf.Approximately(out5.allyAvg, 112f), $"asym allyAvg=112 (got {out5.allyAvg})");
        Assert(Mathf.Approximately(out5.enemyAvg, 58f), $"asym enemyAvg=58 (got {out5.enemyAvg})");
        Assert(out5.firstSide == PlayerSide.Player, "asymmetric ally wins");

        // ===== 7. 특성 보정 반영 =====
        var withTrait = new InitiativeInput[]
        {
            new InitiativeInput {
                unitId = "p1",
                side = PlayerSide.Player,
                react = 50, morale = 50, traitBonus = 10,
                hullClass = HullClass.Heavy
            }
        };
        // 50 + 10 + 10 + 0 + 10 = 80
        var out6 = EngagementResolver.Resolve(withTrait, MakeRoll(10));
        Assert(out6.perUnit[0].total == 80, $"trait bonus reflected (got {out6.perUnit[0].total})");
        Assert(out6.perUnit[0].traitBonus == 10, "trait bonus field");

        // ===== 8. d20 경계 — 1, 20 =====
        var boundary = new InitiativeInput[]
        {
            new InitiativeInput { unitId="p1", side=PlayerSide.Player, react=50, morale=50, hullClass=HullClass.Assault },
            new InitiativeInput { unitId="p2", side=PlayerSide.Player, react=50, morale=50, hullClass=HullClass.Assault },
        };
        var out7 = EngagementResolver.Resolve(boundary, MakeRoll(1, 20));
        Assert(out7.perUnit[0].d20 == 1, "d20=1 accepted");
        Assert(out7.perUnit[1].d20 == 20, "d20=20 accepted");
        // 50+10+0+2+1 = 63 / 50+10+0+2+20 = 82
        Assert(out7.perUnit[0].total == 63, $"d20=1 total=63 (got {out7.perUnit[0].total})");
        Assert(out7.perUnit[1].total == 82, $"d20=20 total=82 (got {out7.perUnit[1].total})");

        // ===== 9. d20 클램프 (범위 이탈 입력은 1~20으로 클램프) =====
        var clampTest = new InitiativeInput[]
        {
            new InitiativeInput { unitId="p1", side=PlayerSide.Player, react=50, morale=50, hullClass=HullClass.Assault }
        };
        var out8 = EngagementResolver.Resolve(clampTest, MakeRoll(25));
        Assert(out8.perUnit[0].d20 == 20, $"d20=25 clamped to 20 (got {out8.perUnit[0].d20})");
        var out8b = EngagementResolver.Resolve(clampTest, MakeRoll(-3));
        Assert(out8b.perUnit[0].d20 == 1, $"d20=-3 clamped to 1 (got {out8b.perUnit[0].d20})");

        // ===== 10. ExpectedInitiative — d20 10.5 반영 =====
        var expectInput = new InitiativeInput
        {
            react = 50, morale = 50, traitBonus = 0,
            hullClass = HullClass.Assault
        };
        // 50 + 10 + 0 + 2 + 10.5 = 72.5
        float expected = EngagementResolver.ExpectedInitiative(expectInput);
        Assert(Mathf.Approximately(expected, 72.5f), $"expected=72.5 (got {expected})");

        // ===== 11. 빈 입력 처리 =====
        var outEmpty = EngagementResolver.Resolve(new InitiativeInput[0]);
        Assert(outEmpty.perUnit.Length == 0, "empty input → 0 results");
        Assert(outEmpty.firstSide == PlayerSide.Player, "empty → Player default");

        var outNull = EngagementResolver.Resolve(null);
        Assert(outNull.perUnit.Length == 0, "null input → 0 results");

        // ===== 12. 한쪽만 있는 경우 — 상대편 평균 0 =====
        var allyOnly = new InitiativeInput[]
        {
            new InitiativeInput { unitId="p1", side=PlayerSide.Player, react=50, morale=50, hullClass=HullClass.Assault }
        };
        var out9 = EngagementResolver.Resolve(allyOnly, MakeRoll(10));
        Assert(out9.enemyAvg == 0f, "no enemies → enemyAvg=0");
        Assert(out9.firstSide == PlayerSide.Player, "no enemies → Player first");

        // ===== 결과 =====
        if (failed == 0)
            Log($"=== ALL PASS ({passed}/{passed}) ===");
        else
            Debug.LogError($"[P2C] === FAILED {failed} / {passed + failed} ===");
    }
}
