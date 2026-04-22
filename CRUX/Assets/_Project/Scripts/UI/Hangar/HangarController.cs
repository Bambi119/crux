using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Crux.UI.Hangar
{
    // docs/10b §7.1 — 격납고 허브. 씬 진입 포인트.
    // 역할: SharedState + Bus 보유, ITabModule 레지스트리, 활성 탭 라이프사이클 관리,
    //       LaunchConfirmed → 전투 씬 전환. 모듈 내부 로직은 각 ITabModule 담당.
    public class HangarController : MonoBehaviour
    {
        [SerializeField] string launchTargetScene = "StrategyScene";

        readonly HangarSharedState state = new HangarSharedState();
        readonly HangarBus bus = new HangarBus();
        readonly List<ITabModule> modules = new List<ITabModule>();
        ITabModule activeModule;

        public IHangarStateReadOnly State => state;
        public IHangarBus Bus => bus;
        public HangarSharedState MutableState => state; // 쓰기 주체 모듈이 사용

        void Awake()
        {
            bus.Subscribe<LaunchConfirmedEvent>(OnLaunchConfirmed);
            bus.Subscribe<AwakeningQueueChangedEvent>(OnAwakeningQueueChanged);
        }

        void OnDestroy()
        {
            bus.Unsubscribe<LaunchConfirmedEvent>(OnLaunchConfirmed);
            bus.Unsubscribe<AwakeningQueueChangedEvent>(OnAwakeningQueueChanged);
            foreach (var m in modules) m.OnLeave();
            modules.Clear();
            activeModule = null;
        }

        public void RegisterModule(ITabModule module)
        {
            if (module == null) return;
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i].Tab == module.Tab)
                {
                    Debug.LogError($"[CRUX] [HANGAR] 중복 탭 등록: {module.Tab}");
                    return;
                }
            }
            module.Initialize(state, bus);
            modules.Add(module);
        }

        // 등록 완료 후 호출 — 기본 탭 결정(10b §2.2).
        public void StartHub()
        {
            var initial = state.AwakeningQueueCount > 0 ? HangarTab.Maintenance : HangarTab.Composition;
            SwitchTab(initial);
        }

        public void SwitchTab(HangarTab tab)
        {
            var next = FindModule(tab);
            if (next == null)
            {
                Debug.LogWarning($"[CRUX] [HANGAR] 미등록 탭: {tab}");
                return;
            }
            if (activeModule == next) return;

            var previous = activeModule != null ? activeModule.Tab : state.SelectedTab;
            activeModule?.OnLeave();
            state.HubSetSelectedTab(tab);
            activeModule = next;
            activeModule.OnEnter();
            bus.Publish(new TabChangedEvent(previous, tab));
        }

        void Update()
        {
            activeModule?.Tick(Time.deltaTime);
        }

        ITabModule FindModule(HangarTab tab)
        {
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i].Tab == tab) return modules[i];
            }
            return null;
        }

        void OnAwakeningQueueChanged(AwakeningQueueChangedEvent _)
        {
            // 큐 갱신 시 상태 반영은 이 이벤트를 Publish하는 Ticker 측에서 MutableState로 직접 기록.
            // 여기서는 배지/알림 훅을 걸 자리(후속).
        }

        void OnLaunchConfirmed(LaunchConfirmedEvent _)
        {
            if (string.IsNullOrEmpty(launchTargetScene))
            {
                Debug.LogError("[CRUX] [HANGAR] launchTargetScene 미설정");
                return;
            }
            Debug.Log($"[CRUX] [HANGAR] 출격 확정 → {launchTargetScene}");
            SceneManager.LoadScene(launchTargetScene);
        }
    }
}
