using UnityEngine;

namespace Crux.Cinematic
{
    /// <summary>
    /// ConcreteImpact VFX 파티클 시스템 런타임 구성.
    /// Unity Inspector 조작 없이 코드로 모든 주요 속성 세팅 (MCP 단독 환경 대응).
    /// </summary>
    public static class ParticleSystemConfig
    {
        /// <summary>파편 — 사방으로 튀고 중력 받아 떨어짐</summary>
        public static void ConfigureDebris(ParticleSystem ps)
        {
            if (ps == null) return;
            ps.Stop();
            ps.Clear();

            // Main
            var main = ps.main;
            main.duration = 1.0f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.545f, 0.49f, 0.42f, 1f);  // 회갈색 #8B7D6B
            main.gravityModifier = 1.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 50;

            // Emission — 한 번에 burst
            var emit = ps.emission;
            emit.enabled = true;
            emit.rateOverTime = 0;
            emit.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });

            // Shape — 작은 원에서 방사
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;
            shape.radiusThickness = 1f;  // volume

            // Color over Lifetime — 회갈색 → 진회색 페이드
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(new Color(0.545f, 0.49f, 0.42f), 0f),
                    new GradientColorKey(new Color(0.3f, 0.27f, 0.24f), 1f)
                },
                new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = grad;

            // Rotation over Lifetime
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-Mathf.PI * 2f, Mathf.PI * 2f);

            // Renderer
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingOrder = 100;
                if (renderer.sharedMaterial == null)
                    renderer.sharedMaterial = GetDefaultParticleMaterial();
            }

            ps.Play();
        }

        /// <summary>먼지 구름 — 부풀며 페이드아웃</summary>
        public static void ConfigureDust(ParticleSystem ps)
        {
            if (ps == null) return;
            ps.Stop();
            ps.Clear();

            var main = ps.main;
            main.duration = 2.0f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startColor = new Color(0.91f, 0.867f, 0.784f, 0.6f);  // 베이지 #E8DDC8 alpha 0.6
            main.gravityModifier = -0.1f;  // 약간 상승
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 20;

            var emit = ps.emission;
            emit.enabled = true;
            emit.rateOverTime = 0;
            emit.SetBursts(new[] { new ParticleSystem.Burst(0f, 6) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.2f;
            shape.radiusThickness = 1f;

            // Color over Lifetime — alpha fade
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(new Color(0.91f, 0.867f, 0.784f), 0f),
                    new GradientColorKey(new Color(0.7f, 0.67f, 0.6f), 1f)
                },
                new[] {
                    new GradientAlphaKey(0.5f, 0f),
                    new GradientAlphaKey(0.4f, 0.3f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = grad;

            // Size over Lifetime — 1 → 2배 확장
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 2f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingOrder = 99;
                if (renderer.sharedMaterial == null)
                    renderer.sharedMaterial = GetDefaultParticleMaterial();
            }

            ps.Play();
        }

        private static Material _defaultMat;
        private static Material GetDefaultParticleMaterial()
        {
            if (_defaultMat != null) return _defaultMat;
            // Sprites/Default 쉐이더 + 흰 1x1 텍스처 (투명 렌더 방지)
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
