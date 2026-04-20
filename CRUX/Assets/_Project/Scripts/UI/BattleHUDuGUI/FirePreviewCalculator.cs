using UnityEngine;
using Crux.Grid;
using Crux.Unit;
using Crux.Data;
using Crux.Combat;
using Crux.Core;

namespace Crux.UI
{
    /// <summary>사격 프리뷰 계산 — 재사용 가능한 정적 유틸</summary>
    public static class FirePreviewCalculator
    {
        public struct FirePreviewResult
        {
            public int distance;
            public float baseHit;                // 거리 패널티 적용 후
            public float coverPenalty;           // 엄폐에 의한 차감
            public float smokePenalty;           // 연막에 의한 차감
            public float moraleBonus;            // 공격자 사기 명중 보정 (-0.15 ~ +0.05)
            public float finalHit;
            public HitZone hitZone;
            public float baseArmor;
            public float impactAngle;
            public float effectiveArmor;
            public float penetration;
            public ShotOutcome outcome;
            public float expectedDamagePerShot;  // 판정 반영 데미지 (명중 시)
            public int shotsPerAction;           // 주포 1, 기총 N
            public float totalExpected;          // shotsPerAction × finalHit × damagePerShot
            public bool coveredFromThisAngle;    // 현재 공격각에서 엄폐 유효
            public bool isMG;
        }

