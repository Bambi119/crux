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
        /// </summary>
        public AIDecision Decide(GridManager grid, List<GridTankUnit> allies, List<GridTankUnit> foes)
        {
            if (self == null) self = GetComponent<GridTankUnit>();
            if (self == null || self.IsDestroyed) return AIDecision.Wait(AIState.Engage);

            var ctx = BuildContext(grid, allies, foes);

            // P2: Role 무관하게 Engage 단일 State만 사용
            var state = AIState.Engage;
            var weights = AIWeights.Get(role, state);

            return DecideEngage(ctx, weights, state);
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
                var reachable = ctx.grid.GetReachableCells(self.GridPosition, ctx.myAP);
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
    }
}
