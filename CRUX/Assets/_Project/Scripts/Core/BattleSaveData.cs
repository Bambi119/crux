namespace Crux.Core
{
    /// <summary>전투 전체 상태 스냅샷 — 씬 전환 보존용</summary>
    [System.Serializable]
    public struct BattleSaveData
    {
        public UnitSaveData playerState;
        public UnitSaveData[] enemyStates;
        public int turnCount;
        public TurnPhase phase;

        // 엄폐물 HP
        public float[] coverHPs;

        // 연막 셀 상태
        public int[] smokeTurns; // 전체 셀 순회, 연막 잔여 턴

        // 적 턴 중 사격 시 — 다음에 행동할 적 인덱스
        public int nextEnemyIndex;
    }
}
