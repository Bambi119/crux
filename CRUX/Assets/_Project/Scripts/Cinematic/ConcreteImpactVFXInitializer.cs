using UnityEngine;

namespace Crux.Cinematic
{
    /// <summary>
    /// 폭발 VFX 루트 — 자식 파티클 초기화 + 자동 제거.
    /// Sparks/Flash/Fire/Smoke 4단 구조 (+ Legacy Debris/Dust Fallback).
    /// useCodeDefaults 체크 시에만 ParticleSystemConfig 코드 세팅 강제.
    /// 기본값(false)은 Inspector 값 존중 — 사용자 직접 튜닝 가능.
    /// </summary>
    public class ConcreteImpactVFXInitializer : MonoBehaviour
    {
        [Tooltip("체크 시 코드 기본값으로 세팅(Inspector 값 덮어씀). 끄면 Inspector 값 그대로 재생. " +
                 "기본 true — 씬의 파티클이 Unity 기본값(연속 방출)이라 꺼두면 뭉개뭉개 피어남. " +
                 "Inspector 튜닝을 원하면 먼저 'Crux/VFX/Apply All Presets' 메뉴로 씬 저장 후 체크 해제.")]
        [SerializeField] private bool useCodeDefaults = true;

        [Tooltip("체크 시 URP 2D 비호환 머티리얼을 Sprites/Default 기반으로 자동 교체 (핑크 방지).")]
        [SerializeField] private bool autoFixMaterial = true;

        [Tooltip("자동 제거까지 여유 시간(초)")]
        [SerializeField] private float destroyPadding = 0.5f;

        private void Start()
        {
            var sparks = FindPS("Sparks") ?? FindPS("Debris");
            var flash = FindPS("Flash");
            var fire = FindPS("Fire");
            var smoke = FindPS("Smoke") ?? FindPS("Dust");

            if (useCodeDefaults)
            {
                if (sparks != null) ParticleSystemConfig.ConfigureSparks(sparks);
                if (flash != null) ParticleSystemConfig.ConfigureFlash(flash);
                if (fire != null) ParticleSystemConfig.ConfigureFire(fire);
                if (smoke != null) ParticleSystemConfig.ConfigureSmoke(smoke);
            }

            if (autoFixMaterial)
            {
                var orange = ParticleSystemConfig.GetOrangeEmissiveMaterial();
                var smokeMat = ParticleSystemConfig.GetSmokeMaterial();
                FixMaterial(sparks, orange);
                FixMaterial(flash, orange);
                FixMaterial(fire, orange);
                FixMaterial(smoke, smokeMat);
            }

            // 최대 수명 계산 → Destroy 예약
            float maxLifetime = 0;
            AccumulateMaxLifetime(sparks, ref maxLifetime);
            AccumulateMaxLifetime(flash, ref maxLifetime);
            AccumulateMaxLifetime(fire, ref maxLifetime);
            AccumulateMaxLifetime(smoke, ref maxLifetime);
            if (maxLifetime <= 0) maxLifetime = 1.5f;

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

        /// <summary>URP 2D 비호환 머티리얼(Default-Particle 등) → Sprites/Default 기반으로 무조건 교체.</summary>
        private void FixMaterial(ParticleSystem ps, Material fallback)
        {
            if (ps == null || fallback == null) return;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;
            renderer.sharedMaterial = fallback;
        }
    }
}
