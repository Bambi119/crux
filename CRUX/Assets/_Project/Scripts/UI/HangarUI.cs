using UnityEngine;
using UnityEngine.UI;
using Crux.Data;
using System.Collections.Generic;

namespace Crux.UI
{
    /// <summary>
    /// Hangar 씬 메인 오케스트레이터.
    /// 상단바(자금·사기), 좌측 탭 메뉴(6종), 중앙 탭 콘텐츠, 우측 유닛 정보 패널 관리.
    /// </summary>
    public class HangarUI : MonoBehaviour
    {
        // 열거형: 6개 탭
        public enum HangarTab
        {
            Composition,
            Maintenance,
            Shop,
            Skill,
            Social,
            NPC
        }

        [SerializeField] private Transform leftMenuRoot;
        [SerializeField] private Transform centerContentSlot;
        [SerializeField] private HangarRightPanel rightPanel;
        [SerializeField] private ConvoyInventory convoyRef;

        [SerializeField] private GameObject compositionTabPrefab;
        [SerializeField] private GameObject lockedTabPrefab;
        [SerializeField] private GameObject tabButtonPrefab;

        [SerializeField] private Text moneyText;
        [SerializeField] private Text moraleText;

        [Header("샘플 부대 시드 (MVP — 임시)")]
        [SerializeField] private Crux.Data.CrewMemberSO[] crewRoster;  // 5개 SO 에셋 Inspector 할당

        private HangarTab currentTab = HangarTab.Composition;
        private Dictionary<HangarTab, GameObject> instantiatedTabs = new();
        private GameObject activeOverlay;  // 중복 생성 방지
        private TankInstance selectedTank; // 가장 최근 선택된 탱크

        private void OnEnable()
        {
            if (leftMenuRoot == null || centerContentSlot == null)
                return;

            // ConvoyInventory는 POCO — SerializeField 직렬화 불가. MVP 폴백 인스턴스.
            if (convoyRef == null)
                convoyRef = BuildSampleConvoy();

            BuildTabMenu();
            SelectTab(HangarTab.Composition);
            UpdateTopBar();

            // 첫 탱크 자동 선택 → RightPanel 표시
            if (rightPanel != null && convoyRef.tanks.Count > 0)
            {
                selectedTank = convoyRef.tanks[0];
                rightPanel.SetUnit(selectedTank);
            }
        }

        private void BuildTabMenu()
        {
            // 좌측 메뉴에 6개 탭 버튼 생성
            HangarTab[] tabs = new[] {
                HangarTab.Composition,
                HangarTab.Maintenance,
                HangarTab.Shop,
                HangarTab.Skill,
                HangarTab.Social,
                HangarTab.NPC
            };

            foreach (var tab in tabs)
            {
                GameObject btnObj = tabButtonPrefab != null
                    ? Instantiate(tabButtonPrefab, leftMenuRoot)
                    : new GameObject(tab.ToString());

                Text btnText = btnObj.GetComponentInChildren<Text>();
                if (btnText != null)
                    btnText.text = FormatTabName(tab);

                Button btn = btnObj.GetComponent<Button>();
                if (btn != null)
                {
                    HangarTab tabCopy = tab;
                    btn.onClick.AddListener(() => SelectTab(tabCopy));
                }
            }
        }

        private ConvoyInventory BuildSampleConvoy()
        {
            var convoy = new ConvoyInventory();

#if UNITY_EDITOR
            // Editor fallback — Inspector 미할당 시 AssetDatabase로 5명 자동 로드 (MVP 편의)
            if (crewRoster == null || crewRoster.Length == 0)
            {
                string[] ids = { "astra", "ririd", "grin", "pretena", "iris" };
                var list = new List<Crux.Data.CrewMemberSO>();
                foreach (var id in ids)
                {
                    var path = $"Assets/_Project/Data/Crew/Members/Crew_{id}.asset";
                    var so = UnityEditor.AssetDatabase.LoadAssetAtPath<Crux.Data.CrewMemberSO>(path);
                    if (so != null) list.Add(so);
                }
                crewRoster = list.ToArray();
            }
#endif

            // 1) 승무원 풀 시드 — Inspector 할당 에셋으로
            if (crewRoster != null)
            {
                foreach (var so in crewRoster)
                {
                    if (so == null) continue;
                    convoy.availableCrew.Add(new Crux.Data.CrewMemberRuntime(so));
                }
            }

            // 2) 샘플 탱크 1대 — 로시난테 (출격 슬롯 기본)
            var rocinante = new Crux.Data.TankInstance("로시난테", Crux.Data.HullClass.Assault);
            rocinante.isRocinante = true;
            rocinante.inSortie = true;
            convoy.tanks.Add(rocinante);

            // 2-b) 샘플 탱크 2대 (보관 예시) — T-34 Scout / 셔먼 Support
            var t34 = new Crux.Data.TankInstance("T-34", Crux.Data.HullClass.Scout);
            t34.inSortie = false;
            convoy.tanks.Add(t34);

            var sherman = new Crux.Data.TankInstance("셔먼", Crux.Data.HullClass.Support);
            sherman.inSortie = false;
            convoy.tanks.Add(sherman);

            // 3) 5명 자동 할당 — 풀에 있는 승무원의 Class로 직책 판정
            var classes = new[] {
                Crux.Data.CrewClass.Commander,
                Crux.Data.CrewClass.Gunner,
                Crux.Data.CrewClass.Loader,
                Crux.Data.CrewClass.Driver,
                Crux.Data.CrewClass.GunnerMech
            };
            foreach (var klass in classes)
            {
                // 풀에서 해당 직책의 첫 승무원 id 찾아 할당
                var c = convoy.availableCrew.Find(cr => cr.Class == klass);
                if (c != null && c.data != null)
                    convoy.AssignCrewTo(rocinante, klass, c.data.id);
            }

            return convoy;
        }

