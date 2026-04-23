#if UNITY_EDITOR
using UnityEditor;

namespace Crux.EditorTools
{
    // Coplay MCP execute_script 진입점 (MenuItem 대체)
    public static class CruxInvokeRunner
    {
        public static void Execute()
        {
            CruxTestRunner.RunAllStatic();
        }

        public static void SmokeTerrain3s()
        {
            CruxPlaySmoke.SmokeTerrainTest3s();
        }

        public static void SmokeTerrain8s()
        {
            CruxPlaySmoke.SmokeTerrainTest8s();
        }
    }
}
#endif
