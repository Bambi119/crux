using UnityEngine;

namespace Crux.Data
{
    /// <summary>
    /// 승무원 스킬 ScriptableObject.
    /// 패시브·액티브 즉시형·액티브 반응형 3종 스킬을 정의.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCrewSkill", menuName = "Crux/Crew/Skill")]
    public class CrewSkillSO : ScriptableObject
    {
        [Header("기본 정보")]
        [Tooltip("스킬 고유 ID")]
        public string id = "";

        [Tooltip("스킬 이름")]
        public string displayName = "";

        [Tooltip("스킬 효과 설명")]
        [TextArea(2, 4)]
        public string description = "";

        [Header("스킬 속성")]
        [Tooltip("스킬 타입 — 패시브 / 즉시형 / 반응형")]
        public SkillType type = SkillType.Passive;

        [Tooltip("대상 직책")]
        public CrewClass targetClass = CrewClass.None;

        [Header("요구조건")]
        [Tooltip("장착 요구조건 배열 (모두 만족해야 함)")]
        public SkillRequirement[] requires = new SkillRequirement[0];

        [Header("비용")]
        [Tooltip("즉시형 액티브 스킬의 AP 소비 (패시브는 0)")]
        public int apCost = 0;

        [Tooltip("스킬 쿨다운 (턴 단위)")]
        public int cooldown = 0;

        [Header("듀얼 시스템")]
        [Tooltip("이 스킬이 듀얼(강화 옵션)을 지원하는가")]
        public bool supportsDual = false;

        [Tooltip("듀얼 선택 시 추가 AP 비용")]
        public int dualExtraCost = 5;

        [Header("반응형 속성")]
        [Tooltip("반응형 스킬의 트리거 타입 (ActiveReactive일 때만 유의미)")]
        public ReactiveTrigger triggerType = ReactiveTrigger.None;

        [Header("효과")]
        [Tooltip("런타임 효과 실행 훅 ID")]
        public string effectKey = "";

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(id))
            {
                id = name.ToLower().Replace(" ", "_");
            }

            // 반응형이 아니면 triggerType을 None으로 유지
            if (type != SkillType.ActiveReactive)
            {
                triggerType = ReactiveTrigger.None;
            }

            // 패시브가 아니면 apCost·cooldown은 유의미
            if (type == SkillType.Passive)
            {
                apCost = 0;
            }
        }
#endif
    }
}
