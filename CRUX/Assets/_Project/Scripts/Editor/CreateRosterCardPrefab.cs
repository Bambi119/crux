using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Crux.Editor
{
    public class CreateRosterCardPrefab
    {
        #if UNITY_EDITOR
        public static void Execute()
        {
            Debug.Log("[CRUX] CreateRosterCardPrefab.Execute");

            // 폴더 확인
            string folderPath = "Assets/_Project/Prefabs/UI/Deployment";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                string parentFolder = "Assets/_Project/Prefabs/UI";
                if (!AssetDatabase.IsValidFolder(parentFolder))
                {
                    string uiFolder = "Assets/_Project/Prefabs";
                    if (!AssetDatabase.IsValidFolder(uiFolder))
                    {
                        AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
                    }
                    AssetDatabase.CreateFolder(uiFolder, "UI");
                }
                AssetDatabase.CreateFolder(parentFolder, "Deployment");
            }

            // RosterCard 루트 생성
            var cardObj = new GameObject("RosterCard");
            cardObj.layer = LayerMask.NameToLayer("UI");

            // RectTransform
            var cardRect = cardObj.AddComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(300, 60);

            // Background Image
            var bgImage = cardObj.AddComponent<Image>();
            bgImage.color = new Color(26f / 255f, 29f / 255f, 35f / 255f, 1f); // #1a1d23

            // Button
            var button = cardObj.AddComponent<Button>();
            var buttonColors = button.colors;
            buttonColors.normalColor = bgImage.color;
            buttonColors.highlightedColor = new Color(107f / 255f, 120f / 255f, 86f / 255f, 1f); // olive
            buttonColors.pressedColor = new Color(201f / 255f, 165f / 255f, 116f / 255f, 1f); // sepia
            button.colors = buttonColors;

            // LayoutElement
            var layoutElement = cardObj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 60;

            // HorizontalLayoutGroup
            var hlg = cardObj.AddComponent<HorizontalLayoutGroup>();
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.spacing = 8;
            hlg.padding = new RectOffset(8, 8, 4, 4);

            // Portrait Placeholder
            var portraitObj = new GameObject("Portrait");
            portraitObj.transform.SetParent(cardObj.transform, false);
            var portraitImage = portraitObj.AddComponent<Image>();
            portraitImage.color = new Color(60f / 255f, 60f / 255f, 60f / 255f, 1f); // darkgray
            var portraitRect = portraitObj.GetComponent<RectTransform>();
            portraitRect.sizeDelta = new Vector2(48, 48);
            var portraitLayout = portraitObj.AddComponent<LayoutElement>();
            portraitLayout.preferredWidth = 48;
            portraitLayout.preferredHeight = 48;

            // Text Container
            var textContainerObj = new GameObject("TextContainer");
            textContainerObj.transform.SetParent(cardObj.transform, false);
            var textContainerRect = textContainerObj.AddComponent<RectTransform>();
            var textContainerVlg = textContainerObj.AddComponent<VerticalLayoutGroup>();
            textContainerVlg.childForceExpandHeight = false;
            textContainerVlg.childForceExpandWidth = true;

            // CrewName Text
            var crewNameObj = new GameObject("CrewName");
            crewNameObj.transform.SetParent(textContainerObj.transform, false);
            var crewNameText = crewNameObj.AddComponent<TextMeshProUGUI>();
            crewNameText.text = "Crew Name";
            crewNameText.fontSize = 18;
            crewNameText.color = Color.white;
            crewNameText.alignment = TextAlignmentOptions.Left;
            var crewNameRect = crewNameObj.AddComponent<RectTransform>();
            var crewNameLayout = crewNameObj.AddComponent<LayoutElement>();
            crewNameLayout.preferredHeight = 20;

            // ClassLabel Text
            var classLabelObj = new GameObject("ClassLabel");
            classLabelObj.transform.SetParent(textContainerObj.transform, false);
            var classLabelText = classLabelObj.AddComponent<TextMeshProUGUI>();
            classLabelText.text = "Commander";
            classLabelText.fontSize = 13;
            classLabelText.color = new Color(160f / 255f, 160f / 255f, 160f / 255f, 1f); // #a0a0a0
            classLabelText.alignment = TextAlignmentOptions.Left;
            var classLabelRect = classLabelObj.AddComponent<RectTransform>();
            var classLabelLayout = classLabelObj.AddComponent<LayoutElement>();
            classLabelLayout.preferredHeight = 16;

            // AssignedIndicator (작은 닷)
            var indicatorObj = new GameObject("AssignedIndicator");
            indicatorObj.transform.SetParent(cardObj.transform, false);
            var indicatorImage = indicatorObj.AddComponent<Image>();
            indicatorImage.color = new Color(107f / 255f, 120f / 255f, 86f / 255f, 1f); // olive
            var indicatorRect = indicatorObj.GetComponent<RectTransform>();
            indicatorRect.sizeDelta = new Vector2(8, 8);
            var indicatorLayout = indicatorObj.AddComponent<LayoutElement>();
            indicatorLayout.preferredWidth = 8;
            indicatorLayout.preferredHeight = 8;

            // 프리팹 저장
            string prefabPath = folderPath + "/RosterCard.prefab";
            PrefabUtility.SaveAsPrefabAsset(cardObj, prefabPath);
            Object.DestroyImmediate(cardObj);

            Debug.Log($"[CRUX] Created RosterCard prefab at {prefabPath}");
        }
        #endif
    }
}
