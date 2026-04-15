using UnityEngine;

namespace Crux.Data
{
    /// <summary>
    /// 승무원 특성(장점/약점) ScriptableObject.
    /// 캐릭터당 장점 1 + 약점 1 고정. 생성 시 결정, 제거 불가.
    /// </summary>
    [CreateAssetMenu(fileName = "NewTrait", menuName = "Crux/Crew/Trait")]
    public class TraitSO : ScriptableObject
    {
        [Header("기본 정보")]
        [Tooltip("특성 고유 ID (예: cold_blooded, careless, charming)")]
        public string id = "";

        [Tooltip("특성 이름")]
        public string displayName = "";

        [Tooltip("특성 설명")]
        [TextArea(2, 4)]
        public string description = "";

        [Header("효과")]
        [Tooltip("긍정 특성인지 여부")]
        public bool isPositive = true;

        [Tooltip("직책 제약 — None이면 모든 직책 가능")]
        public CrewClass classRestriction = CrewClass.None;

        [Tooltip("런타임 효과 훅 ID (예: add_aim_penalty, ignore_morale_drop)")]
        public string effectKey = "";

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
