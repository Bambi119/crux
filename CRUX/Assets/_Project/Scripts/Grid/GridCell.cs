using UnityEngine;
using Crux.Core;

namespace Crux.Grid
{
    /// <summary>그리드 셀 하나의 데이터 (육각 — odd-q offset)</summary>
    public class GridCell
    {
        public Vector2Int Position { get; private set; }
        public CellType Type { get; set; }
        public TerrainType Terrain { get; set; }

        /// <summary>점유 유닛 (없으면 null)</summary>
        public GameObject Occupant { get; set; }

        /// <summary>이 셀의 엄폐물 (없으면 null)</summary>
        public GridCoverObject Cover { get; set; }

        /// <summary>엄폐물이 있어도 통행 가능 — 유닛이 올라가서 엄폐 효과를 받음</summary>
        public bool IsWalkable => Type != CellType.Impassable && Occupant == null;

        /// <summary>이 셀에 활성 엄폐물이 있는지</summary>
        public bool HasCover => Cover != null && !Cover.IsDestroyed;

        /// <summary>연막 잔여 턴 (0=없음)</summary>
        public int SmokeTurnsLeft { get; set; }

        /// <summary>연막 활성 여부</summary>
        public bool HasSmoke => SmokeTurnsLeft > 0;

        public GridCell(Vector2Int pos, CellType type = CellType.Empty)
        {
            Position = pos;
            Type = type;
            Terrain = TerrainType.Open;
        }
    }
}
