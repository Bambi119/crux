using UnityEngine;
using UnityEditor;

/// <summary>Resources.Load("VFX/ConcreteImpactVFX") 작동 확인.</summary>
public static class VFXPrefabSmoke
{
    public static void Execute()
    {
        var prefab = Resources.Load<GameObject>("VFX/ConcreteImpactVFX");
        if (prefab == null)
        {
            Debug.LogError("[VFX Smoke] Resources.Load 실패 — 프리팹이 Resources 폴더에 없음");
            return;
        }
        int children = prefab.transform.childCount;
        Debug.Log($"[VFX Smoke] 로드 성공. 루트 자식 {children}개 (Sparks/Flash/Fire/Smoke 기대)");
        for (int i = 0; i < children; i++)
        {
            var c = prefab.transform.GetChild(i);
            var psr = c.GetComponent<ParticleSystemRenderer>();
            string mat = psr != null && psr.sharedMaterial != null ? psr.sharedMaterial.name : "<NULL>";
            Debug.Log($"[VFX Smoke]   {c.name}: material = {mat}");
        }
    }
}
