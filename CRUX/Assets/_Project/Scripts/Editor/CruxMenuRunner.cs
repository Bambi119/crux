using UnityEditor;
using UnityEngine;

namespace Crux.Editor
{
    public static class CruxMenuRunner
    {
        public static void RunAllStatic()
        {
            bool ok = EditorApplication.ExecuteMenuItem("Crux/Test/Run All Static");
            Debug.Log($"[CruxMenuRunner] Run All Static executed: {ok}");
        }

        public static void PlaySmoke3s()
        {
            bool ok = EditorApplication.ExecuteMenuItem("Crux/Test/PlaySmoke TerrainTest (3s)");
            Debug.Log($"[CruxMenuRunner] PlaySmoke TerrainTest (3s) executed: {ok}");
        }
    }
}
