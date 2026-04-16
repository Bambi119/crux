using UnityEngine;

namespace Crux.Data
{
    /// <summary>
    /// 엔진 파츠 데이터 — docs/05 §2.1.
    /// 출력, 연비, 과열 한계를 정의.
    /// 출력 부족 → 이동 AP 증가, 회전 AP 증가. 여유 출력 많음 → 중장갑 탑재 여유.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEngine", menuName = "CRUX/Parts/Engine")]
    public class EnginePartSO : PartDataSO
    {
        [Header("엔진 성능")]
        [Tooltip("공급 출력값. 차체 최소 출력 요구 + 장착 파츠 출력 수요 합보다 커야 함")]
        public float powerOutput = 100f;

        [Tooltip("전략 맵 이동 거리 당 연료 소모량 (현재 전투 영향 없음)")]
        public float efficiency = 0.5f;

        [Tooltip("매 턴 부스트·중량 초과 시 누적되는 과열 한계값")]
        public float overheatLimit = 80f;

        private void OnEnable()
        {
            category = PartCategory.Engine;
        }
    }
}
