using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Crux.Data;

namespace Crux.UI.Hangar.Parts
{
    // docs/10b §3.1 / 10d RIGHT — 부품 창고.
    // 카테고리 탭(전체/엔진/포탑/주포/장갑/궤도/보조) + 필터링된 카드 리스트.
    // EQUIP 버튼 → state.SelectedTank에 장착. bus.Publish(PartEquippedEvent).
    public class PartsInventoryBinder : MonoBehaviour
    {
        HangarSharedState mutableState;
        IHangarStateReadOnly state;
        IHangarBus bus;
        ConvoyInventory convoy;
        Transform panelRoot;

        Transform tabsContainer;
        Transform listContainer;

        readonly List<GameObject> cards = new List<GameObject>();
        readonly List<Button> tabButtons = new List<Button>();

        PartCategory? activeFilter = null; // null = 전체

        static readonly (PartCategory? cat, string label)[] TabSpec =
        {
            (null, "전체"),
            (PartCategory.Engine, "엔진"),
            (PartCategory.Turret, "포탑"),
            (PartCategory.MainGun, "주포"),
            (PartCategory.Armor, "장갑"),
            (PartCategory.Track, "궤도"),
            (PartCategory.Auxiliary, "보조"),
        };

        public void WireScene(HangarSharedState mutableState, ConvoyInventory convoy, Transform panelRoot)
        {
            this.mutableState = mutableState;
            this.convoy = convoy;
            this.panelRoot = panelRoot;
        }

        public void Initialize(IHangarStateReadOnly state, IHangarBus bus)
        {
            this.state = state;
            this.bus = bus;

            EnsureContainers();
            BuildTabs();
            RebuildCards();

            bus.Subscribe<TankSelectedEvent>(OnTankSelected);
            bus.Subscribe<PartEquippedEvent>(OnLoadoutChanged);
            bus.Subscribe<PartUnequippedEvent>(OnLoadoutChanged);
        }

        public void Refresh(TankInstance tank)
        {
            RebuildCards();
        }

        void OnDestroy()
        {
            if (bus != null)
            {
                bus.Unsubscribe<TankSelectedEvent>(OnTankSelected);
                bus.Unsubscribe<PartEquippedEvent>(OnLoadoutChanged);
                bus.Unsubscribe<PartUnequippedEvent>(OnLoadoutChanged);
            }
        }

        void OnTankSelected(TankSelectedEvent evt) => RebuildCards();
        void OnLoadoutChanged<T>(T _) => RebuildCards();

        void EnsureContainers()
        {
            if (panelRoot == null) return;
            tabsContainer = panelRoot.Find("CategoryTabs") ?? CreateTabsContainer(panelRoot);
            listContainer = panelRoot.Find("PartsList") ?? CreateListContainer(panelRoot);
        }

