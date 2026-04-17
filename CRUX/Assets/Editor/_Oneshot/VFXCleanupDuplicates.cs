using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 중복 생성된 Flash/Fire 자식 정리. 각 이름당 1개만 남김.
/// </summary>
public static class VFXCleanupDuplicates
{
    public static void Execute()
    {
        var scene = EditorSceneManager.GetActiveScene();
        GameObject root = null;
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == "ConcreteImpactVFX") { root = go; break; }
        if (root == null) { Debug.LogError("[VFX Cleanup] ConcreteImpactVFX 없음"); return; }

        var seen = new HashSet<string>();
        var toDelete = new List<GameObject>();
        foreach (Transform child in root.transform)
        {
            if (seen.Contains(child.name)) toDelete.Add(child.gameObject);
            else seen.Add(child.name);
        }
        foreach (var go in toDelete) Object.DestroyImmediate(go);
        Debug.Log($"[VFX Cleanup] 중복 {toDelete.Count}개 제거. 남은 자식: {root.transform.childCount}");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();
    }
}
