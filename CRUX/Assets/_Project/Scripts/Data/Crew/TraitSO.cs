using UnityEngine;

namespace Crux.Data
{
    /// <summary>
    /// 승무원 특성(개방형 단일 축) ScriptableObject.
    /// 특성은 더 이상 "장점/약점" 쌍이 아니라 "누적 카운트 기반 잠금 해제"로 운영된다.
    /// 캐릭터당 0~2개 트레잇 가능 (MVP는 1개).
    /// docs/04 §3.5.7 참조.
    /// </summary>
    [CreateAssetMenu(fileName = "NewTrait", menuName = "Crux/Crew/Trait")]
    public class TraitSO : ScriptableObject
    {
        [Header("기본 정보")]
        [Tooltip("특성 고유 ID (예: donquixote_dream, hermit_eye)")]
        public string id = "";

        [Tooltip("특성 이름")]
        public string displayName = "";

        [Tooltip("특성 설명")]
        [TextArea(2, 4)]
        public string description = "";

        [Header("잠금 해제 조건")]
        [Tooltip("직책 제약 — None이면 모든 직책 가능")]
        public CrewClass classRestriction = CrewClass.None;

        [Tooltip("이 특성을 활성화하는 축 (누적 카운트 기준)")]
        public TraitAxis axisType = TraitAxis.None;

        [Tooltip("활성화 누적 카운트 임계값")]
        public int axisThreshold = 10;

        [Tooltip("잠금 해제 시 획득하는 고유 스킬 (Phase 2, null 허용)")]
        public CrewSkillSO signatureSkillRef;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(id))
            {
                id = name.ToLower().Replace(" ", "_");
            }
        }
#endif
    }
}
