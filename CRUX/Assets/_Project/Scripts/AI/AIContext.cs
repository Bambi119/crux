using System.Collections.Generic;
using UnityEngine;
using Crux.Grid;
using Crux.Unit;

namespace Crux.AI
{
    /// <summary>
    /// 적 AI가 한 턴 의사결정을 내리기 위해 필요한 모든 상황 정보 (스냅샷).
    /// EnemyAIController.Decide 호출 전 빌드됨. Read-only 취급.
    /// </summary>
    /// <remarks>기획 레퍼런스: docs/12_enemy_ai.md §2.1 SituationEval</remarks>
    public struct AIContext
    {
        /// <summary>결정을 내리는 유닛 (나)</summary>
        public GridTankUnit self;

        /// <summary>그리드 매니저 참조 — LOS/이동 계산 직접 수행 가능</summary>
        public GridManager grid;

        /// <summary>내 진영 (PlayerSide.Enemy 기준). 같은 진영 유닛 집합</summary>
        public List<GridTankUnit> allies;

        /// <summary>적 진영 (나에게 플레이어 + 플레이어측 유닛)</summary>
        public List<GridTankUnit> foes;

        /// <summary>내 현재 AP (self.CurrentAP 캐시)</summary>
        public int myAP;

        /// <summary>사거리 상한 (GameConstants.MaxFireRange)</summary>
        public int maxFireRange;

        /// <summary>현재 LOS로 보이는 적 목록 (foes 중 필터링)</summary>
        public List<GridTankUnit> visibleFoes;
    }
}
