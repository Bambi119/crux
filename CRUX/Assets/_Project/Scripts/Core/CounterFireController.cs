using UnityEngine;
using UnityEngine.SceneManagement;
using Crux.Unit;
using Crux.Combat;

namespace Crux.Core
{
    /// <summary>
    /// 반격 WeaponSelect 세션 로직 — BattleController에서 분리.
    /// Enter/Commit/Cancel 3개 메서드 전담. BattleController는 1줄 위임.
    /// </summary>
    internal class CounterFireController
    {
        private readonly BattleController bc;
        private readonly CounterFireSession session;

        internal CounterFireController(BattleController owner, CounterFireSession session)
        {
            bc = owner;
            this.session = session;
        }

        // ===== 세션 상태 조회 =====

        internal bool IsCounterFireMode => session.IsCounterFireMode;
        internal int  CounterFireSecondsLeft => session.CounterFireSecondsLeft;
        internal GridTankUnit PendingAttacker => session.PendingAttacker;

        // ===== 세션 진입 =====

        /// <summary>
        /// 피격 후 반격 WeaponSelect 세션 진입.
        /// BattleStateManager.ApplyPendingResult()에서 호출.
        /// </summary>
        internal void Enter(GridTankUnit attackerEnemy)
        {
            if (attackerEnemy == null || attackerEnemy.IsDestroyed)
            {
                Debug.LogWarning("[COUNTER] EnterCounterFireWeaponSelect: 공격자 없음/격파됨 — 세션 진입 취소");
                return;
            }

            bc.SelectedWeaponInternal = WeaponType.MainGun;
            bc.InputModeInternal = BattleController.InputModeEnum.WeaponSelect;
            // pendingTarget 재사용 — 반격 세션에서 playerUnit이 공격하는 대상은 attackerEnemy
            bc.PendingTargetInternal = attackerEnemy;

            session.Enter(
                attackerEnemy,
                (_atk) => Commit(bc.SelectedWeaponInternal),
                () => Cancel()
            );

            bc.StartCoroutinePublic(session.CountdownCoroutine());

            bc.ShowBanner(
                $"[반격] {bc.PlayerUnitRef?.Data?.tankName} → {attackerEnemy.Data?.tankName}" +
                $"  [Space/Enter] 확정  [N/0] 취소  ({session.CounterFireSecondsLeft}s)",
                new Color(1f, 0.5f, 0.2f),
                3.5f);

            Debug.Log($"[COUNTER] 반격 WeaponSelect 세션 진입 — 공격자:{attackerEnemy.Data?.tankName}");
        }

        // ===== 세션 확정 =====

        /// <summary>사용자가 무기 선택 후 반격 확정 — FireActionScene 씬 전환</summary>
        internal void Commit(WeaponType weapon)
        {
            var attacker = session.PendingAttacker;
            session.Commit();

            if (bc.PlayerUnitRef == null || bc.PlayerUnitRef.IsDestroyed)
            {
                Debug.LogWarning("[COUNTER] CommitCounterFire: playerUnit 없음/격파됨");
                bc.StateManagerRef.ResumeEnemyTurn();
                return;
            }
            if (attacker == null || attacker.IsDestroyed)
            {
                Debug.LogWarning("[COUNTER] CommitCounterFire: 공격자 격파됨 — 반격 취소");
                bc.StateManagerRef.ResumeEnemyTurn();
                return;
            }

            bc.FireExecutorRef.EnqueueCounterFire(bc.PlayerUnitRef, attacker, weapon);
            bc.StateManagerRef.Save();
            SceneManager.LoadScene("FireActionScene");
        }

        // ===== 세션 취소 =====

        /// <summary>반격 취소 — 타이머 중단 + 적 턴 재개</summary>
        internal void Cancel()
        {
            session.Cancel();
            bc.PendingTargetInternal = null;
            bc.InputModeInternal = BattleController.InputModeEnum.Select;
            Debug.Log("[COUNTER] 반격 취소 — 적 턴 재개");
            bc.StateManagerRef.ResumeEnemyTurn();
        }
    }
}
