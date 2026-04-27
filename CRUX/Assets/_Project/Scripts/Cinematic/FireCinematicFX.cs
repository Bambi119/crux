using System.Collections;
using UnityEngine;

namespace Crux.Cinematic
{
    /// <summary>
    /// 연출 씬 파티클·이펙트·카메라 흔들림·전차 밀림 담당.
    /// MonoBehaviour 아님 — 코루틴은 host MonoBehaviour에 위임.
    /// </summary>
    internal class FireCinematicFX
    {
        private readonly MonoBehaviour host;
        private UnityEngine.Camera cam;

        internal FireCinematicFX(MonoBehaviour host)
        {
            this.host = host;
        }

        internal void SetCamera(UnityEngine.Camera camera)
        {
            cam = camera;
        }

        // ===== 탄약고 유폭 이펙트 =====

        /// <summary>탄약고 유폭 시 추가 대형 폭발 그래픽 (수직 파편 분출)</summary>
        internal void SpawnAmmoCookoffEffect(Vector3 pos)
        {
            // 수직 화염 기둥
            for (int i = 0; i < 20; i++)
            {
                var p = new GameObject("Cookoff");
                p.transform.position = pos + (Vector3)(Random.insideUnitCircle * 0.15f);

                var sr = p.AddComponent<SpriteRenderer>();
                sr.sprite = GetCircleSprite();
                float t = Random.value;
                sr.color = t < 0.4f ? new Color(1f, 0.95f, 0.4f, 1f)
                        : t < 0.75f ? new Color(1f, 0.55f, 0.1f, 0.95f)
                                    : new Color(0.9f, 0.2f, 0.05f, 0.9f);
                sr.sortingOrder = 65;
                p.transform.localScale = Vector3.one * Random.Range(0.15f, 0.3f);

                var rb = p.AddComponent<Rigidbody2D>();
                rb.gravityScale = -0.5f; // 위로 솟구침
                rb.linearDamping = 0.8f;
                rb.linearVelocity = new Vector2(
                    Random.Range(-1.5f, 1.5f),
                    Random.Range(3f, 6f));

                p.AddComponent<Combat.FadeAndShrink>();
                Object.Destroy(p, Random.Range(0.8f, 1.3f));
            }

            // 외곽 쇼크웨이브 링
            var ring = new GameObject("CookoffRing");
            ring.transform.position = pos;
            var rs = ring.AddComponent<SpriteRenderer>();
            rs.sprite = GetCircleSprite();
            rs.color = new Color(1f, 0.9f, 0.5f, 0.7f);
            rs.sortingOrder = 55;
            ring.transform.localScale = Vector3.one * 0.3f;
            host.StartCoroutine(ExpandRing(ring.transform, rs, 2.5f, 0.5f));
        }

        internal IEnumerator ExpandRing(Transform t, SpriteRenderer sr, float maxScale, float duration)
        {
            float el = 0f;
            Color c0 = sr.color;
            while (el < duration)
            {
                el += Time.deltaTime;
                float f = el / duration;
                t.localScale = Vector3.one * Mathf.Lerp(0.3f, maxScale, f);
                sr.color = new Color(c0.r, c0.g, c0.b, c0.a * (1f - f));
                yield return null;
            }
            Object.Destroy(t.gameObject);
        }

        // ===== 화재 지시자 =====

        /// <summary>화재 시작 지시자 — 대상 위 작은 불꽃</summary>
        internal void SpawnFireIndicator(Vector3 pos)
        {
            for (int i = 0; i < 6; i++)
            {
                var p = new GameObject("FireInd");
                p.transform.position = pos + Vector3.up * 0.15f + (Vector3)(Random.insideUnitCircle * 0.2f);
                var sr = p.AddComponent<SpriteRenderer>();
                sr.sprite = GetCircleSprite();
                sr.color = new Color(1f, 0.6f, 0.15f, 0.85f);
                sr.sortingOrder = 50;
                p.transform.localScale = Vector3.one * Random.Range(0.08f, 0.15f);

                var rb = p.AddComponent<Rigidbody2D>();
                rb.gravityScale = -0.3f;
                rb.linearDamping = 1.5f;
                rb.linearVelocity = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.8f, 1.5f));

