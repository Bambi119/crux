using System.Collections.Generic;
using UnityEngine;
using Crux.Core;
using Crux.Grid;
using Crux.Unit;

namespace Crux.AI
{
    /// <summary>
    /// 개별 적 유닛에 붙는 AI 컨트롤러. Role과 상황에 따라 State를 결정하고
    /// 최적 행동(이동+사격)을 반환.
    /// </summary>
    /// <remarks>
    /// P2: Medium/Engage 단일 Role만 구현. 이후 Role은 P3에서 확장.
    /// BattleController.ProcessEnemyTurn이 이 객체의 Decide()를 호출.
    /// </remarks>
    [DisallowMultipleComponent]
    public class EnemyAIController : MonoBehaviour
    {
        [SerializeField] private AIRole role = AIRole.Medium;
        public AIRole Role { get => role; set => role = value; }

        private GridTankUnit self;

        private void Awake()
        {
            self = GetComponent<GridTankUnit>();
        }

        /// <summary>
        /// 한 턴의 행동을 결정. BattleController가 매 적 턴마다 호출.
        /// Role별로 분기하여 상황에 맞는 State 선택.
        /// </summary>
        public AIDecision Decide(GridManager grid, List<GridTankUnit> allies, List<GridTankUnit> foes)
        {
            if (self == null) self = GetComponent<GridTankUnit>();
            if (self == null || self.IsDestroyed) return AIDecision.Wait(AIState.Engage);

            var ctx = BuildContext(grid, allies, foes);

            return role switch
            {
                AIRole.Vehicle => DecideVehicle(ctx),
                AIRole.Heavy => DecideHeavy(ctx),
                AIRole.Medium => DecideMedium(ctx),
                _ => DecideEngage(ctx, AIWeights.Get(role, AIState.Engage), AIState.Engage)
            };
        }

        // ===== 상황 평가 =====

        private AIContext BuildContext(GridManager grid, List<GridTankUnit> allies, List<GridTankUnit> foes)
        {
            var ctx = new AIContext
            {
                self = self,
                grid = grid,
                allies = allies,
                foes = foes,
                myAP = self.CurrentAP,
                maxFireRange = GameConstants.MaxFireRange,
                visibleFoes = new List<GridTankUnit>()
            };

            // LOS 가능 적 필터
            foreach (var f in foes)
            {
                if (f == null || f.IsDestroyed) continue;
                if (grid.HasLOS(self.GridPosition, f.GridPosition))
                    ctx.visibleFoes.Add(f);
            }
            return ctx;
        }

        // ===== Engage 상태 =====

        /// <summary>
        /// Engage: 사거리/LOS 확보 후 사격. 최선의 (이동, 사격 대상) 조합 선택.
        /// 현 위치에서 바로 사격 가능하면 이동 없이 사격.
        /// 아니면 이동 후보 × 타깃 후보 전체를 탐색.
        /// </summary>
        private AIDecision DecideEngage(AIContext ctx, AIWeights.Weights w, AIState state)
        {
            // 타깃 후보: LOS 불문, 전체 생존 적
            var targets = new List<GridTankUnit>();
            foreach (var f in ctx.foes)
                if (f != null && !f.IsDestroyed) targets.Add(f);

            if (targets.Count == 0) return AIDecision.Wait(state);

            // 1) 현 위치에서 바로 사격 가능 — 우선 평가
            AIDecision best = AIDecision.Wait(state);
            best.score = float.MinValue;

            if (self.CanFire())
            {
                foreach (var t in targets)
                {
                    int d = ctx.grid.GetDistance(self.GridPosition, t.GridPosition);
                    if (d > ctx.maxFireRange) continue;
                    if (!ctx.grid.HasLOS(self.GridPosition, t.GridPosition)) continue;

                    float s = AIScoring.ScoreEngage(ctx, self.GridPosition, t, w);
                    // 사격 가능 보너스 — 이동 없이 끝낼 수 있으면 점수 가산
                    s += 1.5f;
                    if (s > best.score)
                    {
                        best = new AIDecision
                        {
                            moveTo = null,
                            fireTarget = t,
                            score = s,
                            state = state
                        };
                    }
                }
            }

            // 2) 이동 후보 × 타깃 후보 — 이동 후 사격 가능성까지 포함
            if (self.CanMove() && ctx.myAP > 0)
            {
                var reachable = ctx.grid.GetReachableCells(self.GridPosition, ctx.myAP, self);
                foreach (var pos in reachable)
                {
                    // 이동 목적지에 점유가 없어야 함 (GetReachableCells가 보장하지만 재확인)
                    var cell = ctx.grid.GetCell(pos);
                    if (cell == null || cell.Occupant != null) continue;

                    foreach (var t in targets)
                    {
                        int d = ctx.grid.GetDistance(pos, t.GridPosition);
                        float s = AIScoring.ScoreEngage(ctx, pos, t, w);

                        if (s > best.score)
                        {
                            // 이동 후 사거리 내·LOS면 사격도 포함
                            bool canFireFromNew = d <= ctx.maxFireRange
                                && ctx.grid.HasLOS(pos, t.GridPosition)
                                && self.CanFire();

                            best = new AIDecision
                            {
                                moveTo = pos,
                                fireTarget = canFireFromNew ? t : null,
                                score = s,
                                state = state
                            };
                        }
                    }
                }
            }

            // 3) 아무 후보도 개선 못하면 현 위치 유지
            if (best.score == float.MinValue)
                return AIDecision.Wait(state);

            return best;
        }

