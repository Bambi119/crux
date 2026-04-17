using UnityEngine;
using Crux.Data;
using Crux.Unit;

namespace Crux.Core
{
    /// <summary>
    /// GridTankUnit에 TankCrew 컴포넌트 부착·초기화 책임.
    /// BattleController에서 P-S7 일환으로 추출.
    /// </summary>
    public static class BattleCrewBinder
    {
        /// <summary>
        /// 플레이어 유닛과 적 유닛들에 크루 부착.
        /// 플레이어: Hangar 편성 또는 Inspector playercrew 배열 사용.
        /// 적: 기본값(null) 초기화로 사기 50 시작.
        /// </summary>
        public static void AttachAll(GridTankUnit playerUnit, System.Collections.Generic.List<GridTankUnit> enemyUnits, CrewMemberSO[] playerCrew)
        {
            if (playerUnit != null)
                AttachOne(playerUnit, true, playerCrew);

            if (enemyUnits != null)
            {
                foreach (var e in enemyUnits)
                    if (e != null) AttachOne(e, false, null);
            }
        }

        /// <summary>
        /// 한 유닛에 TankCrew 컴포넌트 부착 및 초기화.
        /// isPlayer=true일 때만 entryTank 또는 playerCrew 배열 적용.
        /// </summary>
        private static void AttachOne(GridTankUnit unit, bool isPlayer, CrewMemberSO[] playerCrew)
        {
            var crew = unit.gameObject.GetComponent<TankCrew>();
            if (crew == null)
                crew = unit.gameObject.AddComponent<TankCrew>();

            if (isPlayer)
            {
                CrewMemberSO cmd, gun, load, drv, mg;

                // 1순위: Hangar BattleEntryData 편성 크루
                var entryTank = (BattleEntryData.HasEntry && BattleEntryData.SortieTanks.Count > 0)
                    ? BattleEntryData.SortieTanks[0]
                    : null;

                if (entryTank != null && entryTank.crew != null)
                {
                    cmd = entryTank.crew.commander?.data;
                    gun = entryTank.crew.gunner?.data;
                    load = entryTank.crew.loader?.data;
                    drv = entryTank.crew.driver?.data;
                    mg = entryTank.crew.gunnerMech?.data;
                    Debug.Log($"[CRUX] 편성 크루 주입: {entryTank.tankName} ({cmd?.displayName}/{gun?.displayName}/{load?.displayName}/{drv?.displayName}/{mg?.displayName})");
                }
                else
                {
                    // 2순위: Inspector playerCrew 배열
                    cmd = (playerCrew != null && playerCrew.Length > 0) ? playerCrew[0] : null;
                    gun = (playerCrew != null && playerCrew.Length > 1) ? playerCrew[1] : null;
                    load = (playerCrew != null && playerCrew.Length > 2) ? playerCrew[2] : null;
                    drv = (playerCrew != null && playerCrew.Length > 3) ? playerCrew[3] : null;
                    mg = (playerCrew != null && playerCrew.Length > 4) ? playerCrew[4] : null;
                }

                string hullClassAxis = unit.tankData?.hullClass.ToString();
                crew.Initialize(cmd, gun, load, drv, mg, hullClassAxis);
            }
            else
            {
                // 적 유닛: 모두 null로 호출해 기본 사기 50 시작
                crew.Initialize(null, null, null, null, null, null);
            }

            // GridTankUnit이 TankCrew를 캐시하도록 바인딩
            unit.BindCrew(crew);

            Debug.Log($"[CRUX] TankCrew 초기화 — {(isPlayer ? "player" : "enemy")} {unit.Data?.tankName} morale={crew.Morale}");
        }
    }
}
