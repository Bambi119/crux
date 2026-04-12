using UnityEngine;
using Crux.Core;
using Crux.Data;

namespace Crux.Combat
{
    /// <summary>관통/도탄 판정 — 순수 로직 (MonoBehaviour 비의존)</summary>
    public static class PenetrationCalculator
    {
        /// <summary>피격 부위 판별 — 차체 6섹터 (60° 단위)</summary>
        /// <remarks>
        /// 공격이 "오는" 방향(공격자→대상)을 차체 로컬 각도로 변환:
        /// 전면 0° 기준 ±30°, 60° 단위로 6구역.
        /// </remarks>
        public static HitZone DetermineHitZone(Vector2 attackerPos, Vector2 targetPos, float targetHullAngle)
        {
            // 공격이 들어오는 방향 = 대상 → 공격자
            Vector2 incoming = (attackerPos - targetPos).normalized;
            float incomingAngle = AngleUtil.FromDir(incoming);

            // 차체 전면 기준 상대 각도 (0°=정면, +CW)
            float rel = Mathf.DeltaAngle(targetHullAngle, incomingAngle);
            // [-180, 180] → [0, 360)
            if (rel < 0) rel += 360f;

            // 60° 단위 섹터 (0° ± 30° = Front, 60° ± 30° = FrontRight, ...)
            int sector = Mathf.FloorToInt((rel + 30f) / 60f) % 6;
            return sector switch
            {
                0 => HitZone.Front,
                1 => HitZone.FrontRight,
                2 => HitZone.RearRight,
                3 => HitZone.Rear,
                4 => HitZone.RearLeft,
                5 => HitZone.FrontLeft,
                _ => HitZone.Front
            };
        }

        /// <summary>피격 부위의 기본 장갑 두께 반환 — 6섹터 지원</summary>
        public static float GetBaseArmor(ArmorProfile armor, HitZone zone)
        {
            return armor.GetArmor(zone);
        }

        /// <summary>입사각 계산 (포탄 방향 vs 장갑면 법선)</summary>
        public static float CalculateImpactAngle(Vector2 shellDirection, Vector2 armorNormal)
        {
            float dot = Vector2.Dot(shellDirection.normalized, armorNormal.normalized);
            return Mathf.Acos(Mathf.Abs(dot)) * Mathf.Rad2Deg;
        }

        /// <summary>피격 부위의 장갑면 법선 벡터 — 6섹터별 60° 단위 회전</summary>
        public static Vector2 GetArmorNormal(float targetHullAngle, HitZone zone)
        {
            // 차체 전면 = 0°, 각 섹터 중심이 60° 간격
            float sectorOffset = zone switch
            {
                HitZone.Front => 0f,
                HitZone.FrontRight => 60f,
                HitZone.RearRight => 120f,
                HitZone.Rear => 180f,
                HitZone.RearLeft => 240f,
                HitZone.FrontLeft => 300f,
                HitZone.Turret => 0f,
                _ => 0f
            };
            return AngleUtil.ToDir(targetHullAngle + sectorOffset);
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
