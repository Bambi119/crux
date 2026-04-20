namespace Crux.Data
{
    /// <summary>화재 시스템 밸런스 상수 — Phase 2 전투</summary>
    public static class FireConstants
    {
        public const float AmmoRackFireChance   = 0.40f;  // AmmoRack 관통 시 화재 확률
        public const float EngineFireChance     = 0.30f;  // Engine 관통 시 화재 확률
        public const float OtherModuleFireChance = 0.15f; // 기타 모듈 관통 시 화재 확률
        public const float FireDamagePerTurnPercent = 5f;  // 턴당 maxHP의 5% 데미지
        public const float AutoExtinguishChance = 0.15f;  // 턴 종료 후 자연 소화 확률
        public const int   MaxFireTurns         = 6;      // 최대 화재 지속 턴
        public const float FireAimPenalty       = 0.20f;  // 화재 중 명중률 감소 (20%)
    }
}
