using System.Collections.Generic;

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
    /// TraitSO.effectKey 를 TraitModifier 로 변환하는 dispatch 테이블.
    /// 밸런스 확정 전 placeholder 값 — 실제 수치는 튜닝 패스에서 조정.
    /// </summary>
    public static class TraitEffects
    {
        // TODO(balance): 수치는 임시 — docs/04 승무원 특성 밸런스 섹션 확정 후 조정
        static readonly Dictionary<string, TraitModifier> Table = new Dictionary<string, TraitModifier>
        {
            { "trait.donquixote_dream",      new TraitModifier { initiativeBonus = +2, aimBonus = -3 } },
            { "trait.first_battle_fear",     new TraitModifier { initiativeBonus = -3, moraleFloor = -10 } },
            { "trait.hermit_eye",            new TraitModifier { aimBonus = +5 } },
            { "trait.dialogue_phobia",       new TraitModifier { initiativeBonus = -1 } },
            { "trait.little_hand_prodigy",   new TraitModifier { reactBonus = +2 } },
            { "trait.wordless_comrade",      new TraitModifier { moraleFloor = +5 } },
            { "trait.rocinante_owner",       new TraitModifier { initiativeBonus = +3, moraleFloor = +5 } },
            { "trait.brother_dependent",     new TraitModifier { moraleFloor = +5 } },
            { "trait.silent_worker",         new TraitModifier { reactBonus = +1 } },
            { "trait.spoiled",               new TraitModifier { moraleFloor = -5 } },
        };

        /// <summary>effectKey 가 등록돼 있으면 해당 TraitModifier 반환, 아니면 빈 값.</summary>
        public static TraitModifier Get(string effectKey)
        {
            if (string.IsNullOrEmpty(effectKey)) return default;
            return Table.TryGetValue(effectKey, out var mod) ? mod : default;
        }

        /// <summary>특성 SO가 null 이거나 effectKey 없으면 빈 값 반환.</summary>
        public static TraitModifier Get(TraitSO trait)
        {
            if (trait == null) return default;
            return Get(trait.effectKey);
        }

        /// <summary>승무원 한 명의 trait 2개(장점+약점) 합산 delta. 필드별 합.</summary>
        public static TraitModifier SumForCrewMember(TraitSO positive, TraitSO negative)
        {
            var p = Get(positive);
            var n = Get(negative);
            return new TraitModifier
            {
                initiativeBonus = p.initiativeBonus + n.initiativeBonus,
                aimBonus        = p.aimBonus        + n.aimBonus,
                reactBonus      = p.reactBonus      + n.reactBonus,
                moraleFloor     = p.moraleFloor     + n.moraleFloor,
            };
        }
    }
}
