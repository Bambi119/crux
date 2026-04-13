using System.Collections.Generic;
using UnityEngine;
using Crux.Core;

namespace Crux.Grid
{
    /// <summary>육각 그리드 시각화 — 셀 하이라이트, 이동 범위, 사거리, 엄폐 방호면</summary>
    public class GridVisualizer : MonoBehaviour
    {
        private GridManager grid;
        private List<GameObject> highlights = new();
        private List<GameObject> coverArcs = new();

        private static Sprite _cachedHexMask;

        public void Initialize(GridManager grid)
        {
            this.grid = grid;
        }

        /// <summary>이동 가능 범위 표시 (파란색)</summary>
        public void ShowMoveRange(List<Vector2Int> cells)
        {
            ClearHighlights();
            foreach (var pos in cells)
                CreateHighlight(pos, new Color(0.2f, 0.5f, 1f, 0.3f));
        }

        /// <summary>사격 가능 범위 표시 (빨간색) — 적 셀만</summary>
        public void ShowFireRange(Vector2Int center, int range)
        {
            ClearHighlights();
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    var pos = new Vector2Int(x, y);
                    if (grid.GetDistance(center, pos) > range) continue;

                    var cell = grid.GetCell(pos);
                    if (cell != null && cell.Occupant != null)
                    {
                        var side = cell.Occupant.GetComponent<SideIdentifier>();
                        if (side != null && side.Side == PlayerSide.Enemy)
                            CreateHighlight(pos, new Color(1f, 0.2f, 0.2f, 0.4f));
                    }
                }
            }
        }

        public void ShowSelected(Vector2Int pos)
        {
            CreateHighlight(pos, new Color(1f, 1f, 0.3f, 0.5f));
        }

        public void HighlightCell(Vector2Int pos, Color color)
        {
            CreateHighlight(pos, new Color(color.r, color.g, color.b, 0.5f));
        }

        /// <summary>유닛의 엄폐 방호면을 변 위의 두꺼운 선으로 표시</summary>
        public void ShowCoverFacets(Vector2Int unitPos, HexFacet facets, Color color)
        {
            ClearCoverArcs();
            if (facets == HexFacet.None) return;

            foreach (var dir in facets.Enumerate())
            {
                var edgeObj = CreateFacetEdge(unitPos, dir, color);
                if (edgeObj != null) coverArcs.Add(edgeObj);
            }
        }

        /// <summary>특정 변에 두꺼운 선 세그먼트 생성</summary>
        private GameObject CreateFacetEdge(Vector2Int unitPos, HexCoord.HexDir dir, Color color)
        {
            // 변의 양 끝점 계산
            var corners = HexCoord.Corners(unitPos, GameConstants.CellSize);
            // 플랫탑 hex의 변 i는 corners[i]와 corners[(i+1)%6] 사이
            // 단, corners 배열 순서와 HexDir 순서 매핑이 필요
            // Corners는 각도 0°부터 60° 단위 (CCW). flat-top 기준:
            //   corner 0: 0° (오른쪽), corner 1: 60° (우상), corner 2: 120° (좌상),
            //   corner 3: 180° (왼쪽), corner 4: 240° (좌하), corner 5: 300° (우하)
            // 변 0 = c0~c1 = 우상변 ≈ NE facet
            // 변 1 = c1~c2 = 상변     ≈ N facet
            // 변 2 = c2~c3 = 좌상변  ≈ NW facet
            // 변 3 = c3~c4 = 좌하변  ≈ SW facet
            // 변 4 = c4~c5 = 하변    ≈ S facet
            // 변 5 = c5~c0 = 우하변  ≈ SE facet
            int edgeIndex = dir switch
            {
                HexCoord.HexDir.NE => 0,
                HexCoord.HexDir.N => 1,
                HexCoord.HexDir.NW => 2,
                HexCoord.HexDir.SW => 3,
                HexCoord.HexDir.S => 4,
                HexCoord.HexDir.SE => 5,
                _ => 0
            };

            Vector3 a = corners[edgeIndex];
            Vector3 b = corners[(edgeIndex + 1) % 6];
            Vector3 mid = (a + b) * 0.5f;
            Vector3 delta = b - a;

            var edgeObj = new GameObject($"Facet_{dir}");
            edgeObj.transform.position = mid;
            edgeObj.transform.SetParent(transform);

            var mesh = new Mesh();
            // 변 방향 단위 벡터
            Vector2 edgeDir = new Vector2(delta.x, delta.y).normalized;
            // 수직 (셀 중심에서 바깥으로 향함)
            Vector3 centerW = HexCoord.OffsetToWorld(unitPos, GameConstants.CellSize);
            Vector2 outward = ((Vector2)(mid - centerW)).normalized;

            float edgeLen = delta.magnitude;
            float halfW = edgeLen * 0.5f;
            float thick = 0.08f;

            Vector3 e = new Vector3(edgeDir.x, edgeDir.y, 0) * halfW;
            Vector3 o = new Vector3(outward.x, outward.y, 0) * thick;

            var verts = new Vector3[4]
            {
                -e - o,  // 0: 시작, 안쪽
                -e + o,  // 1: 시작, 바깥
                 e + o,  // 2: 끝, 바깥
                 e - o,  // 3: 끝, 안쪽
            };
            var tris = new int[] { 0, 1, 2, 0, 2, 3 };
            var colors = new Color[] { color, color, color, color };

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.colors = colors;
            mesh.RecalculateNormals();

            var mf = edgeObj.AddComponent<MeshFilter>();
            mf.mesh = mesh;

            var mr = edgeObj.AddComponent<MeshRenderer>();
            mr.material = new Material(Shader.Find("Sprites/Default"));
            mr.sortingOrder = 1;

            return edgeObj;
        }

        public void ClearCoverArcs()
        {
            foreach (var obj in coverArcs)
                if (obj != null) Destroy(obj);
            coverArcs.Clear();
        }

        // 연막 오버레이
        private Dictionary<Vector2Int, GameObject> smokeOverlays = new();

        public void ShowSmoke(Vector2Int pos)
        {
            if (smokeOverlays.ContainsKey(pos)) return;

            var obj = new GameObject("Smoke");
            obj.transform.position = grid.GridToWorld(pos);
            obj.transform.SetParent(transform);

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = GetHexMaskSprite();
            sr.color = new Color(0.6f, 0.6f, 0.55f, 0.45f);
            sr.sortingOrder = 5;
            obj.transform.localScale = Vector3.one * GameConstants.CellSize * 1.95f;

            smokeOverlays[pos] = obj;
        }

        public void ClearSmoke(Vector2Int pos)
        {
            if (smokeOverlays.TryGetValue(pos, out var obj))
            {
                if (obj != null) Destroy(obj);
                smokeOverlays.Remove(pos);
            }
        }

        public void ClearHighlights()
        {
            foreach (var obj in highlights)
                if (obj != null) Destroy(obj);
            highlights.Clear();
            ClearCoverArcs();
        }

        private void CreateHighlight(Vector2Int pos, Color color)
        {
            var obj = new GameObject("Highlight");
            obj.transform.position = grid.GridToWorld(pos);
            obj.transform.SetParent(transform);

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = GetHexMaskSprite();
            sr.color = color;
            sr.sortingOrder = -1;
            // Hex 스프라이트(반지름 0.5) → CellSize * 2 로 스케일링, 살짝 margin
            obj.transform.localScale = Vector3.one * GameConstants.CellSize * 1.9f;

            highlights.Add(obj);
        }

        /// <summary>육각 마스크 스프라이트 (채워진 플랫탑 hex)</summary>
        private static Sprite GetHexMaskSprite()
        {
            if (_cachedHexMask != null) return _cachedHexMask;

            int s = 32;
            var tex = new Texture2D(s, s);
            tex.filterMode = FilterMode.Point;
            var px = new Color[s * s];
            for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

            float cx = (s - 1) * 0.5f;
            float cy = (s - 1) * 0.5f;
            float rx = s * 0.5f;
            float ry = s * 0.433f;

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = Mathf.Abs(x - cx) / rx;
                    float dy = Mathf.Abs(y - cy) / ry;
                    if (dx <= 1f && dy <= 1f && (2f * dx + dy) <= 2f)
                        px[y * s + x] = Color.white;
                }
            }

            tex.SetPixels(px); tex.Apply();
            _cachedHexMask = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
            return _cachedHexMask;
        }
    }
}