        public void SelectTab(HangarTab tab)
        {
            currentTab = tab;

            // CenterContent 기존 자식 삭제
            foreach (Transform child in centerContentSlot)
            {
                Destroy(child.gameObject);
            }
            instantiatedTabs.Clear();

            // 탭별 프리팹 인스턴스화
            GameObject prefabToUse = (tab == HangarTab.Composition) ? compositionTabPrefab : lockedTabPrefab;
            if (prefabToUse != null)
            {
                GameObject instance = Instantiate(prefabToUse, centerContentSlot);
                instantiatedTabs[tab] = instance;

                if (tab == HangarTab.Composition)
                    BindTankSlots(instance);
            }
        }

        public void UpdateTopBar()
        {
            if (convoyRef != null && moneyText != null)
                moneyText.text = $"자금: ₩{convoyRef.Money}";

            if (convoyRef != null && moraleText != null)
                moraleText.text = $"사기: {convoyRef.Morale}";
        }

        public void OnUnitSelected(TankInstance tank)
        {
            selectedTank = tank;
            if (rightPanel != null)
                rightPanel.SetUnit(tank);
        }

        public void OnUnitDeselected()
        {
            if (rightPanel != null)
                rightPanel.Clear();
        }

        private void BindTankSlots(GameObject compositionTabInstance)
        {
            if (convoyRef == null) return;

            // SortieGrid, StorageGrid는 프리팹 안에 이름으로 찾는다
            Transform sortieGrid = compositionTabInstance.transform.Find("SortieGrid");
            Transform storageGrid = compositionTabInstance.transform.Find("StorageGrid");

            // tank.inSortie 플래그 기반으로 출격/보관 분리 배치
            var sortieTanks = convoyRef.tanks.FindAll(t => t.inSortie);
            var storageTanks = convoyRef.tanks.FindAll(t => !t.inSortie);
            int sortieCount = 5, storageCount = 5;

            // 카운트 라벨 갱신
            var sortieLabel = compositionTabInstance.transform.Find("SortieLabel")?.GetComponent<Text>();
            if (sortieLabel != null) sortieLabel.text = $"출격 ({sortieTanks.Count}/5)";
            var storageLabel = compositionTabInstance.transform.Find("StorageLabel")?.GetComponent<Text>();
            if (storageLabel != null) storageLabel.text = $"보관 ({storageTanks.Count}/5)";

            for (int i = 0; i < sortieCount; i++)
            {
                if (sortieGrid == null || i >= sortieGrid.childCount) break;
                Transform slot = sortieGrid.GetChild(i);
                TankInstance tank = (i < sortieTanks.Count) ? sortieTanks[i] : null;
                BindOneSlot(slot, tank);
            }

            for (int i = 0; i < storageCount; i++)
            {
                if (storageGrid == null || i >= storageGrid.childCount) break;
                Transform slot = storageGrid.GetChild(i);
                TankInstance tank = (i < storageTanks.Count) ? storageTanks[i] : null;
                BindOneSlot(slot, tank);
            }

            AttachOpenPartsButton(compositionTabInstance);
        }

        private void BindOneSlot(Transform slot, TankInstance tank)
        {
            if (slot == null) return;

            // SlotLabel Text 갱신
            Text label = slot.GetComponentInChildren<Text>();
            if (label != null)
                label.text = (tank != null) ? tank.tankName : "없음";

            // Button 확보 (없으면 AddComponent) + 기존 리스너 제거 + 신규 연결
            Button btn = slot.GetComponent<Button>();
            if (btn == null)
                btn = slot.gameObject.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();

            if (tank != null)
            {
                var captured = tank;  // 클로저 캡처 보호
                btn.onClick.AddListener(() => OnUnitSelected(captured));
            }
        }

