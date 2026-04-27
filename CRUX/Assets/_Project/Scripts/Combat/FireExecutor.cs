using UnityEngine;
using Crux.Grid;
using Crux.Unit;
using Crux.Data;
using Crux.Core;
using TerrainData = Crux.Core.TerrainData;

namespace Crux.Combat
{
    /// <summary>사격 실행 — 주포/기총 hit/penetration 계산 + FireActionContext 설정</summary>
    public class FireExecutor
    {
        private readonly GridManager grid;
        private readonly System.Collections.Generic.List<GridTankUnit> enemyUnits;
        private readonly MachineGunDataSO coaxialMGData;
        private readonly MachineGunDataSO mountedMGData;

        public FireExecutor(GridManager grid, System.Collections.Generic.List<GridTankUnit> enemyUnits,
                            MachineGunDataSO coaxialMGData, MachineGunDataSO mountedMGData)
        {
            this.grid = grid;
            this.enemyUnits = enemyUnits;
            this.coaxialMGData = coaxialMGData;
            this.mountedMGData = mountedMGData;
        }

        /// <summary>무기 분기 — FireActionContext까지 설정. 씬 전환은 호출자가 수행.</summary>
        public void Execute(GridTankUnit attacker, GridTankUnit target, WeaponType weapon)
        {
            // 스테일 액션 제거 — 첫 Enqueue 전에 컨텍스트 초기화
            FireActionContext.Clear();

            if (weapon == WeaponType.MainGun)
            {
                ExecuteMainGun(attacker, target);
            }
            else if (weapon == WeaponType.CoaxialMG && coaxialMGData != null)
            {
                ExecuteMG(attacker, target, coaxialMGData);
            }
            else if (weapon == WeaponType.MountedMG && mountedMGData != null)
            {
                ExecuteMG(attacker, target, mountedMGData);
            }
            else
            {
                ExecuteMainGun(attacker, target);
            }

        }

