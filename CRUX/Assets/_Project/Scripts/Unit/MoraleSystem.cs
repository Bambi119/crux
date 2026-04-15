using Crux.Data;

namespace Crux.Unit
{
    /// <summary>사기 변동 이벤트 — docs/04 §6.1</summary>
    public enum MoraleEvent
    {
        EnemyKilled,        // +5
        Ricochet,           // +3 (아군 피격 도탄)
        PenetrationHit,     // +3 (아군 주포 관통 명중)
        CrewInjured,        // -15
        ModuleDamaged,      // -5
        SideRearHit,        // -10 (아군 측·후면 피격)
        AmmoRackNear,       // -20 (탄약고 근접 피격)
        CommanderEncourage  // +15 (지휘 스킬 기본)
    }

    /// <summary>
    /// 사기 시스템 — 이벤트 델타 테이블, 밴드 판정, 페널티 조회.
    /// 실제 상태는 TankCrew가 보유. 본 클래스는 무상태 유틸.
    /// docs/04 §6.1 참조.
    /// </summary>
    public static class MoraleSystem
    {
        public static int DefaultDelta(MoraleEvent kind) => kind switch
        {
            MoraleEvent.EnemyKilled => +5,
            MoraleEvent.Ricochet => +3,
            MoraleEvent.PenetrationHit => +3,
            MoraleEvent.CommanderEncourage => +15,
            MoraleEvent.CrewInjured => -15,
            MoraleEvent.ModuleDamaged => -5,
            MoraleEvent.SideRearHit => -10,
            MoraleEvent.AmmoRackNear => -20,
            _ => 0
        };

        public static MoraleBand GetBand(int morale)
        {
            if (morale >= 80) return MoraleBand.High;
            if (morale >= 50) return MoraleBand.Normal;
            if (morale >= 25) return MoraleBand.Shaken;
            return MoraleBand.Panic;
        }

        /// <summary>밴드별 명중 보정 (aim)</summary>
        public static int AimModifier(MoraleBand band) => band switch
        {
            MoraleBand.High => +5,
            MoraleBand.Normal => 0,
            MoraleBand.Shaken => -5,
            MoraleBand.Panic => -15,
            _ => 0
        };

        /// <summary>공황 시 매 턴 AP 페널티 (-1)</summary>
        public static int TurnApPenalty(MoraleBand band)
            => band == MoraleBand.Panic ? 1 : 0;

        /// <summary>공황 시 액티브 스킬 사용 금지</summary>
        public static bool ForbidsActiveSkills(MoraleBand band)
            => band == MoraleBand.Panic;

        /// <summary>흔들림 시 반응 사격 발동 실패 확률 (20%)</summary>
        public static float ReactionFailChance(MoraleBand band)
            => band == MoraleBand.Shaken ? 0.2f : 0f;

        /// <summary>사기충천 시 전투 시작 쿨다운 -1 (1회)</summary>
        public static bool GetsStartOfBattleCooldownReduction(MoraleBand band)
            => band == MoraleBand.High;
    }
}
