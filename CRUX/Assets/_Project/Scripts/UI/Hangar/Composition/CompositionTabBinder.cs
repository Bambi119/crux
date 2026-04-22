using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Crux.Data;
using Crux.UI.Hangar.Parts;

namespace Crux.UI.Hangar.Composition
{
    // docs/10b §3.1 / 10c 편성 탭. ITabModule 구현체.
    // 책임: LEFT 차량 리스트 렌더 + CENTER/RIGHT 서브뷰 라이프사이클 + TankSelected 기록.
    // 쓰기 주체: HangarSharedState.CompSet* (10c Composition 오너십).
    public class CompositionTabBinder : MonoBehaviour, ITabModule
    {
        public HangarTab Tab => HangarTab.Composition;

        IHangarStateReadOnly state;
        HangarSharedState mutableState;
        IHangarBus bus;
        ConvoyInventory convoy;

        Transform leftPanel;
        Transform centerPanel;
        Transform rightPanel;

        LoadoutDetailBinder loadoutView;
        PartsInventoryBinder partsView;

        Transform listContainer;
        readonly List<GameObject> cards = new List<GameObject>();

        // HangarSceneBootstrap에서 씬 의존성 주입.
        public void WireScene(
            HangarSharedState mutableState,
            ConvoyInventory convoy,
            Transform leftPanel,
            Transform centerPanel,
            Transform rightPanel)
        {
            this.mutableState = mutableState;
            this.convoy = convoy;
            this.leftPanel = leftPanel;
            this.centerPanel = centerPanel;
            this.rightPanel = rightPanel;
        }

        // ITabModule
        public void Initialize(IHangarStateReadOnly state, IHangarBus bus)
        {
            this.state = state;
            this.bus = bus;

            bus.Subscribe<TankSelectedEvent>(OnTankSelected);
            bus.Subscribe<PartEquippedEvent>(OnLoadoutChanged);
            bus.Subscribe<PartUnequippedEvent>(OnLoadoutChanged);
        }

        public void OnEnter()
        {
            EnsureListContainer();
            EnsureSubViews();
            RebuildVehicleList();
            loadoutView?.Refresh(state.SelectedTank);
            partsView?.Refresh(state.SelectedTank);
            SetPanelsActive(true);
        }

        public void OnLeave()
        {
            SetPanelsActive(false);
        }

        public void Tick(float deltaTime) { }

        void OnDestroy()
        {
            if (bus != null)
            {
                bus.Unsubscribe<TankSelectedEvent>(OnTankSelected);
                bus.Unsubscribe<PartEquippedEvent>(OnLoadoutChanged);
                bus.Unsubscribe<PartUnequippedEvent>(OnLoadoutChanged);
            }
        }

        void SetPanelsActive(bool active)
        {
            if (leftPanel != null) leftPanel.gameObject.SetActive(active);
            if (centerPanel != null) centerPanel.gameObject.SetActive(active);
            if (rightPanel != null) rightPanel.gameObject.SetActive(active);
        }

        void EnsureSubViews()
        {
            if (loadoutView == null && centerPanel != null)
            {
                loadoutView = gameObject.AddComponent<LoadoutDetailBinder>();
                loadoutView.WireScene(mutableState, convoy, centerPanel);
                loadoutView.Initialize(state, bus);
            }
            if (partsView == null && rightPanel != null)
            {
                partsView = gameObject.AddComponent<PartsInventoryBinder>();
                partsView.WireScene(mutableState, convoy, rightPanel);
                partsView.Initialize(state, bus);
            }
        }

        void EnsureListContainer()
        {
            if (listContainer != null || leftPanel == null) return;
            listContainer = leftPanel.Find("VehicleList") ?? CreateListContainer(leftPanel);
        }

