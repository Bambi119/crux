using UnityEngine;
using UnityEngine.UI;
using TMPro;
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

        [SerializeField] private TMP_Text moneyText;
        [SerializeField] private TMP_Text moraleText;

        private HangarTab currentTab = HangarTab.Composition;
        private Dictionary<HangarTab, GameObject> instantiatedTabs = new();

        private void OnEnable()
        {
            if (leftMenuRoot == null || centerContentSlot == null)
                return;

            BuildTabMenu();
            SelectTab(HangarTab.Composition);
            UpdateTopBar();
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

                TMP_Text btnText = btnObj.GetComponentInChildren<TMP_Text>();
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
            }
        }

        public void UpdateTopBar()
        {
            if (moneyText != null)
                moneyText.text = "₩0";  // ConvoyInventory에 자금 필드 없음 — 향후 추가

            if (moraleText != null)
                moraleText.text = "0";  // 사기 점수 — 향후 추가
        }

        public void OnUnitSelected(TankInstance tank)
        {
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
            // 탭 버튼 이벤트 해제
            foreach (Transform child in leftMenuRoot)
            {
                Button btn = child.GetComponent<Button>();
                if (btn != null)
                    btn.onClick.RemoveAllListeners();
                Destroy(child.gameObject);
            }

            // 중앙 콘텐츠 정리
            foreach (Transform child in centerContentSlot)
            {
                Destroy(child.gameObject);
            }
            instantiatedTabs.Clear();
        }
    }
}
