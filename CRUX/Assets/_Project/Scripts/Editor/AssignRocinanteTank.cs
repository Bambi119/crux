using UnityEngine;
using Crux.UI.Deployment;
using Crux.Data;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Crux.Editor
{
    public class AssignRocinanteTank
    {
        #if UNITY_EDITOR
        public static void Execute()
        {
            Debug.Log("[CRUX] AssignRocinanteTank.Execute");

            // 현재 씬의 CrewDeploymentController 찾기
            var controller = Object.FindObjectOfType<CrewDeploymentController>();
            if (controller == null)
            {
                Debug.LogError("[CRUX] CrewDeploymentController not found in scene");
                return;
            }

            // Tank_rocinante.asset 로드
            var rocinante = AssetDatabase.LoadAssetAtPath<TankDataSO>("Assets/_Project/Data/Tanks/Tank_rocinante.asset");
            if (rocinante == null)
            {
                Debug.LogError("[CRUX] Tank_rocinante.asset not found");
                return;
            }

            // Controller의 ownedTanks 리스트에 추가
            var serializedController = new SerializedObject(controller);
            var ownedTanksProp = serializedController.FindProperty("ownedTanks");

            if (ownedTanksProp == null)
            {
                Debug.LogError("[CRUX] ownedTanks property not found");
                return;
            }

            ownedTanksProp.ClearArray();
            ownedTanksProp.InsertArrayElementAtIndex(0);
            ownedTanksProp.GetArrayElementAtIndex(0).objectReferenceValue = rocinante;

            serializedController.ApplyModifiedProperties();

            Debug.Log($"[CRUX] Assigned Tank_rocinante to controller.ownedTanks");
        }
        #endif
    }
}
