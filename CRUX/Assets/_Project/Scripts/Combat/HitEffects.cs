using UnityEngine;
using Crux.Core;
using Crux.Data;

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
        private static VfxRenderDataSO _data;

        static VfxRenderDataSO Data
        {
            get
            {
                if (_data == null)
                {
                    _data = Resources.Load<VfxRenderDataSO>("Vfx/VfxRenderData");
                    if (_data == null) _data = ScriptableObject.CreateInstance<VfxRenderDataSO>();
                }
                return _data;
            }
        }

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
            var d = Data;

            // [1] 섬광 코어
            var core = P(position, d.expFlashCoreColor, d.expFlashCoreScale, d.sortExpFlash1);
            Object.Destroy(core, d.expFlashCoreLife);

            // [2] 화염구 3단
            var fire1 = P(position, d.expFire1Color, d.expFire1Scale, d.sortStreakExplosion - 1);
            fire1.AddComponent<FadeAndShrink>();
            Object.Destroy(fire1, d.expFire1Life);

            var fire2 = P(position, d.expFire2Color, d.expFire2Scale, d.sortStreakExplosion - 2);
            fire2.AddComponent<FadeAndShrink>();
            Object.Destroy(fire2, d.expFire2Life);

            var fire3 = P(position, d.expFire3Color, d.expFire3Scale, d.sortStreakExplosion - 3);
            fire3.AddComponent<FadeAndShrink>();
            Object.Destroy(fire3, d.expFire3Life);

            // [3] 방사형 화염 줄기
            var streakSprite = GetStreakSprite();
            for (int i = 0; i < d.expStreakCount; i++)
            {
                float angle = Random.Range(0f, 360f);
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                var streak = new GameObject("ExpStreak");
                streak.transform.position = position;
                streak.transform.rotation = Quaternion.Euler(0, 0, angle);
                var sr = streak.AddComponent<SpriteRenderer>();
                sr.sprite = streakSprite;
                sr.sortingOrder = d.sortStreakExplosion;
                sr.color = Random.value < 0.5f ? d.expStreakColorA : d.expStreakColorB;
                float len = Random.Range(d.expStreakLenMin, d.expStreakLenMax);
                streak.transform.localScale = new Vector3(len, d.expStreakScaleY, 1f);
                var rb = streak.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.expStreakDamping;
                rb.linearVelocity = dir * Random.Range(d.expStreakSpeedMin, d.expStreakSpeedMax);
                streak.AddComponent<FadeAndShrink>();
                Object.Destroy(streak, Random.Range(d.expStreakLifeMin, d.expStreakLifeMax));
            }

            // [4] 금속 파편 (20개, 사방)
            for (int i = 0; i < d.expDebrisCount; i++)
            {
                float angle = Random.Range(0f, 360f);
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                Color c = i < 7 ? d.expDebrisBlack :
                          i < 14 ? d.expDebrisMid :
                                   d.expDebrisLight;
                var obj = P(position, c, Random.Range(d.expDebrisSizeMin, d.expDebrisSizeMax), d.sortDebrisLow);
                var rb = obj.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.expDebrisDamping;
                rb.linearVelocity = dir * Random.Range(d.expDebrisSpeedMin, d.expDebrisSpeedMax);
                obj.AddComponent<FadeAndShrink>();
                Object.Destroy(obj, Random.Range(d.expDebrisLifeMin, d.expDebrisLifeMax));
            }

            // [5] 검은 연기 기둥 (5개)
            for (int i = 0; i < d.expSmokeCount; i++)
            {
                var smoke = P(position + (Vector3)(Random.insideUnitCircle * d.expSmokeSmokeRadius),
                    d.expSmokeColor,
                    Random.Range(d.expSmokeSizeMin, d.expSmokeSizeMax), d.sortSmoke);
                var rb = smoke.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.expSmokeDamping;
                rb.linearVelocity = new Vector2(Random.Range(d.expSmokeSpeedXMin, d.expSmokeSpeedXMax), Random.Range(d.expSmokeSpeedYMin, d.expSmokeSpeedYMax));
                smoke.AddComponent<FadeAndShrink>();
                Object.Destroy(smoke, Random.Range(d.expSmokeLifeMin, d.expSmokeLifeMax));
            }

            // [6] 잔불 (잔광)
            var glow = P(position, d.expGlowColor, d.expGlowScale, d.sortAfterGlow);
            glow.AddComponent<FadeAndShrink>();
            Object.Destroy(glow, d.expGlowLife);
        }

        // ===== 빗나감 — 흙먼지 + 방향성 파편 =====
        private static void SpawnMiss(Vector3 position)
        {
            var d = Data;

            // [1] 중심 흙먼지 디스크 — 크고 빠르게 퍼지다 사라짐
            var dust = P(position, d.missDustColor1, d.missDustScale1, 42);
            dust.AddComponent<FadeAndShrink>();
            Object.Destroy(dust, d.missDustLife1);

            // [2] 작은 흙먼지 보조 — 약간 오프셋
            var dust2 = P(position + (Vector3)Random.insideUnitCircle * 0.12f,
                          d.missDustColor2, d.missDustScale2, 41);
            dust2.AddComponent<FadeAndShrink>();
            Object.Destroy(dust2, d.missDustLife2);

            // [3] 파편 3개 — Rigidbody2D로 방향성 분산
            for (int i = 0; i < 3; i++)
            {
                float ang = Random.Range(0f, 360f);
                var chip = new GameObject("MissChip");
                chip.transform.position = position;
                var sr = chip.AddComponent<SpriteRenderer>();
                sr.sprite = GetCircle();
                sr.sortingOrder = 43;
                sr.color = d.missChipColor;
                chip.transform.localScale = Vector3.one * Random.Range(d.missChipScaleMin, d.missChipScaleMax);
                var rb = chip.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.missChipDamping;
                Vector2 dir = new Vector2(
                    Mathf.Cos(ang * Mathf.Deg2Rad),
                    Mathf.Sin(ang * Mathf.Deg2Rad));
                rb.linearVelocity = dir * Random.Range(d.missChipSpeedMin, d.missChipSpeedMax);
                chip.AddComponent<FadeAndShrink>();
                Object.Destroy(chip, Random.Range(d.missChipLifeMin, d.missChipLifeMax));
            }
        }

        // ===== 도탄 — 스프라이트 + 금속 불꽃 파편 + 파티클 =====
        private static void SpawnRicochet(Vector3 position, Vector2 shellDir, float caliberScale = 1f)
        {
            var d = Data;

            // [1] 스프라이트 애니메이션
            var frames = GetRicochetFrames();
            if (frames != null && frames.Length > 0)
            {
                float angle = Mathf.Atan2(shellDir.y, shellDir.x) * Mathf.Rad2Deg;
                SpriteAnimation.Play(position, frames, 0.8f,
                    scale: 0.12f, sortingOrder: d.sortHitAnim, rotation: angle);
            }

            // [2] 충돌 섬광 — 2단 (밝은 백색 → 주황)
            var flash1 = P(position, d.ricoFlash1Color, d.ricoFlash1Scale, d.sortFlash1);
            Object.Destroy(flash1, d.ricoFlash1Life);
            var flash2 = P(position, d.ricoFlash2Color, d.ricoFlash2Scale, d.sortFlash2);
            flash2.AddComponent<FadeAndShrink>();
            Object.Destroy(flash2, d.ricoFlash2Life);

            // [3] 도탄 방향 계산
            Vector2 ricochetDir = Vector2.Reflect(shellDir,
                                    (Random.insideUnitCircle + Vector2.up).normalized);
            float ricochetAngleDeg = Mathf.Atan2(ricochetDir.y, ricochetDir.x) * Mathf.Rad2Deg;

            // [3.5] 방향성 불꽃 줄기 — 도탄 방향 위주로 좁게 분사 (얇고 빠름)
            int streakCountR = Mathf.RoundToInt(d.ricoStreakBaseCount * caliberScale);
            var streakSpriteR = GetStreakSprite();
            for (int i = 0; i < streakCountR; i++)
            {
                float a = ricochetAngleDeg + Random.Range(-45f, 45f);
                Vector2 dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));

                var streak = new GameObject("RicoStreak");
                streak.transform.position = position;
                streak.transform.rotation = Quaternion.Euler(0, 0, a);
                var sr = streak.AddComponent<SpriteRenderer>();
                sr.sprite = streakSpriteR;
                sr.sortingOrder = d.sortStreakHit;
                sr.color = Random.value < 0.5f ? d.ricoStreakColorA : d.ricoStreakColorB;
                float len = Random.Range(d.ricoStreakLenMin, d.ricoStreakLenMax) * caliberScale;
                streak.transform.localScale = new Vector3(len, d.ricoStreakScaleY, 1f);
                var rb = streak.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.ricoStreakDamping;
                rb.linearVelocity = dir * Random.Range(d.ricoStreakSpeedMin, d.ricoStreakSpeedMax);
                streak.AddComponent<FadeAndShrink>();
                Object.Destroy(streak, Random.Range(d.ricoStreakLifeMin, d.ricoStreakLifeMax));
            }

            // [4] 1차 스파크 — 빠르고 밝은 불꽃 (구경 비례 확장, 넓게 확산)
            int sparkCount = Mathf.RoundToInt(d.ricoSparkBaseCount * caliberScale);
            for (int i = 0; i < sparkCount; i++)
            {
                float spreadAngle = Mathf.Atan2(ricochetDir.y, ricochetDir.x) * Mathf.Rad2Deg;
                spreadAngle += Random.Range(-70f, 70f);
                Vector2 dir = new Vector2(
                    Mathf.Cos(spreadAngle * Mathf.Deg2Rad),
                    Mathf.Sin(spreadAngle * Mathf.Deg2Rad));

                // 색 분포: 35% 백열, 30% 노란, 35% 주황 (개수 변동에 무관)
                float colorRoll = (float)i / sparkCount;
                Color c = colorRoll < 0.35f ? d.ricoSparkColorWhite
                    : colorRoll < 0.65f ? d.ricoSparkColorYellow
                    : d.ricoSparkColorOrange;

                float size = Random.Range(d.ricoSparkSizeMin, d.ricoSparkSizeMax);
                var spark = P(position, c, size, d.sortSpark);
                var rb = spark.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.ricoSparkDamping;
                rb.linearVelocity = dir * Random.Range(d.ricoSparkSpeedMin, d.ricoSparkSpeedMax);
                spark.AddComponent<FadeAndShrink>();
                Object.Destroy(spark, Random.Range(d.ricoSparkLifeMin, d.ricoSparkLifeMax));
            }

            // [5] 2차 스파크 — 느리고 작은 잔불꽃 (8개, 지연 생성)
            for (int i = 0; i < d.ricoEmberCount; i++)
            {
                float spreadAngle = Random.Range(0f, 360f);
                Vector2 dir = new Vector2(
                    Mathf.Cos(spreadAngle * Mathf.Deg2Rad),
                    Mathf.Sin(spreadAngle * Mathf.Deg2Rad));

                Color c = i < 4 ? d.ricoEmberColorA : d.ricoEmberColorB;

                float size = Random.Range(d.ricoEmberSizeMin, d.ricoEmberSizeMax);
                var ember = P(position + (Vector3)(Random.insideUnitCircle * 0.15f), c, size, d.sortDebrisLow);
                var rb = ember.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.ricoEmberDamping;
                rb.linearVelocity = dir * Random.Range(d.ricoEmberSpeedMin, d.ricoEmberSpeedMax);
                ember.AddComponent<FadeAndShrink>();
                Object.Destroy(ember, Random.Range(d.ricoEmberLifeMin, d.ricoEmberLifeMax));
            }

            // [6] 금속 파편 — 무거운 조각 (4개, 느리게 굴러감)
            for (int i = 0; i < d.ricoDebrisCount; i++)
            {
                float spreadAngle = Mathf.Atan2(ricochetDir.y, ricochetDir.x) * Mathf.Rad2Deg;
                spreadAngle += Random.Range(-40f, 40f);
                Vector2 dir = new Vector2(
                    Mathf.Cos(spreadAngle * Mathf.Deg2Rad),
                    Mathf.Sin(spreadAngle * Mathf.Deg2Rad));

                var debris = P(position, d.ricoDebrisColor, Random.Range(d.ricoDebrisSizeMin, d.ricoDebrisSizeMax), d.sortDebrisMid - 2);
                var rb = debris.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.ricoDebrisDamping;
                rb.linearVelocity = dir * Random.Range(d.ricoDebrisSpeedMin, d.ricoDebrisSpeedMax);
                debris.AddComponent<FadeAndShrink>();
                Object.Destroy(debris, Random.Range(d.ricoDebrisLifeMin, d.ricoDebrisLifeMax));
            }

            // [7] 연기 (3개)
            for (int i = 0; i < d.ricoSmokeCount; i++)
            {
                var smoke = P(position + (Vector3)(Random.insideUnitCircle * 0.08f),
                    d.ricoSmokeColor, Random.Range(d.ricoSmokeSizeMin, d.ricoSmokeSizeMax), d.sortSmoke);
                var rb = smoke.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.ricoSmokeDamping;
                rb.linearVelocity = (Vector2)ricochetDir * Random.Range(0.3f, 1.2f)
                    + Vector2.up * Random.Range(0.2f, 0.6f);
                smoke.AddComponent<FadeAndShrink>();
                Object.Destroy(smoke, Random.Range(d.ricoSmokeLifeMin, d.ricoSmokeLifeMax));
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
            var d = Data;
            float effectScale = intensity * caliberScale;
            Vector2 splashDir = -shellDir;
            float splashAngle = Mathf.Atan2(splashDir.y, splashDir.x) * Mathf.Rad2Deg;

            // [1] 스프라이트 애니메이션
            var frames = GetHitFrames();
            if (frames != null && frames.Length > 0)
            {
                SpriteAnimation.Play(position, frames, 0.7f,
                    scale: 0.12f * effectScale, sortingOrder: d.sortHitAnim, rotation: splashAngle);
            }

            // [2] 화구(fireball) — 4단: 백열 코어 → 백노란 → 노란 → 주황
            var core = P(position, d.impactCoreColor, d.impactCoreScale * effectScale, d.sortFireballCore);
            Object.Destroy(core, d.impactCoreLife);

            var glow1 = P(position, d.impactGlow1Color, d.impactGlow1Scale * effectScale, d.sortFireball1);
            glow1.AddComponent<FadeAndShrink>();
            Object.Destroy(glow1, d.impactGlow1Life);

            var glow2 = P(position, d.impactGlow2Color, d.impactGlow2Scale * effectScale, d.sortFlash2);
            glow2.AddComponent<FadeAndShrink>();
            Object.Destroy(glow2, d.impactGlow2Life);

            var outer = P(position, d.impactOuterColor, d.impactOuterScale * effectScale, d.sortFireball3);
            outer.AddComponent<FadeAndShrink>();
            Object.Destroy(outer, d.impactOuterLife);

            // [3] 방사형 불꽃 줄기
            int streakCount = Mathf.RoundToInt(d.impactStreakBase * effectScale);
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
                sr.sortingOrder = d.sortStreakHit;
                float roll = Random.value;
                sr.color = roll < 0.4f ? d.impactStreakColorA
                    : roll < 0.75f ? d.impactStreakColorB
                    : d.impactStreakColorC;

                float len = Random.Range(d.impactStreakLenMin, d.impactStreakLenMax) * effectScale;
                streak.transform.localScale = new Vector3(len, d.impactStreakScaleY, 1f);

                var rb = streak.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.impactStreakDamping;
                rb.linearVelocity = dir * Random.Range(d.impactStreakSpeedMin, d.impactStreakSpeedMax) * intensity;
                streak.AddComponent<FadeAndShrink>();
                Object.Destroy(streak, Random.Range(d.impactStreakLifeMin, d.impactStreakLifeMax));
            }

            // [4] 검은/갈색 파편 다수 — 사방으로 멀리
            int debrisCount = Mathf.RoundToInt(d.impactDebrisBase * effectScale);
            for (int i = 0; i < debrisCount; i++)
            {
                float angle = Random.Range(0f, 360f);
                Vector2 dir = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad));

                Color c;
                if (i < debrisCount / 4)
                    c = d.impactDebrisBlack;
                else if (i < debrisCount / 2)
                    c = d.impactDebrisBrown;
                else if (i < debrisCount * 3 / 4)
                    c = d.impactDebrisLight;
                else
                    c = d.impactDebrisFire;

                float size = Random.Range(d.impactDebrisSizeMin, d.impactDebrisSizeMax) * effectScale;
                var obj = P(position, c, size, d.sortDebrisLow);
                var rb = obj.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.impactDebrisDamping;
                rb.linearVelocity = dir * Random.Range(d.impactDebrisSpeedMin, d.impactDebrisSpeedMax) * intensity;
                obj.AddComponent<FadeAndShrink>();
                Object.Destroy(obj, Random.Range(d.impactDebrisLifeMin, d.impactDebrisLifeMax));
            }

            // [5] 연기/먼지 구름 — 크고 어둡게
            int dustCount = Mathf.RoundToInt(d.impactDustBase * effectScale);
            for (int i = 0; i < dustCount; i++)
            {
                Vector2 dustDir = Random.insideUnitCircle;
                float dustSize = Random.Range(d.impactDustSizeMin, d.impactDustSizeMax) * effectScale;

                Color dustColor;
                if (i < dustCount / 3)
                    dustColor = d.impactDustBlack;
                else if (i < dustCount * 2 / 3)
                    dustColor = d.impactDustBrown;
                else
                    dustColor = d.impactDustLight;

                var dust = P(position + (Vector3)(Random.insideUnitCircle * 0.2f),
                    dustColor, dustSize, d.sortSmoke);
                var rb = dust.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.impactDustDamping;
                rb.linearVelocity = dustDir * Random.Range(d.impactDustSpeedMin, d.impactDustSpeedMax) * intensity;
                dust.AddComponent<FadeAndShrink>();
                Object.Destroy(dust, Random.Range(d.impactDustLifeMin, d.impactDustLifeMax));
            }

            // [6] 잔광(afterglow) — 서서히 사라지는 주황 원
            var afterglow = P(position, d.impactAfterglowColor, d.impactAfterglowScale * effectScale, d.sortAfterGlow);
            afterglow.AddComponent<FadeAndShrink>();
            Object.Destroy(afterglow, d.impactAfterglowLife);
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
            var d = Data;
            Vector2 scatterDir = shellDir.normalized;
            if (scatterDir == Vector2.zero) scatterDir = Vector2.right;
            float scatterAngle = Mathf.Atan2(scatterDir.y, scatterDir.x) * Mathf.Rad2Deg;

            // [1] 회백색 충격 섬광 (2단)
            var flash1 = P(position, d.coverFlash1Color, d.coverFlash1Scale, d.sortFlash1);
            Object.Destroy(flash1, d.coverFlash1Life);
            var flash2 = P(position, d.coverFlash2Color, d.coverFlash2Scale, d.sortFlash2);
            flash2.AddComponent<FadeAndShrink>();
            Object.Destroy(flash2, d.coverFlash2Life);

            // [2] 방향성 콘크리트 파편 줄기
            var streakSprite = GetStreakSprite();
            for (int i = 0; i < d.coverStreakCount; i++)
            {
                float a = scatterAngle + Random.Range(-55f, 55f);
                Vector2 dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));

                var streak = new GameObject("CoverStreak");
                streak.transform.position = position;
                streak.transform.rotation = Quaternion.Euler(0, 0, a);
                var sr = streak.AddComponent<SpriteRenderer>();
                sr.sprite = streakSprite;
                sr.sortingOrder = d.sortFireballCore;
                float roll = Random.value;
                sr.color = roll < 0.4f ? d.coverStreakColorA
                    : roll < 0.75f ? d.coverStreakColorB
                    : d.coverStreakColorC;
                float len = Random.Range(d.coverStreakLenMin, d.coverStreakLenMax);
                streak.transform.localScale = new Vector3(len, d.coverStreakScaleY, 1f);
                var rb = streak.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.coverStreakDamping;
                rb.linearVelocity = dir * Random.Range(d.coverStreakSpeedMin, d.coverStreakSpeedMax);
                streak.AddComponent<FadeAndShrink>();
                Object.Destroy(streak, Random.Range(d.coverStreakLifeMin, d.coverStreakLifeMax));
            }

            // [3] 콘크리트 칩 1차 (밝고 빠름)
            for (int i = 0; i < d.coverChipCount1; i++)
            {
                float a = scatterAngle + Random.Range(-65f, 65f);
                Vector2 dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                float colorRoll = (float)i / d.coverChipCount1;
                Color c = colorRoll < 0.35f ? d.coverChipColorA
                    : colorRoll < 0.7f ? d.coverChipColorB
                    : d.coverChipColorC;
                float size = Random.Range(d.coverChipSizeMin, d.coverChipSizeMax);
                var chip = P(position, c, size, d.sortSpark);
                var rb = chip.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.coverChipDamping;
                rb.linearVelocity = dir * Random.Range(d.coverChipSpeedMin, d.coverChipSpeedMax);
                chip.AddComponent<FadeAndShrink>();
                Object.Destroy(chip, Random.Range(d.coverChipLifeMin, d.coverChipLifeMax));
            }

            // [4] 콘크리트 칩 2차 (느린 잔파편)
            for (int i = 0; i < d.coverEmberCount; i++)
            {
                float a = Random.Range(0f, 360f);
                Vector2 dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                Color c = i < 4 ? d.coverEmberColorA : d.coverEmberColorB;
                var frag = P(position + (Vector3)(Random.insideUnitCircle * 0.14f), c, Random.Range(d.coverEmberSizeMin, d.coverEmberSizeMax), d.sortDebrisLow);
                var rb = frag.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.coverEmberDamping;
                rb.linearVelocity = dir * Random.Range(d.coverEmberSpeedMin, d.coverEmberSpeedMax);
                frag.AddComponent<FadeAndShrink>();
                Object.Destroy(frag, Random.Range(d.coverEmberLifeMin, d.coverEmberLifeMax));
            }

            // [5] 큰 콘크리트 덩어리 (4개, 느리게 굴러감)
            for (int i = 0; i < d.coverChunkCount; i++)
            {
                float a = scatterAngle + Random.Range(-40f, 40f);
                Vector2 dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                var chunk = P(position, d.coverChunkColor, Random.Range(d.coverChunkSizeMin, d.coverChunkSizeMax), d.sortDebrisLow);
                var rb = chunk.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.coverChunkDamping;
                rb.linearVelocity = dir * Random.Range(d.coverChunkSpeedMin, d.coverChunkSpeedMax);
                chunk.AddComponent<FadeAndShrink>();
                Object.Destroy(chunk, Random.Range(d.coverChunkLifeMin, d.coverChunkLifeMax));
            }

            // [6] 먼지 구름 (3개)
            for (int i = 0; i < d.coverDustCount; i++)
            {
                Vector2 dustDir = new Vector2(
                    scatterDir.x * Random.Range(0.2f, 0.6f),
                    Random.Range(0.3f, 0.7f));  // upward bias
                var dust = P(position + (Vector3)(Random.insideUnitCircle * 0.1f),
                    d.coverDustColor,
                    Random.Range(d.coverDustSizeMin, d.coverDustSizeMax), 54);
                var rb = dust.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.coverDustDamping;
                rb.linearVelocity = dustDir * Random.Range(d.coverDustSpeedMin, d.coverDustSpeedMax);
                dust.AddComponent<FadeAndShrink>();
                Object.Destroy(dust, Random.Range(d.coverDustLifeMin, d.coverDustLifeMax));
            }
        }

        /// <summary>탄약고 유폭 — 기본 격파보다 2~3배 스케일, 포탑 비산 연출</summary>
        public static void SpawnExplosionAmmoRack(Vector3 position)
        {
            var d = Data;

            // [1] 순간 백열 섬광 (압력파)
            var core1 = P(position, d.ammoCore1Color, d.ammoCore1Scale, d.sortExpFlash1);
            Object.Destroy(core1, d.ammoCore1Life);
            var core2 = P(position, d.ammoCore2Color, d.ammoCore2Scale, d.sortStreakExplosion + 1);
            Object.Destroy(core2, d.ammoCore2Life);

            // [2] 압력파 링 (동심원 4개 순차 확대 시뮬레이션)
            for (int r = 0; r < d.ammoRingCount; r++)
            {
                float rad = d.ammoRingRadBase + r * d.ammoRingRadStep;
                float alpha = Mathf.Lerp(0.7f, 0f, r / (float)d.ammoRingCount);
                var ring = P(position, new Color(d.ammoRingColorBase.r, d.ammoRingColorBase.g, d.ammoRingColorBase.b, alpha), rad, d.sortStreakExplosion - r);
                ring.AddComponent<FadeAndShrink>();
                Object.Destroy(ring, d.ammoRingLifeBase + r * d.ammoRingLifeStep);
            }

            // [3] 거대 화염구 (기본의 2배)
            var ball1 = P(position, d.ammoBall1Color, d.ammoBall1Scale, d.sortStreakExplosion - 1);
            ball1.AddComponent<FadeAndShrink>();
            Object.Destroy(ball1, d.ammoBall1Life);

            var ball2 = P(position, d.ammoBall2Color, d.ammoBall2Scale, d.sortStreakExplosion - 2);
            ball2.AddComponent<FadeAndShrink>();
            Object.Destroy(ball2, d.ammoBall2Life);

            var ball3 = P(position, d.ammoBall3Color, d.ammoBall3Scale, d.sortStreakExplosion - 3);
            ball3.AddComponent<FadeAndShrink>();
            Object.Destroy(ball3, d.ammoBall3Life);

            // [4] 고속 화염 줄기 (36개, 기본보다 빠르고 김)
            var streakSprite = GetStreakSprite();
            for (int i = 0; i < d.ammoStreakCount; i++)
            {
                float angle = Random.Range(0f, 360f);
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                var streak = new GameObject("AmmoStreak");
                streak.transform.position = position;
                streak.transform.rotation = Quaternion.Euler(0, 0, angle);
                var sr = streak.AddComponent<SpriteRenderer>();
                sr.sprite = streakSprite;
                sr.sortingOrder = d.sortStreakExplosion;
                float roll = Random.value;
                sr.color = roll < 0.3f ? d.ammoStreakColorA
                    : roll < 0.65f ? d.ammoStreakColorB
                    : d.ammoStreakColorC;
                float len = Random.Range(d.ammoStreakLenMin, d.ammoStreakLenMax);
                streak.transform.localScale = new Vector3(len, d.ammoStreakScaleY, 1f);
                var rb = streak.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.ammoStreakDamping;
                rb.linearVelocity = dir * Random.Range(d.ammoStreakSpeedMin, d.ammoStreakSpeedMax);
                streak.AddComponent<FadeAndShrink>();
                Object.Destroy(streak, Random.Range(d.ammoStreakLifeMin, d.ammoStreakLifeMax));
            }

            // [5] 중파편 (30개, 기본보다 크고 빠름)
            for (int i = 0; i < d.ammoDebrisCount; i++)
            {
                float angle = Random.Range(0f, 360f);
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                Color c = i < 10 ? d.ammoDebrisBlack :
                          i < 20 ? d.ammoDebrisMid :
                                   d.ammoDebrisLight;
                var obj = P(position, c, Random.Range(d.ammoDebrisSizeMin, d.ammoDebrisSizeMax), d.sortDebrisLow);
                var rb = obj.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.ammoDebrisDamping;
                rb.linearVelocity = dir * Random.Range(d.ammoDebrisSpeedMin, d.ammoDebrisSpeedMax);
                obj.AddComponent<FadeAndShrink>();
                Object.Destroy(obj, Random.Range(d.ammoDebrisLifeMin, d.ammoDebrisLifeMax));
            }

            // [6] 포탑 비산 (대형 파편 1개 — 회전하며 날아감)
            {
                float angle = Random.Range(d.ammoTurretAngleMin, d.ammoTurretAngleMax);
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                var turret = P(position, d.ammoTurretColor, d.ammoTurretScale, d.sortDebrisLow + 1);
                var rb = turret.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.ammoTurretDamping;
                rb.angularVelocity = Random.Range(d.ammoTurretAngVelMin, d.ammoTurretAngVelMax);
                rb.linearVelocity = dir * Random.Range(d.ammoTurretSpeedMin, d.ammoTurretSpeedMax);
                turret.AddComponent<FadeAndShrink>();
                Object.Destroy(turret, Random.Range(d.ammoTurretLifeMin, d.ammoTurretLifeMax));
            }

            // [7] 짙은 검은 연기 기둥 (8개, 더 크고 높이 솟음)
            for (int i = 0; i < d.ammoSmokeCount; i++)
            {
                var smoke = P(position + (Vector3)(Random.insideUnitCircle * d.ammoSmokeRadius),
                    d.ammoSmokeColor,
                    Random.Range(d.ammoSmokeSizeMin, d.ammoSmokeSizeMax), d.sortSmoke);
                var rb = smoke.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.ammoSmokeDamping;
                rb.linearVelocity = new Vector2(Random.Range(d.ammoSmokeSpeedXMin, d.ammoSmokeSpeedXMax), Random.Range(d.ammoSmokeSpeedYMin, d.ammoSmokeSpeedYMax));
                smoke.AddComponent<FadeAndShrink>();
                Object.Destroy(smoke, Random.Range(d.ammoSmokeLifeMin, d.ammoSmokeLifeMax));
            }

            // [8] 잔열 불씨 (15개)
            for (int i = 0; i < d.ammoEmberCount; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                var ember = P(position + (Vector3)(Random.insideUnitCircle * d.ammoEmberRadius),
                    i < 8 ? d.ammoEmberColorA : d.ammoEmberColorB,
                    Random.Range(d.ammoEmberSizeMin, d.ammoEmberSizeMax), d.sortDebrisMid - 1);
                var rb = ember.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.ammoEmberDamping;
                rb.linearVelocity = dir * Random.Range(d.ammoEmberSpeedMin, d.ammoEmberSpeedMax);
                ember.AddComponent<FadeAndShrink>();
                Object.Destroy(ember, Random.Range(d.ammoEmberLifeMin, d.ammoEmberLifeMax));
            }

            // [9] 잔광 (대형, 지속)
            var afterglow = P(position, d.ammoAfterglowColor, d.ammoAfterglowScale, d.sortAfterGlow);
            afterglow.AddComponent<FadeAndShrink>();
            Object.Destroy(afterglow, d.ammoAfterglowLife);
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
