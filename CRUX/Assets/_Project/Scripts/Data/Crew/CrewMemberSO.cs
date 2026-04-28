using UnityEngine;

namespace Crux.Data
{
    /// <summary>
    /// 승무원 캐릭터 데이터 ScriptableObject.
    /// 고정 풀(20명)로 운영. 기본 스탯, 특성, 시작 마크 포함.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCrewMember", menuName = "Crux/Crew/Member")]
    public class CrewMemberSO : ScriptableObject
    {
        [Header("기본 정보")]
        [Tooltip("승무원 고유 ID")]
        public string id = "";

        [Tooltip("승무원 이름")]
        public string displayName = "";

        [Tooltip("초상화 스프라이트")]
        public Sprite portrait;

        [Header("직책")]
        [Tooltip("직책 (고정, 변경 불가)")]
        public CrewClass klass = CrewClass.None;

        [Header("기본 스탯 (1~100)")]
        [Range(1, 100)]
        [Tooltip("사격 정확도 기반값")]
        public int aim = 50;

        [Range(1, 100)]
        [Tooltip("오버워치·반응 사격 트리거·명중")]
        public int react = 50;

        [Range(1, 100)]
        [Tooltip("스킬 장착 가능량·수리·재장전")]
        public int tech = 50;

        [Header("선호 항목")]
        [Tooltip("선호 항목 타입 — 전차 클래스, 주포 종류, 탄종, 중량대, 수리 모듈")]
        public PreferredTagType preferredTagType = PreferredTagType.TankClass;

        [Tooltip("선호 항목 값 (예: Light, Medium, HE, AP 등)")]
        public string preferredTag = "";

        [Header("특성 — 누적 카운트 기반 개방형")]
        [Tooltip("보유 특성 배열 (0~2개, MVP는 1개)")]
        public TraitSO[] traits = new TraitSO[0];

        [Header("시작 마크 (병렬 배열)")]
        [Tooltip("마크 축 ID 배열 (예: MainGunCaliberLarge, MainGunCaliberMedium)")]
        public string[] startingMarkKeys = new string[0];

        [Tooltip("각 축별 마크 개수 (0~5)")]
        public int[] startingMarkValues = new int[0];

        [Header("스토리")]
        [Tooltip("배경 스토리 참조 또는 간단한 설명")]
        [TextArea(2, 4)]
        public string storyRef = "";

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(id))
            {
                id = name.ToLower().Replace(" ", "_");
            }

            // 마크 배열 길이 동기화
            if (startingMarkKeys.Length != startingMarkValues.Length)
            {
                if (startingMarkKeys.Length > startingMarkValues.Length)
                {
                    System.Array.Resize(ref startingMarkValues, startingMarkKeys.Length);
                }
                else
                {
                    System.Array.Resize(ref startingMarkKeys, startingMarkValues.Length);
                }
            }

            // 마크 값 범위 확인
            for (int i = 0; i < startingMarkValues.Length; i++)
            {
                startingMarkValues[i] = Mathf.Clamp(startingMarkValues[i], 0, 5);
            }
        }
#endif
    }
}
