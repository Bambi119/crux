using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Crux.Cinematic;

/// <summary>
/// One-shot Editor 스크립트 — 현재 열린 VFXTestScene의 ConcreteImpactVFX
/// 자식 파티클에 ParticleSystemConfig 기본값을 영구 저장한 뒤 씬 저장.
/// execute_script MCP로 한 번만 호출 후 삭제 권장.
/// </summary>
public static class VFXApplyPresetOneshot
{
    public static void Execute()
    {
        var root = GameObject.Find("ConcreteImpactVFX");
        if (root == null)
        {
            Debug.LogError("[VFX Oneshot] ConcreteImpactVFX GameObject not found in scene");
            return;
        }

        int count = 0;
        count += Apply(root, "Sparks", ParticleSystemConfig.ConfigureSparks);
        count += Apply(root, "Flash", ParticleSystemConfig.ConfigureFlash);
        count += Apply(root, "Fire", ParticleSystemConfig.ConfigureFire);
        count += Apply(root, "Smoke", ParticleSystemConfig.ConfigureSmoke);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log($"[VFX Oneshot] {count}/4 파티클에 프리셋 저장 완료 + 씬 저장");
    }

    static int Apply(GameObject root, string name, System.Action<ParticleSystem> cfg)
    {
        var t = root.transform.Find(name);
        if (t == null) return 0;
        var ps = t.GetComponent<ParticleSystem>();
        if (ps == null) return 0;
        cfg(ps);
        EditorUtility.SetDirty(ps.gameObject);
        return 1;
    }
}
