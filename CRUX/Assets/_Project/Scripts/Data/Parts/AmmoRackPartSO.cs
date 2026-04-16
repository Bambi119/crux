using UnityEngine;

namespace Crux.Data
{
    /// <summary>
    /// 탄약고 파츠 데이터 — docs/05 §2.4.
    /// 탄약 보유량, 탄종 수용 범위, 유폭 위험을 정의.
    /// 탄약고는 **물리적 저장 모듈**. AmmoDataSO는 별개의 **탄종 데이터**.
    /// 다수 탄종 혼합 가능하나 슬롯 수 제한. 유폭 위험 시 관통 확률↑.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAmmoRack", menuName = "CRUX/Parts/AmmoRack")]
    public class AmmoRackPartSO : PartDataSO
    {
        [Header("탄약고 용량")]
        [Tooltip("주포 탄약 최대 보유량 (탄수)")]
        public int maxMainGunAmmo = 30;

        [Tooltip("기관총 탄약 최대 보유량 (총 탄수)")]
        public int maxMGAmmo = 500;

        [Header("호환성")]
        [Tooltip("수용 가능 탄종 수 (대부분 2~3종, 대형 탄약고는 4종)")]
        public int ammoTypeSlots = 2;

        [Tooltip("규격 호환 제약. 예: 'Scout제약' → Scout 차체에 미장착. 비어있으면 호환성 없음")]
        public string[] hullClassRestrictions;

        [Header("전투 판정")]
        [Tooltip("유폭 위험도 (0~1). 측·후면 피격 시 관통 확률↑")]
        public float ammoExplosionRisk = 0.5f;

        private void OnEnable()
        {
            category = PartCategory.AmmoRack;
        }
    }
}
