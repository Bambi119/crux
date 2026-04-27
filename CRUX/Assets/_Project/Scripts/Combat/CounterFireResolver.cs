using UnityEngine;
using Crux.Grid;
using Crux.Unit;

namespace Crux.Combat
{
    /// <summary>
    /// 반격 시스템 — 8조건 검사 (§06_combat_system §3.4)
    /// 1. 방어측 생존
    /// 2. 주포 무손상
    /// 3. 전방 호 ±60° LOS
    /// 4. AP 충분
    /// 5. 사수 무손상 (Phase 1: 항상 true)
    /// 6. 반격 면역 아님
    /// 7. 연쇄 반격 차단
    /// 8. 플레이어 반격 확정 (플레이어만)
    /// </summary>
    public static class CounterFireResolver
    {
        /// <summary>반격 조건 검사만. 실제 사격 해결은 FireExecutor가 호출.</summary>
        public static bool CanCounter(GridTankUnit defender, GridTankUnit attacker, GridManager grid)
        {
            return CheckWithReason(defender, attacker, grid).canCounter;
        }

        /// <summary>상세 진단(로그/디버그용). 실패 시 사유 반환.</summary>
        public static CounterCheckResult CheckWithReason(GridTankUnit defender, GridTankUnit attacker, GridManager grid)
        {
            // 1. 방어측 생존
            if (defender == null || defender.IsDestroyed)
                return new CounterCheckResult { canCounter = false, reason = CounterFailReason.DefenderDead };

            // 2. 주포 무손상 — Barrel 모듈 체크
            var barrelModule = defender.Modules.Get(ModuleType.Barrel);
            if (barrelModule == null || barrelModule.state != ModuleState.Normal)
                return new CounterCheckResult { canCounter = false, reason = CounterFailReason.MainGunDamaged };

            // 3. 전방 호 ±60° + LOS
            if (!HasLOSInArc(defender, attacker, grid))
                return new CounterCheckResult { canCounter = false, reason = CounterFailReason.OutOfArc };

            // 4. AP 충분
            int fireAPCost = defender.GetFireCost();
            if (defender.CurrentAP < fireAPCost)
                return new CounterCheckResult { canCounter = false, reason = CounterFailReason.InsufficientAP };

            // 5. 사수 무손상 — Phase 1: 항상 통과 (Phase 2에서 Crew 체크 추가)

            // 6. 반격 면역 아님 (오버워치 중인 유닛)
            if (defender.IsCounterImmune)
                return new CounterCheckResult { canCounter = false, reason = CounterFailReason.IsOverwatch };

            // 7. 연쇄 반격 차단
            if (defender.HasCounteredThisExchange)
                return new CounterCheckResult { canCounter = false, reason = CounterFailReason.ChainBlocked };

            // 8. 플레이어 반격 확정 여부
            if (!defender.CounterConfirmed)
                return new CounterCheckResult { canCounter = false, reason = CounterFailReason.PlayerDeclined };

            return new CounterCheckResult { canCounter = true, reason = CounterFailReason.None };
        }

        /// <summary>전방 호 ±60° 내에서 LOS 확인</summary>
        private static bool HasLOSInArc(GridTankUnit defender, GridTankUnit attacker, GridManager grid)
        {
            if (!grid.HasLOS(defender.GridPosition, attacker.GridPosition))
                return false;

            Vector3 defWorldPos = grid.GridToWorld(defender.GridPosition);
            Vector3 attWorldPos = grid.GridToWorld(attacker.GridPosition);
            Vector2 dirToAttacker = ((Vector2)(attWorldPos - defWorldPos)).normalized;
            Vector2 defenderFacing = CompassToVector(defender.HullAngle);
            float angle = Vector2.Angle(defenderFacing, dirToAttacker);
            return angle <= 60f;
        }

        /// <summary>Compass 각도(0~360)를 벡터로 변환 (0°=북, 90°=동)</summary>
        private static Vector2 CompassToVector(float compassAngle)
        {
            float rad = compassAngle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
        }

        public static void LogResult(GridTankUnit defender, GridTankUnit attacker, CounterCheckResult result)
        {
            if (result.canCounter)
                Debug.Log($"[COUNTER] {defender.Data?.tankName} → {attacker.Data?.tankName} : 반격 가능");
            else
                Debug.Log($"[COUNTER] {defender.Data?.tankName} → {attacker.Data?.tankName} : 반격 불가 ({result.reason})");
        }

#if UNITY_EDITOR
        public static void LogDetailedResult(GridTankUnit defender, GridTankUnit attacker,
                                             CounterCheckResult result, GridManager grid)
        {
            string defName = defender.Data?.tankName ?? "Unknown";
            string attName = attacker.Data?.tankName ?? "Unknown";
            Debug.Log($"[COUNTER-DEBUG] {defName} ← {attName}");
            Debug.Log($"  1. Alive: {!defender.IsDestroyed}");
            Debug.Log($"  2. Barrel Healthy: {defender.Modules.Get(ModuleType.Barrel)?.state == ModuleState.Normal}");

            bool hasLOS = grid.HasLOS(defender.GridPosition, attacker.GridPosition);
            Vector3 defWorldPos = grid.GridToWorld(defender.GridPosition);
            Vector3 attWorldPos = grid.GridToWorld(attacker.GridPosition);
            Vector2 dirToAttacker = ((Vector2)(attWorldPos - defWorldPos)).normalized;
            Vector2 defFacing = CompassToVector(defender.HullAngle);
            float angleToAttacker = Vector2.Angle(defFacing, dirToAttacker);

            Debug.Log($"  3. Arc & LOS: LOS={hasLOS}, Facing={defender.HullAngle}°, AngleToAtt={angleToAttacker:F1}°, InArc={angleToAttacker <= 60f}");
            Debug.Log($"  4. AP sufficient: {defender.CurrentAP}/{defender.GetFireCost()}");
            Debug.Log($"  6. Not CounterImmune: {!defender.IsCounterImmune}");
            Debug.Log($"  7. Not already countered: {!defender.HasCounteredThisExchange}");
            Debug.Log($"  8. CounterConfirmed: {defender.CounterConfirmed}");
            Debug.Log($"  Result: {result.reason}");
        }
#endif
    }

    /// <summary>반격 조건 검사 결과</summary>
    public struct CounterCheckResult
    {
        public bool canCounter;
        public CounterFailReason reason;
    }

    /// <summary>반격 불가 사유</summary>
    public enum CounterFailReason
    {
        None,           // 조건 만족 (반격 가능)
        DefenderDead,   // 방어측 사망
        MainGunDamaged, // 주포 손상/파괴
        OutOfArc,       // 전방 호 범위 밖 또는 LOS 없음
        InsufficientAP, // AP 부족
        GunnerDown,     // 사수 사상 (Phase 2 예약)
        IsOverwatch,    // 오버워치 면역 상태
        ChainBlocked,   // 이번 교환에서 이미 반격 실행
        AttackerMoving, // 공격측이 이동 중 (현재 미사용)
        PlayerDeclined  // 플레이어 반격 프롬프트 거부
    }
}
