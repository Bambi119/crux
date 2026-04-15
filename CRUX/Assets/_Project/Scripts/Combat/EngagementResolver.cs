using System;
using System.Collections.Generic;
using UnityEngine;
using Crux.Core;
using Crux.Data;

namespace Crux.Combat
{
    /// <summary>
    /// 전투 시작 이니셔티브 입력 — 유닛 1대의 상태 스냅샷.
    /// docs/03 §2.3 공식: React + Morale/5 + TraitBonus + InitiativeSpeed + d20
    /// </summary>
    public struct InitiativeInput
    {
        public string unitId;      // 디버그·로그용 식별자
        public PlayerSide side;
        public int react;          // 승무원(일반적으로 전차장) React 스탯
        public int morale;         // 전투 시작 사기 (0~100)
        public int traitBonus;     // 특성 보정 (사전 계산, 특성 효과 시스템 미구현 시 0)
        public HullClass hullClass;
    }

    /// <summary>이니셔티브 굴림 결과 — 각 항목 기록으로 디버그·UI 가능</summary>
    public struct InitiativeResult
    {
        public string unitId;
        public PlayerSide side;
        public int react;
        public int moraleDiv;      // morale / 5
        public int traitBonus;
        public int hullSpeed;
        public int d20;
        public int total;
    }

    /// <summary>전체 전투 시작 판정 결과 — 유닛별 roll + 선공 그룹</summary>
    public struct EngagementOutcome
    {
        public InitiativeResult[] perUnit;
        public PlayerSide firstSide;
        public float allyAvg;
        public float enemyAvg;
    }

    /// <summary>
    /// 전투 시작 이니셔티브·선공 그룹 판정 — docs/03 §2.3, docs/06 §3.1.
    /// 옵션 B 턴 구조: 라운드 1에서 어느 진영이 먼저 갈지만 결정. 이후 라운드는 엄격한 ally→enemy 반복.
    /// </summary>
    public static class EngagementResolver
    {
        /// <summary>
        /// 전 유닛 이니셔티브 굴림 + 진영 평균 비교 → 선공 그룹 결정.
        /// </summary>
        /// <param name="units">전투 참여 모든 유닛(아군·적). null/빈 배열은 빈 결과.</param>
        /// <param name="rollD20">d20 공급자. null이면 UnityEngine.Random (1~20). 테스트는 결정적 큐 제공.</param>
        public static EngagementOutcome Resolve(IReadOnlyList<InitiativeInput> units, Func<int> rollD20 = null)
        {
            if (units == null || units.Count == 0)
            {
                return new EngagementOutcome
                {
                    perUnit = new InitiativeResult[0],
                    firstSide = PlayerSide.Player,
                    allyAvg = 0f,
                    enemyAvg = 0f
                };
            }

            rollD20 ??= DefaultRollD20;

            var results = new InitiativeResult[units.Count];
            int allyCount = 0, enemyCount = 0;
            int allySum = 0, enemySum = 0;

            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                int d20 = Mathf.Clamp(rollD20(), 1, 20);
                int moraleDiv = u.morale / 5;
                int hullSpeed = HullClassDefaults.InitiativeSpeedFor(u.hullClass);
                int total = u.react + moraleDiv + u.traitBonus + hullSpeed + d20;

                results[i] = new InitiativeResult
                {
                    unitId = u.unitId,
                    side = u.side,
                    react = u.react,
                    moraleDiv = moraleDiv,
                    traitBonus = u.traitBonus,
                    hullSpeed = hullSpeed,
                    d20 = d20,
                    total = total
                };

                if (u.side == PlayerSide.Player) { allyCount++; allySum += total; }
                else                              { enemyCount++; enemySum += total; }
            }

            float allyAvg = allyCount > 0 ? (float)allySum / allyCount : 0f;
            float enemyAvg = enemyCount > 0 ? (float)enemySum / enemyCount : 0f;

            // 진영 평균 비교 — 동률은 플레이어 우위 (아군 선공)
            PlayerSide first = allyAvg >= enemyAvg ? PlayerSide.Player : PlayerSide.Enemy;

            return new EngagementOutcome
            {
                perUnit = results,
                firstSide = first,
                allyAvg = allyAvg,
                enemyAvg = enemyAvg
            };
        }

        /// <summary>
        /// 기대값 (출격 씬 선공 예상 UI용). d20 평균 10.5 반영.
        /// 실제 Roll 대신 사전 표시용.
        /// </summary>
        public static float ExpectedInitiative(InitiativeInput unit)
            => unit.react + unit.morale / 5 + unit.traitBonus
               + HullClassDefaults.InitiativeSpeedFor(unit.hullClass)
               + 10.5f;

        private static int DefaultRollD20() => UnityEngine.Random.Range(1, 21);
    }
}
