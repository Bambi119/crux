using UnityEngine;

namespace Crux.Grid
{
    /// <summary>연막 셀에 부착되어 연기 파티클 지속 생성</summary>
    public class SmokeCell : MonoBehaviour
    {
        private float spawnTimer;
        private const float spawnInterval = 0.25f;

        private void OnEnable()
        {
            Debug.Log("[CRUX] SmokeCell 활성화 at " + gameObject.name);
        }

        private void Update()
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0)
            {
                spawnTimer = spawnInterval;
                SpawnSmokeParticle();
            }
        }

        private void SpawnSmokeParticle()
        {
            Vector3 basePos = transform.position;

            // 연기 입자
            var smoke = new GameObject("SmokeP");
            smoke.transform.position = basePos + (Vector3)(Random.insideUnitCircle * 0.3f);

            var sr = smoke.AddComponent<SpriteRenderer>();
            sr.sprite = GetSmokeSprite();
            sr.sortingOrder = 6;

            // 회색 랜덤 (약간 투명)
            float r = Random.Range(0.7f, 0.85f);
            float g = Random.Range(0.7f, 0.85f);
            float b = Random.Range(0.7f, 0.8f);
            float a = Random.Range(0.3f, 0.5f);
            sr.color = new Color(r, g, b, a);

            float size = Random.Range(0.15f, 0.35f);
            smoke.transform.localScale = Vector3.one * size;

            var rb = smoke.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.linearDamping = 1.5f;
            rb.linearVelocity = new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.15f, 0.4f));

            smoke.AddComponent<Combat.FadeAndShrink>();
            Object.Destroy(smoke, Random.Range(0.6f, 1.2f));
        }

        private static Sprite _cachedSmoke;
        private Sprite GetSmokeSprite()
        {
            if (_cachedSmoke != null) return _cachedSmoke;
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
            _cachedSmoke = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
            return _cachedSmoke;
        }
    }
}
