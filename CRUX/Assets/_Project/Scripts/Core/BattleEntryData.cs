using System.Collections.Generic;
using Crux.Data;

namespace Crux.Core
{
    /// <summary>
    /// Hangar → BattleController 간 편성 데이터 전달 통로.
    /// 정적 저장소 — 씬 전환 사이 유지. 복귀 시 필요에 따라 Clear 호출.
    ///
    /// 이번 커밋은 전달 스켈레톤 — 실제 TankInstance → GridTankUnit 변환은 후속.
    /// </summary>
    public static class BattleEntryData
    {
        /// <summary>Hangar에서 출격 확정된 탱크 목록. null이면 BattleController가 자체 기본 탱크 생성.</summary>
        public static List<TankInstance> SortieTanks;

        /// <summary>부대 전체 인벤토리 참조 — 전투 후 재고/자금/사기 갱신 시 필요.</summary>
        public static ConvoyInventory Convoy;

        public static bool HasEntry => SortieTanks != null && SortieTanks.Count > 0;

        public static void Clear()
        {
            SortieTanks = null;
            Convoy = null;
        }
    }
}
