using UnityEngine;

namespace Crux.Combat
{
    /// <summary>포연 이펙트 — 경량화 (리소스 절약)</summary>
    public static class MuzzleFlash
    {
        private static Sprite _cachedCircle;

        /// <summary>주포 포연</summary>
        public static void Spawn(Vector3 position, Vector2 direction)
        {
            // 섬광 (1개)
            var flash = CreateP(position + (Vector3)(direction * 0.1f),
                                new Color(1f, 0.9f, 0.3f, 1f), 0.5f, 70);
            Object.Destroy(flash, 0.06f);

            // 화구 (1개)
            var fireball = CreateP(position + (Vector3)(direction * 0.2f),
                                    new Color(1f, 0.6f, 0.15f, 0.9f), 0.35f, 68);
            fireball.AddComponent<FadeAndShrink>();
            Object.Destroy(fireball, 0.15f);

            // 불꽃 (4개)
            for (int i = 0; i < 4; i++)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                angle += Random.Range(-45f, 45f);
                Vector2 dir = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad)
                );

                var flame = CreateP(position, new Color(1f, 0.5f, 0.1f, 0.8f),
                                    Random.Range(0.06f, 0.12f), 65);
                var rb = flame.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 5f;
                rb.linearVelocity = dir * Random.Range(2f, 5f);
                flame.AddComponent<FadeAndShrink>();
                Object.Destroy(flame, 0.2f);
            }

            // 연기 (3개)
            for (int i = 0; i < 3; i++)
            {
                float gray = Random.Range(0.3f, 0.5f);
                var smoke = CreateP(
                    position + (Vector3)(Random.insideUnitCircle * 0.08f),
                    new Color(gray, gray, gray, 0.35f),
                    Random.Range(0.15f, 0.3f), 53);

                var rb = smoke.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 1.5f;
                rb.linearVelocity = direction * Random.Range(0.3f, 1.5f)
                                   + Random.insideUnitCircle * 0.4f;
                smoke.AddComponent<FadeAndShrink>();
                Object.Destroy(smoke, Random.Range(0.5f, 1f));
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
