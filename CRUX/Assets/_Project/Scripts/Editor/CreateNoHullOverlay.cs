using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Crux.Editor
{
    public class CreateNoHullOverlay
    {
        #if UNITY_EDITOR
        public static void Execute()
        {
            Debug.Log("[CRUX] CreateNoHullOverlay.Execute");

            var centerPanel = GameObject.Find("Canvas/MainContent/CenterPanel");
            if (centerPanel == null)
            {
                Debug.LogError("[CRUX] CenterPanel not found");
                return;
            }

            // NoHullOverlay 생성
            var overlayObj = new GameObject("NoHullOverlay");
            overlayObj.transform.SetParent(centerPanel.transform, false);

            var overlayImage = overlayObj.AddComponent<Image>();
            overlayImage.color = new Color(0, 0, 0, 0.5f); // 반투명 검은색

            var overlayRect = overlayObj.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            // Text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(overlayObj.transform, false);
            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = "No hull selected";
            text.fontSize = 32;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            // 초기엔 비활성화
            overlayObj.SetActive(false);

            var binder = Object.FindObjectOfType<Crux.UI.Deployment.CrewDeploymentBinder>();
            if (binder != null)
            {
                var serializedBinder = new SerializedObject(binder);
                serializedBinder.FindProperty("noHullOverlay").objectReferenceValue = overlayObj;
                serializedBinder.ApplyModifiedProperties();
                Debug.Log("[CRUX] Connected NoHullOverlay to Binder");
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
        #endif
    }
}
