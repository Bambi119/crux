using UnityEngine;

namespace Crux.Cinematic
{
    /// <summary>
    /// 폭발 VFX 파티클 시스템 런타임 구성 — v4 4단 구조.
    /// Sparks · Flash · Fire · Smoke 각각 전담 Configure 메서드.
    /// 사용자 지정 구체 수치 준수.
    /// </summary>
    public static class ParticleSystemConfig
    {
        // ---------- 공용 헬퍼 ----------

        /// <summary>주황 발광 근사 머티리얼. HDR 색상 intensity로 Default-Particle 대체.</summary>
        private static Material _orangeEmissiveMat;
        public static Material GetOrangeEmissiveMaterial()
        {
            if (_orangeEmissiveMat != null) return _orangeEmissiveMat;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;
            _orangeEmissiveMat = new Material(shader);
            _orangeEmissiveMat.mainTexture = Texture2D.whiteTexture;
            // Sprites/Default는 HDR intensity 직접 지원 안함 → Color만 밝은 주황으로
            _orangeEmissiveMat.color = new Color(1.2f, 0.6f, 0.15f, 1f);
            return _orangeEmissiveMat;
        }

        /// <summary>검은 연기 근사 머티리얼 (발광 없음).</summary>
        private static Material _smokeMat;
        public static Material GetSmokeMaterial()
        {
            if (_smokeMat != null) return _smokeMat;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;
            _smokeMat = new Material(shader);
            _smokeMat.mainTexture = Texture2D.whiteTexture;
            _smokeMat.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            return _smokeMat;
        }

        // ---------- 1. Sparks (불꽃) ----------

        /// <summary>
        /// Sparks — Burst 15~25, Sphere Shape, Stretched Billboard.
        /// Lifetime 0.2~0.8 / Speed 2~20 / Size 0.05~0.4.
        /// 크기 감쇠 커브 적용.
        /// </summary>
        public static void ConfigureSparks(ParticleSystem ps)
        {
            if (ps == null) return;
            ps.Stop();
            ps.Clear();

            var main = ps.main;
            main.duration = 1.0f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 20f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.4f);
            main.startColor = new Color(1f, 0.7f, 0.2f, 1f);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;

            var emit = ps.emission;
            emit.enabled = true;
            emit.rateOverTime = 0;
            emit.SetBursts(new[] {
                new ParticleSystem.Burst(0f, new ParticleSystem.MinMaxCurve(15f, 25f))
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            // Size over Lifetime — 시간에 따라 작아지는 곡선
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Renderer — Stretched Billboard + Speed Scale 0.03
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.03f;
                renderer.lengthScale = 1f;
                renderer.sortingOrder = 100;
                renderer.sharedMaterial = GetOrangeEmissiveMaterial();
            }

            ps.Play();
        }

        // ---------- 2. Flash (번쩍임) ----------

        /// <summary>
        /// Flash — Burst 1, Shape 비활성, Lifetime 0.1, Speed 0, Size 5.
        /// 크게 시작해 빠르게 작아짐.
        /// </summary>
        public static void ConfigureFlash(ParticleSystem ps)
        {
            if (ps == null) return;
            ps.Stop();
            ps.Clear();

            var main = ps.main;
            main.duration = 0.15f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = 0.1f;
            main.startSpeed = 0f;
            main.startSize = 5f;
            main.startColor = new Color(1.5f, 0.9f, 0.4f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 2;

            var emit = ps.emission;
            emit.enabled = true;
            emit.rateOverTime = 0;
            emit.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            // Shape 비활성 — 제자리 스폰
            var shape = ps.shape;
            shape.enabled = false;

            // Size over Lifetime — 크게 시작 → 빠르게 축소
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.3f, 0.5f),
                new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingOrder = 102;  // Sparks 위
                renderer.sharedMaterial = GetOrangeEmissiveMaterial();
            }

            ps.Play();
        }

        // ---------- 3. Fire (화염) ----------

        /// <summary>
        /// Fire — Burst 10, Sphere Radius 0.2.
        /// Lifetime 0.2~0.4 / Speed 0.5~3 / Size 0.5~1.5. 크기 감쇠.
        /// </summary>
        public static void ConfigureFire(ParticleSystem ps)
        {
            if (ps == null) return;
            ps.Stop();
            ps.Clear();

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startColor = new Color(1.3f, 0.55f, 0.15f, 1f);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 30;

            var emit = ps.emission;
            emit.enabled = true;
            emit.rateOverTime = 0;
            emit.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingOrder = 101;  // Sparks 뒤, Flash 앞
                renderer.sharedMaterial = GetOrangeEmissiveMaterial();
            }

            ps.Play();
        }

        // ---------- 4. Smoke (연기) ----------

        /// <summary>
        /// Smoke — Burst 10, Sphere Radius 0.2.
        /// Lifetime 0.4~0.6 / Speed 0.5~2 / Size 1.5~2.0. 어두운 검정.
        /// sortingOrder -1 — 다른 이펙트 뒤에 렌더링 (Sorting Fudge 근사).
        /// </summary>
        public static void ConfigureSmoke(ParticleSystem ps)
        {
            if (ps == null) return;
            ps.Stop();
            ps.Clear();

            var main = ps.main;
            main.duration = 0.8f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 2.0f);
            main.startColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            main.gravityModifier = -0.05f;  // 살짝 상승
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 30;

            var emit = ps.emission;
            emit.enabled = true;
            emit.rateOverTime = 0;
            emit.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            // Size over Lifetime — 서서히 커지며 페이드
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.8f),
                new Keyframe(1f, 1.3f));
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Color over Lifetime — 어두운 회색으로 페이드 아웃
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(new Color(0.12f, 0.12f, 0.12f), 0f),
                    new GradientColorKey(new Color(0.18f, 0.18f, 0.18f), 1f)
                },
                new[] {
                    new GradientAlphaKey(0.75f, 0f),
                    new GradientAlphaKey(0.3f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = grad;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingOrder = -1;  // 다른 이펙트 뒤 (요구사항 준수)
                renderer.sharedMaterial = GetSmokeMaterial();
            }

            ps.Play();
        }

        // ---------- Legacy 유지 (이전 버전 호환) ----------

        /// <summary>v3 호환 — 이전 Debris 호출 경로. Sparks로 위임.</summary>
        public static void ConfigureDebris(ParticleSystem ps) => ConfigureSparks(ps);
        /// <summary>v3 호환 — 이전 Dust 호출 경로. Smoke로 위임.</summary>
        public static void ConfigureDust(ParticleSystem ps) => ConfigureSmoke(ps);
    }
}
