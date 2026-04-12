using System.Collections.Generic;
using UnityEngine;
using Crux.Core;
using Crux.Data;

namespace Crux.Unit
{
    /// <summary>모듈 타입 — 전차 파츠 7종 (캐터필러 좌/우 분리)</summary>
    public enum ModuleType
    {
        Engine,             // 엔진
        Barrel,             // 포신
        MachineGun,         // 기총
        AmmoRack,           // 탄약고
        Loader,             // 장전기
        CaterpillarLeft,    // 캐터필러 좌
        CaterpillarRight,   // 캐터필러 우
        TurretRing          // 포탑 회전링
    }

    /// <summary>모듈 상태 3단계 + 정상</summary>
    public enum ModuleState
    {
        Normal,     // 정상
        Damaged,    // 손상 — 성능 저하
        Broken,     // 고장 — 기능 사용 불가
        Destroyed   // 완파 — 수리 불가
    }

    /// <summary>단일 모듈 데이터</summary>
    [System.Serializable]
    public class TankModule
    {
        public ModuleType type;
        public ModuleState state;
        public float maxHP;
        public float currentHP;

        public float HPRatio => maxHP > 0 ? currentHP / maxHP : 0f;
        public bool CanRepair => state != ModuleState.Destroyed;

        public TankModule(ModuleType type, float hp)
        {
            this.type = type;
            this.maxHP = hp;
            this.currentHP = hp;
            this.state = ModuleState.Normal;
        }

        /// <summary>데미지 적용 → 상태 자동 전환</summary>
        public ModuleState TakeDamage(float damage)
        {
            if (state == ModuleState.Destroyed) return state;

            var prevState = state;
            currentHP -= damage;

            if (currentHP <= 0)
            {
                currentHP = 0;
                state = ModuleState.Destroyed;
            }
            else if (HPRatio <= 0.25f)
            {
                state = ModuleState.Broken;
            }
            else if (HPRatio <= 0.5f)
            {
                state = ModuleState.Damaged;
            }

            return state;
        }

        /// <summary>상태 저장용</summary>
        public ModuleSaveData Save() => new ModuleSaveData
        {
            type = type,
            state = state,
            currentHP = currentHP
        };

        /// <summary>상태 복원</summary>
        public void Restore(ModuleSaveData data)
        {
            state = data.state;
            currentHP = data.currentHP;
        }
    }

    /// <summary>모듈 상태 직렬화용</summary>
    [System.Serializable]
    public struct ModuleSaveData
    {
        public ModuleType type;
        public ModuleState state;
        public float currentHP;
    }

    /// <summary>사격 결과 후 상태이상/모듈/격파 사전 결정값</summary>
    [System.Serializable]
    public struct DamageOutcome
    {
        public bool killed;              // HP 0이 됨 (피해로 인한 격파)
        public bool ammoExploded;        // 탄약고 유폭 → 격파 보장
        public bool fireStarted;         // 화재 발생
        public bool moduleHit;           // 모듈 피격 발생
        public ModuleType damagedModule; // 피격된 모듈
        public ModuleState newState;     // 새 상태 (롤 결과)
        public float moduleDamageDealt;  // 적용될 모듈 데미지
        public bool stateChanged;        // 상태 변화 여부 (표시용)
    }

    /// <summary>모듈 매니저 — 전차 유닛에 부착, 패널티 계산 API 제공</summary>
    public class ModuleManager
    {
        private Dictionary<ModuleType, TankModule> modules = new();

        public IReadOnlyDictionary<ModuleType, TankModule> All => modules;

        public void Initialize(TankDataSO data)
        {
            var hp = data.moduleHP;
            modules[ModuleType.Engine]           = new TankModule(ModuleType.Engine, hp.engine);
            modules[ModuleType.Barrel]           = new TankModule(ModuleType.Barrel, hp.barrel);
            modules[ModuleType.MachineGun]       = new TankModule(ModuleType.MachineGun, hp.machineGun);
            modules[ModuleType.AmmoRack]         = new TankModule(ModuleType.AmmoRack, hp.ammoRack);
            modules[ModuleType.Loader]           = new TankModule(ModuleType.Loader, hp.loader);
            modules[ModuleType.CaterpillarLeft]  = new TankModule(ModuleType.CaterpillarLeft, hp.caterpillarLeft);
            modules[ModuleType.CaterpillarRight] = new TankModule(ModuleType.CaterpillarRight, hp.caterpillarRight);
            modules[ModuleType.TurretRing]       = new TankModule(ModuleType.TurretRing, hp.turretRing);
        }

        private static readonly TankModule _dummyModule = new TankModule(ModuleType.Engine, 100f);

        public TankModule Get(ModuleType type) => modules.TryGetValue(type, out var m) ? m : _dummyModule;

        // ===== 모듈 피격 — HitZone 기반 확률 분배 =====

        /// <summary>HitZone에 따라 가중 랜덤으로 모듈 선택 후 데미지 적용. 유폭이면 true 반환 (즉시 적용)</summary>
        public bool DamageRandomModule(float damage, HitZone zone, string unitName)
        {
            var outcome = RollModuleHit(damage, zone);
            ApplyModuleHit(outcome, unitName);
            return outcome.ammoExploded;
        }

