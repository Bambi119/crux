using Crux.Combat;

namespace Crux.Core
{
    /// <summary>데미지를 받을 수 있는 오브젝트</summary>
    public interface IDamageable
    {
        void TakeDamage(DamageInfo info);
        float CurrentHP { get; }
        bool IsDestroyed { get; }
    }
}
