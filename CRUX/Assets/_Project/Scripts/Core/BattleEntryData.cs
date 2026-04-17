using System.Collections.Generic;
using Crux.Data;

namespace Crux.Core
{
    /// <summary>전투 결과 열거. None = 미보고</summary>
    public enum BattleResult
    {
        None,
        Victory,
        Defeat
    }

    /// <summary>
    /// Hangar ↔ BattleController 간 데이터 전달 통로.
    /// 정적 저장소 — 씬 전환 사이 유지. 복귀 시 소비/Clear.
    /// </summary>
    public static class BattleEntryData
    {
        /// <summary>Hangar에서 출격 확정된 탱크 목록. null이면 BattleController가 자체 기본 탱크 생성.</summary>
        public static List<TankInstance> SortieTanks;

        /// <summary>부대 전체 인벤토리 참조 — 전투 후 재고/자금/사기 갱신 시 필요.</summary>
        public static ConvoyInventory Convoy;

        /// <summary>직전 전투 결과 — Hangar 복귀 시 소비(Victory 보상 / Defeat 피해).</summary>
        public static BattleResult LastResult = BattleResult.None;

        public static bool HasEntry => SortieTanks != null && SortieTanks.Count > 0;

        public static void Clear()
        {
            SortieTanks = null;
            Convoy = null;
            LastResult = BattleResult.None;
        }
    }
}
