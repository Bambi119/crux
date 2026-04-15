using UnityEngine;
using Crux.Data;

namespace Crux.Unit
{
    /// <summary>
    /// 전차 1대의 승무원 컨테이너. 5직책 슬롯 + 사기 게이지.
    /// GridTankUnit에 함께 붙는 MonoBehaviour로 사용한다.
    /// docs/04 §10.4 참조.
    /// </summary>
    [DisallowMultipleComponent]
    public class TankCrew : MonoBehaviour
    {
        public CrewMemberRuntime commander;
        public CrewMemberRuntime gunner;
        public CrewMemberRuntime loader;
        public CrewMemberRuntime driver;
        public CrewMemberRuntime mgMechanic;

        [SerializeField, Range(0, 100)] private int morale = 50;

        /// <summary>공황 안전장치 사용 여부 — 전투당 1회만 자동 +15 회복 가능</summary>
        public bool PanicSafetyUsed { get; private set; }

        public int Morale => morale;
        public MoraleBand Band => MoraleSystem.GetBand(morale);

        /// <summary>
        /// 5명 할당 + 시작 사기 계산. 전투 시작 시 또는 편성 확정 시 호출.
        /// </summary>
        /// <param name="commanderHullClassAxis">전차장 시작 사기 보너스 계산용 축 ID. null이면 0 보너스.</param>
        public void Initialize(CrewMemberSO cmdr, CrewMemberSO gun, CrewMemberSO load,
                                CrewMemberSO drv, CrewMemberSO mg,
                                string commanderHullClassAxis = null)
        {
            commander = cmdr != null ? new CrewMemberRuntime(cmdr) : null;
            gunner = gun != null ? new CrewMemberRuntime(gun) : null;
            loader = load != null ? new CrewMemberRuntime(load) : null;
            driver = drv != null ? new CrewMemberRuntime(drv) : null;
            mgMechanic = mg != null ? new CrewMemberRuntime(mg) : null;

            // 시작 사기 — docs/04 §6.1: base 50 + (전차장 해당 클래스 마크 × 3)
            int commanderMark = 0;
            if (commander != null)
            {
                commanderMark = !string.IsNullOrEmpty(commanderHullClassAxis)
                    ? commander.GetMark(commanderHullClassAxis)
                    : 0;
            }
            morale = Mathf.Clamp(50 + commanderMark * 3, 0, 100);
            PanicSafetyUsed = false;
        }

        /// <summary>직책으로 승무원 조회. 공석은 null.</summary>
        public CrewMemberRuntime GetByClass(CrewClass klass) => klass switch
        {
            CrewClass.Commander => commander,
            CrewClass.Gunner => gunner,
            CrewClass.Loader => loader,
            CrewClass.Driver => driver,
            CrewClass.GunnerMech => mgMechanic,
            _ => null
        };

        /// <summary>공석 여부 — 미배치 또는 중상/치명상</summary>
        public bool IsVacant(CrewClass klass)
        {
            var m = GetByClass(klass);
            return m == null || !m.IsCombatReady;
        }

        /// <summary>
        /// 사기 이벤트 적용. overrideAmount를 지정하지 않으면 기본 델타 사용 (docs/04 §6.1 테이블).
        /// </summary>
        public void ApplyMoraleEvent(MoraleEvent kind, int? overrideAmount = null)
        {
            int delta = overrideAmount ?? MoraleSystem.DefaultDelta(kind);
            SetMorale(morale + delta);
        }

        /// <summary>사기 직접 지정. 공황 진입 시 1회 자동 +15 안전장치 발동.</summary>
        public void SetMorale(int newValue)
        {
            int prev = morale;
            int clamped = Mathf.Clamp(newValue, 0, 100);

            // 공황 안전장치 — 정상 이상에서 공황으로 넘어가는 순간 1회 발동
            if (!PanicSafetyUsed && prev > 24 && clamped <= 24)
            {
                clamped = Mathf.Clamp(clamped + 15, 0, 100);
                PanicSafetyUsed = true;
            }
            morale = clamped;
        }

        /// <summary>
        /// 매 턴 시작 처리 — 자연 회복(전차장 마크×1) + 모든 승무원 쿨다운 감소.
        /// 공황 상태에서 회복으로 공황을 벗어나면 안전장치는 소비되지 않음(이미 소비됐거나 아직 미진입).
        /// </summary>
        public void TickTurnStart()
        {
            // 자연 회복 — 전차장 마크 × 1 (모든 축 중 최대치 기준)
            if (commander != null)
            {
                int regen = commander.MaxMark();
                if (regen > 0) morale = Mathf.Clamp(morale + regen, 0, 100);
            }

            commander?.TickCooldowns();
            gunner?.TickCooldowns();
            loader?.TickCooldowns();
            driver?.TickCooldowns();
            mgMechanic?.TickCooldowns();
        }

        /// <summary>전투 종료 시 플래그 리셋 (안전장치 다음 전투에서 다시 1회 가능)</summary>
        public void ResetForNextBattle()
        {
            PanicSafetyUsed = false;
        }
    }
}
