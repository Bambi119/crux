using UnityEngine;

namespace Crux.Data
{
    /// <summary>
    /// PartDataSO를 런타임에 감싸는 인스턴스.
    /// 원본 SO는 read-only 참조, 인스턴스는 내구도·사용 횟수 등 가변 상태 보유.
    /// 편성 씬에서 한 부대의 모든 파츠 인벤토리·장착 상태를 표현할 때 사용.
    ///
    /// P4-c: 편성 모델용 런타임 래퍼.
    /// </summary>
    public class PartInstance
    {
        public PartDataSO data { get; }
        public float durability;
        public int chargesRemaining;
        public string instanceId;

        private bool originallyHadCharges;

        /// <summary>
        /// PartInstance 생성.
        /// </summary>
        /// <param name="data">파츠 데이터 SO (null이면 내부 체크)</param>
        /// <param name="instanceId">고유 인스턴스 ID. null이면 자동 생성</param>
        public PartInstance(PartDataSO data, string instanceId = null)
        {
            this.data = data;
            this.instanceId = instanceId ?? System.Guid.NewGuid().ToString("N").Substring(0, 8);

            durability = 1.0f; // 100% 상태로 시작

            // Auxiliary 파츠면 charges 설정, 아니면 -1 (non-applicable)
            if (data is AuxiliaryPartSO auxPart)
            {
                chargesRemaining = auxPart.charges;
                originallyHadCharges = auxPart.charges > 0;
            }
            else
            {
                chargesRemaining = -1;
                originallyHadCharges = false;
            }
        }

        /// <summary>파츠 카테고리 (데이터 SO에서 읽음)</summary>
        public PartCategory Category => data != null ? data.category : default;

        /// <summary>파츠가 기능 중인지 (내구도 > 0 && 소모 미진행)</summary>
        public bool IsFunctional => durability > 0f && !IsDepleted;

        /// <summary>소모성 파츠가 모두 사용되었는지</summary>
        public bool IsDepleted => originallyHadCharges && chargesRemaining == 0;
    }
}
