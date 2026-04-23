using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Crux.Data;

namespace Crux.UI.Hangar.Maintenance
{
    // docs/10b §3.2 / 03b §6.2 — 정비 탭 모듈. ITabModule 구현체.
    // CENTER 3단 스택: (1) 각성 대기 큐  (2) 전차 정비 현황  (3) 승무원 회복.
    // 쓰기 주체: HangarSharedState.MaintSet* (10c 오너십).
    public class MaintenanceTabBinder : MonoBehaviour, ITabModule
    {
        public HangarTab Tab => HangarTab.Maintenance;

        IHangarStateReadOnly state;
        HangarSharedState mutableState;
        IHangarBus bus;
        ConvoyInventory convoy;

        Transform leftPanel;
        Transform centerPanel;
        Transform rightPanel;

        Transform centerRoot;
        GameObject awakeningSection;
        GameObject tankSection;
        GameObject crewSection;

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

        public void Initialize(IHangarStateReadOnly state, IHangarBus bus)
        {
            this.state = state;
            this.bus = bus;

            bus.Subscribe<AwakeningQueueChangedEvent>(OnAwakeningQueueChanged);
            bus.Subscribe<PartEquippedEvent>(OnLoadoutChanged);
            bus.Subscribe<PartUnequippedEvent>(OnLoadoutChanged);
        }

