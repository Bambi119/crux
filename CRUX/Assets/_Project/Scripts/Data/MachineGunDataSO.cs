using UnityEngine;
using Crux.Core;

namespace Crux.Data
{
    [CreateAssetMenu(fileName = "NewMG", menuName = "CRUX/Machine Gun Data")]
    public class MachineGunDataSO : ScriptableObject
    {
        [Header("기본 정보")]
        public string mgName;
        public WeaponType type = WeaponType.CoaxialMG;
        [Tooltip("기관총 구경 (mm)")]
        public float caliber = 7.92f;

        [Header("성능")]
        [Tooltip("1회 공격 시 발사 횟수 (2~12)")]
        public int burstCount = 6;

        [Tooltip("탄당 데미지")]
        public float damagePerShot = 2f;

        [Tooltip("탄당 관통력 (mm)")]
        public float penetration = 15f;

        [Tooltip("AP 비용")]
        public int apCost = 2;

        [Header("정확도")]
        [Tooltip("기본 명중률 보정 (기관총은 주포보다 낮음)")]
        public float accuracyModifier = -0.15f;

        [Tooltip("발당 산포 (도)")]
        public float spreadPerShot = 8f;
    }
}
