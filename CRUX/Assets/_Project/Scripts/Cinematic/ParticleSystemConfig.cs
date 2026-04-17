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

        /// <summary>주황 발광 근사 머티리얼. 원형 soft 텍스처 사용 (사각 파티클 방지).</summary>
        private static Material _orangeEmissiveMat;
        public static Material GetOrangeEmissiveMaterial()
        {
            if (_orangeEmissiveMat != null) return _orangeEmissiveMat;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;
            _orangeEmissiveMat = new Material(shader);
            _orangeEmissiveMat.mainTexture = GetSoftCircleTexture();
            _orangeEmissiveMat.color = new Color(1.2f, 0.6f, 0.15f, 1f);
            return _orangeEmissiveMat;
        }

        /// <summary>검은 연기 머티리얼. 원형 soft 텍스처로 뭉게구름 느낌.</summary>
        private static Material _smokeMat;
        public static Material GetSmokeMaterial()
        {
            if (_smokeMat != null) return _smokeMat;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;
            _smokeMat = new Material(shader);
            _smokeMat.mainTexture = GetSoftCircleTexture();
            // 흰색 tint — 파티클 색은 Main>Start Color에서 제어 (곱셈 중립)
            _smokeMat.color = Color.white;
            return _smokeMat;
        }

        /// <summary>선명한 스파크 텍스처. 중심 꽉찬 alpha, 외곽만 좁게 페이드 — 블러 최소화.</summary>
        private static Texture2D _spark;
        public static Texture2D GetSparkTexture()
        {
            if (_spark != null) return _spark;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            float center = size * 0.5f;
            float maxR = center;
            var colors = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float t = Mathf.Clamp01(dist / maxR);
                    float alpha;
                    if (t < 0.6f) alpha = 1f;                          // 60%까지 완전 불투명
                    else if (t < 0.9f) alpha = 1f - (t - 0.6f) / 0.3f; // 좁은 페이드
                    else alpha = 0f;
                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
            _spark = tex;
            return tex;
        }

        /// <summary>Sparks용 선명 머티리얼 (hard-edge 텍스처).</summary>
        private static Material _sparkMat;
        public static Material GetSparkMaterial()
        {
            if (_sparkMat != null) return _sparkMat;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;
            _sparkMat = new Material(shader);
            _sparkMat.mainTexture = GetSparkTexture();
            _sparkMat.color = new Color(1.4f, 0.85f, 0.25f, 1f);
            return _sparkMat;
        }

        /// <summary>도넛(링) 형태 soft 텍스처. Flash 파동용. 중심/외곽 투명, 중간 띠만 밝음.</summary>
        private static Texture2D _ring;
        public static Texture2D GetRingTexture()
        {
            if (_ring != null) return _ring;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            float center = size * 0.5f;
            float maxR = center;
            const float ringCenter = 0.62f;  // 링 중심 위치 (0=중앙, 1=외곽)
            const float ringWidth = 0.28f;   // 링 반폭
            var colors = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float t = Mathf.Clamp01(dist / maxR);
                    float d = Mathf.Abs(t - ringCenter);
                    float alpha = Mathf.Clamp01(1f - d / ringWidth);
                    alpha = alpha * alpha;
                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
            _ring = tex;
            return tex;
        }

        /// <summary>Flash 파동용 머티리얼 (링 텍스처).</summary>
        private static Material _ringMat;
        public static Material GetRingMaterial()
        {
            if (_ringMat != null) return _ringMat;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;
            _ringMat = new Material(shader);
            _ringMat.mainTexture = GetRingTexture();
            _ringMat.color = new Color(1.5f, 1.0f, 0.5f, 1f);  // 노란빛 감도는 밝은 주황
            return _ringMat;
        }

        /// <summary>중심 밝고 외곽 투명인 원형 soft 그라디언트 텍스처. 한 번 생성 캐싱.</summary>
        private static Texture2D _softCircle;
        public static Texture2D GetSoftCircleTexture()
        {
            if (_softCircle != null) return _softCircle;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            float center = size * 0.5f;
            float maxDist = center;
            var colors = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float t = Mathf.Clamp01(dist / maxDist);
                    // 부드러운 페이드 (quadratic)
                    float alpha = 1f - t;
                    alpha = alpha * alpha;
                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
            _softCircle = tex;
            return tex;
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

            // Renderer — Stretched Billboard. velocityScale 상향으로 길쭉함 강화.
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.15f;  // 속도 비례 스트레치 (선명한 트레일)
                renderer.lengthScale = 2f;       // 기본 길이도 2배 (속도 무관 늘림)
                renderer.sortingOrder = 100;
                renderer.sharedMaterial = GetSparkMaterial();  // 선명 텍스처
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

            // Size over Lifetime — 점에서 외곽으로 확산 (0 → 1 → 페이드)
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.05f),   // 거의 점
                new Keyframe(0.25f, 1f),   // 빠르게 최대치로 확장
                new Keyframe(1f, 0.8f));   // 유지, 사라짐은 alpha로
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Color over Lifetime — alpha 페이드 (커브 끝에 사라짐)
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.4f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = grad;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingOrder = 102;  // Sparks 위
                renderer.sharedMaterial = GetRingMaterial();  // 도넛 파동
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

            // Color over Lifetime — 컬러는 흰색 유지(Start Color 곱셈 중립), alpha만 페이드
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
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
