using System.Collections;
using UnityEngine;
using Crux.Unit;

namespace Crux.Cinematic
{
    /// <summary>
    /// 착탄 후 상태 연출(격파/유폭/화재/모듈) 담당.
    /// MonoBehaviour 아님 — 코루틴은 owner FireSequenceController에 위임.
    /// </summary>
    internal class FirePostImpactHandler
    {
        private readonly FireSequenceController owner;
        private readonly FireCinematicFX fx;

        internal FirePostImpactHandler(FireSequenceController owner, FireCinematicFX fx)
        {
            this.owner = owner;
            this.fx = fx;
        }

        /// <summary>사격 결과 이후 상태 표시 시퀀스 — 격파/유폭/화재/모듈</summary>
        internal IEnumerator PlayPostImpactNarratives(Vector3 impactPos, DamageOutcome outcome)
        {
            yield return new WaitForSeconds(0.3f);

            // 1. 유폭 → 별도 그래픽 + 격파
            if (outcome.ammoExploded)
            {
                owner.ShowNarrative("▲ 탄약고 유폭!", new Color(1f, 0.9f, 0.2f));
                owner.SubText = "내부 탄약 연쇄 폭발";

                Combat.HitEffects.SpawnExplosion(impactPos);
                owner.StartCoroutine(fx.CameraShake(0.5f, 0.22f));
                fx.SpawnAmmoCookoffEffect(impactPos);

                yield return new WaitForSeconds(0.6f);

                Combat.HitEffects.SpawnExplosion(impactPos + Vector3.up * 0.2f);
                owner.StartCoroutine(fx.CameraShake(0.4f, 0.18f));
                yield return new WaitForSeconds(0.5f);

                owner.ShowNarrative("◆ 격파!", new Color(1f, 0.2f, 0.15f));
                owner.SubText = $"{owner.TargetName} 대파";
                yield return new WaitForSeconds(0.8f);
                yield break;
            }

            // 2. 격파 (유폭 제외)
            if (outcome.killed)
            {
                owner.ShowNarrative("◆ 격파!", new Color(1f, 0.2f, 0.15f));
                owner.SubText = $"{owner.TargetName} 전투 불능";
                Combat.HitEffects.SpawnExplosion(impactPos);
                owner.StartCoroutine(fx.CameraShake(0.35f, 0.15f));
                yield return new WaitForSeconds(0.8f);
                yield break;
            }

            // 3. 격파가 아닌 경우: 상태이상
            if (outcome.fireStarted)
            {
                owner.ShowNarrative("🔥 화재 발생!", new Color(1f, 0.5f, 0.15f));
                owner.SubText = "매 턴 HP 감소 / AP 비용 +1";
                fx.SpawnFireIndicator(impactPos);
                yield return new WaitForSeconds(0.7f);
            }

            // 4. 모듈 파괴 판정
            if (outcome.moduleHit && outcome.stateChanged)
            {
                string moduleName = ModuleManager.GetModuleName(outcome.damagedModule);
                string stateName = ModuleManager.GetStateName(outcome.newState);

                Color col = outcome.newState switch
                {
                    ModuleState.Damaged  => new Color(1f, 0.9f, 0.3f),
                    ModuleState.Broken   => new Color(1f, 0.5f, 0.2f),
                    ModuleState.Destroyed => new Color(1f, 0.25f, 0.2f),
                    _ => Color.white
                };
                owner.ShowNarrative($"⚙ {moduleName} {stateName}!", col);
                owner.SubText = GetModulePenaltyDescription(outcome.damagedModule, outcome.newState);
                yield return new WaitForSeconds(0.7f);
            }
        }

        /// <summary>모듈 상태별 효과 설명</summary>
        private static string GetModulePenaltyDescription(ModuleType type, ModuleState state)
        {
            if (state == ModuleState.Destroyed)
            {
                return type switch
                {
                    ModuleType.Engine           => "이동 불가",
                    ModuleType.Barrel           => "주포 사용 불가",
                    ModuleType.MachineGun       => "기총 사용 불가",
                    ModuleType.AmmoRack         => "장전 불가",
                    ModuleType.Loader           => "장전기 작동 불능",
                    ModuleType.TurretRing       => "포탑 회전 불가",
                    ModuleType.CaterpillarLeft  => "좌측 궤도 완파",
                    ModuleType.CaterpillarRight => "우측 궤도 완파",
                    _ => "완파"
                };
            }
            if (state == ModuleState.Broken)
            {
                return type switch
                {
                    ModuleType.Engine     => "이동 불가 (수리 시 복구)",
                    ModuleType.Barrel     => "주포 사용 불가",
                    ModuleType.AmmoRack   => "사격 AP +2",
                    ModuleType.Loader     => "사격 AP +2",
                    ModuleType.TurretRing => "사격 AP +2",
                    _ => "기능 정지"
                };
            }
            return type switch
            {
                ModuleType.Engine  => "이동 AP +1",
                ModuleType.Barrel  => "명중률 -15%",
                ModuleType.MachineGun => "버스트 -2, 명중 -10%",
                ModuleType.Loader or ModuleType.AmmoRack or ModuleType.TurretRing => "사격 AP +1",
                ModuleType.CaterpillarLeft or ModuleType.CaterpillarRight => "이동 AP +1",
                _ => "성능 저하"
            };
        }
    }
}
