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
        private HangarCompositionBinder compositionBinder; // 편성 탭 슬롯 바인딩 담당

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
            // 편성 바인더 초기화
            if (compositionBinder == null)
                compositionBinder = new HangarCompositionBinder(this, convoyRef);

            // 직전 전투 결과 소비 (Victory 보상 / Defeat 피해)
            ApplyBattleResult();

            BuildTabMenu();
            SelectTab(HangarTab.Composition);
            UpdateTopBar();

            // TopBar ▶출격 버튼 (헬퍼 위임)
            if (moneyText != null)
                HangarButtonHelpers.AttachSortieButton(moneyText.transform.parent, StartBattle);

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

                if (tab == HangarTab.Composition && compositionBinder != null)
                    compositionBinder.Bind(instance, selectedTank);
            }
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

        /// <summary>
        /// 직전 전투 결과(Victory/Defeat)를 Convoy에 반영.
        /// 소비 후 BattleEntryData.LastResult는 None으로 초기화.
        /// </summary>
        private void ApplyBattleResult()
        {
            var result = Crux.Core.BattleEntryData.LastResult;
            if (result == Crux.Core.BattleResult.None || convoyRef == null) return;

            if (result == Crux.Core.BattleResult.Victory)
            {
                convoyRef.AddMoney(200);
                convoyRef.ChangeMorale(10);
                Debug.Log("[Hangar] 승리 보상: +₩200 · +10 사기");
            }
            else if (result == Crux.Core.BattleResult.Defeat)
            {
                convoyRef.AddMoney(-100);
                convoyRef.ChangeMorale(-15);
                Debug.Log("[Hangar] 패배 피해: -₩100 · -15 사기");
            }

            Crux.Core.BattleEntryData.LastResult = Crux.Core.BattleResult.None;
        }

        public void OnUnitSelected(TankInstance tank)
        {
            selectedTank = tank;
            if (rightPanel != null)
                rightPanel.SetUnit(tank);

            // 편성 탭의 슬롯 하이라이트 갱신
            if (currentTab == HangarTab.Composition && compositionBinder != null &&
                instantiatedTabs.TryGetValue(HangarTab.Composition, out var compTab) && compTab != null)
            {
                compositionBinder.Bind(compTab, selectedTank);
            }
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
            // Money/Morale PlayerPrefs 저장 (Save-Minimal)
            HangarBootstrap.SaveConvoyStats(convoyRef);

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
