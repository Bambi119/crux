using UnityEngine;
using Crux.Data;

namespace Crux.Combat
{
    /// <summary>포연 이펙트 — 경량화 (리소스 절약)</summary>
    public static class MuzzleFlash
    {
        private static Sprite _cachedCircle;
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

        /// <summary>주포 포연</summary>
        public static void Spawn(Vector3 position, Vector2 direction)
        {
            var d = Data;

            // 섬광 (1개)
            var flash = CreateP(position + (Vector3)(direction * d.muzzleFlashOffset),
                                d.muzzleFlashColor, d.muzzleFlashScale, d.muzzleFlashSort);
            Object.Destroy(flash, d.muzzleFlashLife);

            // 화구 (1개)
            var fireball = CreateP(position + (Vector3)(direction * d.muzzleFireballOffset),
                                    d.muzzleFireballColor, d.muzzleFireballScale, d.muzzleFireballSort);
            fireball.AddComponent<FadeAndShrink>();
            Object.Destroy(fireball, d.muzzleFireballLife);

            // 불꽃 (4개)
            for (int i = 0; i < d.muzzleFlameCount; i++)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                angle += Random.Range(-45f, 45f);
                Vector2 dir = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad)
                );

                var flame = CreateP(position, d.muzzleFlameColor,
                                    Random.Range(d.muzzleFlameSizeMin, d.muzzleFlameSizeMax), d.muzzleFlameSort);
                var rb = flame.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.muzzleFlameDamping;
                rb.linearVelocity = dir * Random.Range(d.muzzleFlameSpeedMin, d.muzzleFlameSpeedMax);
                flame.AddComponent<FadeAndShrink>();
                Object.Destroy(flame, d.muzzleFlameLife);
            }

            // 연기 (3개)
            for (int i = 0; i < d.muzzleSmokeCount; i++)
            {
                float gray = Random.Range(d.muzzleSmokeGrayMin, d.muzzleSmokeGrayMax);
                var smoke = CreateP(
                    position + (Vector3)(Random.insideUnitCircle * d.muzzleSmokeRadius),
                    new Color(gray, gray, gray, d.muzzleSmokeAlpha),
                    Random.Range(d.muzzleSmokeSizeMin, d.muzzleSmokeSizeMax), d.muzzleSmokeSort);

                var rb = smoke.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = d.muzzleSmokeDamping;
                rb.linearVelocity = direction * Random.Range(d.muzzleSmokeSpeedMainMin, d.muzzleSmokeSpeedMainMax)
                                   + Random.insideUnitCircle * d.muzzleSmokeSpeedSideMax;
                smoke.AddComponent<FadeAndShrink>();
                Object.Destroy(smoke, Random.Range(d.muzzleSmokeLifeMin, d.muzzleSmokeLifeMax));
            }
        }

        private static GameObject CreateP(Vector3 pos, Color color, float scale, int sortOrder)
        {
            var obj = new GameObject("MFX");
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

    /// <summary>페이드아웃 + 확대</summary>
    public class FadeAndShrink : MonoBehaviour
    {
        private SpriteRenderer sr;
        private float startTime;
        private float startScale;

        private void Start()
        {
            sr = GetComponent<SpriteRenderer>();
            startTime = Time.time;
            startScale = transform.localScale.x;
        }

        private void Update()
        {
            float elapsed = Time.time - startTime;
            float scale = startScale + elapsed * 0.6f;
            transform.localScale = Vector3.one * scale;

            if (sr != null)
            {
                var c = sr.color;
                float fade = Mathf.Clamp01(elapsed / 0.4f);
                sr.color = new Color(c.r, c.g, c.b, Mathf.Lerp(c.a, 0, fade));
            }
        }
    }
}
