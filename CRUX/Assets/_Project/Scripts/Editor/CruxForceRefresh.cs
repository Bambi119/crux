using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Crux.Editor
{
    public static class CruxForceRefresh
    {
        public static void Execute()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            CompilationPipeline.RequestScriptCompilation();
            Debug.Log("[CruxForceRefresh] AssetDatabase.Refresh + RequestScriptCompilation triggered");
        }
    }
}