        /// <summary>선택 무기 기준 사격 결과 기대값 계산</summary>
        public static FirePreviewResult Compute(
            BattleController controller,
            GridTankUnit attacker,
            GridTankUnit target,
            WeaponType weapon)
        {
            var p = new FirePreviewResult();
            var grid = controller.Grid;
            p.distance = grid.GetDistance(attacker.GridPosition, target.GridPosition);

            // 명중률 분해
            float chance = controller.CalculateHitChance(p.distance, target);
            chance -= attacker.Modules.GetAccuracyPenalty();

            // 지형 고도 차 (공격자 > 목표면 보너스)
            var aCell = grid.GetCell(attacker.GridPosition);
            var tCell = grid.GetCell(target.GridPosition);
            if (aCell != null && tCell != null)
            {
                int elevDelta = Crux.Core.TerrainData.Elevation(aCell.Terrain)
                              - Crux.Core.TerrainData.Elevation(tCell.Terrain);
                if (elevDelta > 0) chance += elevDelta * 0.05f;
            }
            p.baseHit = Mathf.Clamp01(chance);

            // 사기 보정 (P3-c) — 공격자 Band 기반 AimModifier
            p.moraleBonus = 0f;
            var atkCrew = attacker.Crew;
            if (atkCrew != null)
            {
                p.moraleBonus = MoraleSystem.AimModifier(atkCrew.Band) * 0.01f;
                p.baseHit = Mathf.Clamp01(p.baseHit + p.moraleBonus);
            }

            // 엄폐 보정 — 엄폐물 + 지형 고유 엄폐 합산
            p.coverPenalty = 0f;
            if (tCell != null && tCell.HasCover && tCell.Cover != null && !tCell.Cover.IsDestroyed)
            {
                var atkDir = HexCoord.AttackDir(attacker.GridPosition, target.GridPosition, GameConstants.CellSize);
                if (tCell.Cover.IsCovered(atkDir))
                {
                    p.coveredFromThisAngle = true;
                    p.coverPenalty = tCell.Cover.CoverRate * 0.3f;
                }
            }
            if (tCell != null)
            {
                float intrinsic = Crux.Core.TerrainData.IntrinsicCoverRate(tCell.Terrain);
                if (intrinsic > 0f) p.coverPenalty += intrinsic * 0.3f;
            }

            // 은엄폐 (수풀·파편)
            int concealmentPct = tCell != null ? Crux.Core.TerrainData.Concealment(tCell.Terrain) : 0;
            float concealmentPenalty = concealmentPct * 0.01f;

            // 연막 보정
            p.smokePenalty = (tCell != null && tCell.HasSmoke) ? 0.4f : 0f;
            // 은엄폐를 연막 페널티에 합산 (별도 필드 없이 기존 구조 유지)
            p.smokePenalty += concealmentPenalty;

            // 기총 기본 명중률 보정
            if (weapon == WeaponType.CoaxialMG && controller.CoaxialMGData != null)
            {
                p.baseHit = Mathf.Clamp01(p.baseHit + controller.CoaxialMGData.accuracyModifier
                                         - attacker.Modules.GetMGAccuracyPenalty());
            }
            else if (weapon == WeaponType.MountedMG && controller.MountedMGData != null)
            {
                p.baseHit = Mathf.Clamp01(p.baseHit + controller.MountedMGData.accuracyModifier
                                         - attacker.Modules.GetMGAccuracyPenalty());
            }

            p.finalHit = Mathf.Clamp01(p.baseHit - p.coverPenalty - p.smokePenalty);

            // 피격 위치·장갑 — 현재 위치 기준
            p.hitZone = PenetrationCalculator.DetermineHitZone(
                attacker.transform.position, target.transform.position, target.HullAngle);
            p.baseArmor = PenetrationCalculator.GetBaseArmor(target.Data.armor, p.hitZone);
            p.impactAngle = PenetrationCalculator.CalculateImpactAngleFromPositions(
                attacker.transform.position, target.transform.position, target.HullAngle, p.hitZone);
            p.effectiveArmor = PenetrationCalculator.CalculateEffectiveArmor(p.baseArmor, p.impactAngle);

            // 관통력 / 데미지 — 무기에 따라
            float basePenetration;
            float baseDamage;
            if (weapon == WeaponType.MainGun)
            {
                basePenetration = attacker.currentAmmo != null ? attacker.currentAmmo.penetration : 100f;
                baseDamage = attacker.currentAmmo != null ? attacker.currentAmmo.damage : 10f;
                p.shotsPerAction = 1;
                p.isMG = false;

                // 거리 감쇠
                if (attacker.currentAmmo != null && attacker.currentAmmo.penetrationDropPerCell > 0)
                    basePenetration = Mathf.Max(1f, basePenetration - attacker.currentAmmo.penetrationDropPerCell * p.distance);
            }
            else
            {
                var mg = weapon == WeaponType.CoaxialMG ? controller.CoaxialMGData : controller.MountedMGData;
                basePenetration = mg != null ? mg.penetration : 15f;
                baseDamage = mg != null ? mg.damagePerShot : 2f;
                p.shotsPerAction = mg != null
                    ? Mathf.Max(1, mg.burstCount - attacker.Modules.GetBurstPenalty())
                    : 1;
                p.isMG = true;
            }

            p.penetration = basePenetration;

            // 판정 예측 — 확률 경계는 기대값으로 대체 (ratio 기반 결정론 표기)
            p.outcome = PredictOutcome(basePenetration, p.effectiveArmor);

            // 데미지 계산 (판정별)
            float outcomeMult = p.outcome switch
            {
                ShotOutcome.Penetration => p.isMG ? 2f : 2.5f,
                ShotOutcome.Hit => 1f,
                ShotOutcome.Ricochet => 0.03f,
                _ => 0f
            };
            p.expectedDamagePerShot = baseDamage * outcomeMult;
            p.totalExpected = p.shotsPerAction * p.finalHit * p.expectedDamagePerShot;
            return p;
        }

        /// <summary>결정론적 판정 예측 — JudgePenetration의 확률 구간을 단일값으로</summary>
        private static ShotOutcome PredictOutcome(float penetration, float effectiveArmor)
        {
            if (effectiveArmor >= float.MaxValue) return ShotOutcome.Ricochet;
            float ratio = penetration / effectiveArmor;
            if (ratio > 1.2f) return ShotOutcome.Penetration;
            if (ratio > 0.8f) return ShotOutcome.Hit;
            return ShotOutcome.Ricochet;
        }
    }
}
