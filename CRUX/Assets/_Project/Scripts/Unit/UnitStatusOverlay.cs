using System.Collections.Generic;
using UnityEngine;
using Crux.Core;

namespace Crux.Unit
{
    /// <summary>유닛 머리 위 모듈 손상 아이콘 오버레이 — 손상 이상만 표시</summary>
    public class UnitStatusOverlay : MonoBehaviour
    {
        private GridTankUnit unit;
        private List<GameObject> icons = new();
        private ModuleState[] lastStates;

        // 아이콘 설정
        private const float iconSize = 0.18f;
        private const float iconSpacing = 0.22f;
        private const float yOffset = 0.65f;
        private const int sortOrder = 20;

        private static readonly ModuleType[] displayOrder =
        {
            ModuleType.Engine, ModuleType.Barrel, ModuleType.MachineGun,
            ModuleType.AmmoRack, ModuleType.Loader, ModuleType.TurretRing,
            ModuleType.CaterpillarLeft, ModuleType.CaterpillarRight
        };

        public void Initialize(GridTankUnit unit)
        {
            this.unit = unit;
            lastStates = new ModuleState[displayOrder.Length];
            for (int i = 0; i < lastStates.Length; i++)
                lastStates[i] = ModuleState.Normal;
        }

        private void LateUpdate()
        {
            if (unit == null || unit.IsDestroyed)
            {
                ClearIcons();
                return;
            }

            // 상태 변경 감지
            bool changed = false;
            for (int i = 0; i < displayOrder.Length; i++)
            {
                var m = unit.Modules.Get(displayOrder[i]);
                if (m == null) continue;
                if (m.state != lastStates[i])
                {
                    changed = true;
                    lastStates[i] = m.state;
                }
            }

            if (changed)
                RebuildIcons();
        }

        private void RebuildIcons()
        {
            ClearIcons();

            // 손상 이상인 모듈만 수집
            var damaged = new List<(ModuleType type, ModuleState state)>();
            for (int i = 0; i < displayOrder.Length; i++)
            {
                var m = unit.Modules.Get(displayOrder[i]);
                if (m != null && m.state > ModuleState.Normal)
                    damaged.Add((displayOrder[i], m.state));
            }

            if (damaged.Count == 0) return;

            // 중앙 정렬: 총 폭 계산
            float totalWidth = (damaged.Count - 1) * iconSpacing;
            float startX = -totalWidth * 0.5f;

            for (int i = 0; i < damaged.Count; i++)
            {
                var (type, state) = damaged[i];

                var iconObj = new GameObject($"StatusIcon_{type}");
                iconObj.transform.SetParent(unit.transform);
                iconObj.transform.localPosition = new Vector3(startX + i * iconSpacing, yOffset, 0);
                iconObj.transform.localScale = Vector3.one * iconSize;

                var sr = iconObj.AddComponent<SpriteRenderer>();
                sr.sprite = GetModuleSprite(type);
                sr.sortingOrder = sortOrder;
                sr.color = GetStateColor(state);

                // 완파 표시: X 마크 오버레이
                if (state == ModuleState.Destroyed)
                {
                    var xObj = new GameObject("XMark");
                    xObj.transform.SetParent(iconObj.transform);
                    xObj.transform.localPosition = Vector3.zero;
                    xObj.transform.localScale = Vector3.one * 1.2f;

                    var xSr = xObj.AddComponent<SpriteRenderer>();
                    xSr.sprite = GetXSprite();
                    xSr.sortingOrder = sortOrder + 1;
                    xSr.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                }

                icons.Add(iconObj);
            }
        }

        private void ClearIcons()
        {
            foreach (var obj in icons)
                if (obj != null) Destroy(obj);
            icons.Clear();
        }

        private Color GetStateColor(ModuleState state) => state switch
        {
            ModuleState.Damaged   => new Color(1f, 0.85f, 0.15f),      // 노란색
            ModuleState.Broken    => new Color(1f, 0.25f, 0.15f),      // 빨간색
            ModuleState.Destroyed => new Color(0.45f, 0.4f, 0.35f),    // 회색
            _ => Color.white
        };

        // ===== 프로시저럴 스프라이트 =====

        private static Dictionary<ModuleType, Sprite> _spriteCache = new();
        private static Sprite _xSprite;

