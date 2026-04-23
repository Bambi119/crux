using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Crux.Grid;
using Crux.Unit;
using Crux.Data;
using Crux.Combat;

namespace Crux.Core
{
    /// <summary>전투 상태 저장·복원 — 연출 씬 왕복 시 상태 영속성 관리</summary>
    public class BattleStateManager
    {
        private readonly BattleController ctrl;

        public BattleStateManager(BattleController ctrl)
        {
            this.ctrl = ctrl;
        }

        /// <summary>씬 전환 직전 — 전투 상태 저장</summary>
        public void Save()
        {
            // 연출 씬 복귀 대상 씬 기록 (TerrainTestScene 등에서 사격 시 원본으로 안 돌아가게)
            BattleStateStorage.SourceScene = SceneManager.GetActiveScene().name;
            Debug.Log($"[CRUX] SaveBattleState — Player at ({ctrl.PlayerUnitRef.GridPosition.x},{ctrl.PlayerUnitRef.GridPosition.y}), return → {BattleStateStorage.SourceScene}");

            var enemyStates = new UnitSaveData[ctrl.EnemyUnitsRef.Count];
            for (int i = 0; i < ctrl.EnemyUnitsRef.Count; i++)
                enemyStates[i] = ctrl.EnemyUnitsRef[i].SaveState();

            // 엄폐물 HP 저장
            var coverHPs = new List<float>();
            for (int x = 0; x < ctrl.GridRef.Width; x++)
                for (int y = 0; y < ctrl.GridRef.Height; y++)
                {
                    var cell = ctrl.GridRef.GetCell(new Vector2Int(x, y));
                    if (cell != null && cell.Type == CellType.Cover && cell.Cover != null)
                        coverHPs.Add(cell.Cover.CurrentHP);
                }

            // 연막 상태 저장
            var smokeTurns = new List<int>();
            for (int x = 0; x < ctrl.GridRef.Width; x++)
                for (int y = 0; y < ctrl.GridRef.Height; y++)
                {
                    var c = ctrl.GridRef.GetCell(new Vector2Int(x, y));
                    smokeTurns.Add(c != null ? c.SmokeTurnsLeft : 0);
                }

            BattleStateStorage.Save(new BattleSaveData
            {
                playerState = ctrl.PlayerUnitRef.SaveState(),
                enemyStates = enemyStates,
                turnCount = ctrl.TurnCountInternal,
                phase = ctrl.CurrentPhaseInternal,
                coverHPs = coverHPs.ToArray(),
                smokeTurns = smokeTurns.ToArray(),
                nextEnemyIndex = ctrl.CurrentEnemyIndexInternal
            });
        }