        public void OnEnter()
        {
            EnsureScaffolds();
            Rebuild();
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
                bus.Unsubscribe<AwakeningQueueChangedEvent>(OnAwakeningQueueChanged);
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

        void OnAwakeningQueueChanged(AwakeningQueueChangedEvent _) => RebuildAwakeningSection();

        void OnLoadoutChanged<T>(T _) => RebuildTankSection();

        // ---- 스캐폴드 ----

        void EnsureScaffolds()
        {
            EnsureLeftHeader();
            EnsureCenterStack();
            EnsureRightPlaceholder();
        }

        void EnsureLeftHeader()
        {
            if (leftPanel == null || leftPanel.Find("MaintHeader") != null) return;

            var header = new GameObject("MaintHeader", typeof(RectTransform));
            var rt = (RectTransform)header.transform;
            rt.SetParent(leftPanel, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -12);
            rt.sizeDelta = new Vector2(-24, 32);

            var text = header.AddComponent<Text>();
            text.font = HangarButtonHelpers.GetKoreanFont();
            text.fontSize = 18;
            text.color = UIColorPalette.OnSurface;
            text.alignment = TextAnchor.MiddleLeft;
            text.text = "정비";
        }

        void EnsureCenterStack()
        {
            if (centerPanel == null) return;
            centerRoot = centerPanel.Find("MaintStack") ?? CreateCenterStack(centerPanel);
        }

        static Transform CreateCenterStack(Transform parent)
        {
            var go = new GameObject("MaintStack", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 16;
            vlg.padding = new RectOffset(20, 20, 20, 40);
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;
            return go.transform;
        }

        void EnsureRightPlaceholder()
        {
            if (rightPanel == null || rightPanel.Find("MaintDetailHint") != null) return;

            var hint = new GameObject("MaintDetailHint", typeof(RectTransform));
            var rt = (RectTransform)hint.transform;
            rt.SetParent(rightPanel, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(20, 20);
            rt.offsetMax = new Vector2(-20, -20);

            var text = hint.AddComponent<Text>();
            text.font = HangarButtonHelpers.GetKoreanFont();
            text.fontSize = 12;
            text.color = UIColorPalette.OnSurfaceVariant;
            text.alignment = TextAnchor.UpperLeft;
            text.text = "정비 상세 — 섹션을 선택하면 여기에 액션이 표시됩니다.";
        }

        // ---- 본체 빌드 ----

        void Rebuild()
        {
            RebuildAwakeningSection();
            RebuildTankSection();
            RebuildCrewSection();
        }

        void RebuildAwakeningSection()
        {
            if (centerRoot == null) return;
            int count = state != null ? state.AwakeningQueueCount : 0;

            if (awakeningSection != null) Destroy(awakeningSection);

            if (count <= 0)
            {
                awakeningSection = null;
                return;
            }

            awakeningSection = BuildSectionFrame("AwakeningSection", "각성 대기", count.ToString() + "건");
            awakeningSection.transform.SetSiblingIndex(0);

            var body = AddSectionBody(awakeningSection.transform);
            AddBodyLine(body, $"각성 대기열에 {count}건이 있습니다. 출격 확정 시 순차 각성됩니다.");
        }

        void RebuildTankSection()
        {
            if (centerRoot == null) return;
            if (tankSection != null) Destroy(tankSection);

            tankSection = BuildSectionFrame("TankSection", "전차 정비 현황", null);
            tankSection.transform.SetSiblingIndex(awakeningSection != null ? 1 : 0);

            var body = AddSectionBody(tankSection.transform);

            if (convoy == null || convoy.tanks == null || convoy.tanks.Count == 0)
            {
                AddBodyLine(body, "정비 대상 전차가 없습니다.");
                return;
            }

            int brokenTotal = 0;
            foreach (var tank in convoy.tanks)
            {
                var broken = CollectBrokenParts(tank);
                if (broken.Count == 0)
                {
                    AddBodyLine(body, $"{SafeName(tank)} — 이상 없음");
                    continue;
                }
                brokenTotal += broken.Count;
                AddBodyLine(body, $"{SafeName(tank)} — 점검 {broken.Count}건");
                foreach (var p in broken)
                {
                    float pct = Mathf.Clamp01(p.durability) * 100f;
                    string label = p.data != null ? p.data.partName : p.Category.ToString();
                    AddBodyLine(body, $"  · {label} ({p.Category}) — {pct:0}% {DurabilityTag(p.durability)}");
                }
            }

            if (brokenTotal == 0)
                AddBodyLine(body, "모든 파츠가 정상 작동 범위입니다.");
        }

        void RebuildCrewSection()
        {
            if (centerRoot == null) return;
            if (crewSection != null) Destroy(crewSection);

            crewSection = BuildSectionFrame("CrewSection", "승무원 회복", null);
            int idx = 0;
            if (awakeningSection != null) idx++;
            if (tankSection != null) idx++;
            crewSection.transform.SetSiblingIndex(idx);

            var body = AddSectionBody(crewSection.transform);

            var injured = new List<CrewMemberRuntime>();
            if (convoy != null)
            {
                foreach (var c in convoy.availableCrew)
                    if (c != null && c.injuryState != InjuryLevel.None) injured.Add(c);

                foreach (var tank in convoy.tanks)
                {
                    if (tank == null || tank.crew == null) continue;
                    foreach (var slot in tank.crew.All())
                    {
                        var c = slot.crew;
                        if (c != null && c.injuryState != InjuryLevel.None) injured.Add(c);
                    }
                }
            }

            if (injured.Count == 0)
            {
                AddBodyLine(body, "회복이 필요한 승무원이 없습니다.");
                return;
            }

            foreach (var c in injured)
                AddBodyLine(body, $"· {c.DisplayName} ({c.Class}) — {InjuryLabel(c.injuryState)}");
        }

        // ---- 헬퍼 ----

        static string SafeName(TankInstance tank)
            => tank != null && !string.IsNullOrEmpty(tank.tankName) ? tank.tankName : "전차";

        static string DurabilityTag(float durability)
        {
            if (durability < 0.10f) return "[장착 불가]";
            if (durability < 0.40f) return "[파손]";
            if (durability < 0.70f) return "[손상]";
            return string.Empty;
        }

        static string InjuryLabel(InjuryLevel level) => level switch
        {
            InjuryLevel.None => "정상",
            InjuryLevel.Minor => "경상",
            InjuryLevel.Severe => "중상",
            InjuryLevel.Fatal => "치명",
            _ => level.ToString()
        };

        static List<PartInstance> CollectBrokenParts(TankInstance tank)
        {
            var list = new List<PartInstance>();
            if (tank == null) return list;
            foreach (var part in tank.AllEquipped())
                if (part != null && part.durability < 0.70f) list.Add(part);
            return list;
        }

        GameObject BuildSectionFrame(string name, string title, string badgeText)
        {
            var frame = new GameObject(name, typeof(RectTransform));
            frame.transform.SetParent(centerRoot, false);

            var img = frame.AddComponent<Image>();
            img.color = UIColorPalette.SurfaceContainer;

            var le = frame.AddComponent<LayoutElement>();
            le.minHeight = 72;

            var vlg = frame.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.padding = new RectOffset(16, 16, 12, 14);
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;

            AddSectionHeader(frame.transform, title, badgeText);
            return frame;
        }

        static void AddSectionHeader(Transform parent, string title, string badgeText)
        {
            var row = new GameObject("Header", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rowLe = row.AddComponent<LayoutElement>();
            rowLe.minHeight = 20;
            var rowHlg = row.AddComponent<HorizontalLayoutGroup>();
            rowHlg.spacing = 8;
            rowHlg.childControlWidth = true;
            rowHlg.childControlHeight = true;
            rowHlg.childForceExpandWidth = true;
            rowHlg.childForceExpandHeight = false;

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(row.transform, false);
            var titleText = titleGo.AddComponent<Text>();
            titleText.font = HangarButtonHelpers.GetKoreanFont();
            titleText.fontSize = 14;
            titleText.color = UIColorPalette.OnSurface;
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.text = title;

            if (!string.IsNullOrEmpty(badgeText))
            {
                var badgeGo = new GameObject("Badge", typeof(RectTransform));
                badgeGo.transform.SetParent(row.transform, false);
                var badgeLe = badgeGo.AddComponent<LayoutElement>();
                badgeLe.preferredWidth = 64;
                var badgeImg = badgeGo.AddComponent<Image>();
                badgeImg.color = UIColorPalette.PrimaryContainer;

                var labelGo = new GameObject("Label", typeof(RectTransform));
                var labelRt = (RectTransform)labelGo.transform;
                labelRt.SetParent(badgeGo.transform, false);
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;
                var labelText = labelGo.AddComponent<Text>();
                labelText.font = HangarButtonHelpers.GetKoreanFont();
                labelText.fontSize = 11;
                labelText.color = UIColorPalette.OnPrimaryContainer;
                labelText.alignment = TextAnchor.MiddleCenter;
                labelText.text = badgeText;
            }
        }

        static Transform AddSectionBody(Transform parent)
        {
            var body = new GameObject("Body", typeof(RectTransform));
            body.transform.SetParent(parent, false);
            var vlg = body.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2;
            vlg.padding = new RectOffset(0, 0, 4, 0);
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;
            return body.transform;
        }

        static void AddBodyLine(Transform parent, string text)
        {
            var go = new GameObject("Line", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 18;
            var label = go.AddComponent<Text>();
            label.font = HangarButtonHelpers.GetKoreanFont();
            label.fontSize = 12;
            label.color = UIColorPalette.OnSurfaceVariant;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.text = text;
        }
    }
}
