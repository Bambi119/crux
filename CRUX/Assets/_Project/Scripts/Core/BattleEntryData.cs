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
    /// 전투 결과별 Convoy 보상/피해 상수. HangarUI/BattleHUD 공용 단일 출처.
    /// 향후 밸런스 튜닝 시 여기만 조정.
    /// </summary>
    public static class BattleResultRewards
    {
        public const int VictoryMoney = 200;
        public const int VictoryMorale = 10;
        public const int DefeatMoney = -100;
        public const int DefeatMorale = -15;

        public static (int money, int morale) For(BattleResult r) => r switch
        {
            BattleResult.Victory => (VictoryMoney, VictoryMorale),
            BattleResult.Defeat => (DefeatMoney, DefeatMorale),
            _ => (0, 0)
        };

        public static string FormatSigned(int v) => v >= 0 ? $"+{v}" : v.ToString();
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
