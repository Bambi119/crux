using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Crux.Data;

namespace Crux.UI
{
    /// <summary>
    /// Hangar 오버레이(파츠 인벤토리 / 크루 풀 팝업) GameObject 조립 전담.
    /// HangarUI가 활성 오버레이 참조를 관리하고, 이 클래스는 GameObject 생성과 레이아웃을 담당.
    /// </summary>
    public class HangarOverlayBuilder
    {
        private readonly HangarUI owner;
        private readonly ConvoyInventory convoy;

        public HangarOverlayBuilder(HangarUI owner, ConvoyInventory convoy)
        {
            this.owner = owner;
            this.convoy = convoy;
        }

        public GameObject BuildPartsOverlay(TankInstance tank)
        {
            // 배경 panel (반투명 검정 전체 스크린)
            var root = new GameObject("PartsInventoryOverlay");
            root.AddComponent<RectTransform>();
            var bgImg = root.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.75f);

            // 내부 패널 (가운데 640x600 박스)
            var panel = new GameObject("Panel");
            panel.transform.SetParent(root.transform, false);
            var panelRt = panel.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(640f, 600f);
            panelRt.anchoredPosition = Vector2.zero;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.12f, 0.14f, 1f);

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 16, 16);
            vlg.spacing = 8;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // 제목
            AddText(panel.transform, "TitleText", $"파츠 인벤토리 · {tank.tankName}", 20, new Color(1f, 1f, 1f), 32);

            // 5개 슬롯 라벨 + 장착 파츠
            AddSlotRow(panel.transform, "주포", tank.mainGun);
            AddSlotRow(panel.transform, "터렛", tank.turret);
            AddSlotRow(panel.transform, "엔진", tank.engine);
            AddSlotRow(panel.transform, "탄약고", tank.ammoRack);
            AddSlotRow(panel.transform, "궤도", tank.track);

            // "보유 파츠 (여분)" 섹션 라벨
            AddText(panel.transform, "SpareHeaderText", "보유 파츠 (여분)", 18, new Color(0.95f, 0.85f, 0.55f), 28);

            // 카테고리별 여분 파츠 표시
            foreach (var cat in new[] {
                PartCategory.Engine,
                PartCategory.Turret,
                PartCategory.MainGun,
                PartCategory.AmmoRack,
                PartCategory.Track,
            })
            {
                var parts = convoy.GetByCategory(cat);
                foreach (var p in parts)
                {
                    AddSparePartRow(panel.transform, p, tank);
                }
            }

            // 닫기 버튼 (하단)
            AddCloseButton(panel.transform);

            return root;
        }

        public GameObject BuildCrewPoolPopup(TankInstance tank, CrewClass klass)
        {
            // 반투명 풀스크린 배경
            var root = new GameObject("CrewPoolPopup");
            root.AddComponent<RectTransform>();
            var bgImg = root.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.75f);

            // 중앙 패널 420 x 400
            var panel = new GameObject("Panel");
            panel.transform.SetParent(root.transform, false);
            var panelRt = panel.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(420f, 400f);
            panelRt.anchoredPosition = Vector2.zero;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.12f, 0.14f, 1f);

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 16, 16);
            vlg.spacing = 6;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;

            // 제목
            AddText(panel.transform, "TitleText", $"{klass} 후보", 20, Color.white, 32);

            // 풀 전체 순회 — 직책 일치: 활성 / 불일치: 회색 disabled
            if (convoy.availableCrew.Count == 0)
            {
                AddText(panel.transform, "EmptyText", "풀에 승무원 없음", 14, new Color(0.6f, 0.6f, 0.6f), 26);
            }
            else
            {
                // 일치 후보 우선 표시
                foreach (var c in convoy.availableCrew)
                    if (c != null && c.Class == klass)
                        AddCrewCandidateRow(panel.transform, tank, klass, c, enabled: true);

                // 불일치 후보 구분선 + disabled
                var mismatched = convoy.availableCrew.FindAll(c => c != null && c.Class != klass);
                if (mismatched.Count > 0)
                {
                    AddText(panel.transform, "MismatchHeader", "— 다른 직책 (할당 불가) —", 12, new Color(0.5f, 0.5f, 0.5f), 22);
                    foreach (var c in mismatched)
                        AddCrewCandidateRow(panel.transform, tank, klass, c, enabled: false);
                }
            }

            // 닫기 버튼 (하단)
            AddCloseButton(panel.transform);

            return root;
        }

        private void AddText(Transform parent, string name, string text, int fontSize, Color color, float preferredHeight)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>();
            var t = obj.AddComponent<Text>();
            t.font = HangarButtonHelpers.GetKoreanFont();
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TextAnchor.MiddleLeft;
            t.text = text;
            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
        }

        private void AddSlotRow(Transform parent, string label, PartInstance part)
        {
            string value = (part != null && part.data != null) ? part.data.partName : "(비어있음)";
            Color color = (part != null) ? new Color(0.85f, 0.9f, 0.85f) : new Color(0.6f, 0.6f, 0.6f);
            AddText(parent, $"Slot_{label}", $"{label}: {value}", 16, color, 24);
        }

        private void AddSparePartRow(Transform parent, PartInstance part, TankInstance tank)
        {
            if (part == null || part.data == null) return;

            bool compatible = PredictSwap(tank, part);

            // 수평 배치 row
            var rowObj = new GameObject($"Spare_{part.data.partName}");
            rowObj.transform.SetParent(parent, false);
            rowObj.AddComponent<RectTransform>();
            var hlg = rowObj.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            var rowLe = rowObj.AddComponent<LayoutElement>();
            rowLe.preferredHeight = 28;

            // 왼쪽: 파츠 이름 + 카테고리 + 호환 ✓/✗
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(rowObj.transform, false);
            labelObj.AddComponent<RectTransform>();
            var labelText = labelObj.AddComponent<Text>();
            labelText.font = HangarButtonHelpers.GetKoreanFont();
            labelText.fontSize = 14;
            labelText.color = compatible
                ? new Color(0.85f, 0.9f, 0.85f)
                : new Color(0.7f, 0.45f, 0.45f);
            labelText.alignment = TextAnchor.MiddleLeft;
            string mark = compatible ? "✓" : "✗";
            labelText.text = $"{mark} {part.data.partName}  ({part.data.category})";
            var labelLe = labelObj.AddComponent<LayoutElement>();
            labelLe.flexibleWidth = 1;

            // 오른쪽: [교체] 버튼
            var btnObj = new GameObject("SwapButton");
            btnObj.transform.SetParent(rowObj.transform, false);
            btnObj.AddComponent<RectTransform>();
            var btnImg = btnObj.AddComponent<Image>();
            btnImg.color = compatible
                ? new Color(0.3f, 0.35f, 0.42f, 1f)
                : new Color(0.22f, 0.22f, 0.24f, 1f);
            var btn = btnObj.AddComponent<Button>();
            btn.interactable = compatible;
            var btnLe = btnObj.AddComponent<LayoutElement>();
            btnLe.preferredWidth = 60;

            // Button label
            var btnLabelObj = new GameObject("Text");
            btnLabelObj.transform.SetParent(btnObj.transform, false);
            var btnLabelRt = btnLabelObj.AddComponent<RectTransform>();
            btnLabelRt.anchorMin = Vector2.zero;
            btnLabelRt.anchorMax = Vector2.one;
            btnLabelRt.offsetMin = Vector2.zero;
            btnLabelRt.offsetMax = Vector2.zero;
            var btnLabelText = btnLabelObj.AddComponent<Text>();
            btnLabelText.font = HangarButtonHelpers.GetKoreanFont();
            btnLabelText.fontSize = 14;
            btnLabelText.alignment = TextAnchor.MiddleCenter;
            btnLabelText.color = Color.white;
            btnLabelText.text = "교체";

            if (compatible)
            {
                var captured = part;
                btn.onClick.AddListener(() => SwapPart(tank, captured));
            }
        }

        /// <summary>
        /// 파츠 교체 후 호환성만 예측 (상태 변경 없음).
        /// 기존 같은 카테고리 파츠를 제외한 equipped 목록에 newPart를 더해
        /// CompatibilityChecker.CheckAll 호출.
        /// </summary>
        private bool PredictSwap(TankInstance tank, PartInstance newPart)
        {
            if (tank == null || newPart?.data == null) return false;

            var cat = newPart.Category;
            var equipped = new List<PartDataSO>();

            if (tank.engine != null && tank.engine.Category != cat) equipped.Add(tank.engine.data);
            if (tank.turret != null && tank.turret.Category != cat) equipped.Add(tank.turret.data);
            if (tank.mainGun != null && tank.mainGun.Category != cat) equipped.Add(tank.mainGun.data);
            if (tank.ammoRack != null && tank.ammoRack.Category != cat) equipped.Add(tank.ammoRack.data);
            if (tank.track != null && tank.track.Category != cat) equipped.Add(tank.track.data);
            if (tank.armor != null)
                foreach (var a in tank.armor)
                    if (a != null && a.Category != cat) equipped.Add(a.data);
            if (tank.auxiliary != null)
                foreach (var x in tank.auxiliary)
                    if (x != null && x.Category != cat) equipped.Add(x.data);
            equipped.Add(newPart.data);

            var turret = (cat == PartCategory.Turret)
                ? newPart.data as TurretPartSO
                : tank.turret?.data as TurretPartSO;
            var mainGun = (cat == PartCategory.MainGun)
                ? newPart.data as MainGunPartSO
                : tank.mainGun?.data as MainGunPartSO;
            var ammoRack = (cat == PartCategory.AmmoRack)
                ? newPart.data as AmmoRackPartSO
                : tank.ammoRack?.data as AmmoRackPartSO;

            var result = CompatibilityChecker.CheckAll(tank.hullClass, turret, mainGun, ammoRack, equipped);
            return result.isValid;
        }

        private void AddCrewCandidateRow(Transform parent, TankInstance tank, CrewClass klass, CrewMemberRuntime crew, bool enabled = true)
        {
            var row = new GameObject($"Candidate_{crew.DisplayName}");
            row.transform.SetParent(parent, false);
            row.AddComponent<RectTransform>();
            var img = row.AddComponent<Image>();
            img.color = enabled
                ? new Color(0.2f, 0.22f, 0.26f, 1f)
                : new Color(0.14f, 0.14f, 0.16f, 1f);
            var btn = row.AddComponent<Button>();
            btn.interactable = enabled;
            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 32;

            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(row.transform, false);
            var labelRt = labelObj.AddComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(8, 0);
            labelRt.offsetMax = Vector2.zero;
            var text = labelObj.AddComponent<Text>();
            text.font = HangarButtonHelpers.GetKoreanFont();
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = enabled
                ? new Color(0.9f, 0.9f, 0.9f)
                : new Color(0.45f, 0.45f, 0.45f);
            text.text = enabled
                ? $"{crew.DisplayName}  (Aim {crew.BaseAim} · React {crew.BaseReact} · Tech {crew.BaseTech})"
                : $"{crew.DisplayName}  [{crew.Class}]";

            if (!enabled) return;  // 불일치 후보는 리스너 연결하지 않음

            var captured = crew;
            btn.onClick.AddListener(() => {
                convoy.AssignCrewTo(tank, klass, captured.data.id);
                owner.CloseOverlay();
                owner.NotifyUnitSelected(tank);
            });
        }

        private void AddCloseButton(Transform parent)
        {
            // 닫기 버튼을 VLG 바깥 우상단 절대 위치에 배치
            // (파츠 많을 때 VLG 하단 클리핑 방지)
            var closeObj = new GameObject("CloseButton");
            closeObj.transform.SetParent(parent, false);
            var closeRt = closeObj.AddComponent<RectTransform>();

            // 우상단 고정 (부모 Panel의 우상단에서 8px 안쪽)
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-8f, -8f);
            closeRt.sizeDelta = new Vector2(28f, 28f);

            var closeImg = closeObj.AddComponent<Image>();
            closeImg.color = new Color(0.45f, 0.2f, 0.2f, 1f);
            var closeBtn = closeObj.AddComponent<Button>();
            closeBtn.onClick.AddListener(() => owner.CloseOverlay());

            // LayoutGroup에 영향받지 않도록 설정
            var le = closeObj.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            var labelObj = new GameObject("Text");
            labelObj.transform.SetParent(closeObj.transform, false);
            var labelRt = labelObj.AddComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var text = labelObj.AddComponent<Text>();
            text.font = HangarButtonHelpers.GetKoreanFont();
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = "✕";
        }

        private void SwapPart(TankInstance tank, PartInstance newPart)
        {
            if (tank == null || newPart == null) return;
            convoy.ReturnFrom(tank, newPart.Category);
            convoy.EquipTo(tank, newPart.instanceId, newPart.Category);
            owner.RefreshPartsOverlay();
            owner.NotifyUnitSelected(tank);
        }
    }
}