        /// <summary>모듈 피격 사전 롤 — 실제 데미지는 적용하지 않음, 상태 예측만 계산</summary>
        public DamageOutcome RollModuleHit(float damage, HitZone zone)
        {
            var result = new DamageOutcome { moduleHit = true, moduleDamageDealt = damage };
            var weights = GetModuleWeights(zone);
            var target = WeightedRandom(weights);

            var module = modules[target];
            result.damagedModule = target;

            // 현재 HP 기준으로 새 상태 예측 (실제 적용 없이)
            float predictedHP = module.currentHP - damage;
            ModuleState predicted = module.state;
            if (module.state != ModuleState.Destroyed)
            {
                if (predictedHP <= 0) predicted = ModuleState.Destroyed;
                else if (module.maxHP > 0)
                {
                    float ratio = predictedHP / module.maxHP;
                    if (ratio <= 0.25f) predicted = ModuleState.Broken;
                    else if (ratio <= 0.5f) predicted = ModuleState.Damaged;
                }
            }
            result.newState = predicted;
            result.stateChanged = predicted != module.state;

            // 탄약고 완파 → 유폭 예약
            if (target == ModuleType.AmmoRack && predicted == ModuleState.Destroyed)
                result.ammoExploded = true;

            return result;
        }

        /// <summary>사전 롤된 모듈 피격 적용</summary>
        public void ApplyModuleHit(DamageOutcome outcome, string unitName)
        {
            if (!outcome.moduleHit) return;
            var module = modules[outcome.damagedModule];
            module.TakeDamage(outcome.moduleDamageDealt);
            if (outcome.stateChanged)
            {
                string stateStr = GetStateName(outcome.newState);
                Debug.Log($"[CRUX] {unitName} [{GetModuleName(outcome.damagedModule)}] {stateStr}! (HP: {module.currentHP:F0}/{module.maxHP:F0})");
            }
            if (outcome.ammoExploded)
                Debug.Log($"[CRUX] {unitName} 탄약고 유폭!!");
        }

        private Dictionary<ModuleType, float> GetModuleWeights(HitZone zone)
        {
            var w = new Dictionary<ModuleType, float>();
            float eq = 5f; // 균등 분배 기본값

            switch (zone)
            {
                case HitZone.Front:
                    w[ModuleType.Barrel] = 25f;
                    w[ModuleType.TurretRing] = 20f;
                    w[ModuleType.Loader] = 15f;
                    w[ModuleType.Engine] = eq;
                    w[ModuleType.MachineGun] = eq;
                    w[ModuleType.AmmoRack] = eq;
                    w[ModuleType.CaterpillarLeft] = eq;
                    w[ModuleType.CaterpillarRight] = eq;
                    break;

                case HitZone.Side:
                    // TODO: 좌/우 판정 (현재는 랜덤)
                    bool leftSide = Random.value < 0.5f;
                    w[leftSide ? ModuleType.CaterpillarLeft : ModuleType.CaterpillarRight] = 30f;
                    w[ModuleType.Engine] = 20f;
                    w[ModuleType.AmmoRack] = 15f;
                    w[ModuleType.Barrel] = eq;
                    w[ModuleType.MachineGun] = eq;
                    w[ModuleType.Loader] = eq;
                    w[ModuleType.TurretRing] = eq;
                    w[leftSide ? ModuleType.CaterpillarRight : ModuleType.CaterpillarLeft] = eq;
                    break;

                case HitZone.Rear:
                    w[ModuleType.Engine] = 40f;
                    w[ModuleType.AmmoRack] = 25f;
                    w[ModuleType.CaterpillarLeft] = eq;
                    w[ModuleType.CaterpillarRight] = eq;
                    w[ModuleType.Barrel] = eq;
                    w[ModuleType.MachineGun] = eq;
                    w[ModuleType.Loader] = eq;
                    w[ModuleType.TurretRing] = eq;
                    break;

                case HitZone.Turret:
                    w[ModuleType.Barrel] = 30f;
                    w[ModuleType.TurretRing] = 30f;
                    w[ModuleType.MachineGun] = 20f;
                    w[ModuleType.Loader] = 20f;
                    // 차체 모듈은 터렛 피격 시 피해 없음
                    break;

                default:
                    foreach (ModuleType t in System.Enum.GetValues(typeof(ModuleType)))
                        w[t] = 1f;
                    break;
            }

            return w;
        }

        private ModuleType WeightedRandom(Dictionary<ModuleType, float> weights)
        {
            float total = 0;
            foreach (var kv in weights) total += kv.Value;

            float roll = Random.Range(0, total);
            float cumulative = 0;
            foreach (var kv in weights)
            {
                cumulative += kv.Value;
                if (roll < cumulative)
                    return kv.Key;
            }

            // fallback
            foreach (var kv in weights)
                return kv.Key;
            return ModuleType.Engine;
        }

