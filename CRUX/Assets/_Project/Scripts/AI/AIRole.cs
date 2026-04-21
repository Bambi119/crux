using System.Collections.Generic;

namespace Crux.AI
{
    /// <summary>AI 역할 — Role별로 State 전이와 가중치 테이블이 달라짐</summary>
    public enum AIRole
    {
        Vehicle,   // 차량 (Scout Flanker)
        Light,     // 경전차 (Fast Striker)
        Medium,    // 중형 전차 (Balanced Opportunist) — P2 첫 구현
        Heavy,     // 중전차 (Anvil)
        Drone,     // 자폭 드론 (Suicide Runner)
        Infantry   // 보병 (Module Hunter)
    }

    /// <summary>AI State — Role × 상황 → 이 턴의 의도</summary>
    public enum AIState
    {
        Engage,      // 사격 준비/실행 — P2 첫 구현
        Flank,       // 우회 기동
        Reposition,  // 불리한 위치 이탈
        Retreat,     // 저HP 후퇴
        Guard,       // 정지 방어
        Suppress,    // 플랭커 견제
        Charge,      // 직진 돌진 (드론)
        Ambush,      // 매복 (구축전차)
        ModuleHunt,  // 모듈 파괴 (보병)
        Bombard      // 간접 사격 (자주포)
    }

    /// <summary>Role+State별 팩터 가중치 — 정적 테이블 (P3에서 SO로 이관 가능)</summary>
    public static class AIWeights
    {
        /// <summary>스코어 팩터 가중치 벡터</summary>
        public struct Weights
        {
            public float dist;         // 거리 (음수=접근, 양수=회피)
            public float cover;        // 엄폐
            public float flank;        // 측/후면 공격각
            public float exposure;     // 노출도 (항상 음수로 불리)
            public float kcs;          // Kill Confidence Score
            public float concealment;  // 은엄폐
            public float elev;         // 고도차
            public float facingHold;   // 전면 유지 (구축전차)
            public float modulePriority; // 모듈 우선도 (보병)
            public float proxAlly;     // 아군 근접 (팩 vs 분산)
            public float smokeCover;   // 자셀 연막 보너스 (방어적 포지셔닝)
        }

        private static readonly Dictionary<(AIRole, AIState), Weights> table = new()
        {
            // Vehicle (Scout Flanker)
            {
                (AIRole.Vehicle, AIState.Flank), new Weights
                {
                    flank = 3.0f,
                    cover = 2.5f,
                    exposure = -2.5f,
                    dist = -0.5f,
                    kcs = 1.5f,
                    concealment = 2.0f,
                    elev = 0.5f,
                    facingHold = 0f,
                    modulePriority = 0f,
                    proxAlly = 0f
                }
            },
            {
                (AIRole.Vehicle, AIState.Engage), new Weights
                {
                    dist = -1.5f,
                    cover = 1.5f,
                    kcs = 2.0f,
                    exposure = -2.0f,
                    flank = 2.0f,
                    concealment = 1.5f,
                    elev = 0.5f,
                    facingHold = 0f,
                    modulePriority = 0f,
                    proxAlly = 0f
                }
            },
            {
                (AIRole.Vehicle, AIState.Reposition), new Weights
                {
                    exposure = -3.5f,
                    cover = 3.0f,
                    concealment = 2.5f,
                    smokeCover = 2.5f,
                    dist = 0f,
                    kcs = 0f,
                    flank = 0f,
                    elev = 0f,
                    facingHold = 0f,
                    modulePriority = 0f,
                    proxAlly = 0f
                }
            },
            {
                (AIRole.Vehicle, AIState.Retreat), new Weights
                {
                    exposure = -4.0f,
                    cover = 2.5f,
                    smokeCover = 3.0f,
                    dist = 0f,
                    kcs = 0f,
                    flank = 0f,
                    concealment = 0f,
                    elev = 0f,
                    facingHold = 0f,
                    modulePriority = 0f,
                    proxAlly = 0f
                }
            },

            // Heavy (Anvil)
            {
                (AIRole.Heavy, AIState.Guard), new Weights
                {
                    cover = 3.0f,
                    facingHold = 5.0f,
                    proxAlly = 2.0f,
                    dist = 0.1f,
                    exposure = -1.0f,
                    kcs = 0f,
                    flank = 0f,
                    concealment = 0f,
                    elev = 0f,
                    modulePriority = 0f
                }
            },
            {
                (AIRole.Heavy, AIState.Suppress), new Weights
                {
                    kcs = 3.0f,
                    flank = -2.0f,
                    cover = 2.0f,
                    exposure = -1.5f,
                    dist = 0f,
                    concealment = 0f,
                    elev = 0f,
                    facingHold = 0f,
                    modulePriority = 0f,
                    proxAlly = 0f
                }
            },

            // Medium (Balanced Opportunist) — P2 첫 구현
            {
                (AIRole.Medium, AIState.Engage), new Weights
                {
                    dist = -1.0f,   // 가까울수록 +
                    cover = 2.0f,
                    kcs = 2.5f,
                    exposure = -1.5f,
                    flank = 0f,
                    concealment = 0.5f,
                    elev = 1.0f,
                    facingHold = 0f,
                    modulePriority = 0f,
                    proxAlly = 0f
                }
            },
            {
                (AIRole.Medium, AIState.Flank), new Weights
                {
                    flank = 3.0f,
                    dist = -1.0f,
                    cover = 1.0f,
                    kcs = 2.0f,
                    exposure = -1.5f,
                    concealment = 0.5f,
                    elev = 0f,
                    facingHold = 0f,
                    modulePriority = 0f,
                    proxAlly = 0f
                }
            },
            {
                (AIRole.Medium, AIState.Reposition), new Weights
                {
                    exposure = -3.0f,
                    cover = 2.5f,
                    smokeCover = 2.0f,
                    dist = 0f,
                    kcs = 0f,
                    flank = 0f,
                    concealment = 0f,
                    elev = 0f,
                    facingHold = 0f,
                    modulePriority = 0f,
                    proxAlly = 0f
                }
            },
            {
                (AIRole.Medium, AIState.Retreat), new Weights
                {
                    exposure = -3.5f,
                    cover = 2.5f,
                    smokeCover = 2.5f,
                    dist = 0f,
                    kcs = 0f,
                    flank = 0f,
                    concealment = 0f,
                    elev = 0f,
                    facingHold = 0f,
                    modulePriority = 0f,
                    proxAlly = 0f
                }
            },
            {
                (AIRole.Heavy, AIState.Retreat), new Weights
                {
                    exposure = -3.0f,
                    cover = 3.0f,
                    smokeCover = 2.0f,
                    facingHold = 1.0f,
                    dist = 0f,
                    kcs = 0f,
                    flank = 0f,
                    concealment = 0f,
                    elev = 0f,
                    modulePriority = 0f,
                    proxAlly = 0f
                }
            },
            // 이후 Light/Drone/Infantry은 P3에서 채워짐
        };

        public static Weights Get(AIRole role, AIState state)
        {
            if (table.TryGetValue((role, state), out var w)) return w;
            // Fallback: Medium/Engage 기본값
            return table[(AIRole.Medium, AIState.Engage)];
        }
    }
}
