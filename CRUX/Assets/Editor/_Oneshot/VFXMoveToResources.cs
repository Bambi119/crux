using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// ConcreteImpactVFX 프리팹을 Resources 폴더로 이동 — HitEffects가 Resources.Load로 접근 가능하게.
/// MoveAsset은 GUID 유지하므로 VFXTestRunner의 참조도 자동 업데이트됨.
/// </summary>
public static class VFXMoveToResources
{
    const string From = "Assets/_Project/Prefabs/VFX/ConcreteImpactVFX.prefab";
    const string ToDir = "Assets/_Project/Resources/VFX";
    const string To = "Assets/_Project/Resources/VFX/ConcreteImpactVFX.prefab";

    public static void Execute()
    {
        if (!File.Exists(From))
        {
            Debug.LogError($"[VFX Move] 원본 없음: {From}");
            return;
        }
        Directory.CreateDirectory(ToDir);
        AssetDatabase.Refresh();
        string err = AssetDatabase.MoveAsset(From, To);
        if (!string.IsNullOrEmpty(err))
        {
            Debug.LogError($"[VFX Move] 이동 실패: {err}");
            return;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[VFX Move] 성공: {From} → {To}");
    }
}
