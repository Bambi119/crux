using UnityEngine;

namespace Crux.Data
{
    /// <summary>
    /// 장갑 파츠 데이터 — docs/05 §2.5.
    /// 면별 독립 장착. 각 면(전·측L·측R·후) 별도 슬롯에 이 SO 인스턴스를 배치.
    /// 장갑 유형에 따라 방호력·무게·경사 보정이 상이.
    /// </summary>
    [CreateAssetMenu(fileName = "NewArmor", menuName = "CRUX/Parts/Armor")]
    public class ArmorPartSO : PartDataSO
    {
        [Header("장갑 속성")]
        [Tooltip("장갑 유형 (Light/Composite/Heavy/Reactive)")]
        public ArmorType armorType = ArmorType.Composite;

        [Tooltip("기본 방어력 (mm). 관통력 계산에서 피격 시 감산")]
        public float baseProtection = 80f;

        [Tooltip("경사(입사각) 보정 수치. Light 우수, Heavy 낮음")]
        public float angleModifier = 1.2f;

        [Tooltip("리액티브 장갑 경우 무게 페널티. 일반 장갑은 0")]
        public float reactiveWeight = 0f;

        private void OnEnable()
        {
            category = PartCategory.Armor;
        }
    }
}
