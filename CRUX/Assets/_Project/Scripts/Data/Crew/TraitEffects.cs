using System.Linq;

namespace Crux.Data
{
    /// <summary>
    /// 특성별 스탯 모디파이어 수치 (합산용 델타).
    /// 모든 필드 기본값 0 — 해당 특성이 영향 없는 스탯.
    /// </summary>
    public struct TraitModifier
    {
        public int initiativeBonus;   // InitiativeInput.traitBonus 에 가산
        public int aimBonus;          // 명중률 보정 (%) — 후속 통합
        public int reactBonus;        // React 스탯 보정 — 이니셔티브 공식에서 react 대체용
        public int moraleFloor;       // 시작 사기 추가 가산 — Phase 2 편성 씬 대비
        // 향후 확장: reloadBonus, repairBonus, penetrationBonus, crewCooperationBonus 등
    }

    /// <summary>
    /// TraitSO.id 를 TraitModifier 로 변환하는 dispatch 테이블 (id 기반).
    /// Phase 3.5.7: 더 이상 "장점/약점" 쌍 개념 없음. 특성은 누적 카운트 기반 활성화.
    /// 발동 조건 검증은 외부(TankCrew/InitiativeSetup 등)에서 axisType/axisThreshold 참고.
    /// 여기는 활성화된 특성의 수치 효과만 제공.
    /// </summary>
    public static class TraitEffects
    {
        // TODO(balance): 수치는 임시 — docs/04 승무원 특성 밸런스 섹션 확정 후 조정
        static readonly System.Collections.Generic.Dictionary<string, TraitModifier> Table =
            new System.Collections.Generic.Dictionary<string, TraitModifier>
            {
                { "donquixote_dream",         new TraitModifier { initiativeBonus = +2, aimBonus = -3 } },
                { "hermit_eye",               new TraitModifier { aimBonus = +5 } },
                { "little_hand_prodigy",      new TraitModifier { reactBonus = +2 } },
                { "wordless_comrade",         new TraitModifier { moraleFloor = +5 } },
                { "rocinante_owner",          new TraitModifier { initiativeBonus = +3, moraleFloor = +5 } },
                { "silent_worker",            new TraitModifier { reactBonus = +1 } },
            };

        /// <summary>특성 id 로 TraitModifier 조회. 미등록이면 빈 값.</summary>
        public static TraitModifier Get(string traitId)
        {
            if (string.IsNullOrEmpty(traitId)) return default;
            return Table.TryGetValue(traitId, out var mod) ? mod : default;
        }

        /// <summary>특성 SO가 null 이면 빈 값, 아니면 id로 조회.</summary>
        public static TraitModifier Get(TraitSO trait)
        {
            if (trait == null) return default;
            return Get(trait.id);
        }

        /// <summary>승무원 traits[] 배열 합산. 모든 특성의 효과 누적 (활성 조건 무시).</summary>
        public static TraitModifier SumForCrewMember(TraitSO[] traits)
        {
            var result = default(TraitModifier);
            if (traits == null || traits.Length == 0)
                return result;

            foreach (var t in traits.Where(t => t != null))
            {
                var mod = Get(t);
                result.initiativeBonus += mod.initiativeBonus;
                result.aimBonus        += mod.aimBonus;
                result.reactBonus      += mod.reactBonus;
                result.moraleFloor     += mod.moraleFloor;
            }

            return result;
        }

        /// <summary>
        /// 초기 상태(누적 카운트 0)에서 활성화된 특성만 합산. axisType == None 인 특성만 해당.
        /// 누적 카운트 기반 특성은 threshold 도달 전까지 효과 없음.
        /// </summary>
        public static TraitModifier SumActiveAtInit(TraitSO[] traits)
        {
            if (traits == null || traits.Length == 0) return default;
            var always = traits.Where(t => t != null && t.axisType == TraitAxis.None).ToArray();
            return SumForCrewMember(always);
        }
    }
}
