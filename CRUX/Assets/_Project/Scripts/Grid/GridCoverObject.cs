using UnityEngine;
using Crux.Core;
using Crux.Combat;

namespace Crux.Grid
{
    /// <summary>엄폐물 — 6면 방호 슬롯, 내구력, 파괴 시 파편화</summary>
    public class GridCoverObject : MonoBehaviour
    {
        [Header("설정")]
        public string coverName = "콘크리트 벽";
        public CoverSize size = CoverSize.Medium;
        public float maxHP = 80f;
        public float maxCoverRate = 0.65f;

        /// <summary>완전 상태(HP 100%)에서 방호하는 방향 슬롯 (비트 플래그)</summary>
        public HexFacet protectedFacets = HexFacet.None;

        private float currentHP;
        private SpriteRenderer sr;
        private Sprite intactSprite;
        private bool isDestroyed;

        // ===== 프로퍼티 =====
        public float CurrentHP => currentHP;
        public bool IsDestroyed => isDestroyed;
        public float HPRatio => isDestroyed ? 0f : currentHP / maxHP;

        /// <summary>현재 엄폐율 — HP에 비례하여 감소</summary>
        public float CoverRate
        {
            get
            {
                if (isDestroyed) return 0f;
                return maxCoverRate * HPRatio;
            }
        }

        /// <summary>현재 유효 방호면 — HP 비율에 따라 슬롯이 순차 소실</summary>
        /// <remarks>
        /// HP 100~66%: 원본 전체 슬롯
        /// HP 66~33%: 슬롯 1개 제거 (가장 마지막 비트부터)
        /// HP 33~0%:  슬롯 2개 제거
        /// </remarks>
        public HexFacet CurrentFacets
        {
            get
            {
                if (isDestroyed) return HexFacet.None;
                float ratio = HPRatio;
                int drop = ratio > 2f / 3f ? 0 : ratio > 1f / 3f ? 1 : 2;
                if (drop == 0) return protectedFacets;

                // 비트 제거 — 가장 낮은 비트부터 제거 (N, NE, SE, S, SW, NW 순)
                int bits = (int)protectedFacets;
                for (int i = 0; i < drop && bits != 0; i++)
                {
                    // 가장 낮은 set bit 하나 제거
                    bits &= bits - 1;
                }
                return (HexFacet)bits;
            }
        }

        public System.Action<GridCoverObject> OnDestroyed;

        public void Initialize(string name, CoverSize coverSize, float hp, float coverRate,
                                HexFacet facets, Sprite sprite)
        {
            coverName = name;
            size = coverSize;
            maxHP = hp;
            currentHP = hp;
            maxCoverRate = coverRate;
            protectedFacets = facets;
            intactSprite = sprite;

            sr = GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.sprite = sprite;
        }

        /// <summary>공격 방향이 현재 방호면 슬롯에 포함되는지 판정</summary>
        /// <param name="attackDir">공격자→대상 방향을 6방향으로 스냅한 HexDir</param>
        public bool IsCovered(HexCoord.HexDir attackDir)
        {
            if (isDestroyed) return false;
            // 공격이 "오는" 방향 = attackDir의 반대
            var incoming = OppositeDir(attackDir);
            return CurrentFacets.Contains(incoming);
        }

        public static HexCoord.HexDir OppositeDir(HexCoord.HexDir dir) =>
            (HexCoord.HexDir)(((int)dir + 3) % HexCoord.DirCount);

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
                    sr.color = Color.Lerp(new Color(0.7f, 0.65f, 0.55f), Color.white, (ratio - 0.5f) * 2f);
                }
                else if (ratio > 0.25f)
                {
                    sr.color = Color.Lerp(new Color(0.5f, 0.4f, 0.3f), new Color(0.7f, 0.65f, 0.55f), (ratio - 0.25f) * 4f);
                }
                else
                {
                    sr.color = Color.Lerp(new Color(0.3f, 0.25f, 0.2f), new Color(0.5f, 0.4f, 0.3f), ratio * 4f);
                }

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

        private void Destroy()
        {
            isDestroyed = true;
            SpawnDebris();
            if (sr != null)
            {
                sr.color = new Color(0.3f, 0.25f, 0.2f, 0.4f);
                transform.localScale = Vector3.one * 0.3f;
            }
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
