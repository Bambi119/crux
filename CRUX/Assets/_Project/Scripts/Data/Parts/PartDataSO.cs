using UnityEngine;

namespace Crux.Data
{
    /// <summary>
    /// 파츠 데이터 기본 클래스 — abstract SO.
    /// 모든 파츠(Engine·Turret·MainGun·AmmoRack·Armor·Track·Auxiliary)의 공통 필드 정의.
    /// 호환성 3중 체계: 하중(Weight) · 출력(PowerDraw) · 규격(SpecTags).
    /// 각 서브클래스는 별도 파일에서 [CreateAssetMenu] 지정.
    /// </summary>
    public abstract class PartDataSO : ScriptableObject
    {
        [Header("공통 정보")]
        public string partName;
        public PartCategory category;
        [TextArea(2, 3)]
        public string description;

        [Header("호환성 축 — 3중 체계")]
        [Tooltip("파츠 무게 (kg). 차체 하중 한도에 소모")]
        public float weight = 10f;

        [Tooltip("파츠 전력 수요 (전력 단위). 엔진 출력이 이 값을 충족해야 장착 가능")]
        public float powerDraw = 5f;

        [Header("규격 태그")]
        [Tooltip("규격 호환성 식별용 태그 배열. 예: 'SmallCaliberTurret', 'HeavyAmmoRack'. null이면 호환성 제약 없음")]
        public string[] specTags;
    }
}