        /// <summary>주포 사격 실행</summary>
        private void ExecuteMainGun(GridTankUnit attacker, GridTankUnit target)
        {
            attacker.ConsumeFireAP();
            attacker.ConsumeMainGunRound();

            int distance = grid.GetDistance(attacker.GridPosition, target.GridPosition);
            float hitChance = CalculateHitChanceWithCover(attacker, target);

            bool hit = Random.value <= hitChance;

            ShotResult result = new ShotResult { hit = false, outcome = ShotOutcome.Miss, hitChance = hitChance };
            bool hitCover = false;
            float coverDmgDealt = 0f;
            string hitCoverName = "";

            // 대상 엄폐 여부 — 6방향 슬롯 판정
            var targetCellForCover = grid.GetCell(target.GridPosition);
            var attackHexDir = HexCoord.AttackDir(attacker.GridPosition, target.GridPosition, GameConstants.CellSize);

            bool targetInCover = false;
            string targetCoverNameForVisual = "";

            if (targetCellForCover != null && targetCellForCover.HasCover
                && targetCellForCover.Cover != null
                && !targetCellForCover.Cover.IsDestroyed)
            {
                targetInCover = targetCellForCover.Cover.IsCovered(attackHexDir);
                if (targetInCover)
                    targetCoverNameForVisual = targetCellForCover.Cover.coverName;
            }

            if (!hit)
            {
                // 빗나감 — 기본값 유지
            }
            else
            {
                // ===== 엄폐 판정 — 방향 기반: 커버 범위 내일 때만 엄폐물이 막을 수 있음 =====
                var targetCell = grid.GetCell(target.GridPosition);

                if (targetInCover && targetCell != null && targetCell.Cover != null)
                {
                    float coverRate = targetCell.Cover.CoverRate;
                    if (Random.value < coverRate)
                    {
                        // 엄폐물이 피격됨!
                        hitCover = true;
                        float dmg = attacker.currentAmmo != null ? attacker.currentAmmo.damage : 10f;
                        coverDmgDealt = dmg;
                        hitCoverName = targetCell.Cover.coverName;

                        var coverRef = targetCell.Cover; // TakeDamage 전에 참조 보존
                        coverRef.TakeDamage(dmg);

                        result = new ShotResult
                        {
                            hit = true,
                            outcome = ShotOutcome.Hit,
                            hitZone = HitZone.Front,
                            effectiveArmor = 0,
                            damageDealt = 0, // 전차에 데미지 없음
                            hitChance = hitChance
                        };

                        Debug.Log($"[CRUX] 엄폐물 피격! {hitCoverName} ({coverRef.size}) HP: {coverRef.CurrentHP:F0}/{coverRef.maxHP:F0} 엄폐율: {coverRef.CoverRate:P0} 방호면: {coverRef.CurrentFacets}");
                    }
                }

                if (!hitCover)
                {
                // ===== 전차 직접 피격 =====
                var hitZone = PenetrationCalculator.DetermineHitZone(
                    attacker.transform.position, target.transform.position, target.HullAngle);

                float baseArmor = PenetrationCalculator.GetBaseArmor(target.Data.armor, hitZone);
                float impactAngle = PenetrationCalculator.CalculateImpactAngleFromPositions(
                    attacker.transform.position, target.transform.position, target.HullAngle, hitZone);
                float effectiveArmor = PenetrationCalculator.CalculateEffectiveArmor(baseArmor, impactAngle);

                float pen = attacker.currentAmmo != null ? attacker.currentAmmo.penetration : 100f;
                var outcome = PenetrationCalculator.JudgePenetration(pen, effectiveArmor);

                float dmg = attacker.currentAmmo != null ? attacker.currentAmmo.damage : 10f;
                float finalDmg = outcome switch
                {
                    ShotOutcome.Ricochet => dmg * 0.03f,
                    ShotOutcome.Hit => dmg,
                    ShotOutcome.Penetration => dmg * 2.5f,
                    _ => 0f
                };

                result = new ShotResult
                {
                    hit = true,
                    outcome = outcome,
                    hitZone = hitZone,
                    effectiveArmor = effectiveArmor,
                    damageDealt = finalDmg,
                    hitChance = hitChance
                };
                } // if (!hitCover)
            } // else (hit)

            // 연출 씬으로 데이터 전달
            int targetIndex = enemyUnits.IndexOf(target);

            // 스프라이트 가져오기
            var attackerSr = attacker.GetComponentInChildren<SpriteRenderer>();
            var attackerTurretSr = attacker.transform.Find("Turret")?.GetComponent<SpriteRenderer>();
            var targetSr = target.GetComponentInChildren<SpriteRenderer>();

            // 공격자 엄폐 상태 확인
            var attackerCell = grid.GetCell(attacker.GridPosition);
            bool inCover = attackerCell != null && attackerCell.HasCover
                           && attackerCell.Cover != null && !attackerCell.Cover.IsDestroyed;
            string coverName = inCover ? attackerCell.Cover.coverName : "";

            // 사전 롤: 전차 피해 시에만 (엄폐 피격이 아닌 경우)
            Unit.DamageOutcome mainOutcome = default;
            if (!hitCover && result.hit && result.damageDealt > 0)
            {
                mainOutcome = target.PreRollDamage(new DamageInfo
                {
                    damage = result.damageDealt,
                    outcome = result.outcome,
                    hitZone = result.hitZone,
                    attacker = attacker
                });
            }

            FireActionContext.Enqueue(new FireActionData
            {
                attackerWorldPos = attacker.transform.position,
                attackerHullAngle = attacker.HullAngle,
                attackerName = attacker.Data.tankName,
                attackerSide = attacker.side,
                attackerInCover = inCover,
                attackerCoverName = coverName,
                attackerCoverSize = inCover ? attackerCell.Cover.size : CoverSize.Medium,
                attackerCoverFacets = inCover ? attackerCell.Cover.CurrentFacets : HexFacet.None,
                targetInCover = targetInCover,
                targetCoverHit = hitCover,
                coverDamageDealt = coverDmgDealt,
                targetCoverName = hitCover ? hitCoverName : targetCoverNameForVisual,
                targetCoverSize = targetInCover ? targetCellForCover.Cover.size : CoverSize.Medium,
                targetCoverFacets = targetInCover ? targetCellForCover.Cover.CurrentFacets : HexFacet.None,
                targetWorldPos = target.transform.position,
                targetHullAngle = target.HullAngle,
                targetName = target.Data.tankName,
                weaponType = WeaponType.MainGun,
                ammoData = attacker.currentAmmo,
                result = result,
                mainOutcome = mainOutcome,
                targetUnitIndex = targetIndex,
                targetSide = target.side,
                attackerHullSprite = attackerSr != null ? attackerSr.sprite : null,
                attackerTurretSprite = attackerTurretSr != null ? attackerTurretSr.sprite : null,
                attackerSpriteRotOffset = GetSpriteRotOffset(attacker.transform),
                attackerMuzzleOffset = attacker.Data.muzzleOffset,
                targetHullSprite = targetSr != null ? targetSr.sprite : null,
                targetTurretSprite = target.transform.Find("Turret")?.GetComponent<SpriteRenderer>()?.sprite,
                targetSpriteRotOffset = GetSpriteRotOffset(target.transform)
            });
        }

