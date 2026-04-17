using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Crux.Cinematic;

/// <summary>
/// One-shot Editor 스크립트 — VFX 에셋(머티리얼/텍스처) 영구 저장 + 씬/프리팹에 적용.
/// 런타임 생성 머티리얼은 씬/프리팹 재로드 시 끊어져 핑크가 되므로 에셋으로 저장 필수.
/// </summary>
public static class VFXApplyPresetOneshot
{
    const string PrefabPath = "Assets/_Project/Prefabs/VFX/ConcreteImpactVFX.prefab";
    const string AssetFolder = "Assets/_Project/Materials/VFX";
    const string ScenePath = "Assets/_Project/Scenes/VFXTestScene.unity";

    // 에셋 머티리얼 캐시 (한 번 생성하면 이후 재사용)
    static Material _sparkMat, _flashMat, _fireMat, _smokeMat;

    public static void Execute()
    {
        PrepareAssets();
        ApplyToScene();
        ApplyToPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void PrepareAssets()
    {
        Directory.CreateDirectory(AssetFolder);
        var sparkTex = SaveTex(ParticleSystemConfig.GetSparkTexture(), "SparkTex");
        var ringTex = SaveTex(ParticleSystemConfig.GetRingTexture(), "RingTex");
        var softTex = SaveTex(ParticleSystemConfig.GetSoftCircleTexture(), "SoftCircleTex");

        var shader = Shader.Find("Sprites/Default");
        _sparkMat = SaveMat("SparkMat", shader, sparkTex, new Color(1.4f, 0.85f, 0.25f, 1f));
        _flashMat = SaveMat("FlashRingMat", shader, ringTex, new Color(1.5f, 1.0f, 0.5f, 1f));
        _fireMat = SaveMat("FireMat", shader, softTex, new Color(1.3f, 0.55f, 0.15f, 1f));
        _smokeMat = SaveMat("SmokeMat", shader, softTex, Color.white);
        Debug.Log("[VFX Oneshot] 에셋 머티리얼 4종 + 텍스처 3종 준비 완료");
    }

    static Texture2D SaveTex(Texture2D tex, string name)
    {
        string path = $"{AssetFolder}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material SaveMat(string name, Shader shader, Texture2D tex, Color color)
    {
        string path = $"{AssetFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.mainTexture = tex;
        mat.color = color;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static GameObject FindInActiveScene(string name)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }

    static void ApplyToScene()
    {
        // 활성 씬이 다르면 VFXTestScene 열기
        var active = EditorSceneManager.GetActiveScene();
        if (!active.IsValid() || active.path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var root = FindInActiveScene("ConcreteImpactVFX");
        if (root == null)
        {
            Debug.LogWarning("[VFX Oneshot] 씬에 ConcreteImpactVFX 없음 — 씬 적용 건너뜀");
            return;
        }
        int count = 0;
        count += Apply(root, "Sparks", ParticleSystemConfig.ConfigureSparks, _sparkMat);
        count += Apply(root, "Flash", ParticleSystemConfig.ConfigureFlash, _flashMat);
        count += Apply(root, "Fire", ParticleSystemConfig.ConfigureFire, _fireMat);
        count += Apply(root, "Smoke", ParticleSystemConfig.ConfigureSmoke, _smokeMat);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[VFX Oneshot] 씬 {count}/4 파티클 프리셋 + 에셋 머티리얼 저장");
    }

    static void ApplyToPrefab()
    {
        var sceneRoot = FindInActiveScene("ConcreteImpactVFX");
        if (sceneRoot == null)
        {
            Debug.LogError("[VFX Oneshot] 씬에 ConcreteImpactVFX 없음 — 프리팹 덮어쓰기 불가");
            return;
        }
        PrefabUtility.SaveAsPrefabAsset(sceneRoot, PrefabPath);
        Debug.Log($"[VFX Oneshot] 씬 ConcreteImpactVFX를 프리팹에 덮어씀: {PrefabPath}");
    }

    static int Apply(GameObject root, string name, System.Action<ParticleSystem> cfg, Material assetMat)
    {
        var t = root.transform.Find(name);
        if (t == null) return 0;
        var ps = t.GetComponent<ParticleSystem>();
        if (ps == null) return 0;
        cfg(ps);
        // 런타임 머티리얼을 에셋 머티리얼로 교체 (재로드 후에도 유지)
        var psr = ps.GetComponent<ParticleSystemRenderer>();
        if (psr != null && assetMat != null)
        {
            psr.sharedMaterial = assetMat;
            EditorUtility.SetDirty(psr);
        }
        EditorUtility.SetDirty(ps.gameObject);
        return 1;
    }
}
