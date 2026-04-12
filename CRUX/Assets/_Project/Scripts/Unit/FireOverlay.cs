using UnityEngine;

namespace Crux.Unit
{
    /// <summary>유닛 위 화염 파티클 — 화재 상태일 때만 표시</summary>
    public class FireOverlay : MonoBehaviour
    {
        private GridTankUnit unit;
        private bool wasOnFire;
        private float spawnTimer;
        private const float spawnInterval = 0.15f;

        public void Initialize(GridTankUnit unit)
        {
            this.unit = unit;
        }

        private void Update()
        {
            if (unit == null || unit.IsDestroyed)
            {
                enabled = false;
                return;
            }

            if (!unit.IsOnFire)
            {
                wasOnFire = false;
                return;
            }

            wasOnFire = true;
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0)
            {
                spawnTimer = spawnInterval;
                SpawnFlameParticle();
            }
        }

        private void SpawnFlameParticle()
        {
            Vector3 basePos = unit.transform.position;

            // 화염 입자
            var flame = new GameObject("FlameP");
            flame.transform.position = basePos + (Vector3)(Random.insideUnitCircle * 0.2f);

            var sr = flame.AddComponent<SpriteRenderer>();
            sr.sprite = GetFlameSprite();
            sr.sortingOrder = 15;

            // 노란~빨간 랜덤
            float t = Random.value;
            sr.color = t < 0.3f
                ? new Color(1f, 1f, 0.5f, 0.8f)     // 밝은 노란
                : t < 0.6f
                    ? new Color(1f, 0.6f, 0.1f, 0.7f) // 주황
                    : new Color(0.8f, 0.2f, 0.05f, 0.6f); // 빨간

            float size = Random.Range(0.08f, 0.18f);
            flame.transform.localScale = Vector3.one * size;

            var rb = flame.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.linearDamping = 2f;
            rb.linearVelocity = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.5f, 1.5f));

            flame.AddComponent<Combat.FadeAndShrink>();
            Destroy(flame, Random.Range(0.3f, 0.6f));
        }

        private static Sprite _cachedFlame;
        private Sprite GetFlameSprite()
        {
            if (_cachedFlame != null) return _cachedFlame;
            int s = 6;
            var tex = new Texture2D(s, s);
            tex.filterMode = FilterMode.Point;
            var px = new Color[s * s];
            int half = s / 2;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half));
                    px[y * s + x] = d <= half ? Color.white : Color.clear;
                }
            tex.SetPixels(px); tex.Apply();
            _cachedFlame = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
            return _cachedFlame;
        }
    }
}