                p.AddComponent<Combat.FadeAndShrink>();
                Object.Destroy(p, 0.8f);
            }
        }

        // ===== 보조 이펙트 =====

        /// <summary>지면 충돌 — 먼지+파편 (도탄 포탄 착지)</summary>
        internal void SpawnGroundImpact(Vector3 pos)
        {
            for (int i = 0; i < 4; i++)
            {
                var obj = new GameObject("Dust");
                obj.transform.position = pos;
                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = GetCircleSprite();
                sr.color = new Color(0.5f, 0.45f, 0.35f, 0.5f);
                sr.sortingOrder = 40;
                obj.transform.localScale = Vector3.one * Random.Range(0.08f, 0.15f);

                var rb = obj.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 3f;
                rb.linearVelocity = Random.insideUnitCircle * Random.Range(1f, 3f);
                obj.AddComponent<Combat.FadeAndShrink>();
                Object.Destroy(obj, 0.6f);
            }
        }

        /// <summary>관통 출구 화염</summary>
        internal void SpawnExitFlame(Vector3 pos, Vector2 dir)
        {
            for (int i = 0; i < 3; i++)
            {
                var obj = new GameObject("ExitFlame");
                obj.transform.position = pos;
                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = GetCircleSprite();
                sr.color = new Color(1f, 0.5f, 0.1f, 0.8f);
                sr.sortingOrder = 45;
                obj.transform.localScale = Vector3.one * Random.Range(0.1f, 0.2f);

                var rb = obj.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 3f;
                rb.linearVelocity = dir * Random.Range(2f, 5f) + Random.insideUnitCircle * 1f;
                obj.AddComponent<Combat.FadeAndShrink>();
                Object.Destroy(obj, 0.4f);
            }
        }

        /// <summary>지속 연기 (피격/관통 후)</summary>
        internal void SpawnLingeringSmoke(Vector3 pos)
        {
            for (int i = 0; i < 2; i++)
            {
                var obj = new GameObject("Smoke");
                obj.transform.position = pos + (Vector3)(Random.insideUnitCircle * 0.1f);
                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = GetCircleSprite();
                sr.color = new Color(0.1f, 0.1f, 0.08f, 0.4f);
                sr.sortingOrder = 35;
                obj.transform.localScale = Vector3.one * Random.Range(0.12f, 0.25f);

                var rb = obj.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 1f;
                rb.linearVelocity = Vector2.up * Random.Range(0.3f, 0.8f);
                obj.AddComponent<Combat.FadeAndShrink>();
                Object.Destroy(obj, Random.Range(1f, 2f));
            }
        }

        // ===== 기관총 보조 이펙트 (작고 가벼움) =====

        internal void SpawnSmallFlash(Vector3 pos)
        {
            var obj = new GameObject("MGFlash");
            obj.transform.position = pos;
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = GetCircleSprite();
            sr.color = new Color(1f, 0.9f, 0.4f, 0.8f);
            sr.sortingOrder = 60;
            obj.transform.localScale = Vector3.one * 0.08f;
            Object.Destroy(obj, 0.05f);
        }

        internal void SpawnSmallSpark(Vector3 pos)
        {
            for (int i = 0; i < 2; i++)
            {
                var obj = new GameObject("MGSpark");
                obj.transform.position = pos;
                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = GetCircleSprite();
                sr.color = new Color(1f, 0.9f, 0.4f);
                sr.sortingOrder = 55;
                obj.transform.localScale = Vector3.one * 0.02f;
                var rb = obj.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0; rb.linearDamping = 5f;
                rb.linearVelocity = Random.insideUnitCircle * 3f;
                Object.Destroy(obj, 0.15f);
            }
        }

        internal void SpawnSmallHit(Vector3 pos)
        {
            var obj = new GameObject("MGHit");
            obj.transform.position = pos;
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = GetCircleSprite();
            sr.color = new Color(1f, 0.5f, 0.2f, 0.7f);
            sr.sortingOrder = 55;
            obj.transform.localScale = Vector3.one * 0.06f;
            obj.AddComponent<Combat.FadeAndShrink>();
            Object.Destroy(obj, 0.15f);
        }

        internal void SpawnSmallDust(Vector3 pos)
        {
            var obj = new GameObject("MGDust");
            obj.transform.position = pos;
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = GetCircleSprite();
            sr.color = new Color(0.5f, 0.45f, 0.35f, 0.3f);
            sr.sortingOrder = 40;
            obj.transform.localScale = Vector3.one * 0.04f;
            obj.AddComponent<Combat.FadeAndShrink>();
            Object.Destroy(obj, 0.2f);
        }

        // ===== 카메라 / 전차 이펙트 =====

        internal IEnumerator CameraShake(float duration, float magnitude)
        {
            if (cam == null) yield break;
            Vector3 originalPos = cam.transform.position;
            float elapsed = 0;
            while (elapsed < duration)
            {
                float x = Random.Range(-1f, 1f) * magnitude;
                float y = Random.Range(-1f, 1f) * magnitude;
                cam.transform.position = originalPos + new Vector3(x, y, 0);
                elapsed += Time.deltaTime;
                yield return null;
            }
            cam.transform.position = originalPos;
        }

        internal IEnumerator ShakeObject(GameObject obj, float duration, float magnitude)
        {
            if (obj == null) yield break;
            Vector3 originalPos = obj.transform.position;
            float elapsed = 0;
            while (elapsed < duration && obj != null)
            {
                float x = Random.Range(-1f, 1f) * magnitude;
                float y = Random.Range(-1f, 1f) * magnitude;
                obj.transform.position = originalPos + new Vector3(x, y, 0);
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (obj != null)
                obj.transform.position = originalPos;
        }

        internal IEnumerator TankRecoil(GameObject tank, Vector2 dir, float intensity)
        {
            if (tank == null) yield break;
            Vector3 original = tank.transform.position;
            Vector3 pushed = original + (Vector3)(dir * intensity);

            float t = 0;
            while (t < 0.1f)
            {
                t += Time.deltaTime;
                tank.transform.position = Vector3.Lerp(original, pushed, t / 0.1f);
                yield return null;
            }
            t = 0;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                tank.transform.position = Vector3.Lerp(pushed, original, t / 0.15f);
                yield return null;
            }
            tank.transform.position = original;
        }

        // ===== 스프라이트 헬퍼 =====

        private static Sprite _cachedCircle;

        internal Sprite GetCircleSprite()
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
            _cachedCircle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _cachedCircle;
        }
    }
}
