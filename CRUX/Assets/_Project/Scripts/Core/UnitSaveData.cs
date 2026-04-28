using UnityEngine;

namespace Crux.Core
{
    /// <summary>전투 상태 스냅샷 — 유닛 1기 분량</summary>
    [System.Serializable]
    public struct UnitSaveData
    {
        public Vector2Int gridPosition;
        public float currentHP;
        public int currentAP;
        public float hullAngle;
        public bool isDestroyed;
        public Unit.ModuleSaveData[] moduleSaves;
        public bool isOnFire;
        public int fireTurnsLeft;
        public int consecutiveMisses;
        public int remainingSmokeCharges;
        public int mainGunAmmoCount;
        public int mgAmmoLoaded;
        public int mgAmmoTotal;
        public bool isOverwatching;
        public bool isCounterImmune;           // 반격 면역 (오버워치 중)
        public bool hasCounteredThisExchange;  // 이번 교환에서 이미 반격 실행
        public bool counterConfirmed;          // 플레이어 반격 확정 여부
    }
}