        // ===== 패널티 API =====

        /// <summary>이동 AP 추가 비용 (엔진 + 캐터필러)</summary>
        public int GetMoveAPPenalty()
        {
            int penalty = 0;
            var engine = Get(ModuleType.Engine);
            if (engine.state == ModuleState.Damaged) penalty += 1;

            var catL = Get(ModuleType.CaterpillarLeft);
            var catR = Get(ModuleType.CaterpillarRight);
            if (catL.state == ModuleState.Damaged) penalty += 1;
            if (catR.state == ModuleState.Damaged) penalty += 1;

            return penalty;
        }

        /// <summary>사격 AP 추가 비용 (탄약고 + 장전기 + 포탑링)</summary>
        public int GetFireAPPenalty()
        {
            int penalty = 0;
            var ammo = Get(ModuleType.AmmoRack);
            var loader = Get(ModuleType.Loader);
            var ring = Get(ModuleType.TurretRing);

            if (ammo.state == ModuleState.Damaged) penalty += 1;
            else if (ammo.state == ModuleState.Broken) penalty += 2;

            if (loader.state == ModuleState.Damaged) penalty += 1;
            else if (loader.state == ModuleState.Broken) penalty += 2;

            if (ring.state == ModuleState.Damaged) penalty += 1;
            else if (ring.state == ModuleState.Broken) penalty += 2;

            return penalty;
        }

        /// <summary>주포 명중률 감소 (포신 상태)</summary>
        public float GetAccuracyPenalty()
        {
            var barrel = Get(ModuleType.Barrel);
            return barrel.state switch
            {
                ModuleState.Damaged => 0.15f,
                ModuleState.Broken or ModuleState.Destroyed => 0.30f,
                _ => 0f
            };
        }

        /// <summary>기총 명중률 감소</summary>
        public float GetMGAccuracyPenalty()
        {
            var mg = Get(ModuleType.MachineGun);
            return mg.state switch
            {
                ModuleState.Damaged => 0.10f,
                _ => 0f
            };
        }

        /// <summary>기총 버스트 감소량</summary>
        public int GetBurstPenalty()
        {
            var mg = Get(ModuleType.MachineGun);
            return mg.state == ModuleState.Damaged ? 2 : 0;
        }

        /// <summary>이동 가능 여부</summary>
        public bool CanMove()
        {
            var engine = Get(ModuleType.Engine);
            if (engine.state >= ModuleState.Broken) return false;

            var catL = Get(ModuleType.CaterpillarLeft);
            var catR = Get(ModuleType.CaterpillarRight);
            if (catL.state >= ModuleState.Broken && catR.state >= ModuleState.Broken)
                return false;

            return true;
        }

        /// <summary>주포 사격 가능 여부</summary>
        public bool CanFireMainGun()
        {
            var barrel = Get(ModuleType.Barrel);
            if (barrel.state >= ModuleState.Broken) return false;

            var loader = Get(ModuleType.Loader);
            if (loader.state == ModuleState.Destroyed) return false;

            return true;
        }

        /// <summary>기총 사격 가능 여부</summary>
        public bool CanFireMG()
        {
            var mg = Get(ModuleType.MachineGun);
            return mg.state < ModuleState.Broken;
        }

        /// <summary>차체 회전 가능 여부 (캐터 한쪽 고장 → 불가)</summary>
        public bool CanRotate()
        {
            var catL = Get(ModuleType.CaterpillarLeft);
            var catR = Get(ModuleType.CaterpillarRight);
            return catL.state < ModuleState.Broken && catR.state < ModuleState.Broken;
        }

        /// <summary>탄약고 완파 여부 (유폭 판정)</summary>
        public bool IsAmmoRackDestroyed() =>
            Get(ModuleType.AmmoRack).state == ModuleState.Destroyed;

        // ===== 상태 저장/복원 =====

        public ModuleSaveData[] SaveAll()
        {
            var list = new List<ModuleSaveData>();
            foreach (var kv in modules)
                list.Add(kv.Value.Save());
            return list.ToArray();
        }

        public void RestoreAll(ModuleSaveData[] data)
        {
            if (data == null) return;
            foreach (var d in data)
            {
                if (modules.TryGetValue(d.type, out var m))
                    m.Restore(d);
            }
        }

        // ===== 유틸 =====

        public static string GetModuleName(ModuleType type) => type switch
        {
            ModuleType.Engine => "엔진",
            ModuleType.Barrel => "포신",
            ModuleType.MachineGun => "기총",
            ModuleType.AmmoRack => "탄약고",
            ModuleType.Loader => "장전기",
            ModuleType.CaterpillarLeft => "캐터L",
            ModuleType.CaterpillarRight => "캐터R",
            ModuleType.TurretRing => "포탑링",
            _ => type.ToString()
        };

        public static string GetStateName(ModuleState state) => state switch
        {
            ModuleState.Normal => "정상",
            ModuleState.Damaged => "손상",
            ModuleState.Broken => "고장",
            ModuleState.Destroyed => "완파",
            _ => ""
        };
    }
}
