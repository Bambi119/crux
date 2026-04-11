using UnityEngine;
using Crux.Core;
using Crux.Data;

namespace Crux.Combat
{
    /// <summary>관통/도탄 판정 — 순수 로직 (MonoBehaviour 비의존)</summary>
    public static class PenetrationCalculator
    {
        /// <summary>피격 부위 판별 (공격자 위치, 대상 위치, 대상 차체 각도)</summary>
        public static HitZone DetermineHitZone(Vector2 attackerPos, Vector2 targetPos, float targetHullAngle)
        {
            Vector2 attackDir = (targetPos - attackerPos).normalized;
            float attackAngle = AngleUtil.FromDir(attackDir);

            // 공격 방향과 차체 전면 방향의 각도 차
            float angleDiff = Mathf.Abs(Mathf.DeltaAngle(attackAngle, targetHullAngle + 180f));

            if (angleDiff <= 45f) return HitZone.Front;
            if (angleDiff >= 135f) return HitZone.Rear;
            return HitZone.Side;
        }

        /// <summary>피격 부위의 기본 장갑 두께 반환</summary>
        public static float GetBaseArmor(ArmorProfile armor, HitZone zone)
        {
            return zone switch
            {
                HitZone.Front => armor.front,
                HitZone.Side => armor.side,
                HitZone.Rear => armor.rear,
                HitZone.Turret => armor.turret,
                _ => armor.front
            };
        }

        /// <summary>입사각 계산 (포탄 방향 vs 장갑면 법선)</summary>
        public static float CalculateImpactAngle(Vector2 shellDirection, Vector2 armorNormal)
        {
            float dot = Vector2.Dot(shellDirection.normalized, armorNormal.normalized);
            return Mathf.Acos(Mathf.Abs(dot)) * Mathf.Rad2Deg;
        }

        /// <summary>피격 부위의 장갑면 법선 벡터 (나침반 각도 기준)</summary>
        public static Vector2 GetArmorNormal(float targetHullAngle, HitZone zone)
        {
            // 차체 전면 법선 = 차체 전방 방향
            Vector2 forward = AngleUtil.ToDir(targetHullAngle);
            return zone switch
            {
                HitZone.Front => forward,
                HitZone.Rear => -forward,
                HitZone.Side => new Vector2(-forward.y, forward.x), // 우측면 법선
                HitZone.Turret => forward,
                _ => forward
            };
        }

        /// <summary>공격자/대상 위치와 대상 차체 각도로 실제 입사각 계산</summary>
        public static float CalculateImpactAngleFromPositions(
            Vector2 attackerPos, Vector2 targetPos, float targetHullAngle, HitZone hitZone)
        {
            Vector2 shellDir = (targetPos - attackerPos).normalized;
            Vector2 armorNormal = GetArmorNormal(targetHullAngle, hitZone);
            return CalculateImpactAngle(shellDir, armorNormal);
        }

        /// <summary>유효 장갑 두께 = 기본 장갑 / cos(입사각)</summary>
        public static float CalculateEffectiveArmor(float baseArmor, float impactAngle)
        {
            if (impactAngle >= GameConstants.AutoRicochetAngle)
                return float.MaxValue; // 자동 도탄

            float rad = impactAngle * Mathf.Deg2Rad;
            float cosAngle = Mathf.Cos(rad);

            if (cosAngle <= 0.01f)
                return float.MaxValue;

            return baseArmor / cosAngle;
        }

        /// <summary>관통 판정 — 도탄/피격/관통 3단계</summary>
        public static ShotOutcome JudgePenetration(float penetration, float effectiveArmor)
        {
            if (effectiveArmor >= float.MaxValue)
                return ShotOutcome.Ricochet;

            float ratio = penetration / effectiveArmor;

            // 관통력이 유효장갑의 120% 이상 → 관통 (크리티컬)
            if (ratio > 1.2f)
                return ShotOutcome.Penetration;

            // 관통력이 유효장갑의 80~120% → 피격 (일반 데미지)
            if (ratio > 0.8f)
            {
                // 경계값 근처는 확률적
                float chance = (ratio - 0.8f) / 0.4f;
                return Random.value < chance * 0.3f ? ShotOutcome.Penetration : ShotOutcome.Hit;
            }

            // 관통력이 유효장갑의 80% 미만 → 도탄
            return ShotOutcome.Ricochet;
        }
    }
}
