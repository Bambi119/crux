using UnityEngine;
using Crux.Core;

namespace Crux.Combat
{
    /// <summary>피격 이펙트 — 경량화 (리소스 절약, 시각 효과 유지)</summary>
    public static class HitEffects
    {
        private static Sprite _cachedCircle;
        private static Sprite[] _ricochetFrames;
        private static Sprite[] _hitFrames;
        private static GameObject _cachedImpactPrefab;
        private static bool _impactPrefabProbed;

        /// <summary>ConcreteImpactVFX 프리팹 지연 로드 (Resources/VFX/ConcreteImpactVFX).</summary>
        private static GameObject GetImpactPrefab()
        {
            if (_impactPrefabProbed) return _cachedImpactPrefab;
            _cachedImpactPrefab = Resources.Load<GameObject>("VFX/ConcreteImpactVFX");
            _impactPrefabProbed = true;
            if (_cachedImpactPrefab == null)
                Debug.LogWarning("[CRUX] ConcreteImpactVFX 프리팹 로드 실패 — 레거시 SpawnImpact로 폴백");
            return _cachedImpactPrefab;
        }

        /// <summary>피격 이펙트 진입점</summary>
        /// <param name="caliberScale">무기 구경 스케일 — 1.0=주포 표준, 0.4=기관총, 1.5=대구경</param>
        public static void Spawn(Vector3 position, ShotOutcome outcome, Vector2 shellDir, float caliberScale = 1f)
        {
            caliberScale = Mathf.Clamp(caliberScale, 0.3f, 2.5f);
            switch (outcome)
            {
                case ShotOutcome.Miss:
                    SpawnMiss(position);
                    break;
                case ShotOutcome.Ricochet:
                    SpawnRicochet(position, shellDir, caliberScale);
                    break;
                case ShotOutcome.Hit:
                    SpawnImpact(position, shellDir, 1f, caliberScale);
                    break;
                case ShotOutcome.Penetration:
                    SpawnImpact(position, shellDir, 1.6f, caliberScale);
                    break;
            }
        }

        /// <summary>주포 ammo damage → 구경 스케일 추정 (HitEffects.Spawn 호출용)</summary>
        public static float CaliberScaleFromAmmoDamage(float damage)
        {
            // 주포 표준 30 데미지 = 1.0, 작은 구경 0.6, 대구경 1.6 부근
            return Mathf.Clamp(damage / 30f, 0.5f, 1.8f);
        }

        /// <summary>전차 격파 폭발 — 기본 규모</summary>
        public static void SpawnExplosion(Vector3 position)
        {
            // [1] 섬광 코어
            var core = P(position, new Color(1f, 1f, 0.95f, 1f), 0.5f, 75);
            Object.Destroy(core, 0.05f);

            // [2] 화염구 3단
            var fire1 = P(position, new Color(1f, 0.92f, 0.5f, 1f), 0.8f, 73);
            fire1.AddComponent<FadeAndShrink>();
            Object.Destroy(fire1, 0.18f);

            var fire2 = P(position, new Color(1f, 0.5f, 0.12f, 0.9f), 1.1f, 72);
            fire2.AddComponent<FadeAndShrink>();
            Object.Destroy(fire2, 0.38f);

            var fire3 = P(position, new Color(0.7f, 0.18f, 0.04f, 0.7f), 1.3f, 71);
            fire3.AddComponent<FadeAndShrink>();
            Object.Destroy(fire3, 0.65f);

            // [3] 방사형 화염 줄기 (20개)
            var streakSprite = GetStreakSprite();
            for (int i = 0; i < 20; i++)
            {
                float angle = Random.Range(0f, 360f);
                Vector2 d = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                var streak = new GameObject("ExpStreak");
                streak.transform.position = position;
                streak.transform.rotation = Quaternion.Euler(0, 0, angle);
                var sr = streak.AddComponent<SpriteRenderer>();
                sr.sprite = streakSprite;
                sr.sortingOrder = 74;
                sr.color = Random.value < 0.5f
                    ? new Color(1f, 0.88f, 0.4f, 0.9f)
                    : new Color(1f, 0.5f, 0.1f, 0.85f);
                float len = Random.Range(0.3f, 0.85f);
                streak.transform.localScale = new Vector3(len, 0.06f, 1f);
                var rb = streak.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 2f;
                rb.linearVelocity = d * Random.Range(9f, 22f);
                streak.AddComponent<FadeAndShrink>();
                Object.Destroy(streak, Random.Range(0.14f, 0.38f));
            }

            // [4] 금속 파편 (20개, 사방)
            for (int i = 0; i < 20; i++)
            {
                float angle = Random.Range(0f, 360f);
                Vector2 d = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                Color c = i < 7 ? new Color(0.1f, 0.08f, 0.06f) :
                          i < 14 ? new Color(0.32f, 0.25f, 0.17f) :
                                   new Color(0.55f, 0.45f, 0.3f);
                var obj = P(position, c, Random.Range(0.03f, 0.09f), 64);
                var rb = obj.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 1.6f;
                rb.linearVelocity = d * Random.Range(4f, 16f);
                obj.AddComponent<FadeAndShrink>();
                Object.Destroy(obj, Random.Range(0.3f, 0.9f));
            }

            // [5] 검은 연기 기둥 (5개)
            for (int i = 0; i < 5; i++)
            {
                var smoke = P(position + (Vector3)(Random.insideUnitCircle * 0.22f),
                    new Color(0.08f, 0.07f, 0.05f, 0.72f),
                    Random.Range(0.35f, 0.65f), 55);
                var rb = smoke.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 0.9f;
                rb.linearVelocity = new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(1f, 2.5f));
                smoke.AddComponent<FadeAndShrink>();
                Object.Destroy(smoke, Random.Range(1.0f, 2.0f));
            }

