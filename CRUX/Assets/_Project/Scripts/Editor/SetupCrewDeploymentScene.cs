using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Crux.Data;
using Crux.UI.Deployment;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Crux.Editor
{
    /// <summary>
    /// 편성 씬의 UI 구조와 프리팹을 자동으로 생성하는 유틸리티.
    /// </summary>
    public class SetupCrewDeploymentScene
    {
        #if UNITY_EDITOR
        public static void Execute()
        {
            Debug.Log("[CRUX] SetupCrewDeploymentScene.Execute");

            // 현재 씬 로드
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "CrewDeploymentScene")
            {
                Debug.LogError("[CRUX] Current scene is not CrewDeploymentScene");
                return;
            }

            // 씬 루트 정리: 기존 Canvas 제거하고 새로 생성
            var oldCanvas = GameObject.Find("MainCanvas");
            if (oldCanvas != null)
                Object.DestroyImmediate(oldCanvas);

            // Canvas 생성
            var canvasObj = new GameObject("Canvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var canvasScaler = canvasObj.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);

            var graphicRaycaster = canvasObj.AddComponent<GraphicRaycaster>();

            // Canvas 크기 설정
            var rectTransform = canvasObj.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            // 색상 설정 (배경)
            var canvasImage = canvasObj.AddComponent<Image>();
            canvasImage.color = new Color(16f / 255f, 19f / 255f, 25f / 255f, 1f); // #101319

            // MainContent (3 columns)
            var mainContentObj = new GameObject("MainContent");
            mainContentObj.transform.SetParent(canvasObj.transform, false);
            var mainContentRect = mainContentObj.AddComponent<RectTransform>();
            mainContentRect.anchorMin = Vector2.zero;
            mainContentRect.anchorMax = Vector2.one;
            mainContentRect.offsetMin = Vector2.zero;
            mainContentRect.offsetMax = Vector2.zero;

            // LeftPanel (Deployed Hulls) - 256px
            var leftPanelObj = CreatePanel(mainContentObj, "LeftPanel", 256, new Color(26f / 255f, 29f / 255f, 35f / 255f, 1f));
            var leftPanelRect = leftPanelObj.GetComponent<RectTransform>();
            leftPanelRect.anchorMin = new Vector2(0, 0);
            leftPanelRect.anchorMax = new Vector2(0, 1);
            leftPanelRect.offsetMin = Vector2.zero;
            leftPanelRect.offsetMax = new Vector2(256, 0);

            // CenterPanel (Crew Slots) - flex
            var centerPanelObj = CreatePanel(mainContentObj, "CenterPanel", 0, new Color(16f / 255f, 19f / 255f, 25f / 255f, 1f));
            var centerPanelRect = centerPanelObj.GetComponent<RectTransform>();
            centerPanelRect.anchorMin = new Vector2(256f / 1920f, 0);
            centerPanelRect.anchorMax = new Vector2((1920f - 320f) / 1920f, 1);
            centerPanelRect.offsetMin = Vector2.zero;
            centerPanelRect.offsetMax = Vector2.zero;

            // RightPanel (Barracks) - 320px
            var rightPanelObj = CreatePanel(mainContentObj, "RightPanel", 320, new Color(26f / 255f, 29f / 255f, 35f / 255f, 1f));
            var rightPanelRect = rightPanelObj.GetComponent<RectTransform>();
            rightPanelRect.anchorMin = new Vector2(1, 0);
            rightPanelRect.anchorMax = Vector2.one;
            rightPanelRect.offsetMin = new Vector2(-320, 0);
            rightPanelRect.offsetMax = Vector2.zero;

            // Roster Container (RightPanel 내부에 ScrollView)
            var rosterScrollView = new GameObject("RosterScrollView");
            rosterScrollView.transform.SetParent(rightPanelObj.transform, false);
            var rosterScrollViewRect = rosterScrollView.AddComponent<RectTransform>();
            rosterScrollViewRect.anchorMin = Vector2.zero;
            rosterScrollViewRect.anchorMax = Vector2.one;
            rosterScrollViewRect.offsetMin = new Vector2(8, 8);
            rosterScrollViewRect.offsetMax = new Vector2(-8, -8);

            var scrollRect = rosterScrollView.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            var rosterContainer = new GameObject("Content");
            rosterContainer.transform.SetParent(rosterScrollView.transform, false);
            var rosterContentRect = rosterContainer.AddComponent<RectTransform>();
            rosterContentRect.anchorMin = new Vector2(0, 1);
            rosterContentRect.anchorMax = new Vector2(1, 1);
            rosterContentRect.pivot = new Vector2(0.5f, 1);
            rosterContentRect.offsetMin = Vector2.zero;
            rosterContentRect.offsetMax = Vector2.zero;

            var layoutGroup = rosterContainer.AddComponent<VerticalLayoutGroup>();
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = true;

            scrollRect.content = rosterContentRect;

            // Morale Preview Text
            var moraleTextObj = new GameObject("MoralePreview");
            moraleTextObj.transform.SetParent(centerPanelObj.transform, false);
            var moraleRect = moraleTextObj.AddComponent<RectTransform>();
            moraleRect.anchorMin = new Vector2(0.5f, 0);
            moraleRect.anchorMax = new Vector2(0.5f, 0);
            moraleRect.pivot = new Vector2(0.5f, 0);
            moraleRect.offsetMin = new Vector2(-200, 20);
            moraleRect.offsetMax = new Vector2(200, 80);

            var moraleText = moraleTextObj.AddComponent<TextMeshProUGUI>();
            moraleText.text = "Base 50 | Cmdr +0 | Traits +0 = 50";
            moraleText.fontSize = 20;
            moraleText.alignment = TextAlignmentOptions.Bottom;
            moraleText.color = Color.white;

            // Footer (Back & Confirm 버튼)
            var footerObj = new GameObject("Footer");
            footerObj.transform.SetParent(canvasObj.transform, false);
            var footerRect = footerObj.AddComponent<RectTransform>();
            footerRect.anchorMin = new Vector2(0, 0);
            footerRect.anchorMax = new Vector2(1, 0);
            footerRect.pivot = new Vector2(0.5f, 0);
            footerRect.offsetMin = new Vector2(0, 0);
            footerRect.offsetMax = new Vector2(0, 64);

            var footerImage = footerObj.AddComponent<Image>();
            footerImage.color = new Color(26f / 255f, 29f / 255f, 35f / 255f, 1f);

            var footerLayout = footerObj.AddComponent<HorizontalLayoutGroup>();
            footerLayout.childForceExpandHeight = true;
            footerLayout.childForceExpandWidth = true;
            footerLayout.spacing = 16;
            footerLayout.padding = new RectOffset(16, 16, 8, 8);

            // Back Button
            var backBtnObj = CreateButton(footerObj, "BackButton", "Back", new Color(107f / 255f, 120f / 255f, 86f / 255f, 1f));
            // Confirm Button
            var confirmBtnObj = CreateButton(footerObj, "ConfirmButton", "Confirm", new Color(201f / 255f, 165f / 255f, 116f / 255f, 1f));

            // Bootstrap GameObject
            var bootstrapObj = new GameObject("Bootstrap");
            bootstrapObj.transform.SetParent(canvasObj.transform, false);

            // Controller 추가
            var controller = bootstrapObj.AddComponent<CrewDeploymentController>();

            // Binder 추가
            var binder = bootstrapObj.AddComponent<CrewDeploymentBinder>();

            // Binder 참조 연결 (코드로)
            var crewSlotViewType = typeof(CrewSlotView);

            // 슬롯 프리팹 생성 후 인스턴스화
            CreateCrewSlotsInCenter(centerPanelObj, binder);

            // Binder 필드 설정
            binder = bootstrapObj.GetComponent<CrewDeploymentBinder>();

            // Inspector 필드 와이어링 (SerializedObject 사용)
            var serializedBinder = new SerializedObject(binder);
            serializedBinder.FindProperty("controller").objectReferenceValue = controller;
            serializedBinder.FindProperty("moralePreviewText").objectReferenceValue = moraleText;
            serializedBinder.FindProperty("confirmButton").objectReferenceValue = confirmBtnObj.GetComponent<Button>();
            serializedBinder.FindProperty("backButton").objectReferenceValue = backBtnObj.GetComponent<Button>();
            serializedBinder.FindProperty("rosterContainer").objectReferenceValue = rosterContainer.GetComponent<RectTransform>();

            serializedBinder.ApplyModifiedProperties();

            // 씬 저장
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[CRUX] CrewDeploymentScene setup complete");
        }

        private static GameObject CreatePanel(GameObject parent, string name, float width, Color color)
        {
            var panelObj = new GameObject(name);
            panelObj.transform.SetParent(parent.transform, false);
            var image = panelObj.AddComponent<Image>();
            image.color = color;
            return panelObj;
        }

        private static GameObject CreateButton(GameObject parent, string name, string label, Color color)
        {
            var btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent.transform, false);

            var image = btnObj.AddComponent<Image>();
            image.color = color;

            var button = btnObj.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = new Color(color.r * 1.1f, color.g * 1.1f, color.b * 1.1f, color.a);
            colors.pressedColor = new Color(color.r * 0.9f, color.g * 0.9f, color.b * 0.9f, color.a);
            button.colors = colors;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 24;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            return btnObj;
        }

        private static void CreateCrewSlotsInCenter(GameObject centerPanel, CrewDeploymentBinder binder)
        {
            // 5개 슬롯 생성 (오각형 배치)
            var slotData = new[]
            {
                ("CommanderSlot", CrewClass.Commander, 0.5f, 0.2f),
                ("GunnerSlot", CrewClass.Gunner, 0.25f, 0.45f),
                ("LoaderSlot", CrewClass.Loader, 0.75f, 0.45f),
                ("DriverSlot", CrewClass.Driver, 0.2f, 0.8f),
                ("MechSlot", CrewClass.GunnerMech, 0.8f, 0.8f),
            };

            var serializedBinder = new SerializedObject(binder);

            foreach (var (slotName, crewClass, normX, normY) in slotData)
            {
                var slotObj = new GameObject(slotName);
                slotObj.transform.SetParent(centerPanel.transform, false);

                var slotRect = slotObj.AddComponent<RectTransform>();
                slotRect.anchorMin = Vector2.zero;
                slotRect.anchorMax = Vector2.zero;
                slotRect.sizeDelta = new Vector2(120, 140);
                slotRect.anchoredPosition = new Vector2(normX * centerPanel.GetComponent<RectTransform>().rect.width - slotRect.sizeDelta.x / 2,
                                                         -normY * centerPanel.GetComponent<RectTransform>().rect.height + slotRect.sizeDelta.y / 2);

                var slotImage = slotObj.AddComponent<Image>();
                slotImage.color = new Color(26f / 255f, 29f / 255f, 35f / 255f, 1f);

                var slotView = slotObj.AddComponent<CrewSlotView>();

                // 텍스트 자식 생성
                var labelObj = new GameObject("Label");
                labelObj.transform.SetParent(slotObj.transform, false);
                var labelRect = labelObj.AddComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0.5f, 1);
                labelRect.anchorMax = new Vector2(0.5f, 1);
                labelRect.pivot = new Vector2(0.5f, 1);
                labelRect.offsetMin = new Vector2(-50, -20);
                labelRect.offsetMax = new Vector2(50, -2);

                var labelText = labelObj.AddComponent<TextMeshProUGUI>();
                labelText.text = crewClass.ToString();
                labelText.fontSize = 14;
                labelText.alignment = TextAlignmentOptions.Top;
                labelText.color = new Color(160f / 255f, 160f / 255f, 160f / 255f, 1f); // #a0a0a0

                var nameObj = new GameObject("CrewName");
                nameObj.transform.SetParent(slotObj.transform, false);
                var nameRect = nameObj.AddComponent<RectTransform>();
                nameRect.anchorMin = Vector2.zero;
                nameRect.anchorMax = Vector2.one;
                nameRect.offsetMin = new Vector2(4, 4);
                nameRect.offsetMax = new Vector2(-4, -4);

                var nameText = nameObj.AddComponent<TextMeshProUGUI>();
                nameText.text = "— VACANT —";
                nameText.fontSize = 14;
                nameText.alignment = TextAlignmentOptions.Center;
                nameText.color = Color.white;

                // Binder에 슬롯 참조 설정
                string propertyName = crewClass switch
                {
                    CrewClass.Commander => "commanderSlot",
                    CrewClass.Gunner => "gunnerSlot",
                    CrewClass.Loader => "loaderSlot",
                    CrewClass.Driver => "driverSlot",
                    CrewClass.GunnerMech => "mgMechanicSlot",
                    _ => null
                };

                if (propertyName != null)
                {
                    var prop = serializedBinder.FindProperty(propertyName);
                    if (prop != null)
                        prop.objectReferenceValue = slotView;
                }
            }

            serializedBinder.ApplyModifiedProperties();
        }
        #endif
    }
}
