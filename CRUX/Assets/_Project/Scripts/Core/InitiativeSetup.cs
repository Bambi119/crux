using System.Collections.Generic;
using UnityEngine;
using Crux.Combat;
using Crux.Unit;

namespace Crux.Core
{
    /// <summary>
    /// 전투 시작 시 EngagementResolver 입력을 구성하고 결과 로그·분기를 제공하는 static helper.
    /// BattleController.InitializeBattle 말미에서 사용.
    /// </summary>
    public static class InitiativeSetup
    {
        /// <summary>
        /// 아군 + 적군 유닛으로부터 InitiativeInput을 조립하고 EngagementResolver 실행.
        /// 결과 firstSide 를 반환하며 [CRUX] 이니셔티브 로그 출력.
        /// </summary>
        public static PlayerSide Resolve(GridTankUnit playerUnit, IReadOnlyList<GridTankUnit> enemyUnits)
        {
            var inputs = new List<InitiativeInput>();

            if (playerUnit != null && !playerUnit.IsDestroyed)
                inputs.Add(BuildInitiativeInput(playerUnit));

            if (enemyUnits != null)
            {
                foreach (var e in enemyUnits)
                    if (e != null && !e.IsDestroyed)
                        inputs.Add(BuildInitiativeInput(e));
            }

            var outcome = EngagementResolver.Resolve(inputs);
            Debug.Log($"[CRUX] 이니셔티브: 아군={outcome.allyAvg:F1} 적군={outcome.enemyAvg:F1} 선공={outcome.firstSide}");
            return outcome.firstSide;
        }

        /// <summary>유닛 정보로 InitiativeInput 구성</summary>
        static InitiativeInput BuildInitiativeInput(GridTankUnit unit)
        {
            int react = 0, morale = 50;
            if (unit.Crew != null)
            {
                morale = unit.Crew.Morale;
                if (unit.Crew.commander?.data != null)
                    react = unit.Crew.commander.data.react;
            }
            return new InitiativeInput
            {
                unitId = unit.Data?.tankName ?? unit.name,
                side = unit.side,
                react = react,
                morale = morale,
                traitBonus = 0,
                hullClass = unit.Data != null ? unit.Data.hullClass : default
            };
        }
    }
}
