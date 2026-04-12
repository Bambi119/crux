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

        public static void Spawn(Vector3 position, ShotOutcome outcome, Vector2 shellDir)
        {
            switch (outcome)
            {
                case ShotOutcome.Miss:
                    SpawnMiss(position);
                    break;
                case ShotOutcome.Ricochet:
                    SpawnRicochet(position, shellDir);
                    break;
                case ShotOutcome.Hit:
                    SpawnHit(position, shellDir);
                    break;
                case ShotOutcome.Penetration:
                    SpawnPenetration(position, shellDir);
                    break;
            }
        }

        /// <summary>전차 파괴 폭발</summary>
        public static void SpawnExplosion(Vector3 position)
        {
            // 화염구 2단
            var fire1 = P(position, new Color(1f, 0.9f, 0.4f, 1f), 0.7f, 71);
            fire1.AddComponent<FadeAndShrink>();
            Object.Destroy(fire1, 0.15f);

            var fire2 = P(position, new Color(1f, 0.4f, 0.1f, 0.8f), 0.5f, 70);
            fire2.AddComponent<FadeAndShrink>();
            Object.Destroy(fire2, 0.35f);

            // 파편 (8개)
            for (int i = 0; i < 8; i++)
            {
                Color c = i < 4 ? new Color(1f, 0.5f, 0.1f) : new Color(0.3f, 0.25f, 0.2f);
                var obj = P(position, c, Random.Range(0.04f, 0.1f), 66);
                var rb = obj.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 2f;
                rb.linearVelocity = Random.insideUnitCircle * Random.Range(4f, 10f);
                obj.AddComponent<FadeAndShrink>();
                Object.Destroy(obj, Random.Range(0.3f, 0.7f));
            }

            // 검은 연기 (3개)
            for (int i = 0; i < 3; i++)
            {
                var smoke = P(position + (Vector3)(Random.insideUnitCircle * 0.15f),
                              new Color(0.1f, 0.1f, 0.08f, 0.5f),
                              Random.Range(0.2f, 0.4f), 54);
                var rb = smoke.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 1f;
                rb.linearVelocity = Vector2.up * Random.Range(0.5f, 1.5f);
                smoke.AddComponent<FadeAndShrink>();
                Object.Destroy(smoke, Random.Range(0.8f, 1.5f));
            }
        }

        // ===== 빗나감 =====
        private static void SpawnMiss(Vector3 position)
        {
            var obj = P(position, new Color(0.5f, 0.45f, 0.3f, 0.5f), 0.08f, 40);
            obj.AddComponent<FadeAndShrink>();
            Object.Destroy(obj, 0.3f);
        }

        // ===== 도탄 — 스프라이트 + 금속 불꽃 파편 + 파티클 =====
        private static void SpawnRicochet(Vector3 position, Vector2 shellDir)
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

            // [4] 1차 스파크 — 빠르고 밝은 불꽃 (14개, 넓게 확산)
            for (int i = 0; i < 14; i++)
            {
                float spreadAngle = Mathf.Atan2(ricochetDir.y, ricochetDir.x) * Mathf.Rad2Deg;
                spreadAngle += Random.Range(-70f, 70f);
                Vector2 dir = new Vector2(
                    Mathf.Cos(spreadAngle * Mathf.Deg2Rad),
                    Mathf.Sin(spreadAngle * Mathf.Deg2Rad));

                Color c = i < 5
                    ? new Color(1f, 1f, 0.7f)      // 백열
                    : i < 9
                        ? new Color(1f, 0.85f, 0.3f)  // 밝은 노란
                        : new Color(1f, 0.55f, 0.1f);  // 주황

                float size = Random.Range(0.03f, 0.09f);
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

        // ===== 피격 — 착탄 방향 기반 스프라이트 + 화염 + 파편 =====
        private static void SpawnHit(Vector3 position, Vector2 shellDir)
        {
            SpawnImpact(position, shellDir, 1f);
        }

        /// <summary>피격/관통 공통 — 레퍼런스: 화구 + 방사형 불꽃줄기 + 파편 다수 + 연기</summary>
        private static void SpawnImpact(Vector3 position, Vector2 shellDir, float intensity)
        {
            Vector2 splashDir = -shellDir;
            float splashAngle = Mathf.Atan2(splashDir.y, splashDir.x) * Mathf.Rad2Deg;

            // [1] 스프라이트 애니메이션
            var frames = GetHitFrames();
            if (frames != null && frames.Length > 0)
            {
                SpriteAnimation.Play(position, frames, 0.7f,
                    scale: 0.12f * intensity, sortingOrder: 68, rotation: splashAngle);
            }

            // [2] 화구(fireball) — 4단: 백열 코어 → 백노란 → 노란 → 주황
            var core = P(position, new Color(1f, 1f, 1f, 1f), 0.4f * intensity, 72);
            Object.Destroy(core, 0.05f);

            var glow1 = P(position, new Color(1f, 1f, 0.85f, 1f), 0.55f * intensity, 71);
            glow1.AddComponent<FadeAndShrink>();
            Object.Destroy(glow1, 0.12f);

            var glow2 = P(position, new Color(1f, 0.85f, 0.3f, 0.9f), 0.7f * intensity, 70);
            glow2.AddComponent<FadeAndShrink>();
            Object.Destroy(glow2, 0.25f);

            var outer = P(position, new Color(1f, 0.5f, 0.1f, 0.6f), 0.9f * intensity, 69);
            outer.AddComponent<FadeAndShrink>();
            Object.Destroy(outer, 0.4f);

            // [3] 방사형 불꽃 줄기 — 선형 스프라이트가 중심에서 사방으로 뻗어나감
            int streakCount = (int)(16 * intensity);
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
                sr.sortingOrder = 66;
                // 밝은 노란~백색
                sr.color = Random.value < 0.5f
                    ? new Color(1f, 0.95f, 0.7f, 0.9f)
                    : new Color(1f, 0.8f, 0.3f, 0.8f);

                float len = Random.Range(0.3f, 0.8f) * intensity;
                streak.transform.localScale = new Vector3(len, 0.03f, 1f);

                var rb = streak.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 3f;
                rb.linearVelocity = dir * Random.Range(8f, 20f) * intensity;
                streak.AddComponent<FadeAndShrink>();
                Object.Destroy(streak, Random.Range(0.1f, 0.3f));
            }

            // [4] 검은/갈색 파편 다수 — 사방으로 멀리
            int debrisCount = (int)(30 * intensity);
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

                float size = Random.Range(0.015f, 0.05f) * intensity;
                var obj = P(position, c, size, 62);
                var rb = obj.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 1.5f;
                rb.linearVelocity = dir * Random.Range(3f, 18f) * intensity;
                obj.AddComponent<FadeAndShrink>();
                Object.Destroy(obj, Random.Range(0.3f, 0.9f));
            }

            // [5] 연기/먼지 구름 — 크고 어둡게
            int dustCount = (int)(6 * intensity);
            for (int i = 0; i < dustCount; i++)
            {
                Vector2 dustDir = Random.insideUnitCircle;
                float dustSize = Random.Range(0.2f, 0.5f) * intensity;

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
            var afterglow = P(position, new Color(1f, 0.4f, 0.1f, 0.4f), 0.5f * intensity, 56);
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

        // ===== 관통 — Hit VFX 확대 + 추가 파티클 =====
        private static void SpawnPenetration(Vector3 position, Vector2 shellDir)
        {
            SpawnImpact(position, shellDir, 1.6f);
        }

        // ===== 엄폐물 피격 — 콘크리트/돌 파편 =====
        public static void SpawnCoverHit(Vector3 position)
        {
            // 회갈색 섬광
            var flash = P(position, new Color(0.7f, 0.65f, 0.5f, 1f), 0.25f, 68);
            Object.Destroy(flash, 0.06f);

            // 돌 파편 (6개)
            for (int i = 0; i < 6; i++)
            {
                Color c = i < 3
                    ? new Color(0.5f, 0.45f, 0.35f)   // 갈색 돌
                    : new Color(0.6f, 0.58f, 0.55f);   // 회색 돌
                var obj = P(position, c, Random.Range(0.03f, 0.07f), 63);
                var rb = obj.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 3f;
                rb.linearVelocity = Random.insideUnitCircle * Random.Range(2f, 6f);
                obj.AddComponent<FadeAndShrink>();
                Object.Destroy(obj, Random.Range(0.2f, 0.5f));
            }

            // 먼지 연기 (2개)
            for (int i = 0; i < 2; i++)
            {
                var smoke = P(position + (Vector3)(Random.insideUnitCircle * 0.1f),
                              new Color(0.55f, 0.5f, 0.4f, 0.4f),
                              Random.Range(0.15f, 0.25f), 54);
                var rb = smoke.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 1.5f;
                rb.linearVelocity = Vector2.up * Random.Range(0.3f, 0.8f);
                smoke.AddComponent<FadeAndShrink>();
                Object.Destroy(smoke, Random.Range(0.5f, 0.9f));
            }
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
