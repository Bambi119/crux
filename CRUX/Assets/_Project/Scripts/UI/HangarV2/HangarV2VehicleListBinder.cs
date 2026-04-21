using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Crux.Data;

namespace Crux.UI
{
    /// <summary>
    /// LEFT 패널 — 차량 목록 바인더.
    /// ConvoyInventory.tanks 순회 → 런타임 카드 생성.
    /// HullClass 배지·HP 게이지·선택 하이라이트·출격 인디케이터.
    /// </summary>
    public class HangarV2VehicleListBinder : MonoBehaviour
    {
        private HangarV2Bootstrap bootstrap;
        private Transform panelRoot;
        private Transform listContainer;

        private readonly List<GameObject> cards = new();
        private TankInstance lastSelected;

        public void Initialize(HangarV2Bootstrap bootstrap, Transform panelRoot)
        {
            this.bootstrap = bootstrap;
            this.panelRoot = panelRoot;

            listContainer = panelRoot.Find("VehicleList");
            if (listContainer == null)
            {
                listContainer = CreateListContainer(panelRoot);
            }

            Rebuild();
            bootstrap.SelectedTankChanged += OnSelectedTankChanged;
        }

        private void OnDestroy()
        {
            if (bootstrap != null)
                bootstrap.SelectedTankChanged -= OnSelectedTankChanged;
        }

        private Transform CreateListContainer(Transform parent)
        {
            var go = new GameObject("VehicleList", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(12, 12);
            rt.offsetMax = new Vector2(-12, -56);

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;
            return go.transform;
        }

        public void Rebuild()
        {
            foreach (var card in cards)
                if (card != null) Destroy(card);
            cards.Clear();

            if (bootstrap?.Convoy == null) return;

            foreach (var tank in bootstrap.Convoy.tanks)
            {
                if (tank == null) continue;
                cards.Add(CreateCard(tank));
            }

            ApplyHighlight(bootstrap.SelectedTank);
        }

        private GameObject CreateCard(TankInstance tank)
        {
            var card = new GameObject($"Card_{tank.tankName}", typeof(RectTransform));
            card.transform.SetParent(listContainer, false);

            var img = card.AddComponent<Image>();
            img.color = UIColorPalette.SurfaceContainerLow;

            var le = card.AddComponent<LayoutElement>();
            le.preferredHeight = 88;

            var btn = card.AddComponent<Button>();
            btn.targetGraphic = img;
            var capturedTank = tank;
            btn.onClick.AddListener(() => bootstrap.SelectTank(capturedTank));

            AddAccentBar(card.transform, tank == bootstrap.SelectedTank);
            AddTankNameLabel(card.transform, tank);
            AddHullBadge(card.transform, tank);
            AddIntegrityBar(card.transform, tank);
            AddSortieTag(card.transform, tank);

            return card;
        }

        private void AddAccentBar(Transform parent, bool active)
        {
            var go = new GameObject("Accent", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(3, 0);
            rt.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = active ? UIColorPalette.PrimaryContainer : UIColorPalette.OutlineVariant;
        }

        private void AddTankNameLabel(Transform parent, TankInstance tank)
        {
            var go = new GameObject("Name", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(12, -10);
            rt.sizeDelta = new Vector2(-60, 22);

            var text = go.AddComponent<Text>();
            text.font = HangarButtonHelpers.GetKoreanFont();
            text.fontSize = 16;
            text.color = UIColorPalette.OnSurface;
            text.alignment = TextAnchor.MiddleLeft;
            text.text = tank.tankName;
        }

        private void AddHullBadge(Transform parent, TankInstance tank)
        {
            var go = new GameObject("HullBadge", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-8, -10);
            rt.sizeDelta = new Vector2(56, 20);

            var img = go.AddComponent<Image>();
            img.color = UIColorPalette.SurfaceContainerHigh;

            var labelGo = new GameObject("Text", typeof(RectTransform));
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

        private void AddIntegrityBar(Transform parent, TankInstance tank)
        {
            float ratio = tank.MaxHP > 0 ? (float)tank.CurrentHP / tank.MaxHP : 0f;
            ratio = Mathf.Clamp01(ratio);

            var frame = new GameObject("IntegrityFrame", typeof(RectTransform));
            var frameRt = (RectTransform)frame.transform;
            frameRt.SetParent(parent, false);
            frameRt.anchorMin = new Vector2(0, 0);
            frameRt.anchorMax = new Vector2(1, 0);
            frameRt.pivot = new Vector2(0, 0);
            frameRt.anchoredPosition = new Vector2(12, 28);
            frameRt.sizeDelta = new Vector2(-24, 6);
            var frameImg = frame.AddComponent<Image>();
            frameImg.color = UIColorPalette.SurfaceContainerLowest;

            var fill = new GameObject("Fill", typeof(RectTransform));
            var fillRt = (RectTransform)fill.transform;
            fillRt.SetParent(frame.transform, false);
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(ratio, 1);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = IntegrityColor(ratio);

            var labelGo = new GameObject("HPLabel", typeof(RectTransform));
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.SetParent(parent, false);
            labelRt.anchorMin = new Vector2(0, 0);
            labelRt.anchorMax = new Vector2(0, 0);
            labelRt.pivot = new Vector2(0, 0);
            labelRt.anchoredPosition = new Vector2(12, 8);
            labelRt.sizeDelta = new Vector2(200, 16);
            var label = labelGo.AddComponent<Text>();
            label.font = HangarButtonHelpers.GetKoreanFont();
            label.fontSize = 11;
            label.color = UIColorPalette.OnSurfaceVariant;
            label.alignment = TextAnchor.MiddleLeft;
            label.text = $"내구 {tank.CurrentHP}/{tank.MaxHP}";
        }

        private void AddSortieTag(Transform parent, TankInstance tank)
        {
            if (!tank.inSortie) return;

            var go = new GameObject("SortieTag", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
            rt.anchoredPosition = new Vector2(-8, 8);
            rt.sizeDelta = new Vector2(48, 16);

            var img = go.AddComponent<Image>();
            img.color = UIColorPalette.SecondaryContainer;

            var labelGo = new GameObject("Text", typeof(RectTransform));
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

        private void OnSelectedTankChanged(TankInstance tank)
        {
            lastSelected = tank;
            ApplyHighlight(tank);
        }

        private void ApplyHighlight(TankInstance tank)
        {
            if (bootstrap?.Convoy == null) return;
            for (int i = 0; i < cards.Count && i < bootstrap.Convoy.tanks.Count; i++)
            {
                var card = cards[i];
                if (card == null) continue;
                bool active = bootstrap.Convoy.tanks[i] == tank;
                var accent = card.transform.Find("Accent")?.GetComponent<Image>();
                if (accent != null)
                    accent.color = active ? UIColorPalette.PrimaryContainer : UIColorPalette.OutlineVariant;
                var bg = card.GetComponent<Image>();
                if (bg != null)
                    bg.color = active ? UIColorPalette.SurfaceContainerHigh : UIColorPalette.SurfaceContainerLow;
            }
        }

        private static string HullClassLabel(HullClass cls) => cls switch
        {
            HullClass.Scout => "경정찰",
            HullClass.Assault => "돌격",
            HullClass.Support => "지원",
            HullClass.Heavy => "중장",
            HullClass.Siege => "공성",
            _ => cls.ToString()
        };

        private static Color IntegrityColor(float ratio)
        {
            if (ratio >= 0.8f) return UIColorPalette.SecondaryContainer;
            if (ratio >= 0.25f) return UIColorPalette.PrimaryContainer;
            return UIColorPalette.TertiaryContainer;
        }
    }
}