        // ===== Vehicle (Scout Flanker) 상태 =====

        /// <summary>Vehicle: HP 기반 Retreat → Engage → Reposition → Flank 우선순위</summary>
        private AIDecision DecideVehicle(AIContext ctx)
        {
            // HP ≤ 40% → Retreat
            float hpRatio = self.Data != null && self.Data.maxHP > 0
                ? self.CurrentHP / self.Data.maxHP
                : 1f;
            if (hpRatio <= 0.4f)
                return DecideRetreat(ctx);

            // 공격각 확보(FlankFactor > 0.4 타겟 존재) → Engage 1턴
            foreach (var t in ctx.visibleFoes)
            {
                int d = ctx.grid.GetDistance(self.GridPosition, t.GridPosition);
                if (d <= ctx.maxFireRange && AIScoring.FlankFactor(ctx, self.GridPosition, t) >= 0.4f)
                {
                    var w = AIWeights.Get(AIRole.Vehicle, AIState.Engage);
                    return DecideEngage(ctx, w, AIState.Engage);
                }
            }

            // 노출 위험 → Reposition
            if (AIScoring.ExposureFactor(ctx, self.GridPosition) >= 2f)
                return DecideReposition(ctx);

            // 기본: Flank
            return DecideFlank(ctx, AIRole.Vehicle);
        }

        // ===== Heavy (Anvil) 상태 =====

        /// <summary>Heavy: Suppress(측면 침입자 견제) → Guard(정지 방어)</summary>
        private AIDecision DecideHeavy(AIContext ctx)
        {
            // Suppress: 시야 내 적이 아군 측면에 접근 중인지 체크
            foreach (var foe in ctx.visibleFoes)
            {
                foreach (var ally in ctx.allies)
                {
                    if (ally == null || ally.IsDestroyed || ally == self) continue;
                    if (AIScoring.FlankFactor(ctx, foe.GridPosition, ally) >= 0.5f)
                    {
                        var sw = AIWeights.Get(AIRole.Heavy, AIState.Suppress);
                        return DecideEngageTarget(ctx, foe, sw, AIState.Suppress);
                    }
                }
            }

            // 기본: Guard (이동 없이 최적 타깃 사격)
            return DecideGuard(ctx);
        }

        /// <summary>Guard: 이동 없이 현 위치에서 최적 타깃 선택 사격</summary>
        private AIDecision DecideGuard(AIContext ctx)
        {
            if (!self.CanFire()) return AIDecision.Wait(AIState.Guard);

            var w = AIWeights.Get(AIRole.Heavy, AIState.Guard);
            AIDecision best = AIDecision.Wait(AIState.Guard);
            best.score = float.MinValue;

            foreach (var t in ctx.foes)
            {
                if (t == null || t.IsDestroyed) continue;
                int d = ctx.grid.GetDistance(self.GridPosition, t.GridPosition);
                if (d > ctx.maxFireRange) continue;
                if (!ctx.grid.HasLOS(self.GridPosition, t.GridPosition)) continue;

                float s = AIScoring.ScoreEngage(ctx, self.GridPosition, t, w);
                if (s > best.score)
                    best = new AIDecision
                    {
                        moveTo = null,
                        fireTarget = t,
                        score = s,
                        state = AIState.Guard
                    };
            }
            return best.score == float.MinValue ? AIDecision.Wait(AIState.Guard) : best;
        }

        // ===== Medium (Balanced Opportunist) 상태 =====

        /// <summary>Medium: Flank(측면 확보) → Reposition(노출 회피) → Engage</summary>
        private AIDecision DecideMedium(AIContext ctx)
        {
            // Flank 트리거: 시야 내 적이 측/후면 노출
            foreach (var t in ctx.visibleFoes)
            {
                if (AIScoring.FlankFactor(ctx, self.GridPosition, t) >= 0.4f)
                    return DecideFlank(ctx, AIRole.Medium);
            }

            // Reposition 트리거: 자신이 노출 >= 2 AND 이동 가능한 엄폐 있음
            if (AIScoring.ExposureFactor(ctx, self.GridPosition) >= 2f && self.CanMove())
                return DecideReposition(ctx);

            // 기본: Engage
            var w = AIWeights.Get(AIRole.Medium, AIState.Engage);
            return DecideEngage(ctx, w, AIState.Engage);
        }

        // ===== 공용 상태 =====

