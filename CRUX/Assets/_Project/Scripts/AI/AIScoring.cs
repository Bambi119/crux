using UnityEngine;
using Crux.Core;
using Crux.Grid;
using Crux.Unit;
using TerrainData = Crux.Core.TerrainData;

namespace Crux.AI
{
    /// <summary>공통 팩터 스코어 함수 — 모든 Role이 공유</summary>
    /// <remarks>
    /// 각 함수는 [AIContext + 후보 위치 + (선택) 목표]를 받아 float 반환.
    /// 실제 score = Σ weight × factor. 정규화는 팩터 내부에서 수행.
    /// 기획 §2.2 Factor Vocabulary 참조.
    /// </remarks>
    public static class AIScoring
    {
        // ===== 기본 팩터 =====

        /// <summary>거리 팩터 — 정규화된 [0, 1]. 가까울수록 1에 근접</summary>
        public static float DistFactor(AIContext ctx, Vector2Int from, GridTankUnit target)
        {
            if (target == null) return 0f;
            int d = ctx.grid.GetDistance(from, target.GridPosition);
            // 사거리를 기준으로 정규화
            return 1f - Mathf.Clamp01((float)d / Mathf.Max(1, ctx.maxFireRange));
        }

        /// <summary>LOS 유효 여부 — 0 or 1</summary>
        public static float LosToFactor(AIContext ctx, Vector2Int from, GridTankUnit target)
        {
            if (target == null) return 0f;
            return ctx.grid.HasLOS(from, target.GridPosition) ? 1f : 0f;
        }

        /// <summary>
        /// 엄폐 팩터 — 내가 from 위치에 있을 때 target 방향에서 오는 공격에 대한 엄폐율.
        /// 엄폐물 오브젝트 + 지형 자체 엄폐 합산.
        /// </summary>
        public static float CoverFactor(AIContext ctx, Vector2Int from, GridTankUnit target)
        {
            if (target == null) return 0f;
            var cell = ctx.grid.GetCell(from);
            if (cell == null) return 0f;

            float cov = TerrainData.IntrinsicCoverRate(cell.Terrain);

            if (cell.HasCover && cell.Cover != null && !cell.Cover.IsDestroyed)
            {
                // 공격자(target) → 나(from) 방향으로 방호면 체크
                var atkDir = HexCoord.AttackDir(target.GridPosition, from, GameConstants.CellSize);
                if (cell.Cover.IsCovered(atkDir))
                    cov += cell.Cover.CoverRate;
            }
            return Mathf.Clamp01(cov);
        }

        /// <summary>은엄폐 팩터 (수풀 등)</summary>
        public static float ConcealmentFactor(AIContext ctx, Vector2Int from)
        {
            var cell = ctx.grid.GetCell(from);
            if (cell == null) return 0f;
            return TerrainData.Concealment(cell.Terrain) * 0.01f;
        }

        /// <summary>노출도 팩터 — from 셀을 LOS로 볼 수 있는 적(foe) 수. 0=안전, N=위험</summary>
        public static float ExposureFactor(AIContext ctx, Vector2Int from)
        {
            int count = 0;
            foreach (var foe in ctx.foes)
            {
                if (foe == null || foe.IsDestroyed) continue;
                if (ctx.grid.HasLOS(foe.GridPosition, from)) count++;
            }
            return count; // 가중치 음수 부호로 감산
        }

        /// <summary>플랭크 팩터 — 목표의 측/후면 영역에 내가 위치했는지 (0~1)</summary>
        public static float FlankFactor(AIContext ctx, Vector2Int from, GridTankUnit target)
        {
            if (target == null) return 0f;
            Vector3 fromW = ctx.grid.GridToWorld(from);
            Vector3 tgtW = ctx.grid.GridToWorld(target.GridPosition);
            Vector2 dir = ((Vector2)(fromW - tgtW)).normalized;
            float dirAngle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            float delta = Mathf.Abs(Mathf.DeltaAngle(target.HullAngle, dirAngle));
            // 정면(0~60°) = 0, 측면(60~120°) = 0.5, 후면(120~180°) = 1
            if (delta < 60f) return 0f;
            if (delta < 120f) return 0.5f;
            return 1f;
        }

        /// <summary>고도차 팩터 — 내가 높으면 + (정수 스칼라 그대로)</summary>
        public static float ElevationFactor(AIContext ctx, Vector2Int from, GridTankUnit target)
        {
            if (target == null) return 0f;
            var myCell = ctx.grid.GetCell(from);
            var tCell = ctx.grid.GetCell(target.GridPosition);
            if (myCell == null || tCell == null) return 0f;
            return TerrainData.Elevation(myCell.Terrain) - TerrainData.Elevation(tCell.Terrain);
        }

        /// <summary>Kill Confidence Score — 기대 데미지 × HP 비율 기반 간이 추정</summary>
        public static float KcsFactor(AIContext ctx, Vector2Int from, GridTankUnit target)
        {
            if (target == null) return 0f;
            // 사거리 초과면 0
            int d = ctx.grid.GetDistance(from, target.GridPosition);
            if (d > ctx.maxFireRange) return 0f;
            if (!ctx.grid.HasLOS(from, target.GridPosition)) return 0f;

            // 명중률 근사 — 실제 CalculateHitChanceWithCover와 동일 공식 주요부만
            float chance = GameConstants.BaseAccuracy - d * GameConstants.DistancePenaltyPerCell;
            var tCell = ctx.grid.GetCell(target.GridPosition);
            if (tCell != null)
            {
                if (tCell.HasCover && tCell.Cover != null && !tCell.Cover.IsDestroyed)
                {
                    var atkDir = HexCoord.AttackDir(from, target.GridPosition, GameConstants.CellSize);
                    if (tCell.Cover.IsCovered(atkDir))
                        chance -= tCell.Cover.CoverRate * 0.3f;
                }
                if (tCell.HasSmoke) chance -= 0.4f;
                chance -= TerrainData.Concealment(tCell.Terrain) * 0.01f;
            }
            chance = Mathf.Clamp01(chance);

            // 간이 기대 데미지: 목표 HP 대비 공격자 탄약 데미지 비율
            float myDmg = ctx.self.currentAmmo != null ? ctx.self.currentAmmo.damage : 10f;
            float targetHP = Mathf.Max(1f, target.CurrentHP);
            float ratio = myDmg / targetHP;
            return chance * Mathf.Clamp01(ratio);
        }

        // ===== 통합 스코어 =====

        /// <summary>
        /// Engage 상태에서 (from, target) 쌍 스코어 계산.
        /// dist/cover/kcs/exposure/concealment/elev 팩터만 사용.
        /// </summary>
        public static float ScoreEngage(AIContext ctx, Vector2Int from, GridTankUnit target,
                                         AIWeights.Weights w)
        {
            float s = 0f;
            s += w.dist * DistFactor(ctx, from, target);
            s += w.cover * CoverFactor(ctx, from, target);
            s += w.kcs * KcsFactor(ctx, from, target);
            s += w.exposure * ExposureFactor(ctx, from);
            s += w.concealment * ConcealmentFactor(ctx, from);
            s += w.elev * ElevationFactor(ctx, from, target);
            return s;
        }
    }
}
