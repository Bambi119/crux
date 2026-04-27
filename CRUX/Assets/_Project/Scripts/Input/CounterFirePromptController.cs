using System.Collections;
using UnityEngine;
using Crux.Combat;
using Crux.Unit;
using Crux.Grid;

namespace Crux.PlayerInput
{
    /// <summary>
    /// 플레이어 반격 확인 프롬프트 — 적 사격 직전 Y/N 입력 (1.5초 자동 확인).
    /// BattleController에서 StartCoroutine(CounterFirePromptController.Prompt(...))로 호출.
    /// </summary>
    public static class CounterFirePromptController
    {
        private const float TimeoutSeconds = 1.5f;

        /// <summary>
        /// 반격 프롬프트 코루틴 — BattleController가 yield return StartCoroutine(Prompt(...))으로 사용.
        /// 플레이어가 반격 가능한 경우에만 표시; 조건 미충족 시 즉시 반환.
        /// </summary>
        public static IEnumerator Prompt(
            GridTankUnit playerUnit,
            GridTankUnit attacker,
            GridManager grid,
            System.Action<string, UnityEngine.Color, float> showBanner)
        {
            // CounterConfirmed는 매 턴 시작 시 true로 초기화됨 — 먼저 false로 리셋
            playerUnit.SetCounterConfirmed(false);

            // 반격 가능 여부 사전 확인 (CounterConfirmed 제외 7개 조건)
            bool canPotentiallyCounter = CanCounterIgnoringConfirmation(playerUnit, attacker, grid);
            if (!canPotentiallyCounter)
            {
                // 조건 미충족 — 반격 없음, 즉시 반환
                return null_coroutine();
            }

            // 프롬프트 표시
            showBanner?.Invoke(
                $"[Y] 반격 — {playerUnit.Data?.tankName} → {attacker.Data?.tankName}  [N] 포기  ({TimeoutSeconds:F0}s 자동 확인)",
                new Color(0.3f, 0.8f, 1f),
                TimeoutSeconds + 0.3f);

            return PromptCoroutine(playerUnit, attacker);
        }

        private static IEnumerator null_coroutine() { yield break; }

        private static IEnumerator PromptCoroutine(GridTankUnit playerUnit, GridTankUnit attacker)
        {
            float elapsed = 0f;
            while (elapsed < TimeoutSeconds)
            {
                elapsed += Time.deltaTime;

                if (Input.GetKeyDown(KeyCode.Y))
                {
                    playerUnit.SetCounterConfirmed(true);
                    Debug.Log($"[CRUX] 반격 확인 — {playerUnit.Data?.tankName}");
                    yield break;
                }
                if (Input.GetKeyDown(KeyCode.N))
                {
                    playerUnit.SetCounterConfirmed(false);
                    Debug.Log($"[CRUX] 반격 포기 — {playerUnit.Data?.tankName}");
                    yield break;
                }

                yield return null;
            }

            // 타임아웃 — 자동 확인
            playerUnit.SetCounterConfirmed(true);
            Debug.Log($"[CRUX] 반격 자동 확인 (타임아웃) — {playerUnit.Data?.tankName}");
        }

        /// <summary>CounterConfirmed 조건을 무시하고 나머지 7개 조건만 검사</summary>
        private static bool CanCounterIgnoringConfirmation(GridTankUnit defender, GridTankUnit attacker, GridManager grid)
        {
            if (defender == null || defender.IsDestroyed) return false;

            var barrelModule = defender.Modules.Get(ModuleType.Barrel);
            if (barrelModule == null || barrelModule.state != ModuleState.Normal) return false;

            if (!grid.HasLOS(defender.GridPosition, attacker.GridPosition)) return false;

            Vector3 defWorldPos = grid.GridToWorld(defender.GridPosition);
            Vector3 attWorldPos = grid.GridToWorld(attacker.GridPosition);
            Vector2 dirToAttacker = ((Vector2)(attWorldPos - defWorldPos)).normalized;
            float rad = defender.HullAngle * Mathf.Deg2Rad;
            Vector2 defFacing = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
            float angle = Vector2.Angle(defFacing, dirToAttacker);
            if (angle > 60f) return false;

            if (defender.CurrentAP < defender.GetFireCost()) return false;
            if (defender.IsCounterImmune) return false;
            if (defender.HasCounteredThisExchange) return false;

            return true;
        }
    }
}
