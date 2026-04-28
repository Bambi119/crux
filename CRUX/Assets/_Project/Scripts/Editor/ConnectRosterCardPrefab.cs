using UnityEngine;
using Crux.UI.Deployment;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Crux.Editor
{
    public class ConnectRosterCardPrefab
    {
        #if UNITY_EDITOR
        public static void Execute()
        {
            Debug.Log("[CRUX] ConnectRosterCardPrefab.Execute");

            var binder = Object.FindObjectOfType<CrewDeploymentBinder>();
            if (binder == null)
            {
                Debug.LogError("[CRUX] CrewDeploymentBinder not found");
                return;
            }

            var rosterCardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Deployment/RosterCard.prefab");
            if (rosterCardPrefab == null)
            {
                Debug.LogError("[CRUX] RosterCard prefab not found");
                return;
            }

            var serializedBinder = new SerializedObject(binder);
            serializedBinder.FindProperty("rosterCardPrefab").objectReferenceValue = rosterCardPrefab;
            serializedBinder.ApplyModifiedProperties();

            Debug.Log("[CRUX] Connected RosterCard prefab to Binder");
        }
        #endif
    }
}