        private string FormatTabName(HangarTab tab)
        {
            return tab switch
            {
                HangarTab.Composition => "편성",
                HangarTab.Maintenance => "정비",
                HangarTab.Shop => "상점",
                HangarTab.Skill => "스킬",
                HangarTab.Social => "외교",
                HangarTab.NPC => "NPC",
                _ => "?"
            };
        }

        private void OnDisable()
        {
            // 씬 언로드 시 타겟이 이미 파괴됐을 수 있어 null 가드 필수
            if (leftMenuRoot != null)
            {
                foreach (Transform child in leftMenuRoot)
                {
                    Button btn = child.GetComponent<Button>();
                    if (btn != null)
                        btn.onClick.RemoveAllListeners();
                    Destroy(child.gameObject);
                }
            }

            if (centerContentSlot != null)
            {
                foreach (Transform child in centerContentSlot)
                {
                    Destroy(child.gameObject);
                }
            }
            instantiatedTabs.Clear();
            CloseOverlay();
        }

        private void AttachOpenPartsButton(GameObject compositionTab)
        {
            if (compositionTab == null) return;
            if (compositionTab.transform.Find("OpenPartsButton") != null) return;

            var btnObj = new GameObject("OpenPartsButton");
            btnObj.transform.SetParent(compositionTab.transform, false);
            btnObj.AddComponent<RectTransform>();
            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.3f, 0.35f, 0.42f, 1f);
            var btn = btnObj.AddComponent<Button>();
            var le = btnObj.AddComponent<LayoutElement>();
            le.preferredHeight = 36;
            le.flexibleWidth = 1;

            // Label 자식
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btnObj.transform, false);
            var labelRt = labelObj.AddComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var text = labelObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = "파츠 인벤토리 열기";

            btn.onClick.AddListener(() => {
                if (selectedTank != null)
                    OpenPartsInventory(selectedTank);
            });
        }

        public void OpenPartsInventory(TankInstance tank)
        {
            if (tank == null) return;
            if (activeOverlay != null) Destroy(activeOverlay);

            // OverlayCanvas 찾기 (씬 루트)
            var overlayCanvas = GameObject.Find("OverlayCanvas");
            if (overlayCanvas == null) return;

            activeOverlay = BuildPartsOverlay(tank);
            activeOverlay.transform.SetParent(overlayCanvas.transform, false);
            var rt = activeOverlay.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public void CloseOverlay()
        {
            if (activeOverlay != null)
            {
                Destroy(activeOverlay);
                activeOverlay = null;
            }
        }

        private GameObject BuildPartsOverlay(TankInstance tank)
        {
            // 배경 panel (반투명 검정 전체 스크린)
            var root = new GameObject("PartsInventoryOverlay");
            root.AddComponent<RectTransform>();
            var bgImg = root.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.75f);

            // 내부 패널 (가운데 640x480 박스)
            var panel = new GameObject("Panel");
            panel.transform.SetParent(root.transform, false);
            var panelRt = panel.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(640f, 480f);
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

            // 닫기 버튼 (하단)
            var closeObj = new GameObject("CloseButton");
            closeObj.transform.SetParent(panel.transform, false);
            closeObj.AddComponent<RectTransform>();
            var closeImg = closeObj.AddComponent<Image>();
            closeImg.color = new Color(0.45f, 0.2f, 0.2f, 1f);
            var closeBtn = closeObj.AddComponent<Button>();
            closeBtn.onClick.AddListener(CloseOverlay);
            var closeLe = closeObj.AddComponent<LayoutElement>();
            closeLe.preferredHeight = 36;
            // Label
            var closeLabelObj = new GameObject("Label");
            closeLabelObj.transform.SetParent(closeObj.transform, false);
            var closeLabelRt = closeLabelObj.AddComponent<RectTransform>();
            closeLabelRt.anchorMin = Vector2.zero;
            closeLabelRt.anchorMax = Vector2.one;
            closeLabelRt.offsetMin = Vector2.zero;
            closeLabelRt.offsetMax = Vector2.zero;
            var closeText = closeLabelObj.AddComponent<Text>();
            closeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            closeText.fontSize = 16;
            closeText.alignment = TextAnchor.MiddleCenter;
            closeText.color = Color.white;
            closeText.text = "닫기";

            return root;
        }

        private void AddText(Transform parent, string name, string text, int fontSize, Color color, float preferredHeight)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>();
            var t = obj.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TextAnchor.MiddleLeft;
            t.text = text;
            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
        }

        private void AddSlotRow(Transform parent, string label, Crux.Data.PartInstance part)
        {
            string value = (part != null && part.data != null) ? part.data.partName : "(비어있음)";
            Color color = (part != null) ? new Color(0.85f, 0.9f, 0.85f) : new Color(0.6f, 0.6f, 0.6f);
            AddText(parent, $"Slot_{label}", $"{label}: {value}", 16, color, 24);
        }
    }
}
