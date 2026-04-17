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
        /// 금속 스파크 — 그라인더 불꽃 톤. 탄환급 속도 + 중력≈0 + 짧은 수명.
        /// 해머로 금속 측면을 때린 순간 사방으로 번쩍 비산하는 효과.
        /// Cone forward = transform.forward (+Z), VFXTestRunner에서 방향 지정.
        /// </summary>
        public static void ConfigureDebris(ParticleSystem ps)
        {
            if (ps == null) return;
            ps.Stop();
            ps.Clear();

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);  // 매우 짧음 (번쩍)
            main.startSpeed = new ParticleSystem.MinMaxCurve(40f, 85f);        // 탄환급 + 폭발 상향
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);     // 얇은 불꽃 입자
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            // 그라인더 불꽃 색 — 진한 오렌지 ↔ 밝은 노랑 ↔ 백열
            var colorGrad = new Gradient();
            colorGrad.SetKeys(
                new[] {
                    new GradientColorKey(new Color(1f, 0.9f, 0.6f), 0f),    // 흰노랑 (중심 최고열)
                    new GradientColorKey(new Color(1f, 0.75f, 0.2f), 0.5f), // 밝은 노랑
                    new GradientColorKey(new Color(1f, 0.45f, 0.05f), 1f)   // 오렌지
                },
                new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            main.startColor = new ParticleSystem.MinMaxGradient(colorGrad) {
                mode = ParticleSystemGradientMode.RandomColor
            };

            main.gravityModifier = 0.05f;   // 거의 0 — 직선 비산
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;

            // Emission — 1회 강한 버스트
            var emit = ps.emission;
            emit.enabled = true;
            emit.rateOverTime = 0;
            emit.SetBursts(new[] { new ParticleSystem.Burst(0f, 50) });

            // Shape — Cone 방향성 (부채꼴 넓게)
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 60f;
            shape.radius = 0.03f;
            shape.radiusThickness = 1f;

            // Color over Lifetime — 흰노랑 → 오렌지 → 빨강 → 투명 (식는 불꽃)
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var fadeGrad = new Gradient();
            fadeGrad.SetKeys(
                new[] {
                    new GradientColorKey(new Color(1f, 1f, 0.85f), 0f),   // 흰노랑 (최고열)
                    new GradientColorKey(new Color(1f, 0.7f, 0.15f), 0.4f), // 밝은 노랑
                    new GradientColorKey(new Color(0.9f, 0.3f, 0.05f), 0.85f), // 오렌지
                    new GradientColorKey(new Color(0.4f, 0.1f, 0.05f), 1f)  // 진한 빨강
                },
                new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = fadeGrad;

            // Rotation 유지 — 뾰족 입자가 돌면서 반짝
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-Mathf.PI * 4f, Mathf.PI * 4f);

            // Size over Lifetime — 빠르게 작아짐 (번쩍 후 감쇠)
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0.2f));
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Renderer — Additive 느낌 위해 가산 효과 근사 (Sprites/Default로는 한계. 흰 텍스처 오버)
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
        /// 연기 퍼프 — 불꽃 후 짧게 남는 흰/회 연기. 금속 타격 잔향.
        /// 빠르게 팽창하며 희미해짐. 콘크리트 먼지 구름과 달리 짧고 얇음.
        /// </summary>
        public static void ConfigureDust(ParticleSystem ps)
        {
            if (ps == null) return;
            ps.Stop();
            ps.Clear();

            var main = ps.main;
            main.duration = 0.8f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);   // 짧음
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);

            // 흰/연노랑 (금속 타격 연기)
            var dustGrad = new Gradient();
            dustGrad.SetKeys(
                new[] {
                    new GradientColorKey(new Color(0.95f, 0.92f, 0.80f), 0f),
                    new GradientColorKey(new Color(0.80f, 0.78f, 0.72f), 1f)
                },
                new[] {
                    new GradientAlphaKey(0.5f, 0f),
                    new GradientAlphaKey(0.5f, 1f)
                });
            main.startColor = new ParticleSystem.MinMaxGradient(dustGrad) {
                mode = ParticleSystemGradientMode.RandomColor
            };

            main.gravityModifier = 0f;  // 공중 잔향
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 20;

            var emit = ps.emission;
            emit.enabled = true;
            emit.rateOverTime = 0;
            emit.SetBursts(new[] { new ParticleSystem.Burst(0f, 6) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.15f;
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
                    new GradientAlphaKey(0.45f, 0f),
                    new GradientAlphaKey(0.25f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = fadeGrad;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.8f),
                new Keyframe(1f, 2.0f));
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
