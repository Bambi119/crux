using UnityEngine;
using UnityEngine.UI;
using Crux.Core;
using Crux.Data;
using Crux.UI.Hangar.Composition;

namespace Crux.UI.Hangar
{
    // docs/10b §2.1 — Hangar 씬 진입점. HangarController 부착 + 모듈 등록 + Sortie 버튼 훅.
    // V2Bootstrap을 흡수: convoy 초기화·Canvas 탐색·v1 비활성은 이 컴포넌트가 담당한다.
    public class HangarSceneBootstrap : MonoBehaviour
    {
        [SerializeField] bool disableV1OnStart = true;
        [SerializeField] CrewMemberSO[] crewRoster;

        ConvoyInventory convoy;
        HangarController controller;

        void Awake()
        {
            InitConvoy();

            var canvas = FindCanvas();
            if (canvas == null)
            {
                Debug.LogWarning("[CRUX] [HANGAR] HangarUICanvas_v2를 찾을 수 없음 — Phase 1 프리팹 미배치");
                return;
            }

            if (disableV1OnStart) DisableV1Canvas();

            Transform leftPanel = canvas.Find("MainGrid/LeftPanel");
            Transform centerPanel = canvas.Find("MainGrid/CenterPanel");
            Transform rightPanel = canvas.Find("MainGrid/RightPanel");

            if (leftPanel == null || centerPanel == null || rightPanel == null)
            {
                Debug.LogWarning("[CRUX] [HANGAR] MainGrid 3-Panel 경로 누락 — Left/Center/Right");
                return;
            }

            controller = canvas.gameObject.AddComponent<HangarController>();

            var compositionTab = canvas.gameObject.AddComponent<CompositionTabBinder>();
            compositionTab.WireScene(controller.MutableState, convoy, leftPanel, centerPanel, rightPanel);
            controller.RegisterModule(compositionTab);

            controller.StartHub();

            // 초기 선택 — 편성 탭이 활성 상태이므로 첫 전차를 선택해 서브뷰 초기 렌더 트리거.
            if (convoy != null && convoy.tanks.Count > 0)
            {
                controller.MutableState.CompSetSelectedTank(convoy.tanks[0]);
                controller.Bus.Publish(new TankSelectedEvent(convoy.tanks[0]));
            }

            WireSortieButton(canvas);

            Debug.Log("[CRUX] [HANGAR] HangarSceneBootstrap: 허브 구동 완료 (Composition)");
        }

        void InitConvoy()
        {
            if (BattleEntryData.Convoy != null)
            {
                convoy = BattleEntryData.Convoy;
                return;
            }

            convoy = HangarBootstrap.BuildSampleConvoy(ref crewRoster);
            BattleEntryData.Convoy = convoy;
        }

        Transform FindCanvas()
        {
            var go = GameObject.Find("HangarUICanvas_v2");
            return go != null ? go.transform : null;
        }

        void DisableV1Canvas()
        {
            var v1 = GameObject.Find("HangarUICanvas");
            if (v1 != null)
            {
                v1.SetActive(false);
                Debug.Log("[CRUX] [HANGAR] v1 Canvas 비활성화");
            }
        }

        void WireSortieButton(Transform canvas)
        {
            var sortieBtn = canvas.Find("TopAppBar/SortieButton");
            if (sortieBtn == null) return;
            var btn = sortieBtn.GetComponent<Button>();
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnSortieClicked);
        }

        void OnSortieClicked()
        {
            if (controller == null) return;

            var tank = controller.State.SelectedTank;
            if (tank == null)
            {
                Debug.LogWarning("[CRUX] [HANGAR] 출격 실패 — 선택된 전차 없음");
                return;
            }

            var validation = tank.Validate();
            if (!validation.isValid)
            {
                Debug.LogWarning($"[CRUX] [HANGAR] 출격 실패 — 편성 미완: {string.Join(", ", validation.violations)}");
                return;
            }

            BattleEntryData.SortieTanks.Clear();
            BattleEntryData.SortieTanks.Add(tank);
            HangarBootstrap.SaveConvoyStats(convoy);

            var loadout = new System.Collections.Generic.List<TankInstance> { tank };
            controller.Bus.Publish(new LaunchConfirmedEvent(loadout));
        }
    }
}
