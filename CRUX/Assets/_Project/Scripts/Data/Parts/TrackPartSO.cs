using UnityEngine;

namespace Crux.Data
{
    /// <summary>
    /// 캐터필러(궤도) 파츠 데이터 — docs/05 §2.6.
    /// 기동성, 내구, 지형 적응을 정의.
    /// 캐터필러 파손 시 이동 AP 2배. 기총사수/수리병 마크로 전투 중 복구 가능.
    /// </summary>
    [CreateAssetMenu(fileName = "NewTrack", menuName = "CRUX/Parts/Track")]
    public class TrackPartSO : PartDataSO
    {
        [Header("캐터필러 성능")]
        [Tooltip("기본 이동 AP 계산에 영향. +값은 기동성 개선, -값은 둔화")]
        public int mobilityBonus = 0;

        [Tooltip("내구도. 피격 시 파손 확률 기준")]
        public float durability = 100f;

        [Tooltip("특정 지형(습지·파편·산림)에서 이동 AP 보너스")]
        public float terrainAdaptation = 0f;

        private void OnEnable()
        {
            category = PartCategory.Track;
        }
    }
}
