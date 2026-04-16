namespace Crux.Data
{
    /// <summary>파츠 카테고리 — docs/05 §2. 7가지 기본 카테고리</summary>
    public enum PartCategory
    {
        Engine,         // 엔진 (출력·중량·연비·과열)
        Turret,         // 포탑 (회전 속도·구경 제한·고정포대)
        MainGun,        // 주포 (구경·연사·관통력·데미지)
        AmmoRack,       // 탄약고 (탄약 용량·탄종 수용)
        Armor,          // 장갑 (면별 독립 장착, Light/Composite/Heavy/Reactive)
        Track,          // 캐터필러 (기동성·내구·지형 적응)
        Auxiliary       // 보조 장비 (연막탄·조준 보조·통신 강화 등)
    }
}
