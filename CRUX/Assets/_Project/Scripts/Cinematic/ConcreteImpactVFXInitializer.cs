using UnityEngine;

namespace Crux.Cinematic
{
    /// <summary>
    /// 폭발 VFX 루트 — 자식 파티클 자동 제거만 담당.
    /// 파라미터는 씬의 Inspector 값 그대로 재생 (ParticleSystem의 Play On Awake 활용).
    /// 프리셋 초기화는 1회성 Editor 스크립트(VFXApplyPresetOneshot)로 씬에 영구 저장됨.
    /// Inspector에서 Sparks/Flash/Fire/Smoke 값 자유 튜닝 가능.
    /// </summary>
    public class ConcreteImpactVFXInitializer : MonoBehaviour
    {
        [Tooltip("자동 제거까지 여유 시간(초)")]
        [SerializeField] private float destroyPadding = 0.5f;

        private void Start()
        {
            float maxLifetime = 0;
            AccumulateMaxLifetime(FindPS("Sparks") ?? FindPS("Debris"), ref maxLifetime);
            AccumulateMaxLifetime(FindPS("Flash"), ref maxLifetime);
            AccumulateMaxLifetime(FindPS("Fire"), ref maxLifetime);
            AccumulateMaxLifetime(FindPS("Smoke") ?? FindPS("Dust"), ref maxLifetime);
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
    }
}
