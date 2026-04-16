namespace Crux.Data
{
    /// <summary>장갑 유형 — docs/05 §2.5. 면별 독립 장착 시 각 면의 장갑 속성</summary>
    public enum ArmorType
    {
        Light,          // 경장갑: 가볍고 도탄 유도 우선, 경사 보너스 큼
        Composite,      // 복합장갑: 범용 균형잡힌 방호
        Heavy,          // 중장갑: 고 방호·매우 무거움 — Heavy/Siege만 가능
        Reactive        // 리액티브: 관통 1회 완화·파손, 한정 재사용
    }
}
