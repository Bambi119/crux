using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Crux.UI.Hangar
{
    // docs/10b §2.1 — 좌측 세로 5탭 메뉴. Composition / Maintenance 활성, Shop / Mess / People 잠금.
    // HangarController.SwitchTab(tab)로 탭 전환. TabChangedEvent 구독 → 활성 탭 하이라이트.
    public class SideNavBar : MonoBehaviour
    {
        const float BarWidth = 110f;

        HangarController controller;
        IHangarBus bus;
        IHangarStateReadOnly state;

        readonly Dictionary<HangarTab, Button> tabButtons = new Dictionary<HangarTab, Button>();
        readonly Dictionary<HangarTab, Image> tabBackgrounds = new Dictionary<HangarTab, Image>();
        readonly Dictionary<HangarTab, Text> tabLabels = new Dictionary<HangarTab, Text>();
        readonly Dictionary<HangarTab, Text> tabBadges = new Dictionary<HangarTab, Text>();

        public void Bind(Transform canvasRoot, HangarController controller)
        {
            if (canvasRoot == null || controller == null) return;
            this.controller = controller;
            this.bus = controller.Bus;
            this.state = controller.State;

            BuildBar(canvasRoot);
            bus.Subscribe<TabChangedEvent>(OnTabChanged);
            bus.Subscribe<AwakeningQueueChangedEvent>(OnAwakeningQueueChanged);
            UpdateActiveHighlight(state.SelectedTab);
            UpdateMaintenanceBadge(state.AwakeningQueueCount);
        }

        void OnDestroy()
        {
            if (bus != null)
            {
                bus.Unsubscribe<TabChangedEvent>(OnTabChanged);
                bus.Unsubscribe<AwakeningQueueChangedEvent>(OnAwakeningQueueChanged);
            }
        }

        void BuildBar(Transform canvasRoot)
        {
            var existing = canvasRoot.Find("SideNavBar");
            if (existing != null) DestroyImmediate(existing.gameObject);

            var bar = new GameObject("SideNavBar", typeof(RectTransform));
            var rt = (RectTransform)bar.transform;
            rt.SetParent(canvasRoot, false);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(BarWidth, 0);

            var bg = bar.AddComponent<Image>();
            bg.color = UIColorPalette.SurfaceContainerLowest;

            var vlg = bar.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.padding = new RectOffset(8, 8, 72, 12); // top 72 — leave room for logo / TopAppBar overlap
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;

            AddTab(bar.transform, HangarTab.Composition, "편성", unlocked: true);
            AddTab(bar.transform, HangarTab.Maintenance, "정비", unlocked: true);
            AddTab(bar.transform, HangarTab.Shop, "상점", unlocked: false);
            AddTab(bar.transform, HangarTab.Mess, "식당", unlocked: false);
            AddTab(bar.transform, HangarTab.People, "인사", unlocked: false);
        }

        void AddTab(Transform parent, HangarTab tab, string label, bool unlocked)
        {
            var tabGo = new GameObject("Tab_" + tab, typeof(RectTransform));
            tabGo.transform.SetParent(parent, false);

            var le = tabGo.AddComponent<LayoutElement>();
            le.minHeight = 64;
            le.preferredHeight = 64;

            var bgImage = tabGo.AddComponent<Image>();
            bgImage.color = UIColorPalette.SurfaceContainer;
            tabBackgrounds[tab] = bgImage;

            var vlg = tabGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2;
            vlg.padding = new RectOffset(4, 4, 8, 8);
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.MiddleCenter;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(tabGo.transform, false);
            var labelLe = labelGo.AddComponent<LayoutElement>();
            labelLe.minHeight = 20;
            var labelText = labelGo.AddComponent<Text>();
            labelText.font = HangarButtonHelpers.GetKoreanFont();
            labelText.fontSize = 14;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = unlocked ? UIColorPalette.OnSurface : UIColorPalette.OnSurfaceVariant;
            labelText.text = label;
            tabLabels[tab] = labelText;

            var badgeGo = new GameObject("Badge", typeof(RectTransform));
            badgeGo.transform.SetParent(tabGo.transform, false);
            var badgeLe = badgeGo.AddComponent<LayoutElement>();
            badgeLe.minHeight = 16;
            var badgeText = badgeGo.AddComponent<Text>();
            badgeText.font = HangarButtonHelpers.GetKoreanFont();
            badgeText.fontSize = 11;
            badgeText.alignment = TextAnchor.MiddleCenter;
            badgeText.color = UIColorPalette.PrimaryContainer;
            badgeText.text = unlocked ? string.Empty : "잠김";
            tabBadges[tab] = badgeText;

            var btn = tabGo.AddComponent<Button>();
            btn.targetGraphic = bgImage;
            btn.interactable = unlocked;
            if (unlocked)
                btn.onClick.AddListener(() => OnTabClicked(tab));

            tabButtons[tab] = btn;
        }

        void OnTabClicked(HangarTab tab)
        {
            if (controller == null) return;
            controller.SwitchTab(tab);
        }

        void OnTabChanged(TabChangedEvent evt) => UpdateActiveHighlight(evt.Current);

        void OnAwakeningQueueChanged(AwakeningQueueChangedEvent evt) => UpdateMaintenanceBadge(evt.Count);

        void UpdateActiveHighlight(HangarTab active)
        {
            foreach (var kvp in tabBackgrounds)
            {
                bool isActive = kvp.Key == active;
                bool unlocked = tabButtons.TryGetValue(kvp.Key, out var btn) && btn.interactable;
                if (isActive)
                    kvp.Value.color = UIColorPalette.PrimaryContainer;
                else
                    kvp.Value.color = unlocked ? UIColorPalette.SurfaceContainer : UIColorPalette.SurfaceContainerLow;

                if (tabLabels.TryGetValue(kvp.Key, out var label))
                {
                    if (isActive) label.color = UIColorPalette.OnPrimaryContainer;
                    else label.color = unlocked ? UIColorPalette.OnSurface : UIColorPalette.OnSurfaceVariant;
                }
            }
        }

        void UpdateMaintenanceBadge(int count)
        {
            if (!tabBadges.TryGetValue(HangarTab.Maintenance, out var badge)) return;
            badge.text = count > 0 ? count.ToString() : string.Empty;
            badge.color = count > 0 ? UIColorPalette.PrimaryContainer : UIColorPalette.OnSurfaceVariant;
        }
    }
}
