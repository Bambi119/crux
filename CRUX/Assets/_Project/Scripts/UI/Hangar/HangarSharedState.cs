using System;
using System.Collections.Generic;
using Crux.Data;

namespace Crux.UI.Hangar
{
    // docs/10b §4.1 — 격납고 허브 공유 상태 읽기 전용 뷰.
    // 모듈(10c/10d/10e/10f/MaintenanceTicker)은 이 인터페이스만 받는다.
    public interface IHangarStateReadOnly
    {
        HangarTab SelectedTab { get; }
        TankInstance SelectedTank { get; }
        IReadOnlyList<TankInstance> LaunchSlotAssignment { get; }
        IReadOnlyList<TraitModifier> SelectedTankTraits { get; }
        PartCategory? PartsFilterCategory { get; }
        bool PartsFilterCompatibleOnly { get; }
        int AwakeningQueueCount { get; }
    }

    // docs/10b §4.1 — 격납고 공유 상태 POCO.
    // 쓰기 주체는 모듈별로 고정(XML 주석의 "Owner:" 표기).
    // 배치가 Assembly-CSharp 단일 어셈블리이므로 컴파일 수준 차단 불가 → 컨벤션 + 리뷰로 강제.
    public class HangarSharedState : IHangarStateReadOnly
    {
        public const int LaunchSlotCount = 4;

        // Owner: HangarController (10b hub)
        public HangarTab SelectedTab { get; private set; } = HangarTab.Composition;

        // Owner: 10c Composition
        public TankInstance SelectedTank { get; private set; }

        readonly TankInstance[] launchSlots = new TankInstance[LaunchSlotCount];
        public IReadOnlyList<TankInstance> LaunchSlotAssignment => launchSlots;

        // Owner: 10f Traits (read-only calc)
        TraitModifier[] selectedTankTraits = Array.Empty<TraitModifier>();
        public IReadOnlyList<TraitModifier> SelectedTankTraits => selectedTankTraits;

        // Owner: 10d Parts
        public PartCategory? PartsFilterCategory { get; private set; }
        public bool PartsFilterCompatibleOnly { get; private set; } = true;

        // Owner: MaintenanceTicker (03b)
        public int AwakeningQueueCount { get; private set; }

        // === 쓰기 API — 호출 주체별 접두사 ===

        // HangarController(Hub)
        public void HubSetSelectedTab(HangarTab tab) => SelectedTab = tab;

        // 10c Composition
        public void CompSetSelectedTank(TankInstance tank) => SelectedTank = tank;

        public void CompSetLaunchSlot(int index, TankInstance tank)
        {
            if (index < 0 || index >= LaunchSlotCount) return;
            launchSlots[index] = tank;
        }

        public void CompClearLaunchSlots()
        {
            for (int i = 0; i < launchSlots.Length; i++) launchSlots[i] = null;
        }

        // 10f Traits
        public void TraitsSetSnapshot(IReadOnlyList<TraitModifier> traits)
        {
            if (traits == null || traits.Count == 0)
            {
                selectedTankTraits = Array.Empty<TraitModifier>();
                return;
            }
            var arr = new TraitModifier[traits.Count];
            for (int i = 0; i < traits.Count; i++) arr[i] = traits[i];
            selectedTankTraits = arr;
        }

        // 10d Parts
        public void PartsSetFilterCategory(PartCategory? category) => PartsFilterCategory = category;

        public void PartsSetFilterCompatibleOnly(bool compatibleOnly) => PartsFilterCompatibleOnly = compatibleOnly;

        // MaintenanceTicker
        public void MaintSetAwakeningQueueCount(int count) => AwakeningQueueCount = count < 0 ? 0 : count;
    }
}
