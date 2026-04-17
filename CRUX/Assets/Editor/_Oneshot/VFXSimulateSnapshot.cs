using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// VFX 스냅샷 — Edit Mode에서 파티클 Simulate로 특정 시점의 렌더 상태 고정.
/// capture_scene_object와 조합해 머티리얼/색상 확인용.
/// </summary>
public static class VFXSimulateSnapshot
{
    public static void Execute()
    {
        GameObject root = null;
        var scene = EditorSceneManager.GetActiveScene();
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == "ConcreteImpactVFX") { root = go; break; }
        if (root == null) { Debug.LogError("[VFX Sim] ConcreteImpactVFX 없음"); return; }

        // 모든 파티클 시스템을 0.3초 지점으로 시뮬레이션 (확장 중간 시점)
        var systems = root.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in systems)
        {
            ps.Simulate(0.3f, true, true, false);
            ps.Pause();
        }
        Debug.Log($"[VFX Sim] {systems.Length}개 파티클 0.3초 시점으로 Simulate + Pause");

        // 머티리얼 참조 검증
        foreach (var ps in systems)
        {
            var psr = ps.GetComponent<ParticleSystemRenderer>();
            string matName = psr != null && psr.sharedMaterial != null ? psr.sharedMaterial.name : "<NULL>";
            Debug.Log($"[VFX Sim] {ps.name}: material = {matName}");
        }
        SceneView.RepaintAll();
    }
}
