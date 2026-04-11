using System.Collections.Generic;
using UnityEngine;
using Crux.Core;

namespace Crux.Grid
{
    /// <summary>그리드 시각화 — 셀 하이라이트, 이동 범위, 사거리, 엄폐 범위</summary>
    public class GridVisualizer : MonoBehaviour
    {
        private GridManager grid;
        private List<GameObject> highlights = new();
        private List<GameObject> coverArcs = new();

        private static Sprite _cachedSquare;

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

        /// <summary>사격 가능 범위 표시 (빨간색)</summary>
        public void ShowFireRange(Vector2Int center, int range)
        {
            ClearHighlights();
            for (int x = -range; x <= range; x++)
            {
                for (int y = -range; y <= range; y++)
                {
                    var pos = center + new Vector2Int(x, y);
                    if (!grid.IsInBounds(pos)) continue;
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

        /// <summary>선택된 셀 강조 (노란색)</summary>
        public void ShowSelected(Vector2Int pos)
        {
            CreateHighlight(pos, new Color(1f, 1f, 0.3f, 0.5f));
        }

        public void HighlightCell(Vector2Int pos, Color color)
        {
            CreateHighlight(pos, new Color(color.r, color.g, color.b, 0.5f));
        }

        /// <summary>유닛의 엄폐 커버 범위를 부채꼴로 표시</summary>
        public void ShowCoverArc(Vector2Int unitPos, float coverDirection, float coverArc, Color color)
        {
            ClearCoverArcs();

            var worldPos = grid.GridToWorld(unitPos);
            int segments = 12;
            float halfArc = coverArc * 0.5f;
            float radius = 0.9f;

            // 부채꼴을 삼각형 메시로 생성
            var arcObj = new GameObject("CoverArc");
            arcObj.transform.position = worldPos;
            arcObj.transform.SetParent(transform);

            var mesh = new Mesh();
            var vertices = new Vector3[segments + 2];
            var triangles = new int[segments * 3];
            var colors = new Color[segments + 2];

            vertices[0] = Vector3.zero; // 중심
            colors[0] = new Color(color.r, color.g, color.b, color.a * 0.6f);

            for (int i = 0; i <= segments; i++)
            {
                float angle = coverDirection - halfArc + (coverArc * i / segments);
                float rad = angle * Mathf.Deg2Rad;
                // 나침반 → x,y
                vertices[i + 1] = new Vector3(Mathf.Sin(rad), Mathf.Cos(rad), 0) * radius;
                colors[i + 1] = new Color(color.r, color.g, color.b, color.a * 0.15f);
            }

            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.colors = colors;
            mesh.RecalculateNormals();

            var mf = arcObj.AddComponent<MeshFilter>();
            mf.mesh = mesh;

            var mr = arcObj.AddComponent<MeshRenderer>();
            mr.material = new Material(Shader.Find("Sprites/Default"));
            mr.sortingOrder = 1;

            coverArcs.Add(arcObj);
        }

        public void ClearCoverArcs()
        {
            foreach (var obj in coverArcs)
                if (obj != null) Destroy(obj);
            coverArcs.Clear();
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
            sr.sprite = GetSquareSprite();
            sr.color = color;
            sr.sortingOrder = -1;
            obj.transform.localScale = Vector3.one * GameConstants.CellSize * 0.9f;

            highlights.Add(obj);
        }

        private static Sprite GetSquareSprite()
        {
            if (_cachedSquare != null) return _cachedSquare;

            var tex = new Texture2D(4, 4);
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            _cachedSquare = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            return _cachedSquare;
        }
    }
}
