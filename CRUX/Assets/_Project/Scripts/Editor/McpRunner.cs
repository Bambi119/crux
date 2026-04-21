#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Crux.EditorTools
{
    public static class McpRunner
    {
        public static void RefreshAssets()
        {
            AssetDatabase.Refresh();
            Debug.Log("[McpRunner] AssetDatabase.Refresh() done");
        }

        public static void RunAllStatic()
        {
            EditorApplication.ExecuteMenuItem("Crux/Test/Run All Static");
        }

        public static void PlaySmokeTerrain()
        {
            EditorApplication.ExecuteMenuItem("Crux/Test/PlaySmoke TerrainTest (3s)");
        }

        public static void CaptureUIAPPreview()
        {
            EditorApplication.ExecuteMenuItem("Crux/Test/UICapture AP Preview");
        }

        public static void CaptureUIIdleHUD()
        {
            EditorApplication.ExecuteMenuItem("Crux/Test/UICapture Idle HUD");
        }

        public static void AbortUICapture()
        {
            EditorApplication.ExecuteMenuItem("Crux/Test/UICapture Abort");
        }
    }
}
#endif
