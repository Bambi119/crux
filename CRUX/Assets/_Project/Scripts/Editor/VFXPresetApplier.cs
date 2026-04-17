#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Crux.Cinematic;

namespace Crux.EditorTools
{
    /// <summary>
    /// ParticleSystemConfig의 코드 기본값을 씬의 ParticleSystem에 영구 저장.
    /// 선택된 GameObject에 메뉴로 적용 → EditorUtility.SetDirty → 씬 저장 시 반영.
    /// 이후 useCodeDefaults=false로 두고 Inspector 수동 튜닝 가능.
    /// </summary>
    public static class VFXPresetApplier
    {
        [MenuItem("Crux/VFX/Apply All Presets (ConcreteImpactVFX root)")]
        public static void ApplyAll()
        {
            var root = Selection.activeGameObject;
            if (root == null)
            {
                EditorUtility.DisplayDialog("VFX Preset", "Hierarchy에서 ConcreteImpactVFX 루트를 선택한 뒤 다시 시도하세요.", "확인");
                return;
            }

            int count = 0;
            count += TryApply(root.transform.Find("Sparks"), ParticleSystemConfig.ConfigureSparks);
            count += TryApply(root.transform.Find("Flash"), ParticleSystemConfig.ConfigureFlash);
            count += TryApply(root.transform.Find("Fire"), ParticleSystemConfig.ConfigureFire);
            count += TryApply(root.transform.Find("Smoke"), ParticleSystemConfig.ConfigureSmoke);

            EditorUtility.DisplayDialog(
                "VFX Preset 적용 완료",
                $"{count}/4 파티클(Sparks/Flash/Fire/Smoke)에 코드 기본값을 저장했습니다.\n\nCtrl+S로 씬 저장을 완료해 주세요.",
                "확인");
        }

        [MenuItem("Crux/VFX/Apply Sparks Preset (선택된 PS)")]
        public static void ApplySparks() => ApplyToSelected(ParticleSystemConfig.ConfigureSparks, "Sparks");
        [MenuItem("Crux/VFX/Apply Flash Preset (선택된 PS)")]
        public static void ApplyFlash() => ApplyToSelected(ParticleSystemConfig.ConfigureFlash, "Flash");
        [MenuItem("Crux/VFX/Apply Fire Preset (선택된 PS)")]
        public static void ApplyFire() => ApplyToSelected(ParticleSystemConfig.ConfigureFire, "Fire");
        [MenuItem("Crux/VFX/Apply Smoke Preset (선택된 PS)")]
        public static void ApplySmoke() => ApplyToSelected(ParticleSystemConfig.ConfigureSmoke, "Smoke");

        private static int TryApply(Transform t, System.Action<ParticleSystem> cfg)
        {
            if (t == null) return 0;
            var ps = t.GetComponent<ParticleSystem>();
            if (ps == null) return 0;
            cfg(ps);
            EditorUtility.SetDirty(ps.gameObject);
            return 1;
        }

        private static void ApplyToSelected(System.Action<ParticleSystem> cfg, string label)
        {
            var go = Selection.activeGameObject;
            var ps = go?.GetComponent<ParticleSystem>();
            if (ps == null)
            {
                EditorUtility.DisplayDialog("VFX Preset", $"ParticleSystem이 붙은 GameObject를 선택하세요 ({label}).", "확인");
                return;
            }
            cfg(ps);
            EditorUtility.SetDirty(ps.gameObject);
            EditorUtility.DisplayDialog("VFX Preset 적용", $"{go.name}에 {label} preset 적용.\nCtrl+S로 씬 저장.", "확인");
        }
    }
}
#endif
