using UnityEngine;

namespace Crux.Cinematic
{
    /// <summary>
    /// 폭발 VFX 루트 — 자식 파티클 초기화 + 자동 제거.
    /// useCodeDefaults 체크 시에만 ParticleSystemConfig 코드 세팅 강제.
    /// 기본값(false)은 **Inspector 값 존중** — 사용자 직접 튜닝 가능.
    /// </summary>
    public class ConcreteImpactVFXInitializer : MonoBehaviour
    {
        [Tooltip("체크 시 코드 기본값으로 세팅(Inspector 값 덮어씀). 끄면 Inspector 값 그대로 재생. 기본 true — 첫 재생 시 폭발 프리셋.")]
        [SerializeField] private bool useCodeDefaults = true;

        [Tooltip("체크 시 URP 2D 비호환 머티리얼을 Sprites/Default로 자동 교체 (핑크 방지).")]
        [SerializeField] private bool autoFixMaterial = true;

        [Tooltip("자동 제거까지 여유 시간(초)")]
        [SerializeField] private float destroyPadding = 0.5f;

        private void Start()
        {
            var sparks = FindPS("Sparks") ?? FindPS("Debris");
            var flash = FindPS("Flash");
            var fire = FindPS("Fire");
            var smoke = FindPS("Smoke") ?? FindPS("Dust");
            var core = FindPS("CoreBlast");
            var shock = FindPS("Shockwave");

            if (useCodeDefaults)
            {
                if (sparks != null) ParticleSystemConfig.ConfigureSparks(sparks);
                if (flash != null) ParticleSystemConfig.ConfigureFlash(flash);
                if (fire != null) ParticleSystemConfig.ConfigureFire(fire);
                if (smoke != null) ParticleSystemConfig.ConfigureSmoke(smoke);
                if (core != null) ParticleSystemConfig.ConfigureCoreBlast(core);
                if (shock != null) ParticleSystemConfig.ConfigureShockwave(shock);
            }

            if (autoFixMaterial)
            {
                var orange = ParticleSystemConfig.GetOrangeEmissiveMaterial();
                var smokeMat = ParticleSystemConfig.GetSmokeMaterial();
                FixMaterial(sparks, orange);
                FixMaterial(flash, orange);
                FixMaterial(fire, orange);
                FixMaterial(smoke, smokeMat);
                FixMaterial(core, orange);
                FixMaterial(shock, orange);
            }

            // 최대 수명 계산 → Destroy 예약
            float maxLifetime = 0;
            foreach (var ps in new[] { sparks, flash, fire, smoke, core, shock })
                AccumulateMaxLifetime(ps, ref maxLifetime);
            if (maxLifetime <= 0) maxLifetime = 2f;

            Destroy(gameObject, maxLifetime + destroyPadding);
        }

        private ParticleSystem FindPS(string name)
        {
            var t = transform.Find(name);
            return t != null ? t.GetComponent<ParticleSystem>() : null;
        }

        private void AccumulateMaxLifetime(ParticleSystem ps, ref float maxLifetime)
        {
            if (ps == null) return;
            var main = ps.main;
            maxLifetime = Mathf.Max(maxLifetime, main.duration + main.startLifetime.constantMax);
        }

        private void FixMaterial(ParticleSystem ps, Material fallback)
        {
            if (ps == null || fallback == null) return;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;

            // URP 2D에서는 Unity 기본 Default-Particle이 호환 안 되어 핑크 렌더.
            // autoFixMaterial=true 면 무조건 fallback으로 덮어씀.
            renderer.sharedMaterial = fallback;
        }
    }
}