        /// <summary>Flank: 측면 위치 확보 후 사격. 공격 각도 최적화</summary>
        private AIDecision DecideFlank(AIContext ctx, AIRole r)
        {
            var targets = new List<GridTankUnit>();
            foreach (var f in ctx.foes)
                if (f != null && !f.IsDestroyed) targets.Add(f);
            if (targets.Count == 0) return AIDecision.Wait(AIState.Flank);

            var w = AIWeights.Get(r, AIState.Flank);
            AIDecision best = AIDecision.Wait(AIState.Flank);
            best.score = float.MinValue;

            if (self.CanMove() && ctx.myAP > 0)
            {
                var reachable = ctx.grid.GetReachableCells(self.GridPosition, ctx.myAP, self);
                foreach (var pos in reachable)
                {
                    var cell = ctx.grid.GetCell(pos);
                    if (cell == null || cell.Occupant != null) continue;

                    foreach (var t in targets)
                    {
                        float s = AIScoring.ScoreFlank(ctx, pos, t, w);
                        if (s > best.score)
                        {
                            int d = ctx.grid.GetDistance(pos, t.GridPosition);
                            bool canFire = d <= ctx.maxFireRange
                                && ctx.grid.HasLOS(pos, t.GridPosition)
                                && self.CanFire();
                            best = new AIDecision
                            {
                                moveTo = pos,
                                fireTarget = canFire ? t : null,
                                score = s,
                                state = AIState.Flank
                            };
                        }
                    }
                }
            }
            // 이동 못하면 현위치에서 Engage로 폴백
            if (best.score == float.MinValue)
            {
                var ew = AIWeights.Get(r, AIState.Engage);
                return DecideEngage(ctx, ew, AIState.Engage);
            }
            return best;
        }

        /// <summary>Reposition: 노출 위험 회피 + 엄폐 이동</summary>
        private AIDecision DecideReposition(AIContext ctx)
        {
            if (!self.CanMove()) return AIDecision.Wait(AIState.Reposition);
            var w = AIWeights.Get(role, AIState.Reposition);

            AIDecision best = AIDecision.Wait(AIState.Reposition);
            best.score = float.MinValue;

            var reachable = ctx.grid.GetReachableCells(self.GridPosition, ctx.myAP, self);
            foreach (var pos in reachable)
            {
                var cell = ctx.grid.GetCell(pos);
                if (cell == null || cell.Occupant != null) continue;
                float s = AIScoring.ScoreReposition(ctx, pos, w);
                if (s > best.score)
                    best = new AIDecision
                    {
                        moveTo = pos,
                        fireTarget = null,
                        score = s,
                        state = AIState.Reposition
                    };
            }
            return best.score == float.MinValue ? AIDecision.Wait(AIState.Reposition) : best;
        }

        /// <summary>Retreat: 저HP 후퇴. 적으로부터 최대 거리 확보</summary>
        private AIDecision DecideRetreat(AIContext ctx)
        {
            if (!self.CanMove()) return AIDecision.Wait(AIState.Retreat);
            var w = AIWeights.Get(role, AIState.Retreat);

            AIDecision best = AIDecision.Wait(AIState.Retreat);
            best.score = float.MinValue;

            var reachable = ctx.grid.GetReachableCells(self.GridPosition, ctx.myAP, self);
            foreach (var pos in reachable)
            {
                var cell = ctx.grid.GetCell(pos);
                if (cell == null || cell.Occupant != null) continue;

                // 후퇴 스코어: 적과의 평균 거리 + 엄폐 - 노출도
                float totalDist = 0f;
                int foeCount = 0;
                foreach (var f in ctx.foes)
                {
                    if (f == null || f.IsDestroyed) continue;
                    totalDist += ctx.grid.GetDistance(pos, f.GridPosition);
                    foeCount++;
                }
                float avgDist = foeCount > 0 ? totalDist / foeCount : 0f;
                float s = avgDist * 0.5f
                    + AIScoring.CoverFactor(ctx, pos, null) * 2.0f
                    + AIScoring.ExposureFactor(ctx, pos) * -1.5f;
                if (s > best.score)
                    best = new AIDecision
                    {
                        moveTo = pos,
                        fireTarget = null,
                        score = s,
                        state = AIState.Retreat
                    };
            }
            return best.score == float.MinValue ? AIDecision.Wait(AIState.Retreat) : best;
        }

        /// <summary>특정 타깃을 지정해 사격. Heavy Suppress 등에서 사용</summary>
        private AIDecision DecideEngageTarget(AIContext ctx, GridTankUnit target,
                                              AIWeights.Weights w, AIState state)
        {
            if (!self.CanFire()) return AIDecision.Wait(state);
            int d = ctx.grid.GetDistance(self.GridPosition, target.GridPosition);
            if (d > ctx.maxFireRange || !ctx.grid.HasLOS(self.GridPosition, target.GridPosition))
                return AIDecision.Wait(state);
            float s = AIScoring.ScoreEngage(ctx, self.GridPosition, target, w);
            return new AIDecision
            {
                moveTo = null,
                fireTarget = target,
                score = s,
                state = state
            };
        }
    }
}
