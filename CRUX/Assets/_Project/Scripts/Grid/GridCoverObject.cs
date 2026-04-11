using UnityEngine;
using Crux.Core;
using Crux.Combat;

namespace Crux.Grid
{
    /// <summary>엄폐물 — 엄폐율/내구력/커버 범위, 파괴 시 파편화</summary>
    public class GridCoverObject : MonoBehaviour
    {
        [Header("설정")]
        public string coverName = "콘크리트 벽";
        public CoverSize size = CoverSize.Medium;
        public float maxHP = 80f;
        public float maxCoverRate = 0.65f;
        public float maxCoverArc = 135f; // 커버 범위 (도)

        private float currentHP;
        private SpriteRenderer sr;
        private Sprite intactSprite;
        private bool isDestroyed;

        // ===== 프로퍼티 =====
        public float CurrentHP => currentHP;
        public bool IsDestroyed => isDestroyed;

        /// <summary>현재 엄폐율 — HP에 비례하여 감소</summary>
        public float CoverRate
        {
            get
            {
                if (isDestroyed) return 0f;
                float hpRatio = currentHP / maxHP;
                return maxCoverRate * hpRatio;
            }
        }

        /// <summary>현재 커버 범위 (도) — HP 50% 이하에서 감소 시작, 최소 45°</summary>
        public float CoverArc
        {
            get
            {
                if (isDestroyed) return 0f;
                float hpRatio = currentHP / maxHP;
                if (hpRatio >= 0.5f) return maxCoverArc;
                // 50%→0%: maxCoverArc → 45°
                float t = hpRatio / 0.5f; // 0~1
                return Mathf.Lerp(45f, maxCoverArc, t);
            }
        }

        /// <summary>HP 비율</summary>
        public float HPRatio => isDestroyed ? 0f : currentHP / maxHP;

        public System.Action<GridCoverObject> OnDestroyed;

        public void Initialize(string name, CoverSize coverSize, float hp, float coverRate, float arc, Sprite sprite)
        {
            coverName = name;
            size = coverSize;
            maxHP = hp;
            currentHP = hp;
            maxCoverRate = coverRate;
            maxCoverArc = arc;
            intactSprite = sprite;

            sr = GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.sprite = sprite;
        }

        /// <summary>사격 방향이 커버 범위 안에 있는지 판정</summary>
        /// <param name="coverDirection">유닛→엄폐물 방향 (나침반 각도)</param>
        /// <param name="attackAngle">공격자→대상 방향 (나침반 각도)</param>
        public bool IsCovered(float coverDirection, float attackAngle)
        {
            if (isDestroyed) return false;

            // attackAngle = 공격자→대상 방향
            // 공격이 "오는" 방향 = attackAngle + 180° (반대)
            // 엄폐물이 이 방향에 있으면 막아줌
            float incomingAngle = attackAngle + 180f;
            float diff = Mathf.DeltaAngle(incomingAngle, coverDirection);
            return Mathf.Abs(diff) <= CoverArc * 0.5f;
        }

        /// <summary>엄폐물이 피격당함 — 데미지 적용</summary>
        public void TakeDamage(float damage)
        {
            if (isDestroyed) return;

            currentHP -= damage;

            // 시각 피드백: HP에 따라 단계별 변화
            if (sr != null)
            {
                float ratio = HPRatio;

                if (ratio > 0.5f)
                {
                    // 정상 — 약간 어두워짐
                    sr.color = Color.Lerp(new Color(0.7f, 0.65f, 0.55f), Color.white, (ratio - 0.5f) * 2f);
                }
                else if (ratio > 0.25f)
                {
                    // 균열 — 갈색 톤
                    sr.color = Color.Lerp(new Color(0.5f, 0.4f, 0.3f), new Color(0.7f, 0.65f, 0.55f), (ratio - 0.25f) * 4f);
                }
                else
                {
                    // 심한 파손 — 어두운 갈색
                    sr.color = Color.Lerp(new Color(0.3f, 0.25f, 0.2f), new Color(0.5f, 0.4f, 0.3f), ratio * 4f);
                }

                // 크기 감소 (기본 스케일 기준)
                float baseScale = size switch
                {
                    CoverSize.Small => 0.5f,
                    CoverSize.Medium => 0.8f,
                    CoverSize.Large => 1.1f,
                    _ => 0.8f
                };
                float scaleRatio = 0.6f + 0.4f * ratio;
                transform.localScale = Vector3.one * baseScale * scaleRatio;
            }

            if (currentHP <= 0)
            {
                currentHP = 0;
                Destroy();
            }
        }

        /// <summary>엄폐물 파괴</summary>
        private void Destroy()
        {
            isDestroyed = true;

            // 파편 이펙트
            SpawnDebris();

            // 스프라이트를 파편 이미지로 변경
            if (sr != null)
            {
                sr.color = new Color(0.3f, 0.25f, 0.2f, 0.4f);
                transform.localScale = Vector3.one * 0.3f;
            }

            // 셀 타입 변경 (통과 가능)
            OnDestroyed?.Invoke(this);

            Debug.Log($"[CRUX] {coverName} ({size}) 파괴!");
        }

        private void SpawnDebris()
        {
            int debrisCount = size switch
            {
                CoverSize.Small => 3,
                CoverSize.Medium => 5,
                CoverSize.Large => 8,
                _ => 4
            };

            for (int i = 0; i < debrisCount; i++)
            {
                var debris = new GameObject("Debris");
                debris.transform.position = transform.position;

                var debrisSr = debris.AddComponent<SpriteRenderer>();
                debrisSr.sprite = GetDebrisSprite();
                debrisSr.color = new Color(0.45f, 0.35f, 0.25f);
                debrisSr.sortingOrder = 3;
                debris.transform.localScale = Vector3.one * Random.Range(0.05f, 0.12f);

                var rb = debris.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 3f;
                rb.linearVelocity = Random.insideUnitCircle * Random.Range(1.5f, 3f);

                Object.Destroy(debris, 1.5f);
            }
        }

        private static Sprite _cachedDebris;
        private Sprite GetDebrisSprite()
        {
            if (_cachedDebris != null) return _cachedDebris;
            var tex = new Texture2D(4, 4);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px); tex.Apply();
            tex.filterMode = FilterMode.Point;
            _cachedDebris = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            return _cachedDebris;
        }
    }
}
