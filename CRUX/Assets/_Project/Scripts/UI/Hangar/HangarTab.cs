namespace Crux.UI.Hangar
{
    // 격납고 5탭 식별자. docs/10b §2.1 탭 목록 기준.
    // Composition(편성) / Maintenance(정비)는 첫 빌드 활성, 나머지 3종은 잠금.
    public enum HangarTab
    {
        Composition = 0,
        Maintenance = 1,
        Shop = 2,
        Mess = 3,
        People = 4
    }
}
