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
                rightPanel.SetUnit(convoyRef.tanks[0]);
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

            // 2) 샘플 탱크 1대 — 로시난테
            var rocinante = new Crux.Data.TankInstance("로시난테", Crux.Data.HullClass.Assault);
            rocinante.isRocinante = true;
            convoy.tanks.Add(rocinante);

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

            // 현재 MVP: 출격/보관 구분 없음 → convoy.tanks를 순서대로 sortie 먼저 채우고 나머지는 storage
            // 향후 tank.inSortie 플래그 도입 예정
            var tanks = convoyRef.tanks;
            int sortieCount = 5, storageCount = 5;

            for (int i = 0; i < sortieCount; i++)
            {
                if (sortieGrid == null || i >= sortieGrid.childCount) break;
                Transform slot = sortieGrid.GetChild(i);
                TankInstance tank = (i < tanks.Count) ? tanks[i] : null;
                BindOneSlot(slot, tank);
            }

            for (int i = 0; i < storageCount; i++)
            {
                if (storageGrid == null || i >= storageGrid.childCount) break;
                Transform slot = storageGrid.GetChild(i);
                int tankIdx = i + sortieCount;
                TankInstance tank = (tankIdx < tanks.Count) ? tanks[tankIdx] : null;
                BindOneSlot(slot, tank);
            }
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
        }
    }
}
