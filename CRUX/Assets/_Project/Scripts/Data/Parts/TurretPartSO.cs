using UnityEngine;

namespace Crux.Data
{
    /// <summary>
    /// 포탑 파츠 데이터 — docs/05 §2.2.
    /// 회전 속도, 장착 가능 주포 구경 제한, 무게, 고정포대 여부를 정의.
    /// </summary>
    [CreateAssetMenu(fileName = "NewTurret", menuName = "CRUX/Parts/Turret")]
    public class TurretPartSO : PartDataSO
    {
        [Header("포탑 성능")]
        [Tooltip("포탑 회전 속도 기준값 (조종수 마크·스킬로 추가 보정)")]
        public float rotationSpeed = 60f;

        [Tooltip("장착 가능 최대 주포 구경 (mm). 예: 소=45, 중=75, 대=120")]
        public int caliberLimit = 75;

        [Tooltip("포탑 안정화 보정 수치 — 움직임 중 명중 페널티 완화")]
        public float stabilization = 0f;

        [Tooltip("고정포대 모드 (Siege 일부만). true시 회전 불가 대신 안정성 우수")]
        public bool isFixedEmbrasure = false;

        private void OnEnable()
        {
            category = PartCategory.Turret;
        }
    }
}
