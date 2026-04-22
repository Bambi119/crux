namespace Crux.UI.Hangar
{
    // 각 격납고 탭 바인더가 구현할 인터페이스. docs/10b §7.2.
    // HangarController가 탭 목록을 등록하고 활성 탭에만 Tick을 전달한다.
    public interface ITabModule
    {
        HangarTab Tab { get; }

        void Initialize(IHangarStateReadOnly state, IHangarBus bus);

        void OnEnter();

        void OnLeave();

        void Tick(float deltaTime);
    }
}