            // [6] 잔불 (잔광)
            var glow = P(position, new Color(1f, 0.35f, 0.05f, 0.4f), 0.7f, 56);
            glow.AddComponent<FadeAndShrink>();
            Object.Destroy(glow, 1.0f);
        }

        // ===== 빗나감 — 흙먼지 + 방향성 파편 =====
        private static void SpawnMiss(Vector3 position)
        {
            // [1] 중심 흙먼지 디스크 — 크고 빠르게 퍼지다 사라짐
            var dust = P(position, new Color(0.62f, 0.52f, 0.32f, 0.7f), 0.22f, 42);
            dust.AddComponent<FadeAndShrink>();
            Object.Destroy(dust, 0.55f);

            // [2] 작은 흙먼지 보조 — 약간 오프셋
            var dust2 = P(position + (Vector3)Random.insideUnitCircle * 0.12f,
                          new Color(0.55f, 0.47f, 0.28f, 0.5f), 0.14f, 41);
            dust2.AddComponent<FadeAndShrink>();
            Object.Destroy(dust2, 0.4f);

            // [3] 파편 3개 — Rigidbody2D로 방향성 분산
            for (int i = 0; i < 3; i++)
            {
                float ang = Random.Range(0f, 360f);
                var chip = new GameObject("MissChip");
                chip.transform.position = position;
                var sr = chip.AddComponent<SpriteRenderer>();
                sr.sprite = GetCircle();
                sr.sortingOrder = 43;
                sr.color = new Color(0.6f, 0.5f, 0.3f, 0.85f);
                chip.transform.localScale = Vector3.one * Random.Range(0.04f, 0.07f);
                var rb = chip.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 4f;
                Vector2 dir = new Vector2(
                    Mathf.Cos(ang * Mathf.Deg2Rad),
                    Mathf.Sin(ang * Mathf.Deg2Rad));
                rb.linearVelocity = dir * Random.Range(3f, 7f);
                chip.AddComponent<FadeAndShrink>();
                Object.Destroy(chip, Random.Range(0.3f, 0.5f));
            }
        }

        // ===== 도탄 — 스프라이트 + 금속 불꽃 파편 + 파티클 =====
        private static void SpawnRicochet(Vector3 position, Vector2 shellDir, float caliberScale = 1f)
        {
            // [1] 스프라이트 애니메이션
            var frames = GetRicochetFrames();
            if (frames != null && frames.Length > 0)
            {
                float angle = Mathf.Atan2(shellDir.y, shellDir.x) * Mathf.Rad2Deg;
                SpriteAnimation.Play(position, frames, 0.8f,
                    scale: 0.12f, sortingOrder: 68, rotation: angle);
            }

            // [2] 충돌 섬광 — 2단 (밝은 백색 → 주황)
            var flash1 = P(position, new Color(1f, 1f, 0.9f, 1f), 0.4f, 71);
            Object.Destroy(flash1, 0.04f);
            var flash2 = P(position, new Color(1f, 0.8f, 0.3f, 0.8f), 0.3f, 70);
            flash2.AddComponent<FadeAndShrink>();
            Object.Destroy(flash2, 0.12f);

            // [3] 도탄 방향 계산
            Vector2 ricochetDir = Vector2.Reflect(shellDir,
                                    (Random.insideUnitCircle + Vector2.up).normalized);
            float ricochetAngleDeg = Mathf.Atan2(ricochetDir.y, ricochetDir.x) * Mathf.Rad2Deg;

            // [3.5] 방향성 불꽃 줄기 — 도탄 방향 위주로 좁게 분사 (얇고 빠름)
            int streakCountR = Mathf.RoundToInt(12 * caliberScale);
            var streakSpriteR = GetStreakSprite();
            for (int i = 0; i < streakCountR; i++)
            {
                float a = ricochetAngleDeg + Random.Range(-45f, 45f);
                Vector2 d = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));

