using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Crux.Data;
using System.Collections.Generic;

namespace Crux.UI
{
    /// <summary>
    /// Hangar 씬 메인 오케스트레이터.
    /// 상단바(자금·사기), 좌측 탭 메뉴(6종), 중앙 탭 콘텐츠, 우측 유닛 정보 패널 관리.
    /// 오버레이(파츠·크루) 생성·소멸은 HangarOverlayBuilder로 위임.
    /// 부대 시드는 HangarBootstrap으로 위임.
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

        [Header("전투 연결")]
        [SerializeField] private string battleSceneName = "StrategyScene";

        private HangarTab currentTab = HangarTab.Composition;
        private Dictionary<HangarTab, GameObject> instantiatedTabs = new();
        private GameObject activeOverlay;  // 중복 생성 방지
        private TankInstance selectedTank; // 가장 최근 선택된 탱크
        private HangarOverlayBuilder overlayBuilder; // 오버레이 생성 담당

        private void OnEnable()
        {
            if (leftMenuRoot == null || centerContentSlot == null)
                return;

            // ConvoyInventory는 POCO — SerializeField 직렬화 불가. MVP 폴백 인스턴스.
            // 전투 씬에서 복귀한 경우 BattleEntryData.Convoy 재사용 (편성 상태 유지).
            if (convoyRef == null)
            {
                if (Crux.Core.BattleEntryData.Convoy != null)
                {
                    convoyRef = Crux.Core.BattleEntryData.Convoy;
                    Debug.Log("[Hangar] 이전 세션 Convoy 복원");
                }
                else
                {
                    convoyRef = HangarBootstrap.BuildSampleConvoy(ref crewRoster);
                }
            }

            // 오버레이 빌더 초기화
            if (overlayBuilder == null)
                overlayBuilder = new HangarOverlayBuilder(this, convoyRef);

            BuildTabMenu();
            SelectTab(HangarTab.Composition);
            UpdateTopBar();
            AttachSortieButton();

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

        /// <summary>
        /// TopBar 오른쪽에 "출격" 버튼을 런타임 추가. 클릭 시 battleSceneName 로드.
        /// 중복 생성 방지. HorizontalLayoutGroup이 자동 배치.
        /// </summary>
        private void AttachSortieButton()
        {
            if (moneyText == null) return;
            Transform topBar = moneyText.transform.parent;
            if (topBar == null) return;
            if (topBar.Find("SortieButton") != null) return;

            var btnObj = new GameObject("SortieButton");
            btnObj.transform.SetParent(topBar, false);
            btnObj.AddComponent<RectTransform>();
            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.7f, 0.35f, 0.2f, 1f);
            var btn = btnObj.AddComponent<Button>();
            var le = btnObj.AddComponent<LayoutElement>();
            le.preferredWidth = 120;
            le.preferredHeight = 42;

            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btnObj.transform, false);
            var labelRt = labelObj.AddComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var text = labelObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = "▶ 출격";

            btn.onClick.AddListener(StartBattle);
        }

        public void StartBattle()
        {
            if (string.IsNullOrEmpty(battleSceneName))
            {
                Debug.LogWarning("[Hangar] battleSceneName 미설정");
                return;
            }

            // BattleEntryData에 편성 데이터 저장 — BattleController가 수신
            if (convoyRef != null)
            {
                Crux.Core.BattleEntryData.Convoy = convoyRef;
                Crux.Core.BattleEntryData.SortieTanks = convoyRef.tanks.FindAll(t => t.inSortie);
                Debug.Log($"[Hangar] 출격 편성: {Crux.Core.BattleEntryData.SortieTanks.Count}대 → {battleSceneName}");
            }
            else
            {
                Debug.Log($"[Hangar] 출격 — convoyRef null, 기본 전투 씬 로드");
            }

            SceneManager.LoadScene(battleSceneName);
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

        /// <summary>
        /// 선택된 탱크의 inSortie 토글. 출격 한도 5 초과 시 보관으로만 허용.
        /// 토글 후 편성 탭 재바인딩으로 슬롯 갱신.
        /// </summary>
        public void ToggleSortie(TankInstance tank)
        {
            if (tank == null || convoyRef == null) return;

            if (!tank.inSortie)
            {
                // 보관 → 출격: 한도 체크 (기존 출격 4 이하여야 +1 가능)
                int sortieCount = convoyRef.tanks.FindAll(t => t.inSortie).Count;
                if (sortieCount >= 5)
                {
                    Debug.LogWarning("[Hangar] 출격 한도 5 초과 — 토글 거부");
                    return;
                }
            }

            tank.inSortie = !tank.inSortie;
            Debug.Log($"[Hangar] {tank.tankName} inSortie={tank.inSortie}");

            // 편성 탭 재바인딩 + RightPanel 갱신
            if (currentTab == HangarTab.Composition)
                SelectTab(HangarTab.Composition);
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
            if (tank == null || overlayBuilder == null) return;
            if (activeOverlay != null) Destroy(activeOverlay);

            var overlayCanvas = GameObject.Find("OverlayCanvas");
            if (overlayCanvas == null) return;

            activeOverlay = overlayBuilder.BuildPartsOverlay(tank);
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

        public void RefreshPartsOverlay()
        {
            if (activeOverlay != null && selectedTank != null)
            {
                CloseOverlay();
                OpenPartsInventory(selectedTank);
            }
        }

        public void UnassignCrewAndRefresh(TankInstance tank, CrewClass klass)
        {
            if (tank == null || convoyRef == null) return;
            convoyRef.UnassignCrewFrom(tank, klass);
            if (rightPanel != null) rightPanel.SetUnit(tank);
        }

        public void OpenCrewPool(TankInstance tank, CrewClass klass)
        {
            if (tank == null || overlayBuilder == null) return;
            if (activeOverlay != null) Destroy(activeOverlay);

            var overlayCanvas = GameObject.Find("OverlayCanvas");
            if (overlayCanvas == null) return;

            activeOverlay = overlayBuilder.BuildCrewPoolPopup(tank, klass);
            activeOverlay.transform.SetParent(overlayCanvas.transform, false);
            var rt = activeOverlay.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Overlay 내부 콜백이 우측 패널을 갱신하도록 호출할 공개 메서드.
        /// </summary>
        public void NotifyUnitSelected(TankInstance tank)
        {
            if (rightPanel != null) rightPanel.SetUnit(tank);
        }
    }
}