        private Sprite GetModuleSprite(ModuleType type)
        {
            if (_spriteCache.TryGetValue(type, out var cached)) return cached;

            int s = 8;
            var tex = new Texture2D(s, s);
            tex.filterMode = FilterMode.Point;
            var px = new Color[s * s];

            // 배경 클리어
            for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

            switch (type)
            {
                case ModuleType.Engine: // ⚙ 톱니바퀴 — 십자 + 모서리
                    DrawCross(px, s); DrawCorners(px, s);
                    break;
                case ModuleType.Barrel: // ▮ 세로 막대
                    DrawVerticalBar(px, s);
                    break;
                case ModuleType.MachineGun: // ∴ 점 3개
                    DrawDots3(px, s);
                    break;
                case ModuleType.AmmoRack: // ◆ 마름모
                    DrawDiamond(px, s);
                    break;
                case ModuleType.Loader: // ↻ 순환 — 원
                    DrawCircle(px, s);
                    break;
                case ModuleType.CaterpillarLeft: // ◁ 좌 삼각
                    DrawTriangleLeft(px, s);
                    break;
                case ModuleType.CaterpillarRight: // ▷ 우 삼각
                    DrawTriangleRight(px, s);
                    break;
                case ModuleType.TurretRing: // ○ 링
                    DrawRing(px, s);
                    break;
            }

            tex.SetPixels(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
            _spriteCache[type] = sprite;
            return sprite;
        }

        private Sprite GetXSprite()
        {
            if (_xSprite != null) return _xSprite;
            int s = 8;
            var tex = new Texture2D(s, s);
            tex.filterMode = FilterMode.Point;
            var px = new Color[s * s];
            for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

            // X 대각선
            for (int i = 0; i < s; i++)
            {
                px[i * s + i] = Color.white;
                px[i * s + (s - 1 - i)] = Color.white;
            }
            tex.SetPixels(px); tex.Apply();
            _xSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
            return _xSprite;
        }

        // 도형 그리기 헬퍼
        private void Set(Color[] px, int s, int x, int y)
        {
            if (x >= 0 && x < s && y >= 0 && y < s)
                px[y * s + x] = Color.white;
        }

        private void DrawCross(Color[] px, int s)
        {
            int m = s / 2;
            for (int i = 1; i < s - 1; i++) { Set(px, s, m, i); Set(px, s, i, m); }
        }

        private void DrawCorners(Color[] px, int s)
        {
            Set(px, s, 1, 1); Set(px, s, s-2, 1);
            Set(px, s, 1, s-2); Set(px, s, s-2, s-2);
        }

        private void DrawVerticalBar(Color[] px, int s)
        {
            int m = s / 2;
            for (int y = 1; y < s - 1; y++) { Set(px, s, m, y); Set(px, s, m - 1, y); }
        }

        private void DrawDots3(Color[] px, int s)
        {
            Set(px, s, s/2, s-2);
            Set(px, s, s/2-1, 2); Set(px, s, s/2+1, 2);
        }

        private void DrawDiamond(Color[] px, int s)
        {
            int m = s / 2;
            for (int i = 0; i <= m; i++)
            {
                Set(px, s, m + i, m); Set(px, s, m - i, m);
                Set(px, s, m, m + i); Set(px, s, m, m - i);
                if (i > 0 && i < m) {
                    Set(px, s, m+i, m+1); Set(px, s, m-i, m+1);
                    Set(px, s, m+i, m-1); Set(px, s, m-i, m-1);
                }
            }
        }

        private void DrawCircle(Color[] px, int s)
        {
            int m = s / 2;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(m, m));
                    if (d >= m - 1.5f && d <= m - 0.3f)
                        Set(px, s, x, y);
                }
        }

        private void DrawTriangleLeft(Color[] px, int s)
        {
            for (int y = 1; y < s - 1; y++)
            {
                int w = (int)(((float)(y - 1) / (s - 2)) * (s / 2));
                for (int x = s - 2; x >= s - 2 - w; x--)
                    Set(px, s, x, y);
            }
        }

        private void DrawTriangleRight(Color[] px, int s)
        {
            for (int y = 1; y < s - 1; y++)
            {
                int w = (int)(((float)(y - 1) / (s - 2)) * (s / 2));
                for (int x = 1; x <= 1 + w; x++)
                    Set(px, s, x, y);
            }
        }

        private void DrawRing(Color[] px, int s)
        {
            int m = s / 2;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(m, m));
                    if (d >= m - 1.8f && d <= m - 0.5f)
                        Set(px, s, x, y);
                }
            // 중앙 점
            Set(px, s, m, m);
        }
    }
}
