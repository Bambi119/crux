using System.Collections;
using UnityEngine;
using Crux.Unit;

namespace Crux.Combat
{
    /// <summary>
    /// 반격 WeaponSelect 세션 상태기계 — 피격 후 사용자 반격 선택 흐름 관리.
    /// MonoBehaviour 미상속. BattleController가 StartCoroutine으로 타이머를 구동.
    /// </summary>
    public class CounterFireSession
    {
        private const int TimeoutSeconds = 3;

        // ===== 상태 =====
        private bool isActive;
        private int secondsLeft;
        private GridTankUnit pendingAttacker; // 반격 대상인 적 유닛 (공격해온 적)

        // ===== 외부 콜백 =====
        private System.Action<GridTankUnit> onCommit;   // (attacker) → CommitCounterFire 실행
        private System.Action onCancel;                  // CancelCounterFire 실행

        // ===== 공개 프로퍼티 =====
        public bool IsCounterFireMode => isActive;
        public int CounterFireSecondsLeft => secondsLeft;
        public GridTankUnit PendingAttacker => pendingAttacker;

        /// <summary>
        /// 반격 WeaponSelect 세션 진입.
        /// BattleController가 호출 — 이후 StartCoroutine(CountdownCoroutine())을 돌려야 함.
        /// </summary>
        public void Enter(GridTankUnit attacker, System.Action<GridTankUnit> commitAction, System.Action cancelAction)
        {
            isActive = true;
            secondsLeft = TimeoutSeconds;
            pendingAttacker = attacker;
            onCommit = commitAction;
            onCancel = cancelAction;
        }

        /// <summary>
        /// 사용자가 무기 선택 확정 — 타이머 중단, CommitCounterFire 호출.
        /// </summary>
        public void Commit()
        {
            if (!isActive) return;
            isActive = false;
            onCommit?.Invoke(pendingAttacker);
            pendingAttacker = null;
        }

        /// <summary>
        /// 반격 취소 ('반격 취소' 메뉴 또는 N키) — 타이머 중단, CancelCounterFire 호출.
        /// </summary>
        public void Cancel()
        {
            if (!isActive) return;
            isActive = false;
            onCancel?.Invoke();
            pendingAttacker = null;
        }

        /// <summary>
        /// 3초 카운트다운 코루틴 — BattleController.StartCoroutine으로 구동.
        /// 매 1초 secondsLeft 감소, 0 도달 시 주포 자동 Commit.
        /// </summary>
        public IEnumerator CountdownCoroutine()
        {
            while (secondsLeft > 0 && isActive)
            {
                yield return new WaitForSeconds(1f);
                if (!isActive) yield break;
                secondsLeft--;
                Debug.Log($"[COUNTER] 반격 타이머 — {secondsLeft}s 남음");
            }

            // 타임아웃: 주포 자동 반격
            if (isActive)
            {
                Debug.Log("[COUNTER] 반격 타임아웃 — 주포 자동 반격 실행");
                isActive = false;
                onCommit?.Invoke(pendingAttacker);
                pendingAttacker = null;
            }
        }

        /// <summary>세션 강제 초기화 (씬 재초기화 등 예외 상황용)</summary>
        public void Reset()
        {
            isActive = false;
            secondsLeft = 0;
            pendingAttacker = null;
            onCommit = null;
            onCancel = null;
        }
    }
}
