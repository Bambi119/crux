using UnityEngine;

namespace Crux.Data
{
    /// <summary>
    /// 주포 파츠 데이터 — docs/05 §2.3.
    /// 구경, 연사 속도, 기본 관통력, 기본 데미지, 호환 탄종을 정의.
    /// 구경과 포수 마크 축은 직결 (docs/04 §3).
    /// </summary>
    [CreateAssetMenu(fileName = "NewMainGun", menuName = "CRUX/Parts/MainGun")]
    public class MainGunPartSO : PartDataSO
    {
        [Header("주포 기본 스펙")]
        [Tooltip("주포 구경 (mm). 예: 소=45, 중=75, 대=120. 포탑의 caliberLimit과 비교 검사")]
        public int caliber = 75;

        [Tooltip("연사 속도 (턴당 발사 횟수). 1.0 = 턴당 1회, 2.0 = 턴당 2회")]
        public float rateOfFire = 1f;

        [Header("사격 성능")]
        [Tooltip("기본 관통력 (mm). 거리 감쇠·경사 보정 적용 전 기초값")]
        public float basePenetration = 120f;

        [Tooltip("기본 데미지")]
        public float baseDamage = 40f;

        [Header("탄종 호환성")]
        [Tooltip("이 주포가 발사 가능한 탄종들. AmmoDataSO 참조 배열")]
        public AmmoDataSO[] compatibleAmmo;

        private void OnEnable()
        {
            category = PartCategory.MainGun;
        }
    }
}
