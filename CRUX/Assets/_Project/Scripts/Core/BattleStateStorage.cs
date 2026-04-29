namespace Crux.Core
{
    /// <summary>씬 전환 시 전투 상태 보존 (static)</summary>
    public static class BattleStateStorage
    {
        public static bool HasSavedState;
        public static BattleSaveData SavedState;

        /// <summary>연출 씬 종료 후 복귀할 전략 씬 이름 (비어있으면 StrategyScene 기본)</summary>
        public static string SourceScene;

        /// <summary>
        /// 마지막으로 플레이어를 공격한 적 유닛의 enemyUnits 인덱스.
        /// -1이면 해당 정보 없음 (플레이어 사격, 또는 반격 세션 외 상황).
        /// </summary>
        public static int LastEnemyAttackerIndex = -1;

        public static void Save(BattleSaveData data)
        {
            SavedState = data;
            HasSavedState = true;
        }

        public static void SaveLastAttacker(int enemyIndex)
        {
            LastEnemyAttackerIndex = enemyIndex;
        }

        public static void Clear()
        {
            HasSavedState = false;
            LastEnemyAttackerIndex = -1;
        }
    }
}
