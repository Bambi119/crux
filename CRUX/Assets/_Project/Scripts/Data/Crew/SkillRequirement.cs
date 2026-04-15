using UnityEngine;

namespace Crux.Data
{
    /// <summary>
    /// 스킬 장착 요구조건 구조체.
    /// 축(axis)에 대해 연산자(op)와 값 배열로 조건 표현.
    /// 예: MainGunCaliber IN [대] OR MainGunMechanism IN [다연장]
    /// </summary>
    [System.Serializable]
    public struct SkillRequirement
    {
        [Tooltip("조건 축 — 주포 구경, 기관총 종류 등")]
        public RequirementAxis axis;

        [Tooltip("연산자 — Any(OR), All(AND), None(제약 없음)")]
        public RequirementOp op;

        [Tooltip("축 값 배열 — 예: [소, 중, 대]")]
        public string[] values;

        public SkillRequirement(RequirementAxis axis, RequirementOp op, params string[] values)
        {
            this.axis = axis;
            this.op = op;
            this.values = values;
        }
    }
}
