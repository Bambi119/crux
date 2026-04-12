using UnityEngine;
using Crux.Core;

namespace Crux.Data
{
    [CreateAssetMenu(fileName = "NewAmmo", menuName = "CRUX/Ammo Data")]
    public class AmmoDataSO : ScriptableObject
    {
        [Header("기본 정보")]
        public string ammoName;
        public AmmoType type;
        [Tooltip("탄종 축약 표기 (예: AP, HE, HEAT)")]
        public string shortCode = "AP";

        [Header("성능")]
        [Tooltip("관통력 (mm)")]
        public float penetration = 100f;

        [Tooltip("관통 시 데미지")]
        public float damage = 30f;

        [Tooltip("폭발 반경 (HE용, 셀 단위)")]
        public float blastRadius = 0f;

        [Header("시각 연출")]
        [Tooltip("탄속 (시각 연출용)")]
        public float velocity = 3f;

        [Tooltip("포탄 스프라이트")]
        public Sprite projectileSprite;

        [Header("거리 감쇠")]
        [Tooltip("셀당 관통력 감소")]
        public float penetrationDropPerCell = 0f;
    }
}