        /// <summary>연출 씬 복귀 — InitializeBattle 재호출 + 저장 상태 복원 + 데미지 적용</summary>
        public void ApplyPendingResult()
        {
            Debug.Log($"[CRUX] ApplyPendingResult — HasSavedState: {BattleStateStorage.HasSavedState}");

            // 먼저 씬 초기화 (그리드, 유닛 생성)
            ctrl.ReinitializeBattle();

            // 저장된 상태 복원
            if (BattleStateStorage.HasSavedState)
            {
                var state = BattleStateStorage.SavedState;

                // 플레이어 복원
                ctrl.PlayerUnitRef.RestoreState(ctrl.GridRef, state.playerState);

                // 적 복원
                for (int i = 0; i < ctrl.EnemyUnitsRef.Count && i < state.enemyStates.Length; i++)
                    ctrl.EnemyUnitsRef[i].RestoreState(ctrl.GridRef, state.enemyStates[i]);

                // 엄폐물 HP 복원
                int coverIdx = 0;
                for (int x = 0; x < ctrl.GridRef.Width; x++)
                    for (int y = 0; y < ctrl.GridRef.Height; y++)
                    {
                        var cell = ctrl.GridRef.GetCell(new Vector2Int(x, y));
                        if (cell != null && cell.Type == CellType.Cover && cell.Cover != null)
                        {
                            if (coverIdx < state.coverHPs.Length)
                            {
                                float hp = state.coverHPs[coverIdx++];
                                float dmg = cell.Cover.CurrentHP - hp;
                                if (dmg > 0)
                                    cell.Cover.TakeDamage(dmg);
                            }
                        }
                    }

                // 연막 복원
                if (state.smokeTurns != null)
                {
                    int si = 0;
                    for (int x2 = 0; x2 < ctrl.GridRef.Width; x2++)
                        for (int y2 = 0; y2 < ctrl.GridRef.Height; y2++)
                        {
                            if (si < state.smokeTurns.Length)
                            {
                                var sc = ctrl.GridRef.GetCell(new Vector2Int(x2, y2));
                                if (sc != null)
                                {
                                    sc.SmokeTurnsLeft = state.smokeTurns[si];
                                    if (sc.HasSmoke) ctrl.VisualizerRef.ShowSmoke(sc.Position);
                                }
                                si++;
                            }
                        }
                }

                ctrl.TurnCountInternal = state.turnCount;
                ctrl.CurrentPhaseInternal = state.phase;

                // 연출 결과 데미지 적용 — 모든 큐된 액션 순회 (메인 공격 + 반격 양쪽)
                Debug.Log($"[CRUX] ApplyPendingResult — action queue count: {FireActionContext.Actions.Count}");
                for (int ai = 0; ai < FireActionContext.Actions.Count; ai++)
                {
                    var actionData = FireActionContext.Actions[ai];
                    GridTankUnit target = null;

                    if (actionData.targetSide == PlayerSide.Enemy
                        && actionData.targetUnitIndex >= 0
                        && actionData.targetUnitIndex < ctrl.EnemyUnitsRef.Count)
                        target = ctrl.EnemyUnitsRef[actionData.targetUnitIndex];
                    else if (actionData.targetSide == PlayerSide.Player)
                        target = ctrl.PlayerUnitRef;

                    if (target == null || target.IsDestroyed) continue;

                    if (actionData.weaponType == WeaponType.MainGun)
                    {
                        // 주포 데미지 — 사전 롤된 결과 적용
                        if (actionData.result.hit && actionData.result.damageDealt > 0)
                        {
                            Debug.Log($"[CRUX] Apply action[{ai}] main → {target.Data?.tankName} dmg={actionData.result.damageDealt}");
                            target.ApplyPrerolledDamage(new DamageInfo
                            {
                                damage = actionData.result.damageDealt,
                                outcome = actionData.result.outcome,
                                hitZone = actionData.result.hitZone
                            }, actionData.mainOutcome);
                        }
                    }
                    else if (actionData.mgResults != null)
                    {
                        // 기총 총 데미지 합산 + 사전 롤된 모듈/화재/격파 적용
                        float total = 0f;
                        bool anyPen = false;
                        HitZone zone = HitZone.Front;
                        foreach (var r in actionData.mgResults)
                        {
                            if (r.hit && r.damageDealt > 0)
                            {
                                total += r.damageDealt;
                                if (r.outcome == ShotOutcome.Penetration) anyPen = true;
                                zone = r.hitZone;
                            }
                        }
                        if (total > 0)
                        {
                            Debug.Log($"[CRUX] Apply action[{ai}] MG → {target.Data?.tankName} dmg={total}");
                            target.ApplyPrerolledDamage(new DamageInfo
                            {
                                damage = total,
                                outcome = anyPen ? ShotOutcome.Penetration : ShotOutcome.Hit,
                                hitZone = zone
                            }, actionData.mgAggregateOutcome);
                        }
                    }
                }

                // 먼저 클리어 — 적 턴 재개 시 새 데이터를 덮어쓸 수 있도록
                int nextEnemy = state.nextEnemyIndex;
                TurnPhase savedPhase = state.phase;

                BattleStateStorage.Clear();
                FireActionContext.Clear();

                // 적 턴 중이었으면 나머지 적 행동 이어서 진행
                if (savedPhase == TurnPhase.EnemyTurn)
                {
                    ctrl.CurrentPhaseInternal = TurnPhase.EnemyTurn;
                    ctrl.StartProcessEnemyTurnFrom(nextEnemy);
                }

                return;
            }

            FireActionContext.Clear();
        }
    }
}