                var streak = new GameObject("RicoStreak");
                streak.transform.position = position;
                streak.transform.rotation = Quaternion.Euler(0, 0, a);
                var sr = streak.AddComponent<SpriteRenderer>();
                sr.sprite = streakSpriteR;
                // 도탄 섬광/플래시(70~71) 위에 렌더
                sr.sortingOrder = 73;
                sr.color = Random.value < 0.5f
                    ? new Color(1f, 0.95f, 0.6f, 0.95f)   // 백황
                    : new Color(1f, 0.7f, 0.2f, 0.85f);    // 주황
                float len = Random.Range(0.18f, 0.45f) * caliberScale;
                streak.transform.localScale = new Vector3(len, 0.05f, 1f);
                var rb = streak.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 3f;
                rb.linearVelocity = d * Random.Range(10f, 22f);
                streak.AddComponent<FadeAndShrink>();
                Object.Destroy(streak, Random.Range(0.1f, 0.25f));
            }

            // [4] 1차 스파크 — 빠르고 밝은 불꽃 (구경 비례 확장, 넓게 확산)
            int sparkCount = Mathf.RoundToInt(14 * caliberScale);
            for (int i = 0; i < sparkCount; i++)
            {
                float spreadAngle = Mathf.Atan2(ricochetDir.y, ricochetDir.x) * Mathf.Rad2Deg;
                spreadAngle += Random.Range(-70f, 70f);
                Vector2 dir = new Vector2(
                    Mathf.Cos(spreadAngle * Mathf.Deg2Rad),
                    Mathf.Sin(spreadAngle * Mathf.Deg2Rad));

                // 색 분포: 35% 백열, 30% 노란, 35% 주황 (개수 변동에 무관)
                float colorRoll = (float)i / sparkCount;
                Color c = colorRoll < 0.35f
                    ? new Color(1f, 1f, 0.7f)
                    : colorRoll < 0.65f
                        ? new Color(1f, 0.85f, 0.3f)
                        : new Color(1f, 0.55f, 0.1f);

                float size = Random.Range(0.025f, 0.07f);
                var spark = P(position, c, size, 67);
                var rb = spark.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 2.5f;
                rb.linearVelocity = dir * Random.Range(6f, 16f);
                spark.AddComponent<FadeAndShrink>();
                Object.Destroy(spark, Random.Range(0.12f, 0.35f));
            }

            // [5] 2차 스파크 — 느리고 작은 잔불꽃 (8개, 지연 생성)
            for (int i = 0; i < 8; i++)
            {
                float spreadAngle = Random.Range(0f, 360f);
                Vector2 dir = new Vector2(
                    Mathf.Cos(spreadAngle * Mathf.Deg2Rad),
                    Mathf.Sin(spreadAngle * Mathf.Deg2Rad));

                Color c = i < 4
                    ? new Color(1f, 0.7f, 0.2f, 0.9f)
                    : new Color(0.9f, 0.4f, 0.1f, 0.7f);

                float size = Random.Range(0.02f, 0.05f);
                var ember = P(position + (Vector3)(Random.insideUnitCircle * 0.15f), c, size, 64);
                var rb = ember.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 4f;
                rb.linearVelocity = dir * Random.Range(2f, 6f);
                ember.AddComponent<FadeAndShrink>();
                Object.Destroy(ember, Random.Range(0.25f, 0.55f));
            }

            // [6] 금속 파편 — 무거운 조각 (4개, 느리게 굴러감)
            for (int i = 0; i < 4; i++)
            {
                float spreadAngle = Mathf.Atan2(ricochetDir.y, ricochetDir.x) * Mathf.Rad2Deg;
                spreadAngle += Random.Range(-40f, 40f);
                Vector2 dir = new Vector2(
                    Mathf.Cos(spreadAngle * Mathf.Deg2Rad),
                    Mathf.Sin(spreadAngle * Mathf.Deg2Rad));

                var debris = P(position, new Color(0.45f, 0.4f, 0.35f), Random.Range(0.04f, 0.07f), 62);
                var rb = debris.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 5f;
                rb.linearVelocity = dir * Random.Range(3f, 8f);
                debris.AddComponent<FadeAndShrink>();
                Object.Destroy(debris, Random.Range(0.4f, 0.8f));
            }

            // [7] 연기 (3개)
            for (int i = 0; i < 3; i++)
            {
                var smoke = P(position + (Vector3)(Random.insideUnitCircle * 0.08f),
                    new Color(0.25f, 0.23f, 0.2f, 0.35f), Random.Range(0.1f, 0.2f), 55);
                var rb = smoke.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 1.5f;
                rb.linearVelocity = (Vector2)ricochetDir * Random.Range(0.3f, 1.2f)
                    + Vector2.up * Random.Range(0.2f, 0.6f);
                smoke.AddComponent<FadeAndShrink>();
                Object.Destroy(smoke, Random.Range(0.4f, 0.8f));
            }
        }

        private static Sprite[] GetRicochetFrames()
        {
            if (_ricochetFrames != null) return _ricochetFrames;

            _ricochetFrames = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                string path = $"Sprites/FX/ricochet_{(i + 1):D2}";
                _ricochetFrames[i] = Resources.Load<Sprite>(path);
                Debug.Log($"[CRUX] 도탄 스프라이트 로드: {path} → {(_ricochetFrames[i] != null ? "성공" : "실패")}");
            }

            // 로드 실패 체크
            if (_ricochetFrames[0] == null)
            {
                Debug.LogWarning("[CRUX] 도탄 스프라이트 로드 실패 — Resources/Sprites/FX/ricochet_01~04 확인");
                _ricochetFrames = null;
            }

            return _ricochetFrames;
        }

        /// <summary>피격/관통 — ConcreteImpactVFX 프리팹 사용. 없으면 레거시 스프라이트 VFX로 폴백.</summary>
        /// <param name="intensity">결과별 강도 (Hit=1, Penetration=1.6)</param>
        /// <param name="caliberScale">무기 구경 (1=주포 표준, 0.4=MG)</param>
        private static void SpawnImpact(Vector3 position, Vector2 shellDir, float intensity, float caliberScale = 1f)
        {
            var prefab = GetImpactPrefab();
            if (prefab != null)
            {
                Vector2 splashDir = -shellDir.normalized;
                if (splashDir == Vector2.zero) splashDir = Vector2.right;
                float angleDeg = Mathf.Atan2(splashDir.y, splashDir.x) * Mathf.Rad2Deg;
                Quaternion rot = Quaternion.Euler(-90f, 0f, angleDeg - 90f);
                var vfx = Object.Instantiate(prefab, position, rot);
                float vfxScale = Mathf.Max(intensity * caliberScale * 0.3f, 0.2f);
                vfx.transform.localScale = Vector3.one * vfxScale;
                return;
            }
            SpawnImpactLegacy(position, shellDir, intensity, caliberScale);
        }

        /// <summary>레거시 스프라이트 기반 피격 VFX — 프리팹 누락 시 폴백용.</summary>
        private static void SpawnImpactLegacy(Vector3 position, Vector2 shellDir, float intensity, float caliberScale = 1f)
        {
            // 두 스케일을 결합 — 화구·파편·연기 전체 크기에 적용
            float effectScale = intensity * caliberScale;
            Vector2 splashDir = -shellDir;
            float splashAngle = Mathf.Atan2(splashDir.y, splashDir.x) * Mathf.Rad2Deg;

            // [1] 스프라이트 애니메이션
            var frames = GetHitFrames();
            if (frames != null && frames.Length > 0)
            {
                SpriteAnimation.Play(position, frames, 0.7f,
                    scale: 0.12f * effectScale, sortingOrder: 68, rotation: splashAngle);
            }

            // [2] 화구(fireball) — 4단: 백열 코어 → 백노란 → 노란 → 주황
            var core = P(position, new Color(1f, 1f, 1f, 1f), 0.4f * effectScale, 72);
            Object.Destroy(core, 0.05f);

            var glow1 = P(position, new Color(1f, 1f, 0.85f, 1f), 0.55f * effectScale, 71);
            glow1.AddComponent<FadeAndShrink>();
            Object.Destroy(glow1, 0.12f);

            var glow2 = P(position, new Color(1f, 0.85f, 0.3f, 0.9f), 0.7f * effectScale, 70);
            glow2.AddComponent<FadeAndShrink>();
            Object.Destroy(glow2, 0.25f);

            var outer = P(position, new Color(1f, 0.5f, 0.1f, 0.6f), 0.9f * effectScale, 69);
            outer.AddComponent<FadeAndShrink>();
            Object.Destroy(outer, 0.4f);

            // [3] 방사형 불꽃 줄기 — 얇고 많은 직선 스프라이트가 사방으로 뻗어나감
            // 두께 0.015 (이전 0.03의 절반), 개수 28 baseline (이전 16)
            int streakCount = Mathf.RoundToInt(28 * effectScale);
            var streakSprite = GetStreakSprite();
            for (int i = 0; i < streakCount; i++)
            {
                float angle = Random.Range(0f, 360f);
                Vector2 dir = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad));

                var streak = new GameObject("Streak");
                streak.transform.position = position;
                streak.transform.rotation = Quaternion.Euler(0, 0, angle);

                var sr = streak.AddComponent<SpriteRenderer>();
                sr.sprite = streakSprite;
                // 화구 글로우(69~72) 위에 렌더 — 아니면 중심 근처 줄기가 가려짐
                sr.sortingOrder = 73;
                // 밝은 노란~백색 (살짝 더 다양한 톤)
                float roll = Random.value;
                sr.color = roll < 0.4f
                    ? new Color(1f, 0.97f, 0.75f, 0.95f)
                    : roll < 0.75f
                        ? new Color(1f, 0.85f, 0.35f, 0.85f)
                        : new Color(1f, 0.6f, 0.15f, 0.75f);

                float len = Random.Range(0.25f, 0.9f) * effectScale;
                // 두께 0.06 — sprite Y(0.125)와 곱해 0.0075 world 단위, 대략 1~1.5 px
                // 그 이하로 가면 서브픽셀로 안 보임 (0.015 테스트에서 확인)
                streak.transform.localScale = new Vector3(len, 0.06f, 1f);

                var rb = streak.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 2f; // 감쇠 완화 — 줄기가 더 멀리 뻗음
                rb.linearVelocity = dir * Random.Range(10f, 24f) * intensity;
                streak.AddComponent<FadeAndShrink>();
                Object.Destroy(streak, Random.Range(0.12f, 0.35f));
            }

            // [4] 검은/갈색 파편 다수 — 사방으로 멀리
            int debrisCount = Mathf.RoundToInt(30 * effectScale);
            for (int i = 0; i < debrisCount; i++)
            {
                float angle = Random.Range(0f, 360f);
                Vector2 dir = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad));

                Color c;
                if (i < debrisCount / 4)
                    c = new Color(0.1f, 0.08f, 0.06f);       // 검은 파편
                else if (i < debrisCount / 2)
                    c = new Color(0.35f, 0.25f, 0.15f);      // 갈색 파편
                else if (i < debrisCount * 3 / 4)
                    c = new Color(0.6f, 0.5f, 0.3f);         // 밝은 파편
                else
                    c = new Color(1f, 0.7f, 0.2f, 0.8f);     // 불꽃 조각

                float size = Random.Range(0.015f, 0.05f) * effectScale;
                var obj = P(position, c, size, 62);
                var rb = obj.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 1.5f;
                rb.linearVelocity = dir * Random.Range(3f, 18f) * intensity;
                obj.AddComponent<FadeAndShrink>();
                Object.Destroy(obj, Random.Range(0.3f, 0.9f));
            }

            // [5] 연기/먼지 구름 — 크고 어둡게
            int dustCount = Mathf.RoundToInt(6 * effectScale);
            for (int i = 0; i < dustCount; i++)
            {
                Vector2 dustDir = Random.insideUnitCircle;
                float dustSize = Random.Range(0.2f, 0.5f) * effectScale;

                Color dustColor;
                if (i < dustCount / 3)
                    dustColor = new Color(0.12f, 0.1f, 0.08f, 0.5f);   // 검은 연기
                else if (i < dustCount * 2 / 3)
                    dustColor = new Color(0.3f, 0.25f, 0.2f, 0.4f);    // 갈색 연기
                else
                    dustColor = new Color(0.45f, 0.4f, 0.35f, 0.3f);   // 밝은 먼지

                var dust = P(position + (Vector3)(Random.insideUnitCircle * 0.2f),
                    dustColor, dustSize, 55);
                var rb = dust.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 0.8f;
                rb.linearVelocity = dustDir * Random.Range(0.5f, 2.5f) * intensity;
                dust.AddComponent<FadeAndShrink>();
                Object.Destroy(dust, Random.Range(0.6f, 1.5f));
            }

            // [6] 잔광(afterglow) — 서서히 사라지는 주황 원
            var afterglow = P(position, new Color(1f, 0.4f, 0.1f, 0.4f), 0.5f * effectScale, 56);
            afterglow.AddComponent<FadeAndShrink>();
            Object.Destroy(afterglow, 0.8f);
        }

        // 선형 불꽃 줄기 스프라이트 (가로로 긴 선)
        private static Sprite _cachedStreak;
        private static Sprite GetStreakSprite()
        {
            if (_cachedStreak != null) return _cachedStreak;
            int w = 16, h = 2;
            var tex = new Texture2D(w, h);
            tex.filterMode = FilterMode.Point;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float t = (float)x / w;
                    px[y * w + x] = new Color(1f, 1f, 1f, t); // 꼬리 투명 → 선두 불투명
                }
            tex.SetPixels(px); tex.Apply();
            _cachedStreak = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0.5f), w);
            return _cachedStreak;
        }

        private static Sprite[] GetHitFrames()
        {
            if (_hitFrames != null) return _hitFrames;

            _hitFrames = new Sprite[10];
            for (int i = 0; i < 10; i++)
            {
                string path = $"Sprites/FX/hit_{(i + 1):D2}";
                _hitFrames[i] = Resources.Load<Sprite>(path);
            }

            if (_hitFrames[0] == null)
            {
                Debug.LogWarning("[CRUX] 피격 스프라이트 로드 실패 — Resources/Sprites/FX/hit_01~10 확인");
                _hitFrames = null;
            }

            return _hitFrames;
        }

        // ===== 엄폐물 피격 — 콘크리트/돌 파편, 방향성 =====
        public static void SpawnCoverHit(Vector3 position, Vector2 shellDir)
        {
            Vector2 scatterDir = shellDir.normalized;
            if (scatterDir == Vector2.zero) scatterDir = Vector2.right;
            float scatterAngle = Mathf.Atan2(scatterDir.y, scatterDir.x) * Mathf.Rad2Deg;

            // [1] 회백색 충격 섬광 (2단)
            var flash1 = P(position, new Color(0.95f, 0.92f, 0.85f, 1f), 0.4f, 71);
            Object.Destroy(flash1, 0.04f);
            var flash2 = P(position, new Color(0.75f, 0.7f, 0.6f, 0.7f), 0.55f, 70);
            flash2.AddComponent<FadeAndShrink>();
            Object.Destroy(flash2, 0.15f);

            // [2] 방향성 콘크리트 파편 줄기 (ricochet RicoStreak와 동일 구조, 색만 다름)
            int streakCount = 12;
            var streakSprite = GetStreakSprite();
            for (int i = 0; i < streakCount; i++)
            {
                float a = scatterAngle + Random.Range(-55f, 55f);
                Vector2 d = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));

                var streak = new GameObject("CoverStreak");
                streak.transform.position = position;
                streak.transform.rotation = Quaternion.Euler(0, 0, a);
                var sr = streak.AddComponent<SpriteRenderer>();
                sr.sprite = streakSprite;
                sr.sortingOrder = 72;
                float roll = Random.value;
                sr.color = roll < 0.4f
                    ? new Color(0.88f, 0.85f, 0.78f, 0.9f)  // 밝은 시멘트
                    : roll < 0.75f
                        ? new Color(0.62f, 0.58f, 0.5f, 0.85f)  // 회색 돌
                        : new Color(0.42f, 0.38f, 0.32f, 0.8f);  // 어두운 파편
                float len = Random.Range(0.12f, 0.38f);
                streak.transform.localScale = new Vector3(len, 0.09f, 1f);  // 두꺼운 파편
                var rb = streak.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 4.5f;  // 느린 콘크리트
                rb.linearVelocity = d * Random.Range(5f, 13f);
                streak.AddComponent<FadeAndShrink>();
                Object.Destroy(streak, Random.Range(0.15f, 0.32f));
            }

            // [3] 콘크리트 칩 1차 (ricochet sparkCount 대응 — 밝고 빠름)
            int chipCount = 18;
            for (int i = 0; i < chipCount; i++)
            {
                float a = scatterAngle + Random.Range(-65f, 65f);
                Vector2 d = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                float colorRoll = (float)i / chipCount;
                Color c = colorRoll < 0.35f
                    ? new Color(0.82f, 0.78f, 0.7f)
                    : colorRoll < 0.7f
                        ? new Color(0.58f, 0.54f, 0.46f)
                        : new Color(0.36f, 0.32f, 0.27f);
                float size = Random.Range(0.03f, 0.08f);
                var chip = P(position, c, size, 67);
                var rb = chip.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 2.8f;
                rb.linearVelocity = d * Random.Range(5f, 14f);
                chip.AddComponent<FadeAndShrink>();
                Object.Destroy(chip, Random.Range(0.15f, 0.4f));
            }

            // [4] 콘크리트 칩 2차 (리코 ember 대응 — 느린 잔파편 8개)
            for (int i = 0; i < 8; i++)
            {
                float a = Random.Range(0f, 360f);
                Vector2 d = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                Color c = i < 4
                    ? new Color(0.7f, 0.66f, 0.58f, 0.9f)
                    : new Color(0.48f, 0.44f, 0.38f, 0.75f);
                var frag = P(position + (Vector3)(Random.insideUnitCircle * 0.14f), c, Random.Range(0.02f, 0.06f), 64);
                var rb = frag.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 5f;
                rb.linearVelocity = d * Random.Range(1.5f, 5f);
                frag.AddComponent<FadeAndShrink>();
                Object.Destroy(frag, Random.Range(0.3f, 0.65f));
            }

            // [5] 큰 콘크리트 덩어리 (리코 debris 대응 — 4개, 느리게 굴러감)
            for (int i = 0; i < 4; i++)
            {
                float a = scatterAngle + Random.Range(-40f, 40f);
                Vector2 d = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                var chunk = P(position, new Color(0.65f, 0.62f, 0.55f), Random.Range(0.08f, 0.14f), 62);
                var rb = chunk.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 6f;
                rb.linearVelocity = d * Random.Range(2f, 6f);
                chunk.AddComponent<FadeAndShrink>();
                Object.Destroy(chunk, Random.Range(0.45f, 0.9f));
            }

            // [6] 먼지 구름 (3개)
            for (int i = 0; i < 3; i++)
            {
                Vector2 dustDir = new Vector2(
                    scatterDir.x * Random.Range(0.2f, 0.6f),
                    Random.Range(0.3f, 0.7f));  // upward bias
                var dust = P(position + (Vector3)(Random.insideUnitCircle * 0.1f),
                    new Color(0.72f, 0.68f, 0.6f, 0.45f),
                    Random.Range(0.18f, 0.35f), 54);
                var rb = dust.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 1.3f;
                rb.linearVelocity = dustDir * Random.Range(0.5f, 1.5f);
                dust.AddComponent<FadeAndShrink>();
                Object.Destroy(dust, Random.Range(0.55f, 1.1f));
            }
        }

        /// <summary>탄약고 유폭 — 기본 격파보다 2~3배 스케일, 포탑 비산 연출</summary>
        public static void SpawnExplosionAmmoRack(Vector3 position)
        {
            // [1] 순간 백열 섬광 (압력파)
            var core1 = P(position, new Color(1f, 1f, 0.98f, 1f), 1.0f, 76);
            Object.Destroy(core1, 0.04f);
            var core2 = P(position, new Color(1f, 0.97f, 0.8f, 1f), 1.6f, 75);
            Object.Destroy(core2, 0.07f);

            // [2] 압력파 링 (동심원 3개 순차 확대 시뮬레이션)
            for (int r = 0; r < 4; r++)
            {
                float rad = 0.3f + r * 0.45f;
                float alpha = Mathf.Lerp(0.7f, 0f, r / 4f);
                var ring = P(position, new Color(1f, 0.92f, 0.65f, alpha), rad, 74 - r);
                ring.AddComponent<FadeAndShrink>();
                Object.Destroy(ring, 0.1f + r * 0.04f);
            }

            // [3] 거대 화염구 (기본의 2배)
            var ball1 = P(position, new Color(1f, 0.9f, 0.45f, 1f), 1.6f, 73);
            ball1.AddComponent<FadeAndShrink>();
            Object.Destroy(ball1, 0.28f);

            var ball2 = P(position, new Color(1f, 0.5f, 0.1f, 0.92f), 2.2f, 72);
            ball2.AddComponent<FadeAndShrink>();
            Object.Destroy(ball2, 0.55f);

            var ball3 = P(position, new Color(0.65f, 0.15f, 0.04f, 0.75f), 2.5f, 71);
            ball3.AddComponent<FadeAndShrink>();
            Object.Destroy(ball3, 0.9f);

            // [4] 고속 화염 줄기 (36개, 기본보다 빠르고 김)
            var streakSprite = GetStreakSprite();
            for (int i = 0; i < 36; i++)
            {
                float angle = Random.Range(0f, 360f);
                Vector2 d = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                var streak = new GameObject("AmmoStreak");
                streak.transform.position = position;
                streak.transform.rotation = Quaternion.Euler(0, 0, angle);
                var sr = streak.AddComponent<SpriteRenderer>();
                sr.sprite = streakSprite;
                sr.sortingOrder = 74;
                float roll = Random.value;
                sr.color = roll < 0.3f
                    ? new Color(1f, 0.95f, 0.7f, 0.95f)
                    : roll < 0.65f
                        ? new Color(1f, 0.65f, 0.18f, 0.9f)
                        : new Color(1f, 0.35f, 0.04f, 0.85f);
                float len = Random.Range(0.5f, 1.8f);
                streak.transform.localScale = new Vector3(len, 0.07f, 1f);
                var rb = streak.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 1.5f;
                rb.linearVelocity = d * Random.Range(16f, 36f);
                streak.AddComponent<FadeAndShrink>();
                Object.Destroy(streak, Random.Range(0.18f, 0.5f));
            }

            // [5] 중파편 (30개, 기본보다 크고 빠름)
            for (int i = 0; i < 30; i++)
            {
                float angle = Random.Range(0f, 360f);
                Vector2 d = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                Color c = i < 10 ? new Color(0.08f, 0.06f, 0.04f) :
                          i < 20 ? new Color(0.28f, 0.2f, 0.13f) :
                                   new Color(0.5f, 0.4f, 0.28f);
                var obj = P(position, c, Random.Range(0.05f, 0.14f), 64);
                var rb = obj.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 1.2f;
                rb.linearVelocity = d * Random.Range(7f, 24f);
                obj.AddComponent<FadeAndShrink>();
                Object.Destroy(obj, Random.Range(0.5f, 1.3f));
            }

            // [6] 포탑 비산 (대형 파편 1개 — 회전하며 날아감)
            {
                float angle = Random.Range(20f, 160f);
                Vector2 d = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                var turret = P(position, new Color(0.22f, 0.2f, 0.17f), 0.22f, 65);
                var rb = turret.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 0.7f;
                rb.angularVelocity = Random.Range(-280f, 280f);
                rb.linearVelocity = d * Random.Range(8f, 16f);
                turret.AddComponent<FadeAndShrink>();
                Object.Destroy(turret, Random.Range(0.9f, 1.6f));
            }

            // [7] 짙은 검은 연기 기둥 (8개, 더 크고 높이 솟음)
            for (int i = 0; i < 8; i++)
            {
                var smoke = P(position + (Vector3)(Random.insideUnitCircle * 0.32f),
                    new Color(0.06f, 0.05f, 0.04f, 0.82f),
                    Random.Range(0.5f, 1.1f), 55);
                var rb = smoke.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 0.5f;
                rb.linearVelocity = new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(1.8f, 4.2f));
                smoke.AddComponent<FadeAndShrink>();
                Object.Destroy(smoke, Random.Range(1.5f, 3.2f));
            }

            // [8] 잔열 불씨 (15개)
            for (int i = 0; i < 15; i++)
            {
                Vector2 d = Random.insideUnitCircle.normalized;
                var ember = P(position + (Vector3)(Random.insideUnitCircle * 0.5f),
                    i < 8 ? new Color(1f, 0.6f, 0.1f, 0.8f) : new Color(0.8f, 0.25f, 0.05f, 0.6f),
                    Random.Range(0.03f, 0.07f), 63);
                var rb = ember.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 2f;
                rb.linearVelocity = d * Random.Range(3f, 11f);
                ember.AddComponent<FadeAndShrink>();
                Object.Destroy(ember, Random.Range(0.5f, 1.3f));
            }

            // [9] 잔광 (대형, 지속)
            var afterglow = P(position, new Color(1f, 0.28f, 0.04f, 0.5f), 1.4f, 56);
            afterglow.AddComponent<FadeAndShrink>();
            Object.Destroy(afterglow, 1.6f);
        }

        // ===== 유틸 =====
        private static GameObject P(Vector3 pos, Color color, float scale, int sortOrder)
        {
            var obj = new GameObject("FX");
            obj.transform.position = pos;
            obj.transform.localScale = Vector3.one * scale;

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = GetCircle();
            sr.color = color;
            sr.sortingOrder = sortOrder;

            return obj;
        }

        private static Sprite GetCircle()
        {
            if (_cachedCircle != null) return _cachedCircle;

            int size = 8;
            var tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[size * size];
            int half = size / 2;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(half, half));
                    pixels[y * size + x] = dist <= half ? Color.white : Color.clear;
                }

            tex.SetPixels(pixels);
            tex.Apply();
            _cachedCircle = Sprite.Create(tex, new Rect(0, 0, size, size),
                                           new Vector2(0.5f, 0.5f), size);
            return _cachedCircle;
        }
    }
}