        static Transform CreateListContainer(Transform parent)
        {
            var go = new GameObject("VehicleList", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8;
            vlg.padding = new RectOffset(12, 12, 12, 56);
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;
            return go.transform;
        }

        void RebuildVehicleList()
        {
            foreach (var c in cards) if (c != null) Destroy(c);
            cards.Clear();

            if (convoy == null || listContainer == null) return;

            foreach (var tank in convoy.tanks)
                cards.Add(CreateCard(tank));
        }

        void OnTankSelected(TankSelectedEvent _) => ApplyListHighlight();

        void OnLoadoutChanged<T>(T _) => RebuildVehicleList();

        GameObject CreateCard(TankInstance tank)
        {
            var card = new GameObject($"Tank_{tank.instanceId}", typeof(RectTransform));
            card.transform.SetParent(listContainer, false);

            var img = card.AddComponent<Image>();
            img.color = UIColorPalette.SurfaceContainerLow;

            var le = card.AddComponent<LayoutElement>();
            le.preferredHeight = 88;

            var btn = card.AddComponent<Button>();
            btn.targetGraphic = img;
            var capturedTank = tank;
            btn.onClick.AddListener(() => SelectTank(capturedTank));

            AddAccentBar(card.transform, tank);
            AddTankName(card.transform, tank);
            AddHullBadge(card.transform, tank);
            AddIntegrityBar(card.transform, tank);
            if (tank.inSortie) AddSortieTag(card.transform);
            return card;
        }

        void SelectTank(TankInstance tank)
        {
            if (tank == null) return;
            mutableState.CompSetSelectedTank(tank);
            bus.Publish(new TankSelectedEvent(tank));
        }

        void ApplyListHighlight()
        {
            for (int i = 0; i < cards.Count && i < convoy.tanks.Count; i++)
            {
                var card = cards[i];
                if (card == null) continue;
                var bar = card.transform.Find("AccentBar")?.GetComponent<Image>();
                if (bar != null)
                    bar.color = convoy.tanks[i] == state.SelectedTank
                        ? UIColorPalette.PrimaryContainer
                        : UIColorPalette.OutlineVariant;
            }
        }

        void AddAccentBar(Transform parent, TankInstance tank)
        {
            var go = new GameObject("AccentBar", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            rt.sizeDelta = new Vector2(3, 0);
            var img = go.AddComponent<Image>();
            img.color = tank == state.SelectedTank ? UIColorPalette.PrimaryContainer : UIColorPalette.OutlineVariant;
        }

        void AddTankName(Transform parent, TankInstance tank)
        {
            var go = new GameObject("TankName", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(12, -10);
            rt.sizeDelta = new Vector2(-80, 20);
            var text = go.AddComponent<Text>();
            text.font = HangarButtonHelpers.GetKoreanFont();
            text.fontSize = 16;
            text.color = UIColorPalette.OnSurface;
            text.alignment = TextAnchor.MiddleLeft;
            text.text = tank.instanceId ?? "전차";
        }

        void AddHullBadge(Transform parent, TankInstance tank)
        {
            var go = new GameObject("HullBadge", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-12, -10);
            rt.sizeDelta = new Vector2(56, 20);

            var img = go.AddComponent<Image>();
            img.color = UIColorPalette.SurfaceContainerHigh;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.SetParent(go.transform, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var text = labelGo.AddComponent<Text>();
            text.font = HangarButtonHelpers.GetKoreanFont();
            text.fontSize = 11;
            text.color = UIColorPalette.OnSurfaceVariant;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = HullClassLabel(tank.hullClass);
        }

        static string HullClassLabel(HullClass hull) => hull switch
        {
            HullClass.Light => "경전차",
            HullClass.Medium => "중형",
            HullClass.Heavy => "중(重)",
            _ => hull.ToString()
        };

        void AddIntegrityBar(Transform parent, TankInstance tank)
        {
            var frame = new GameObject("IntegrityBar", typeof(RectTransform));
            var frameRt = (RectTransform)frame.transform;
            frameRt.SetParent(parent, false);
            frameRt.anchorMin = new Vector2(0, 0);
            frameRt.anchorMax = new Vector2(1, 0);
            frameRt.pivot = new Vector2(0, 0);
            frameRt.anchoredPosition = new Vector2(12, 26);
            frameRt.sizeDelta = new Vector2(-24, 6);
            var frameImg = frame.AddComponent<Image>();
            frameImg.color = UIColorPalette.SurfaceContainerLowest;

            float ratio = tank.MaxHP > 0 ? (float)tank.CurrentHP / tank.MaxHP : 0f;
            ratio = Mathf.Clamp01(ratio);
            Color fillColor = ratio >= 0.8f
                ? UIColorPalette.SecondaryContainer
                : (ratio >= 0.25f ? UIColorPalette.PrimaryContainer : UIColorPalette.TertiaryContainer);

            var fill = new GameObject("Fill", typeof(RectTransform));
            var fillRt = (RectTransform)fill.transform;
            fillRt.SetParent(frame.transform, false);
            fillRt.anchorMin = new Vector2(0, 0);
            fillRt.anchorMax = new Vector2(ratio, 1);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = fillColor;

            var labelGo = new GameObject("HPLabel", typeof(RectTransform));
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.SetParent(parent, false);
            labelRt.anchorMin = new Vector2(0, 0);
            labelRt.anchorMax = new Vector2(1, 0);
            labelRt.pivot = new Vector2(0, 0);
            labelRt.anchoredPosition = new Vector2(12, 8);
            labelRt.sizeDelta = new Vector2(-24, 16);
            var text = labelGo.AddComponent<Text>();
            text.font = HangarButtonHelpers.GetKoreanFont();
            text.fontSize = 11;
            text.color = UIColorPalette.OnSurfaceVariant;
            text.alignment = TextAnchor.MiddleLeft;
            text.text = $"내구 {tank.CurrentHP}/{tank.MaxHP}";
        }

        void AddSortieTag(Transform parent)
        {
            var go = new GameObject("SortieTag", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
            rt.anchoredPosition = new Vector2(-12, 8);
            rt.sizeDelta = new Vector2(48, 16);

            var img = go.AddComponent<Image>();
            img.color = UIColorPalette.SecondaryContainer;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.SetParent(go.transform, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var text = labelGo.AddComponent<Text>();
            text.font = HangarButtonHelpers.GetKoreanFont();
            text.fontSize = 10;
            text.color = UIColorPalette.OnSecondaryContainer;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = "출격";
        }
    }
}
