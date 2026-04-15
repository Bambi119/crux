using Crux.Core;

namespace Crux.Combat
{
    /// <summary>데미지 정보 전달 구조체</summary>
    public struct DamageInfo
    {
        public float damage;
        public HitZone hitZone;
        public ShotOutcome outcome;
        public float penetrationValue;
        public float effectiveArmor;
        public float impactAngle;
        public Unit.GridTankUnit attacker; // 공격자 참조 (사기 이벤트 라우팅용)
    }

    /// <summary>사격 결과 전체 정보</summary>
    public struct ShotResult
    {
        public bool hit;
        public ShotOutcome outcome;
        public HitZone hitZone;
        public float effectiveArmor;
        public float damageDealt;
        public float ricochetAngle;
        public float hitChance;
    }
}