        /// <summary>기관총 사격 실행</summary>
        private void ExecuteMG(GridTankUnit attacker, GridTankUnit target, MachineGunDataSO mgData)
        {
            attacker.ConsumeFireAP();

            int distance = grid.GetDistance(attacker.GridPosition, target.GridPosition);
            float baseHitChance = CalculateHitChance(distance, target)
                + mgData.accuracyModifier
                - attacker.Modules.GetMGAccuracyPenalty();

            // 기총 손상 시 버스트 감소
            int burstCount = Mathf.Max(1, mgData.burstCount - attacker.Modules.GetBurstPenalty());
            attacker.ConsumeMGBurst(burstCount);

            // 버스트 발당 결과 계산
            var results = new ShotResult[burstCount];
            for (int i = 0; i < burstCount; i++)
            {
                float shotChance = Mathf.Clamp01(baseHitChance - (i * 0.02f)); // 연사할수록 정확도 감소
                bool hit = Random.value <= shotChance;

                if (!hit)
                {
                    results[i] = new ShotResult { hit = false, outcome = ShotOutcome.Miss, hitChance = shotChance };
                }
                else
                {
                    var hitZone = PenetrationCalculator.DetermineHitZone(
                        attacker.transform.position, target.transform.position, target.HullAngle);
                    float baseArmor = PenetrationCalculator.GetBaseArmor(target.Data.armor, hitZone);
                    float impactAngle = PenetrationCalculator.CalculateImpactAngleFromPositions(
                        attacker.transform.position, target.transform.position, target.HullAngle, hitZone);
                    float effectiveArmor = PenetrationCalculator.CalculateEffectiveArmor(baseArmor, impactAngle);
                    var outcome = PenetrationCalculator.JudgePenetration(mgData.penetration, effectiveArmor);

                    float dmg = outcome switch
                    {
                        ShotOutcome.Ricochet => mgData.damagePerShot * 0.03f,
                        ShotOutcome.Hit => mgData.damagePerShot,
                        ShotOutcome.Penetration => mgData.damagePerShot * 2f,
                        _ => 0f
                    };

                    results[i] = new ShotResult
                    {
                        hit = true, outcome = outcome, hitZone = hitZone,
                        effectiveArmor = effectiveArmor, damageDealt = dmg, hitChance = shotChance
                    };
                }
            }

            var attackerSr = attacker.GetComponentInChildren<SpriteRenderer>();
            var attackerTurretSr = attacker.transform.Find("Turret")?.GetComponent<SpriteRenderer>();
            var targetSr = target.GetComponentInChildren<SpriteRenderer>();
            int targetIndex = enemyUnits.IndexOf(target);

            // 기총 총 피해 집계 후 단일 사전 롤 (관통 발생 시에만 모듈/화재 롤)
            float totalMGDamage = 0f;
            bool anyPenetration = false;
            HitZone mgZone = HitZone.Front;
            foreach (var r in results)
            {
                if (r.hit && r.damageDealt > 0)
                {
                    totalMGDamage += r.damageDealt;
                    if (r.outcome == ShotOutcome.Penetration) anyPenetration = true;
                    mgZone = r.hitZone;
                }
            }
            Unit.DamageOutcome mgOutcome = default;
            if (totalMGDamage > 0)
            {
                mgOutcome = target.PreRollDamage(new DamageInfo
                {
                    damage = totalMGDamage,
                    outcome = anyPenetration ? ShotOutcome.Penetration : ShotOutcome.Hit,
                    hitZone = mgZone,
                    attacker = attacker
                });
            }

            FireActionContext.Enqueue(new FireActionData
            {
                attackerWorldPos = attacker.transform.position,
                attackerHullAngle = attacker.HullAngle,
                attackerName = attacker.Data.tankName,
                attackerSide = attacker.side,
                targetWorldPos = target.transform.position,
                targetHullAngle = target.HullAngle,
                targetName = target.Data.tankName,
                weaponType = mgData.type,
                mgData = mgData,
                mgResults = results,
                mgAggregateOutcome = mgOutcome,
                targetUnitIndex = targetIndex,
                targetSide = target.side,
                attackerHullSprite = attackerSr != null ? attackerSr.sprite : null,
                attackerTurretSprite = attackerTurretSr != null ? attackerTurretSr.sprite : null,
                attackerSpriteRotOffset = GetSpriteRotOffset(attacker.transform),
                attackerMuzzleOffset = attacker.Data.muzzleOffset,
                targetHullSprite = targetSr != null ? targetSr.sprite : null,
                targetTurretSprite = target.transform.Find("Turret")?.GetComponent<SpriteRenderer>()?.sprite,
                targetSpriteRotOffset = GetSpriteRotOffset(target.transform)
            });
        }

