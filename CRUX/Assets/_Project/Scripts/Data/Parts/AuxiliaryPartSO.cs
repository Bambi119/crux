using UnityEngine;

namespace Crux.Data
{
    /// <summary>
    /// 보조 장비 파츠 데이터 — docs/05 §2.7.
    /// 자유 배치 슬롯(차체별 1~4개).
    /// 연막탄·조준 보조·통신 강화·적외선 스코프·응급 키트·부스터 등.
    /// effectType 문자열로 효과를 범용 식별. 실행은 런타임 시스템에서 처리.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAuxiliary", menuName = "CRUX/Parts/Auxiliary")]
    public class AuxiliaryPartSO : PartDataSO
    {
        [Header("보조 장비 효과")]
        [Tooltip("효과 유형 식별자. 예: 'SmokeLauncher', 'AimAssist', 'ComLink', 'ThermalScope', 'MedKit', 'Booster'")]
        public string effectType = "Generic";

        [Tooltip("소모성 장비의 사용 횟수. 0 = 무한 (지속 활성화)")]
        public int charges = 0;

        [Tooltip("효과 수치 (범용). 용도: 조준 보정치, 통신 범위 증가, 부스터 AP 추가값 등")]
        public float effectValue = 5f;

        [Tooltip("효과 설명 (런타임 UI 표시용)")]
        [TextArea(1, 3)]
        public string effectDescription;

        private void OnEnable()
        {
            category = PartCategory.Auxiliary;
        }
    }
}
