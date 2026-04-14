using UnityEngine;
using Crux.Unit;

namespace Crux.AI
{
    /// <summary>AI가 한 턴에 수행할 행동 — 이동 + 사격 (둘 다 선택적)</summary>
    public struct AIDecision
    {
        /// <summary>이동 목적지 (null-sentinel: Vector2Int.one * -1 = 이동 없음)</summary>
        public Vector2Int? moveTo;

        /// <summary>사격 대상 (null = 사격 없음)</summary>
        public GridTankUnit fireTarget;

        /// <summary>최종 선택 스코어 (디버그 로그용)</summary>
        public float score;

        /// <summary>선택된 상태 (디버그 로그용)</summary>
        public AIState state;

        /// <summary>대기만 하는 행동</summary>
        public static AIDecision Wait(AIState state) =>
            new AIDecision { moveTo = null, fireTarget = null, score = 0f, state = state };
    }
}