        /// <summary>엄폐 + 모듈 + 지형(고도·은엄폐) 보정 포함 명중률</summary>
        public float CalculateHitChanceWithCover(GridTankUnit attacker, GridTankUnit target)
        {
            int distance = grid.GetDistance(attacker.GridPosition, target.GridPosition);
            float chance = CalculateHitChance(distance, target);

            // 포신 손상 패널티
            chance -= attacker.Modules.GetAccuracyPenalty();

            var attackerCell = grid.GetCell(attacker.GridPosition);
            var targetCell = grid.GetCell(target.GridPosition);

            // 지형 고도 차 — 공격자 > 목표면 +5%/단계
            if (attackerCell != null && targetCell != null)
            {
                int elevDelta = TerrainData.Elevation(attackerCell.Terrain)
                              - TerrainData.Elevation(targetCell.Terrain);
                if (elevDelta > 0) chance += elevDelta * 0.05f;
            }

            // 6방향 슬롯 엄폐 보정
            if (targetCell != null && targetCell.HasCover && targetCell.Cover != null
                && !targetCell.Cover.IsDestroyed)
            {
                var atkDir = HexCoord.AttackDir(attacker.GridPosition, target.GridPosition, GameConstants.CellSize);
                if (targetCell.Cover.IsCovered(atkDir))
                    chance -= targetCell.Cover.CoverRate * 0.3f;
            }

            // 지형 자체 엄폐 (파편지대·탄흔 등) — 엄폐물과 합산
            if (targetCell != null)
            {
                float intrinsicCover = TerrainData.IntrinsicCoverRate(targetCell.Terrain);
                if (intrinsicCover > 0f) chance -= intrinsicCover * 0.3f;
            }

            // 은엄폐 (수풀·파편) — 엄폐와 독립 감산
            if (targetCell != null)
            {
                int concealment = TerrainData.Concealment(targetCell.Terrain);
                if (concealment > 0) chance -= concealment * 0.01f;
            }

            // 연막 보정
            if (targetCell != null && targetCell.HasSmoke)
                chance -= 0.4f;

            // 공격자 사기 명중 보정
            var atkCrew = attacker.Crew;
            if (atkCrew != null)
                chance += MoraleSystem.AimModifier(atkCrew.Band) * 0.01f;

            return Mathf.Clamp01(chance);
        }

        /// <summary>거리 기반 기본 명중률 (모듈/지형 보정 제외)</summary>
        public float CalculateHitChance(int distance, GridTankUnit target)
        {
            float chance = GameConstants.BaseAccuracy;
            chance -= distance * GameConstants.DistancePenaltyPerCell;

            return Mathf.Clamp01(chance);
        }

        /// <summary>유닛의 현재 상태 (개활지/엄폐)</summary>
        public string GetUnitCoverStatus(GridTankUnit unit)
        {
            var cell = grid.GetCell(unit.GridPosition);
            if (cell == null) return "개활지";

            if (cell.HasCover && cell.Cover != null && !cell.Cover.IsDestroyed)
            {
                float rate = cell.Cover.CoverRate;
                return $"엄폐 ({cell.Cover.coverName} 엄폐율:{rate:P0})";
            }

            return "개활지";
        }