        Transform CreateTabsContainer(Transform parent)
        {
            var go = new GameObject("CategoryTabs", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(12, -88);
            rt.offsetMax = new Vector2(-12, -56);

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4;
            hlg.childControlWidth = true;
            hlg.childForceExpandWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandHeight = true;
            return go.transform;
        }

        Transform CreateListContainer(Transform parent)
        {
            var go = new GameObject("PartsList", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(12, 12);
            rt.offsetMax = new Vector2(-12, -96);

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;
            return go.transform;
        }

        void BuildTabs()
        {
            foreach (var btn in tabButtons)
                if (btn != null) Destroy(btn.gameObject);
            tabButtons.Clear();

            foreach (var (cat, label) in TabSpec)
            {
                var btnGo = new GameObject($"Tab_{label}", typeof(RectTransform));
                btnGo.transform.SetParent(tabsContainer, false);
                var img = btnGo.AddComponent<Image>();
                img.color = UIColorPalette.SurfaceContainer;
                var btn = btnGo.AddComponent<Button>();
                btn.targetGraphic = img;

                var labelGo = new GameObject("Text", typeof(RectTransform));
                var labelRt = (RectTransform)labelGo.transform;
                labelRt.SetParent(btnGo.transform, false);
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;
                var text = labelGo.AddComponent<Text>();
                text.font = HangarButtonHelpers.GetKoreanFont();
                text.fontSize = 11;
                text.color = UIColorPalette.OnSurfaceVariant;
                text.alignment = TextAnchor.MiddleCenter;
                text.text = label;

                var capturedCat = cat;
                btn.onClick.AddListener(() => SetActiveFilter(capturedCat));
                tabButtons.Add(btn);
            }

            ApplyTabHighlight();
        }

        void SetActiveFilter(PartCategory? category)
        {
            activeFilter = category;
            if (mutableState != null) mutableState.PartsSetFilterCategory(category);
            ApplyTabHighlight();
            RebuildCards();
        }

        void ApplyTabHighlight()
        {
            for (int i = 0; i < tabButtons.Count && i < TabSpec.Length; i++)
            {
                var btn = tabButtons[i];
                if (btn == null) continue;
                var bg = btn.GetComponent<Image>();
                var label = btn.transform.Find("Text")?.GetComponent<Text>();
                bool active = TabSpec[i].cat.Equals(activeFilter) ||
                              (TabSpec[i].cat == null && activeFilter == null);
                if (bg != null)
                    bg.color = active ? UIColorPalette.PrimaryContainer : UIColorPalette.SurfaceContainer;
                if (label != null)
                    label.color = active ? UIColorPalette.OnPrimaryContainer : UIColorPalette.OnSurfaceVariant;
            }
        }

        void RebuildCards()
        {
            foreach (var card in cards)
                if (card != null) Destroy(card);
            cards.Clear();

            if (convoy == null) return;

            var parts = CollectFiltered();
            foreach (var part in parts)
                cards.Add(CreateCard(part));
        }

        List<PartInstance> CollectFiltered()
        {
            var result = new List<PartInstance>();
            if (convoy == null) return result;

            if (activeFilter.HasValue)
            {
                foreach (var p in convoy.GetByCategory(activeFilter.Value))
                    if (p != null) result.Add(p);
            }
            else
            {
                foreach (PartCategory cat in System.Enum.GetValues(typeof(PartCategory)))
                    foreach (var p in convoy.GetByCategory(cat))
                        if (p != null) result.Add(p);
            }
            return result;
        }

        GameObject CreateCard(PartInstance part)
        {
            var card = new GameObject($"Part_{part.instanceId}", typeof(RectTransform));
            card.transform.SetParent(listContainer, false);

            var img = card.AddComponent<Image>();
            img.color = UIColorPalette.SurfaceContainerLow;

            var le = card.AddComponent<LayoutElement>();
            le.preferredHeight = 64;

            AddPartName(card.transform, part);
            AddPartMeta(card.transform, part);
            AddEquipButton(card.transform, part);
            return card;
        }

        void AddPartName(Transform parent, PartInstance part)
        {
            var go = new GameObject("Name", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(10, -8);
            rt.sizeDelta = new Vector2(-100, 18);

            var text = go.AddComponent<Text>();
            text.font = HangarButtonHelpers.GetKoreanFont();
            text.fontSize = 14;
            text.color = UIColorPalette.OnSurface;
            text.alignment = TextAnchor.MiddleLeft;
            text.text = part.data != null ? part.data.partName : "-";
        }

        void AddPartMeta(Transform parent, PartInstance part)
        {
            var go = new GameObject("Meta", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(10, 8);
            rt.sizeDelta = new Vector2(-100, 16);

            var text = go.AddComponent<Text>();
            text.font = HangarButtonHelpers.GetKoreanFont();
            text.fontSize = 11;
            text.color = UIColorPalette.OnSurfaceVariant;
            text.alignment = TextAnchor.MiddleLeft;

            float weight = part.data != null ? part.data.weight : 0f;
            float power = part.data != null ? part.data.powerDraw : 0f;
            int durabilityPct = Mathf.RoundToInt(part.durability * 100f);
            text.text = $"{CategoryLabel(part.Category)} · {weight:F1}t · {power:F0}kW · 내구 {durabilityPct}%";
        }

        void AddEquipButton(Transform parent, PartInstance part)
        {
            var go = new GameObject("EquipBtn", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(1, 0.5f);
            rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(1, 0.5f);
            rt.anchoredPosition = new Vector2(-8, 0);
            rt.sizeDelta = new Vector2(78, 40);

            var img = go.AddComponent<Image>();
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var labelGo = new GameObject("Text", typeof(RectTransform));
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.SetParent(go.transform, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var text = labelGo.AddComponent<Text>();
            text.font = HangarButtonHelpers.GetKoreanFont();
            text.fontSize = 12;
            text.alignment = TextAnchor.MiddleCenter;

            bool functional = part.IsFunctional;
            bool hasTank = state?.SelectedTank != null;
            if (!functional)
            {
                img.color = UIColorPalette.SurfaceContainerLowest;
                text.color = UIColorPalette.OnSurfaceVariant;
                text.text = "잠김";
                btn.interactable = false;
            }
            else if (!hasTank)
            {
                img.color = UIColorPalette.SurfaceContainer;
                text.color = UIColorPalette.OnSurfaceVariant;
                text.text = "장착";
                btn.interactable = false;
            }
            else
            {
                img.color = UIColorPalette.PrimaryContainer;
                text.color = UIColorPalette.OnPrimaryContainer;
                text.text = "장착";
                var capturedPart = part;
                btn.onClick.AddListener(() => TryEquip(capturedPart));
            }
        }

        void TryEquip(PartInstance part)
        {
            var tank = state?.SelectedTank;
            if (tank == null || part == null || convoy == null) return;

            int slotIndex = 0;
            if (part.Category == PartCategory.Armor)
                slotIndex = FindEmptySlot(tank.armor);
            else if (part.Category == PartCategory.Auxiliary)
                slotIndex = FindEmptySlot(tank.auxiliary);

            var result = convoy.EquipTo(tank, part.instanceId, part.Category, slotIndex);
            if (!result.isValid)
            {
                Debug.LogWarning($"[CRUX] [HANGAR] 장착 실패 — {string.Join(", ", result.violations)}");
                return;
            }

            bus.Publish(new PartEquippedEvent(tank, part, part.Category));
        }

        static int FindEmptySlot(List<PartInstance> slots)
        {
            if (slots == null) return 0;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i] == null) return i;
            return 0;
        }

        static string CategoryLabel(PartCategory cat) => cat switch
        {
            PartCategory.Engine => "엔진",
            PartCategory.Turret => "포탑",
            PartCategory.MainGun => "주포",
            PartCategory.AmmoRack => "탄약",
            PartCategory.Armor => "장갑",
            PartCategory.Track => "궤도",
            PartCategory.Auxiliary => "보조",
            _ => cat.ToString()
        };
    }
}
