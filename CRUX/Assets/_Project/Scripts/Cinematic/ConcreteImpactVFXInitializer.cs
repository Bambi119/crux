using UnityEngine;

namespace Crux.Cinematic
{
    /// <summary>
    /// 폭발 VFX 루트 — Sparks/Flash/Fire/Smoke 4개 자식 파티클 초기화 + 자동 제거.
    /// Legacy 이름(Debris/Dust)도 Fallback 지원.
    /// </summary>
    public class ConcreteImpactVFXInitializer : MonoBehaviour
    {
        private float maxLifetime;

        private void Start()
        {
            // 우선 신규 4단 구조
            var sparks = FindPS("Sparks") ?? FindPS("Debris");
            var flash = FindPS("Flash");
            var fire = FindPS("Fire");
            var smoke = FindPS("Smoke") ?? FindPS("Dust");

            if (sparks != null) ParticleSystemConfig.ConfigureSparks(sparks);
            if (flash != null) ParticleSystemConfig.ConfigureFlash(flash);
            if (fire != null) ParticleSystemConfig.ConfigureFire(fire);
            if (smoke != null) ParticleSystemConfig.ConfigureSmoke(smoke);

            // 최대 수명 계산
            maxLifetime = 0;
            AccumulateMaxLifetime(sparks);
            AccumulateMaxLifetime(flash);
            AccumulateMaxLifetime(fire);
            AccumulateMaxLifetime(smoke);
            if (maxLifetime <= 0) maxLifetime = 1.5f;

            Destroy(gameObject, maxLifetime + 0.3f);
        }

        private ParticleSystem FindPS(string name)
        {
            var t = transform.Find(name);
            return t != null ? t.GetComponent<ParticleSystem>() : null;
        }

        private void AccumulateMaxLifetime(ParticleSystem ps)
        {
            if (ps == null) return;
            var main = ps.main;
            maxLifetime = Mathf.Max(maxLifetime, main.duration + main.startLifetime.constantMax);
        }
    }
}