        /// <summary>
        /// 반격 사격 큐 추가 — 피격 후 WeaponSelect 세션에서 사용자가 확정 시 호출.
        /// Execute()에서 자동 Enqueue하던 방식을 대체.
        /// weapon이 MainGun 외의 경우 현재 MainGun으로 강등 (Phase 2 TD).
        /// </summary>
        public void EnqueueCounterFire(GridTankUnit counterAttacker, GridTankUnit target, WeaponType weapon)
        {
            // Phase 2 TD: CoaxialMG/MountedMG 반격은 아직 미구현 — MainGun으로 강등
            if (weapon != WeaponType.MainGun)
            {
                Debug.LogWarning($"[FIRE] 반격 무기 {weapon}은 미지원 — MainGun으로 강등 (Phase 2 TD)");
                weapon = WeaponType.MainGun;
            }

            counterAttacker.ConsumeFireAP();
            counterAttacker.ConsumeMainGunRound();
            counterAttacker.SetCountered(true);

            float hitChance = Mathf.Clamp01(CalculateHitChanceWithCover(counterAttacker, target) - 0.15f);
            bool hit = Random.value <= hitChance;

            ShotResult result = new ShotResult { hit = false, outcome = ShotOutcome.Miss, hitChance = hitChance };
            Unit.DamageOutcome mainOutcome = default;

            if (hit)
            {
                var hitZone = PenetrationCalculator.DetermineHitZone(
                    counterAttacker.transform.position, target.transform.position, target.HullAngle);
                float baseArmor = PenetrationCalculator.GetBaseArmor(target.Data.armor, hitZone);
                float impactAngle = PenetrationCalculator.CalculateImpactAngleFromPositions(
                    counterAttacker.transform.position, target.transform.position, target.HullAngle, hitZone);
                float effectiveArmor = PenetrationCalculator.CalculateEffectiveArmor(baseArmor, impactAngle);
                float pen = counterAttacker.currentAmmo != null ? counterAttacker.currentAmmo.penetration : 100f;
                var outcome = PenetrationCalculator.JudgePenetration(pen, effectiveArmor);
                float dmg = counterAttacker.currentAmmo != null ? counterAttacker.currentAmmo.damage : 10f;
                float finalDmg = outcome switch
                {
                    ShotOutcome.Ricochet    => dmg * 0.03f,
                    ShotOutcome.Hit         => dmg,
                    ShotOutcome.Penetration => dmg * 2.5f,
                    _                       => 0f
                };
                result = new ShotResult
                {
                    hit = true, outcome = outcome, hitZone = hitZone,
                    effectiveArmor = effectiveArmor, damageDealt = finalDmg, hitChance = hitChance
                };
                if (finalDmg > 0)
                    mainOutcome = target.PreRollDamage(new DamageInfo
                    {
                        damage = finalDmg, outcome = outcome, hitZone = hitZone, attacker = counterAttacker
                    });
            }

            FireActionContext.Enqueue(BuildCounterFireActionData(counterAttacker, target, result, mainOutcome));
            Debug.Log($"[FIRE] 반격 Enqueue — {counterAttacker.Data?.tankName} → {target.Data?.tankName} hit={hit} outcome={result.outcome}");
        }

        /// <summary>반격 FireActionData 빌드 (엄폐 없음 — 반격은 개활지 판정)</summary>
        private FireActionData BuildCounterFireActionData(GridTankUnit attacker, GridTankUnit target,
                                                          ShotResult result, Unit.DamageOutcome mainOutcome)
        {
            int targetIndex = target.side == PlayerSide.Enemy ? enemyUnits.IndexOf(target) : -1;
            var attackerSr = attacker.GetComponentInChildren<SpriteRenderer>();
            var attackerTurretSr = attacker.transform.Find("Turret")?.GetComponent<SpriteRenderer>();
            var targetSr = target.GetComponentInChildren<SpriteRenderer>();
            return new FireActionData
            {
                attackerWorldPos = attacker.transform.position,
                attackerHullAngle = attacker.HullAngle,
                attackerName = attacker.Data.tankName,
                attackerSide = attacker.side,
                targetWorldPos = target.transform.position,
                targetHullAngle = target.HullAngle,
                targetName = target.Data.tankName,
                weaponType = WeaponType.MainGun,
                ammoData = attacker.currentAmmo,
                result = result,
                mainOutcome = mainOutcome,
                targetUnitIndex = targetIndex,
                targetSide = target.side,
                attackerHullSprite = attackerSr != null ? attackerSr.sprite : null,
                attackerTurretSprite = attackerTurretSr != null ? attackerTurretSr.sprite : null,
                attackerSpriteRotOffset = GetSpriteRotOffset(attacker.transform),
                attackerMuzzleOffset = attacker.Data.muzzleOffset,
                targetHullSprite = targetSr != null ? targetSr.sprite : null,
                targetTurretSprite = target.transform.Find("Turret")?.GetComponent<SpriteRenderer>()?.sprite,
                targetSpriteRotOffset = GetSpriteRotOffset(target.transform)
            };
        }

        /// <summary>SpriteContainer가 있으면 그 회전 오프셋을 반환</summary>
        private float GetSpriteRotOffset(Transform unitRoot)
        {
            var container = unitRoot.Find("SpriteContainer");
            if (container != null)
                return container.localEulerAngles.z > 180 ? container.localEulerAngles.z - 360 : container.localEulerAngles.z;
            return 0f;
        }
    }
}
