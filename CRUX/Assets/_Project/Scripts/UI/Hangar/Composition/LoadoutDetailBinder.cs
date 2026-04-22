using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Crux.Data;

namespace Crux.UI.Hangar.Composition
{
    // docs/10b §3.1 / 10c CENTER — 로드아웃 다이어그램 + 스탯 풋터.
    // 7개 슬롯: 주포 / 포탑 / 엔진 / 궤도(좌) / 궤도(우) / 보조1 / 보조2
    // 궤도 L/R은 같은 TankInstance.track 참조 공유. 장착된 슬롯 클릭 → 회수.
    public class LoadoutDetailBinder : MonoBehaviour
    {
        HangarSharedState mutableState;
        IHangarStateReadOnly state;
        IHangarBus bus;
        ConvoyInventory convoy;
        Transform panelRoot;

        Transform slotsContainer;
        Transform statsFooter;
        readonly List<GameObject> slotViews = new List<GameObject>();

        Text weightValueText;
        Text powerValueText;
        Text ratingValueText;

        enum SlotKey { MainGun, Turret, Engine, TrackL, TrackR, Aux1, Aux2 }

        static readonly (SlotKey key, string label, PartCategory category, int slotIndex)[] SlotSpec =
        {
            (SlotKey.MainGun, "주포",      PartCategory.MainGun,   0),
            (SlotKey.Turret,  "포탑",      PartCategory.Turret,    0),
            (SlotKey.Engine,  "엔진",      PartCategory.Engine,    0),
            (SlotKey.TrackL,  "궤도(좌)", PartCategory.Track,     0),
            (SlotKey.TrackR,  "궤도(우)", PartCategory.Track,     0),
            (SlotKey.Aux1,    "보조1",     PartCategory.Auxiliary, 0),
            (SlotKey.Aux2,    "보조2",     PartCategory.Auxiliary, 1),
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
            BuildSlots();

            bus.Subscribe<TankSelectedEvent>(OnTankSelected);
            bus.Subscribe<PartEquippedEvent>(OnLoadoutChanged);
            bus.Subscribe<PartUnequippedEvent>(OnLoadoutChanged);
        }

