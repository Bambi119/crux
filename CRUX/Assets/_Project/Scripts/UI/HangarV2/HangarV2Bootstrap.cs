using UnityEngine;
using Crux.Core;
using Crux.Data;

namespace Crux.UI
{
    /// <summary>
    /// Stitch _04 격납고 v2 진입점 — Hangar 씬 시작 시 v2 Canvas를 탐색해 바인더 부착.
    /// v1(HangarUICanvas)과 공존 단계. UseV2Canvas=true면 v1 비활성화.
    /// </summary>
    public class HangarV2Bootstrap : MonoBehaviour
    {
        [SerializeField] private bool useV2Canvas = true;
        [SerializeField] private bool disableV1OnStart = true;
        [SerializeField] private CrewMemberSO[] crewRoster;

        private ConvoyInventory convoy;
        private TankInstance selectedTank;

        public ConvoyInventory Convoy => convoy;
        public TankInstance SelectedTank => selectedTank;
        public event System.Action<TankInstance> SelectedTankChanged;
        public event System.Action<TankInstance> LoadoutChanged;

        private HangarV2VehicleListBinder vehicleBinder;
        private HangarV2PartsInventoryBinder partsBinder;
        private HangarV2LoadoutCenterBinder centerBinder;

        private void Awake()
        {
            if (!useV2Canvas) return;

            InitConvoy();

            Transform canvasV2 = FindCanvasV2();
            if (canvasV2 == null)
            {
                Debug.LogWarning("[Hangar] HangarV2Bootstrap: HangarUICanvas_v2를 찾을 수 없습니다. Phase 1 프리팹 미배치 상태");
                return;
            }

            if (disableV1OnStart)
                DisableV1Canvas();

            AttachBinders(canvasV2);

            if (convoy != null && convoy.tanks.Count > 0)
                SelectTank(convoy.tanks[0]);

            Debug.Log("[Hangar] HangarV2Bootstrap: v2 Canvas 바인더 부착 완료");
        }

        private void InitConvoy()
        {
            if (BattleEntryData.Convoy != null)
            {
                convoy = BattleEntryData.Convoy;
                return;
            }

            convoy = HangarBootstrap.BuildSampleConvoy(ref crewRoster);
            BattleEntryData.Convoy = convoy;
        }

        private Transform FindCanvasV2()
        {
            var go = GameObject.Find("HangarUICanvas_v2");
            return go != null ? go.transform : null;
        }

        private void DisableV1Canvas()
        {
            var v1 = GameObject.Find("HangarUICanvas");
            if (v1 != null)
            {
                v1.SetActive(false);
                Debug.Log("[Hangar] v1 Canvas 비활성화");
            }
        }

        private void AttachBinders(Transform canvasV2)
        {
            Transform leftPanel = canvasV2.Find("MainGrid/LeftPanel");
            Transform rightPanel = canvasV2.Find("MainGrid/RightPanel");

            if (leftPanel != null)
            {
                vehicleBinder = leftPanel.gameObject.AddComponent<HangarV2VehicleListBinder>();
                vehicleBinder.Initialize(this, leftPanel);
            }
            else Debug.LogWarning("[Hangar] LeftPanel 경로 누락 (MainGrid/LeftPanel)");

            if (rightPanel != null)
            {
                partsBinder = rightPanel.gameObject.AddComponent<HangarV2PartsInventoryBinder>();
                partsBinder.Initialize(this, rightPanel);
            }
            else Debug.LogWarning("[Hangar] RightPanel 경로 누락 (MainGrid/RightPanel)");

            Transform centerPanel = canvasV2.Find("MainGrid/CenterPanel");
            if (centerPanel != null)
            {
                centerBinder = centerPanel.gameObject.AddComponent<HangarV2LoadoutCenterBinder>();
                centerBinder.Initialize(this, centerPanel);
            }
            else Debug.LogWarning("[Hangar] CenterPanel 경로 누락 (MainGrid/CenterPanel)");

            Transform sortieBtn = canvasV2.Find("TopAppBar/SortieButton");
            if (sortieBtn != null)
            {
                var btn = sortieBtn.GetComponent<UnityEngine.UI.Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(OnSortieClicked);
                }
            }
        }

        public void SelectTank(TankInstance tank)
        {
            if (tank == null || tank == selectedTank) return;
            selectedTank = tank;
            SelectedTankChanged?.Invoke(tank);
        }

        public void NotifyLoadoutChanged()
        {
            LoadoutChanged?.Invoke(selectedTank);
        }

        private void OnSortieClicked()
        {
            if (selectedTank == null)
            {
                Debug.LogWarning("[Hangar] 출격 실패 — 선택된 전차 없음");
                return;
            }
            var result = selectedTank.Validate();
            if (!result.isValid)
            {
                Debug.LogWarning($"[Hangar] 출격 실패 — 편성 미완: {string.Join(", ", result.violations)}");
                return;
            }

            BattleEntryData.SortieTanks.Clear();
            BattleEntryData.SortieTanks.Add(selectedTank);
            HangarBootstrap.SaveConvoyStats(convoy);

            UnityEngine.SceneManagement.SceneManager.LoadScene("StrategyScene");
        }
    }
}
