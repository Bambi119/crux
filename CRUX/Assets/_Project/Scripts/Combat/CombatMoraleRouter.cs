using System.Collections.Generic;
using UnityEngine;
using Crux.Core;
using Crux.Unit;

namespace Crux.Combat
{
    /// <summary>
    /// 사격 피격 → 사기 이벤트 라우팅
    /// 각 유닛의 OnDamageApplied를 구독하고, 피격 결과에 따라 사기 이벤트를 발행.
    /// </summary>
    public class CombatMoraleRouter
    {
        private List<GridTankUnit> playerUnits = new();
        private List<GridTankUnit> enemyUnits = new();

        /// <summary>
        /// 승무원 부착 후 호출 — 플레이어 유닛 + 적 유닛 리스트를 캐시하고
        /// 각 유닛의 OnDamageApplied를 구독.
        /// </summary>
        public void Attach(GridTankUnit playerUnit, List<GridTankUnit> enemies)
        {
            // 진영별 유닛 캐시
            if (playerUnit != null)
            {
                playerUnits.Add(playerUnit);
                playerUnit.OnDamageApplied += HandleDamage;
            }

            enemyUnits = new List<GridTankUnit>(enemies);
            foreach (var enemy in enemyUnits)
            {
                if (enemy != null)
                    enemy.OnDamageApplied += HandleDamage;
            }
        }

        /// <summary>
        /// 피격 이벤트 핸들러 — 피해자 진영과 공격자 진영에 사기 이벤트 발행.
        /// </summary>
        private void HandleDamage(GridTankUnit victim, DamageInfo info, DamageOutcome outcome)
        {
            // 피격자 진영: 아군/적군 판정
            var victimSide = victim.side;
            var victimCrews = victimSide == PlayerSide.Player ? playerUnits : enemyUnits;

            // 공격자 정보
            var attacker = info.attacker;
            var attackerSide = attacker != null ? attacker.side : victimSide; // 공격자 없으면 default

            // ===== 피격측 (피해자 진영 전체) =====

            // ModuleDamaged: 모듈 피격 + 상태 변화
            if (outcome.moduleHit && outcome.stateChanged)
            {
                var prevMorale = victim.Crew?.Morale ?? 50;
                BroadcastMoraleEvent(victimCrews, MoraleEvent.ModuleDamaged);
                var newMorale = victim.Crew?.Morale ?? 50;
                if (victim.Crew != null)
                    Debug.Log($"[CRUX] morale {victimSide} {victim.Data?.tankName} {prevMorale}→{newMorale} ModuleDamaged");
            }

            // SideRearHit: 측면/후면 피격
            if (info.hitZone is HitZone.Rear or HitZone.RearLeft or HitZone.RearRight)
            {
                var prevMorale = victim.Crew?.Morale ?? 50;
                BroadcastMoraleEvent(victimCrews, MoraleEvent.SideRearHit);
                var newMorale = victim.Crew?.Morale ?? 50;
                if (victim.Crew != null)
                    Debug.Log($"[CRUX] morale {victimSide} {victim.Data?.tankName} {prevMorale}→{newMorale} SideRearHit");
            }

            // AmmoRackNear: 탄약고 근접 피격 (손상 이상)
            if (outcome.damagedModule == ModuleType.AmmoRack && outcome.newState >= ModuleState.Damaged)
            {
                var prevMorale = victim.Crew?.Morale ?? 50;
                BroadcastMoraleEvent(victimCrews, MoraleEvent.AmmoRackNear);
                var newMorale = victim.Crew?.Morale ?? 50;
                if (victim.Crew != null)
                    Debug.Log($"[CRUX] morale {victimSide} {victim.Data?.tankName} {prevMorale}→{newMorale} AmmoRackNear");
            }

            // ===== 공격측 (공격자 진영 전체) =====
            if (attacker != null)
            {
                var attackerUnits = attackerSide == PlayerSide.Player ? playerUnits : enemyUnits;

                // PenetrationHit: 관통
                if (info.outcome == ShotOutcome.Penetration)
                {
                    var prevMorale = attacker.Crew?.Morale ?? 50;
                    BroadcastMoraleEvent(attackerUnits, MoraleEvent.PenetrationHit);
                    var newMorale = attacker.Crew?.Morale ?? 50;
                    if (attacker.Crew != null)
                        Debug.Log($"[CRUX] morale {attackerSide} {attacker.Data?.tankName} {prevMorale}→{newMorale} PenetrationHit");
                }

                // Ricochet: 도탄
                if (info.outcome == ShotOutcome.Ricochet)
                {
                    var prevMorale = attacker.Crew?.Morale ?? 50;
                    BroadcastMoraleEvent(attackerUnits, MoraleEvent.Ricochet);
                    var newMorale = attacker.Crew?.Morale ?? 50;
                    if (attacker.Crew != null)
                        Debug.Log($"[CRUX] morale {attackerSide} {attacker.Data?.tankName} {prevMorale}→{newMorale} Ricochet");
                }

                // EnemyKilled: 격파 (진영이 다를 때)
                if (outcome.killed && attacker.side != victim.side)
                {
                    var prevMorale = attacker.Crew?.Morale ?? 50;
                    BroadcastMoraleEvent(attackerUnits, MoraleEvent.EnemyKilled);
                    var newMorale = attacker.Crew?.Morale ?? 50;
                    if (attacker.Crew != null)
                        Debug.Log($"[CRUX] morale {attackerSide} {attacker.Data?.tankName} {prevMorale}→{newMorale} EnemyKilled");
                }
            }
        }

        /// <summary>진영 전체에 사기 이벤트 발행</summary>
        private void BroadcastMoraleEvent(List<GridTankUnit> units, MoraleEvent kind)
        {
            foreach (var unit in units)
            {
                if (unit?.Crew != null)
                    unit.Crew.ApplyMoraleEvent(kind);
            }
        }
    }
}