        public void Refresh(TankInstance tank)
        {
            for (int i = 0; i < SlotSpec.Length && i < slotViews.Count; i++)
                ApplySlotView(slotViews[i], SlotSpec[i], tank);
            UpdateStats(tank);
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

        void OnTankSelected(TankSelectedEvent evt) => Refresh(evt.Tank);
        void OnLoadoutChanged<T>(T _) => Refresh(state?.SelectedTank);

        void EnsureContainers()
        {
            if (panelRoot == null) return;
            slotsContainer = panelRoot.Find("LoadoutSlots") ?? CreateSlotsContainer(panelRoot);
            statsFooter = panelRoot.Find("StatsFooter") ?? CreateStatsFooter(panelRoot);
        }

        Transform CreateSlotsContainer(Transform parent)
        {
            var go = new GameObject("LoadoutSlots", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(16, 96);
            rt.offsetMax = new Vector2(-16, -16);

            var grid = go.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(160, 80);
            grid.spacing = new Vector2(12, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperCenter;
            return go.transform;
        }

        Transform CreateStatsFooter(Transform parent)
        {
            var go = new GameObject("StatsFooter", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = new Vector2(0, 12);
            rt.sizeDelta = new Vector2(-32, 72);

            var bg = go.AddComponent<Image>();
            bg.color = UIColorPalette.SurfaceContainer;

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12;
            hlg.padding = new RectOffset(16, 16, 10, 10);
            hlg.childControlWidth = true;
            hlg.childForceExpandWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandHeight = true;

            weightValueText = CreateStatCell(go.transform, "총 중량", "0.0 / 0 t");
            powerValueText = CreateStatCell(go.transform, "출력", "0 / 0 kW");
            ratingValueText = CreateStatCell(go.transform, "종합 등급", "-");

            return go.transform;
        }

        Text CreateStatCell(Transform parent, string label, string initialValue)
        {
            var cell = new GameObject($"Stat_{label}", typeof(RectTransform));
            cell.transform.SetParent(parent, false);
            var cellImg = cell.AddComponent<Image>();
            cellImg.color = UIColorPalette.SurfaceContainerLow;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.SetParent(cell.transform, false);
            labelRt.anchorMin = new Vector2(0, 1);
            labelRt.anchorMax = new Vector2(1, 1);
            labelRt.pivot = new Vector2(0.5f, 1);
            labelRt.anchoredPosition = new Vector2(0, -6);
            labelRt.sizeDelta = new Vector2(-12, 16);
            var labelText = labelGo.AddComponent<Text>();
            labelText.font = HangarButtonHelpers.GetKoreanFont();
            labelText.fontSize = 11;
            labelText.color = UIColorPalette.OnSurfaceVariant;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.text = label;

            var valueGo = new GameObject("Value", typeof(RectTransform));
            var valueRt = (RectTransform)valueGo.transform;
            valueRt.SetParent(cell.transform, false);
            valueRt.anchorMin = new Vector2(0, 0);
            valueRt.anchorMax = new Vector2(1, 0);
            valueRt.pivot = new Vector2(0.5f, 0);
            valueRt.anchoredPosition = new Vector2(0, 8);
            valueRt.sizeDelta = new Vector2(-12, 20);
            var valueText = valueGo.AddComponent<Text>();
            valueText.font = HangarButtonHelpers.GetKoreanFont();
            valueText.fontSize = 15;
            valueText.color = UIColorPalette.OnSurface;
            valueText.alignment = TextAnchor.MiddleCenter;
            valueText.text = initialValue;
            return valueText;
        }

        void BuildSlots()
        {
            foreach (var v in slotViews) if (v != null) Destroy(v);
            slotViews.Clear();
            if (slotsContainer == null) return;
            foreach (var spec in SlotSpec)
                slotViews.Add(CreateSlotView(spec.key, spec.label));
        }

        GameObject CreateSlotView(SlotKey key, string label)
        {
            var go = new GameObject($"Slot_{key}", typeof(RectTransform));
            go.transform.SetParent(slotsContainer, false);

            var img = go.AddComponent<Image>();
            img.color = UIColorPalette.SurfaceContainerLow;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var capturedKey = key;
            btn.onClick.AddListener(() => OnSlotClicked(capturedKey));

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.SetParent(go.transform, false);
            labelRt.anchorMin = new Vector2(0, 1);
            labelRt.anchorMax = new Vector2(1, 1);
            labelRt.pivot = new Vector2(0.5f, 1);
            labelRt.anchoredPosition = new Vector2(0, -6);
            labelRt.sizeDelta = new Vector2(-12, 14);
            var labelText = labelGo.AddComponent<Text>();
            labelText.font = HangarButtonHelpers.GetKoreanFont();
            labelText.fontSize = 10;
            labelText.color = UIColorPalette.OnSurfaceVariant;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.text = label;

            var partGo = new GameObject("PartName", typeof(RectTransform));
            var partRt = (RectTransform)partGo.transform;
            partRt.SetParent(go.transform, false);
            partRt.anchorMin = new Vector2(0, 0);
            partRt.anchorMax = new Vector2(1, 0);
            partRt.pivot = new Vector2(0.5f, 0);
            partRt.anchoredPosition = new Vector2(0, 10);
            partRt.sizeDelta = new Vector2(-12, 32);
            var partText = partGo.AddComponent<Text>();
            partText.font = HangarButtonHelpers.GetKoreanFont();
            partText.fontSize = 13;
            partText.color = UIColorPalette.OnSurface;
            partText.alignment = TextAnchor.MiddleCenter;
            partText.horizontalOverflow = HorizontalWrapMode.Wrap;
            partText.text = "-";
            return go;
        }

        void ApplySlotView(GameObject view, (SlotKey key, string label, PartCategory category, int slotIndex) spec, TankInstance tank)
        {
            if (view == null) return;

            var img = view.GetComponent<Image>();
            var btn = view.GetComponent<Button>();
            var partText = view.transform.Find("PartName")?.GetComponent<Text>();

            PartInstance equipped = null;
            bool supportsSlot = true;
            if (tank != null)
            {
                equipped = ResolveEquipped(tank, spec.key);
                supportsSlot = IsSlotSupported(tank, spec);
            }

            if (tank == null)
            {
                if (img != null) img.color = UIColorPalette.SurfaceContainerLowest;
                if (partText != null) { partText.text = "-"; partText.color = UIColorPalette.OnSurfaceVariant; }
                if (btn != null) btn.interactable = false;
            }
            else if (!supportsSlot)
            {
                if (img != null) img.color = UIColorPalette.SurfaceContainerLowest;
                if (partText != null) { partText.text = "지원 안 됨"; partText.color = UIColorPalette.OnSurfaceVariant; }
                if (btn != null) btn.interactable = false;
            }
            else if (equipped != null)
            {
                if (img != null) img.color = UIColorPalette.PrimaryContainer;
                if (partText != null)
                {
                    partText.text = equipped.data != null ? equipped.data.partName : "장착됨";
                    partText.color = UIColorPalette.OnPrimaryContainer;
                }
                if (btn != null) btn.interactable = true;
            }
            else
            {
                if (img != null) img.color = UIColorPalette.SurfaceContainerLow;
                if (partText != null) { partText.text = "비어있음"; partText.color = UIColorPalette.OnSurfaceVariant; }
                if (btn != null) btn.interactable = false;
            }
        }

        static PartInstance ResolveEquipped(TankInstance tank, SlotKey key) => key switch
        {
            SlotKey.MainGun => tank.mainGun,
            SlotKey.Turret => tank.turret,
            SlotKey.Engine => tank.engine,
            SlotKey.TrackL => tank.track,
            SlotKey.TrackR => tank.track,
            SlotKey.Aux1 => SafeList(tank.auxiliary, 0),
            SlotKey.Aux2 => SafeList(tank.auxiliary, 1),
            _ => null
        };

        static PartInstance SafeList(List<PartInstance> list, int idx)
        {
            if (list == null || idx < 0 || idx >= list.Count) return null;
            return list[idx];
        }

        static bool IsSlotSupported(TankInstance tank, (SlotKey key, string label, PartCategory category, int slotIndex) spec)
        {
            if (spec.category != PartCategory.Auxiliary) return true;
            return spec.slotIndex < tank.slotTable.auxiliary;
        }

        void OnSlotClicked(SlotKey key)
        {
            var tank = state?.SelectedTank;
            if (tank == null || convoy == null) return;

            var equipped = ResolveEquipped(tank, key);
            if (equipped == null) return;

            PartCategory category;
            int slotIndex;
            switch (key)
            {
                case SlotKey.MainGun: category = PartCategory.MainGun; slotIndex = 0; break;
                case SlotKey.Turret: category = PartCategory.Turret; slotIndex = 0; break;
                case SlotKey.Engine: category = PartCategory.Engine; slotIndex = 0; break;
                case SlotKey.TrackL:
                case SlotKey.TrackR: category = PartCategory.Track; slotIndex = 0; break;
                case SlotKey.Aux1: category = PartCategory.Auxiliary; slotIndex = 0; break;
                case SlotKey.Aux2: category = PartCategory.Auxiliary; slotIndex = 1; break;
                default: return;
            }

            var removed = convoy.ReturnFrom(tank, category, slotIndex);
            if (removed == null) return;

            bus.Publish(new PartUnequippedEvent(tank, removed, category));
        }

        void UpdateStats(TankInstance tank)
        {
            if (tank == null)
            {
                SetStats("0.0 / 0 t", "0 / 0 kW", "-", UIColorPalette.OnSurface, UIColorPalette.OnSurface);
                return;
            }

            float weight = tank.TotalWeight;
            int weightCap = tank.WeightCapacity;
            float powerSupply = tank.TotalPowerSupply;
            float powerDemand = tank.TotalPowerDemand;

            Color weightColor = weight > weightCap ? UIColorPalette.TertiaryContainer : UIColorPalette.OnSurface;
            Color powerColor = powerSupply < powerDemand ? UIColorPalette.TertiaryContainer : UIColorPalette.OnSurface;

            string rating = ComputeRating(tank, weight, weightCap, powerSupply, powerDemand);
            SetStats(
                $"{weight:F1} / {weightCap} t",
                $"{powerSupply:F0} / {powerDemand:F0} kW",
                rating,
                weightColor,
                powerColor);
        }

        void SetStats(string weight, string power, string rating, Color weightColor, Color powerColor)
        {
            if (weightValueText != null) { weightValueText.text = weight; weightValueText.color = weightColor; }
            if (powerValueText != null) { powerValueText.text = power; powerValueText.color = powerColor; }
            if (ratingValueText != null) ratingValueText.text = rating;
        }

        static string ComputeRating(TankInstance tank, float weight, int weightCap, float powerSupply, float powerDemand)
        {
            var validation = tank.Validate();
            if (!validation.isValid) return "편성 미완";
            if (weight > weightCap) return "과적재";
            if (powerSupply < powerDemand) return "출력 부족";

            float weightRatio = weightCap > 0 ? weight / weightCap : 0f;
            float powerRatio = powerDemand > 0 ? powerSupply / powerDemand : 1f;
            if (weightRatio <= 0.75f && powerRatio >= 1.25f) return "우수";
            if (weightRatio <= 0.9f && powerRatio >= 1.1f) return "양호";
            return "보통";
        }
    }
}
