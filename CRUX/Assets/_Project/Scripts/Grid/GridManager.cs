using System.Collections.Generic;
using UnityEngine;
using Crux.Core;

namespace Crux.Grid
{
    /// <summary>8x10 턴제 그리드 — 셀 관리, 좌표 변환, A* 경로 탐색</summary>
    public class GridManager : MonoBehaviour
    {
        [Header("그리드 설정")]
        [SerializeField] private int width = GameConstants.GridWidth;
        [SerializeField] private int height = GameConstants.GridHeight;

        private Dictionary<Vector2Int, GridCell> cells = new();

        public int Width => width;
        public int Height => height;

        private void Awake()
        {
            InitializeGrid();
        }

        private void InitializeGrid()
        {
            cells.Clear();
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    cells[new Vector2Int(x, y)] = new GridCell(new Vector2Int(x, y));
        }

        // ===== 좌표 변환 =====

        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            float worldX = gridPos.x * GameConstants.CellSize;
            float worldY = gridPos.y * GameConstants.CellSize;
            return new Vector3(worldX, worldY, 0f);
        }

        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            int x = Mathf.RoundToInt(worldPos.x / GameConstants.CellSize);
            int y = Mathf.RoundToInt(worldPos.y / GameConstants.CellSize);
            return new Vector2Int(x, y);
        }

        // ===== 셀 접근 =====

        public GridCell GetCell(Vector2Int pos)
        {
            cells.TryGetValue(pos, out var cell);
            return cell;
        }

        public bool IsInBounds(Vector2Int pos)
        {
            return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
        }

        public List<GridCell> GetNeighbors(Vector2Int pos)
        {
            var neighbors = new List<GridCell>();
            Vector2Int[] dirs = {
                Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
                new(1,1), new(1,-1), new(-1,1), new(-1,-1) // 대각선
            };
            foreach (var dir in dirs)
            {
                var cell = GetCell(pos + dir);
                if (cell != null) neighbors.Add(cell);
            }
            return neighbors;
        }

        // ===== A* 경로 탐색 =====

        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int end)
        {
            if (!IsInBounds(start) || !IsInBounds(end)) return null;
            var endCell = GetCell(end);
            if (endCell == null || !endCell.IsWalkable) return null;

            var openList = new List<PathNode>();
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var gCost = new Dictionary<Vector2Int, float>();
            var visited = new HashSet<Vector2Int>();

            gCost[start] = 0;
            openList.Add(new PathNode(start, Heuristic(start, end)));

            while (openList.Count > 0)
            {
                openList.Sort((a, b) => a.fCost.CompareTo(b.fCost));
                var current = openList[0];
                openList.RemoveAt(0);

                if (current.pos == end)
                    return ReconstructPath(cameFrom, end);

                if (visited.Contains(current.pos)) continue;
                visited.Add(current.pos);

                foreach (var neighbor in GetNeighbors(current.pos))
                {
                    if (!neighbor.IsWalkable || visited.Contains(neighbor.Position))
                        continue;

                    float tentativeG = gCost[current.pos] + 1f;
                    if (!gCost.ContainsKey(neighbor.Position) || tentativeG < gCost[neighbor.Position])
                    {
                        cameFrom[neighbor.Position] = current.pos;
                        gCost[neighbor.Position] = tentativeG;
                        openList.Add(new PathNode(neighbor.Position, tentativeG + Heuristic(neighbor.Position, end)));
                    }
                }
            }
            return null;
        }

        /// <summary>AP 기반 이동 가능 범위</summary>
        public List<Vector2Int> GetReachableCells(Vector2Int start, int maxCost)
        {
            var reachable = new List<Vector2Int>();
            var visited = new Dictionary<Vector2Int, float>();
            var queue = new Queue<(Vector2Int pos, float cost)>();

            queue.Enqueue((start, 0));
            visited[start] = 0;

            while (queue.Count > 0)
            {
                var (pos, cost) = queue.Dequeue();
                foreach (var neighbor in GetNeighbors(pos))
                {
                    float newCost = cost + 1f;
                    if (newCost > maxCost || !neighbor.IsWalkable) continue;
                    if (visited.ContainsKey(neighbor.Position) && newCost >= visited[neighbor.Position]) continue;

                    visited[neighbor.Position] = newCost;
                    reachable.Add(neighbor.Position);
                    queue.Enqueue((neighbor.Position, newCost));
                }
            }
            return reachable;
        }

        /// <summary>두 셀 간 거리 (셀 단위)</summary>
        public int GetDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }

        // ===== 유틸 =====

        private float Heuristic(Vector2Int a, Vector2Int b)
        {
            return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }

        private List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
        {
            var path = new List<Vector2Int> { current };
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Add(current);
            }
            path.Reverse();
            return path;
        }

        private struct PathNode
        {
            public Vector2Int pos;
            public float fCost;
            public PathNode(Vector2Int p, float f) { pos = p; fCost = f; }
        }

        // ===== 디버그 =====

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.2f);
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    var worldPos = GridToWorld(new Vector2Int(x, y));
                    Gizmos.DrawWireCube(worldPos, Vector3.one * GameConstants.CellSize * 0.95f);
                }
        }
    }
}
