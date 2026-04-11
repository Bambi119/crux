using UnityEngine;
using Crux.Core;

namespace Crux.Data
{
    [CreateAssetMenu(fileName = "NewCover", menuName = "CRUX/Cover Data")]
    public class CoverDataSO : ScriptableObject
    {
        [Header("기본 정보")]
        public string coverName;
        public CoverSize size = CoverSize.Medium;

        [Header("내구력")]
        public float maxHP = 80f;

        [Header("엄폐 성능")]
        [Tooltip("최대 엄폐율 (0~1)")]
        [Range(0f, 1f)]
        public float maxCoverRate = 0.65f;

        [Tooltip("커버 범위 (도) — 전방 기준, 최대 180°")]
        [Range(45f, 180f)]
        public float coverArc = 135f;

        [Header("방호")]
        [Tooltip("엄폐물 자체 장갑 (mm) — 이 값 이하 관통력은 엄폐물을 뚫지 못함")]
        public float armorValue = 30f;

        [Header("시각")]
        public Sprite intactSprite;
        public Sprite damagedSprite;
        public Sprite destroyedSprite;

        [Header("파괴 효과")]
        [Tooltip("파괴 시 파편 파티클 프리팹")]
        public GameObject debrisParticlePrefab;
    }
}
