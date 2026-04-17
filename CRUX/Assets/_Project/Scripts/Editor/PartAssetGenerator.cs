using UnityEngine;
using UnityEditor;
using Crux.Data;

namespace Crux.EditorTools
{
    /// <summary>
    /// 샘플 PartSO 10개 에셋 생성기 — D-1.
    /// 런타임 ScriptableObject.CreateInstance 폴백을 에셋 영속화로 전환.
    /// Assets/_Project/Data/Parts/Samples/ 아래에 생성.
    ///
    /// 기존 에셋은 스킵 (덮어쓰지 않음). 실행 후 HangarBootstrap이 AssetDatabase.LoadAssetAtPath로 로드.
    /// </summary>
    public static class PartAssetGenerator
    {
        const string SamplesPath = "Assets/_Project/Data/Parts/Samples";

        [MenuItem("Crux/Generate/Sample Parts")]
        public static void GenerateSampleParts()
        {
            EnsureFolder(SamplesPath);

            // Engine 2
            CreateEngine("engine_v8_diesel", "V8 디젤", 500f, 400f);
            CreateEngine("engine_v6_gasoline", "V6 가솔린", 380f, 280f);

            // Turret 2
            CreateTurret("turret_medium", "중형 터렛", 300f, 75);
            CreateTurret("turret_large", "대형 터렛", 450f, 120);

            // MainGun 2
            CreateMainGun("maingun_76mm", "76mm 장포신", 250f, 75, 120f);
            CreateMainGun("maingun_88mm", "88mm 단포신", 350f, 88, 150f);

            // AmmoRack 2
            CreateAmmoRack("ammorack_standard", "표준 탄약고", 100f, 30);
            CreateAmmoRack("ammorack_large", "대용량 탄약고", 160f, 50);

            // Track 2
            CreateTrack("track_standard", "표준궤", 200f);
            CreateTrack("track_wide", "광궤", 280f);

            // Armor 2
            CreateArmor("armor_light", "경장갑판", 20f, 60f, ArmorType.Light, 1.4f);
            CreateArmor("armor_heavy", "중장갑판", 60f, 140f, ArmorType.Heavy, 1.0f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CRUX] 샘플 파츠 12개 생성 완료 (Assets/_Project/Data/Parts/Samples)");
        }

        static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
                string leaf = System.IO.Path.GetFileName(path);
                if (!AssetDatabase.IsValidFolder(parent))
                    EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        static EnginePartSO CreateEngine(string id, string displayName, float weight, float powerOutput)
        {
            string assetPath = $"{SamplesPath}/{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnginePartSO>(assetPath);
            if (existing != null) { Debug.LogWarning($"[CRUX] {assetPath} 존재 — 스킵"); return existing; }

            var so = ScriptableObject.CreateInstance<EnginePartSO>();
            so.partName = displayName;
            so.weight = weight;
            so.powerOutput = powerOutput;
            AssetDatabase.CreateAsset(so, assetPath);
            return so;
        }

        static TurretPartSO CreateTurret(string id, string displayName, float weight, int caliberLimit)
        {
            string assetPath = $"{SamplesPath}/{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TurretPartSO>(assetPath);
            if (existing != null) { Debug.LogWarning($"[CRUX] {assetPath} 존재 — 스킵"); return existing; }

            var so = ScriptableObject.CreateInstance<TurretPartSO>();
            so.partName = displayName;
            so.weight = weight;
            so.caliberLimit = caliberLimit;
            AssetDatabase.CreateAsset(so, assetPath);
            return so;
        }

        static MainGunPartSO CreateMainGun(string id, string displayName, float weight, int caliber, float penetration)
        {
            string assetPath = $"{SamplesPath}/{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<MainGunPartSO>(assetPath);
            if (existing != null) { Debug.LogWarning($"[CRUX] {assetPath} 존재 — 스킵"); return existing; }

            var so = ScriptableObject.CreateInstance<MainGunPartSO>();
            so.partName = displayName;
            so.weight = weight;
            so.caliber = caliber;
            so.basePenetration = penetration;
            AssetDatabase.CreateAsset(so, assetPath);
            return so;
        }

        static AmmoRackPartSO CreateAmmoRack(string id, string displayName, float weight, int maxAmmo)
        {
            string assetPath = $"{SamplesPath}/{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<AmmoRackPartSO>(assetPath);
            if (existing != null) { Debug.LogWarning($"[CRUX] {assetPath} 존재 — 스킵"); return existing; }

            var so = ScriptableObject.CreateInstance<AmmoRackPartSO>();
            so.partName = displayName;
            so.weight = weight;
            so.maxMainGunAmmo = maxAmmo;
            AssetDatabase.CreateAsset(so, assetPath);
            return so;
        }

        static TrackPartSO CreateTrack(string id, string displayName, float weight)
        {
            string assetPath = $"{SamplesPath}/{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TrackPartSO>(assetPath);
            if (existing != null) { Debug.LogWarning($"[CRUX] {assetPath} 존재 — 스킵"); return existing; }

            var so = ScriptableObject.CreateInstance<TrackPartSO>();
            so.partName = displayName;
            so.weight = weight;
            AssetDatabase.CreateAsset(so, assetPath);
            return so;
        }

        static ArmorPartSO CreateArmor(string id, string displayName, float weight, float baseProtection, ArmorType type, float angleModifier)
        {
            string assetPath = $"{SamplesPath}/{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ArmorPartSO>(assetPath);
            if (existing != null) { Debug.LogWarning($"[CRUX] {assetPath} 존재 — 스킵"); return existing; }

            var so = ScriptableObject.CreateInstance<ArmorPartSO>();
            so.partName = displayName;
            so.weight = weight;
            so.baseProtection = baseProtection;
            so.armorType = type;
            so.angleModifier = angleModifier;
            AssetDatabase.CreateAsset(so, assetPath);
            return so;
        }
    }
}
