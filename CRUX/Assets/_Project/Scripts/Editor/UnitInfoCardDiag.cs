#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Crux.EditorTools
{
    public static class UnitInfoCardDiag
    {
        [MenuItem("Crux/Diag/UnitInfoCard Dump")]
        public static void Dump()
        {
            var scenePath = "Assets/_Project/Scenes/TerrainTestScene.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[DIAG] scene={scene.name} isLoaded={scene.isLoaded}");

            GameObject canvas = null;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == "BattleHUDCanvas") { canvas = go; break; }
                var found = FindRecursive(go.transform, "BattleHUDCanvas");
                if (found != null) { canvas = found.gameObject; break; }
            }

            if (canvas == null)
            {
                sb.AppendLine("[DIAG] BattleHUDCanvas NOT FOUND");
                Debug.Log(sb.ToString());
                return;
            }

            sb.AppendLine($"[DIAG] Canvas found: {canvas.name} active={canvas.activeSelf}");

            var card = FindRecursive(canvas.transform, "UnitInfoCard");
            if (card == null)
            {
                sb.AppendLine("[DIAG] UnitInfoCard NOT FOUND under canvas");
                DumpChildren(canvas.transform, sb, 0, 3);
                Debug.Log(sb.ToString());
                return;
            }

            sb.AppendLine($"[DIAG] UnitInfoCard found.");
            DumpTransform(card, sb, "");

            sb.AppendLine($"[DIAG] UnitInfoCard children ({card.childCount}):");
            DumpChildren(card, sb, 1, 3);

            var logPath = "Temp/unitinfocard-diag.log";
            File.WriteAllText(logPath, sb.ToString());
            Debug.Log($"[DIAG] wrote {logPath}");
            Debug.Log(sb.ToString());
        }

        static Transform FindRecursive(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindRecursive(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        static void DumpTransform(Transform t, System.Text.StringBuilder sb, string indent)
        {
            var rt = t as RectTransform;
            if (rt != null)
            {
                sb.AppendLine($"{indent}{t.name} active={t.gameObject.activeSelf} anchMin={rt.anchorMin} anchMax={rt.anchorMax} pivot={rt.pivot} anchPos={rt.anchoredPosition} sizeDelta={rt.sizeDelta} scale={rt.localScale}");
            }
            else
            {
                sb.AppendLine($"{indent}{t.name} (non-RectTransform) active={t.gameObject.activeSelf}");
            }
        }

        static void DumpChildren(Transform parent, System.Text.StringBuilder sb, int depth, int maxDepth)
        {
            if (depth > maxDepth) return;
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                var indent = new string(' ', depth * 2);
                DumpTransform(c, sb, indent);
                DumpChildren(c, sb, depth + 1, maxDepth);
            }
        }
    }
}
#endif
