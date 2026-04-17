using UnityEngine;

namespace Crux.Cinematic
{
    /// <summary>
    /// ConcreteImpact VFX 파티클 시스템 런타임 구성.
    /// Unity Inspector 조작 없이 코드로 모든 주요 속성 세팅 (MCP 단독 환경 대응).
    /// v2: 색상 Gradient · Cone 방향성 · 속도·버스트 상향.
    /// </summary>
    public static class ParticleSystemConfig
    {
        /// <summary>
        /// 파편 — Cone shape으로 피격 반대 방향 부채꼴 비산. 중력 영향.
        /// Transform rotation으로 피격 방향 제어 (Cone 방향 = transform.forward).
        /// </summary>
        public static void ConfigureDebris(ParticleSystem ps)
        {
            if (ps == null) return;
            ps.Stop();
            ps.Clear();

            var main = ps.main;
            main.duration = 1.2f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 18f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            // 흰/회/진회색 Random Color — 콘크리트 파편 톤 분산
            var colorGrad = new Gradient();
            colorGrad.SetKeys(
                new[] {
                    new GradientColorKey(new Color(0.92f, 0.90f, 0.85f), 0f),  // 밝은 흰회
                    new GradientColorKey(new Color(0.62f, 0.58f, 0.52f), 0.5f), // 중간 회
                    new GradientColorKey(new Color(0.28f, 0.25f, 0.22f), 1f)   // 짙은 회갈
                },
                new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            main.startColor = new ParticleSystem.MinMaxGradient(colorGrad) {
                mode = ParticleSystemGradientMode.RandomColor
            };

            main.gravityModifier = 2.0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 100;

            // Emission — 1회 강한 버스트
            var emit = ps.emission;
            emit.enabled = true;
            emit.rateOverTime = 0;
            emit.SetBursts(new[] { new ParticleSystem.Burst(0f, 35) });

            // Shape — Cone 방향성
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 55f;        // 부채꼴 넓이 (55도)
            shape.radius = 0.05f;
            shape.radiusThickness = 1f;
            // Cone forward: transform.forward (+Z). 2D에선 Transform z-rotation으로 방향 조정.

            // Color over Lifetime — 페이드아웃
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var fadeGrad = new Gradient();
            fadeGrad.SetKeys(
                new[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = fadeGrad;

            // Rotation over Lifetime
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-Mathf.PI * 3f, Mathf.PI * 3f);

            // Size over Lifetime — 살짝 작아짐 (떨어지면서 작아보임)
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0.6f));
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Renderer
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingOrder = 100;
                if (renderer.sharedMaterial == null || renderer.sharedMaterial.mainTexture == null)
                    renderer.sharedMaterial = GetDefaultParticleMaterial();
            }

            ps.Play();
        }

        /// <summary>
        /// 먼지 구름 — 부풀며 페이드. 피격 방향과 무관하게 전방향 퍼짐.
        /// </summary>
        public static void ConfigureDust(ParticleSystem ps)
        {
            if (ps == null) return;
            ps.Stop();
            ps.Clear();

            var main = ps.main;
            main.duration = 2.0f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);

            // 베이지 ~ 흰 범위 Random
            var dustGrad = new Gradient();
            dustGrad.SetKeys(
                new[] {
                    new GradientColorKey(new Color(0.95f, 0.92f, 0.85f), 0f),
                    new GradientColorKey(new Color(0.75f, 0.70f, 0.60f), 1f)
                },
                new[] {
                    new GradientAlphaKey(0.7f, 0f),
                    new GradientAlphaKey(0.7f, 1f)
                });
            main.startColor = new ParticleSystem.MinMaxGradient(dustGrad) {
                mode = ParticleSystemGradientMode.RandomColor
            };

            main.gravityModifier = -0.15f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 30;

            var emit = ps.emission;
            emit.enabled = true;
            emit.rateOverTime = 0;
            emit.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
            shape.radiusThickness = 1f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var fadeGrad = new Gradient();
            fadeGrad.SetKeys(
                new[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[] {
                    new GradientAlphaKey(0.6f, 0f),
                    new GradientAlphaKey(0.4f, 0.4f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = fadeGrad;

            // Size over Lifetime — 크게 부풀음
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.6f),
                new Keyframe(1f, 2.5f));
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingOrder = 99;
                if (renderer.sharedMaterial == null || renderer.sharedMaterial.mainTexture == null)
                    renderer.sharedMaterial = GetDefaultParticleMaterial();
            }

            ps.Play();
        }

        private static Material _defaultMat;
        private static Material GetDefaultParticleMaterial()
        {
            if (_defaultMat != null) return _defaultMat;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _defaultMat = new Material(shader);
                _defaultMat.mainTexture = Texture2D.whiteTexture;
            }
            return _defaultMat;
        }
    }
}
