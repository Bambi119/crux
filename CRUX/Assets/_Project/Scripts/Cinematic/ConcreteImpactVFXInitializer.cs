using UnityEngine;

namespace Crux.Cinematic
{
    /// <summary>ConcreteImpactVFX 루트에서 Debris/Dust 파티클 시스템 초기화 및 자동 제거.</summary>
    public class ConcreteImpactVFXInitializer : MonoBehaviour
    {
        private ParticleSystem debrisPS;
        private ParticleSystem dustPS;
        private float maxLifetime;

        private void Start()
        {
            // 자식 파티클 시스템 찾기
            debrisPS = transform.Find("Debris")?.GetComponent<ParticleSystem>();
            dustPS = transform.Find("Dust")?.GetComponent<ParticleSystem>();

            if (debrisPS != null)
                ParticleSystemConfig.ConfigureDebris(debrisPS);

            if (dustPS != null)
                ParticleSystemConfig.ConfigureDust(dustPS);

            // 둘 다의 lifetime 중 최대값으로 제거 시간 계산
            maxLifetime = 0;
            if (debrisPS != null)
                maxLifetime = Mathf.Max(maxLifetime, debrisPS.main.duration + 1f);
            if (dustPS != null)
                maxLifetime = Mathf.Max(maxLifetime, dustPS.main.duration + 1f);

            // 자동 제거 예약
            Destroy(gameObject, maxLifetime);
        }
    }
}
