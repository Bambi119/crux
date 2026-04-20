using System.Collections.Generic;
using UnityEngine;
using Crux.Core;
using TerrainData = Crux.Core.TerrainData;

namespace Crux.Grid
{
    /// <summary>육각 턴제 그리드 — 셀 관리, 좌표 변환, A* 경로 탐색</summary>
    /// <remarks>Flat-top 육각, odd-q offset 저장 (HexCoord 참고)</remarks>
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

        /// <summary>그리드 차원 재설정 + 즉시 재초기화 — Awake 이후에만 의미 있음</summary>
        /// <remarks>BattleController가 테스트 맵 모드에서 12×12 등으로 확장할 때 사용.</remarks>
        public void SetDimensions(int newWidth, int newHeight)
        {
            width = newWidth;
            height = newHeight;
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

        public Vector3 GridToWorld(Vector2Int gridPos) =>
            HexCoord.OffsetToWorld(gridPos, GameConstants.CellSize);

        public Vector2Int WorldToGrid(Vector3 worldPos) =>
            HexCoord.WorldToOffset(worldPos, GameConstants.CellSize);

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
            foreach (var np in HexCoord.Neighbors(pos))
            {
                var cell = GetCell(np);
                if (cell != null) neighbors.Add(cell);
            }
            return neighbors;
        }

        // ===== 통과 판정 (지형 연동) =====

        /// <summary>지상 유닛(전차) 통과 가능 — CellType + Occupant + 지형 연동</summary>
        private static bool IsGroundPassable(GridCell cell)
        {
            if (cell == null) return false;
            if (cell.Type == CellType.Impassable) return false;
            if (cell.Occupant != null) return false;
            if (!TerrainData.GroundPassable(cell.Terrain)) return false;
            int cost = TerrainData.MoveCost(cell.Terrain);
            return cost < int.MaxValue;
        }

        /// <summary>이 셀 진입에 드는 AP 비용 (지형 기반)</summary>
        private static int StepCost(GridCell cell) =>
            cell != null ? TerrainData.MoveCost(cell.Terrain) : int.MaxValue;

        // ===== A* 경로 탐색 =====

        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int end)
        {
            if (!IsInBounds(start) || !IsInBounds(end)) return null;
            var endCell = GetCell(end);
            if (endCell == null || !IsGroundPassable(endCell)) return null;

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
                    if (!IsGroundPassable(neighbor) || visited.Contains(neighbor.Position))
                        continue;

                    float tentativeG = gCost[current.pos] + StepCost(neighbor);
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

        /// <summary>AP 기반 이동 가능 범위 (지형 비용 반영)</summary>
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
                    if (!IsGroundPassable(neighbor)) continue;
                    float newCost = cost + StepCost(neighbor);
                    if (newCost > maxCost) continue;
                    if (visited.ContainsKey(neighbor.Position) && newCost >= visited[neighbor.Position]) continue;

                    visited[neighbor.Position] = newCost;
                    reachable.Add(neighbor.Position);
                    queue.Enqueue((neighbor.Position, newCost));
                }
            }
            return reachable;
        }

        /// <summary>AP 기반 이동 가능 범위 (tank 실효 이동 비용 + 지형 비용 포함)</summary>
        /// <remarks>tank가 null이 아니면 엔진·궤도·화재 등의 이동 비용 보정 적용</remarks>
        public List<Vector2Int> GetReachableCells(Vector2Int start, int maxCost, Crux.Unit.GridTankUnit tank)
        {
            var reachable = new List<Vector2Int>();
            var visited = new Dictionary<Vector2Int, float>();
            var queue = new Queue<(Vector2Int pos, float cost)>();

            queue.Enqueue((start, 0));
            visited[start] = 0;

            // tank의 기본 이동 비용 (엔진·궤도·화재 보정 포함)
            int extraCostPerStep = 0;
            if (tank != null)
                extraCostPerStep = tank.GetMoveCostPerCell() - GameConstants.MoveCostPerCell;

            while (queue.Count > 0)
            {
                var (pos, cost) = queue.Dequeue();
                foreach (var neighbor in GetNeighbors(pos))
                {
                    if (!IsGroundPassable(neighbor)) continue;
                    float newCost = cost + StepCost(neighbor) + extraCostPerStep;
                    if (newCost > maxCost) continue;
                    if (visited.ContainsKey(neighbor.Position) && newCost >= visited[neighbor.Position]) continue;

                    visited[neighbor.Position] = newCost;
                    reachable.Add(neighbor.Position);
                    queue.Enqueue((neighbor.Position, newCost));
                }
            }
            return reachable;
        }

        // ===== LOS (Line of Sight) =====

        /// <summary>두 셀 간 LOS 유효 여부 — 중간 셀의 건물/벽이 차단, 고도차로 1단계 무력화</summary>
        /// <remarks>기획 §4.4 참조. 공격자/목표 위치의 고도가 중간 장애물보다 높으면 통과.</remarks>
        public bool HasLOS(Vector2Int from, Vector2Int to)
        {
            if (from == to) return true;
            var line = HexCoord.LineBetween(from, to);
            if (line.Count <= 2) return true; // 인접 셀은 LOS 항상 유효

            var fromCell = GetCell(from);
            var toCell = GetCell(to);
            int fromElev = fromCell != null ? TerrainData.Elevation(fromCell.Terrain) : 0;
            int toElev = toCell != null ? TerrainData.Elevation(toCell.Terrain) : 0;

            for (int i = 1; i < line.Count - 1; i++)
            {
                var mid = GetCell(line[i]);
                if (mid == null) continue;
                // 연막 체크 — 연막이 있으면 LOS 차단
                if (mid.HasSmoke) return false;
                if (!TerrainData.BlocksLOS(mid.Terrain)) continue;
                int midElev = TerrainData.Elevation(mid.Terrain);
                // 고도 규칙: 공격자 또는 목표가 중간보다 높으면 1단계까지 넘김
                if (fromElev > midElev && toElev >= midElev) continue;
                return false;
            }
            return true;
        }

        /// <summary>두 셀 간 육각 거리 (cube distance)</summary>
        public int GetDistance(Vector2Int a, Vector2Int b) =>
            HexCoord.Distance(a, b);

        // ===== 유틸 =====

        private float Heuristic(Vector2Int a, Vector2Int b) => HexCoord.Distance(a, b);

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

        /// <summary>모든 셀의 연막 턴 감소</summary>
        /// <remarks>BattleController가 플레이어 턴 시작 시 호출. visualizer가 null이면 시각 효과 생략.</remarks>
        public void TickSmoke(GridVisualizer visualizer = null)
        {
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    var cell = GetCell(new Vector2Int(x, y));
                    if (cell != null && cell.SmokeTurnsLeft > 0)
                    {
                        cell.SmokeTurnsLeft--;
                        if (cell.SmokeTurnsLeft <= 0)
                            visualizer?.ClearSmoke(cell.Position);
                    }
                }
        }

        // ===== 디버그 =====

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    var corners = HexCoord.Corners(new Vector2Int(x, y), GameConstants.CellSize);
                    for (int i = 0; i < 6; i++)
                        Gizmos.DrawLine(corners[i], corners[(i + 1) % 6]);
                }
        }
    }
}
