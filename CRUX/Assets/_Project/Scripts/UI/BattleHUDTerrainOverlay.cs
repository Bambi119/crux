using UnityEngine;
using Crux.Grid;
using Crux.Data;
using Crux.Core;
using TerrainData = Crux.Core.TerrainData;

namespace Crux.UI
{
    /// <summary>지형 디버그 오버레이 전담 — BattleHUD에서 위임받아 렌더링</summary>
    public class BattleHUDTerrainOverlay
    {
        private BattleHUD hud;
        private Crux.Core.BattleController controller;

        public BattleHUDTerrainOverlay(BattleHUD hud, Crux.Core.BattleController controller)
        {
            this.hud = hud;
            this.controller = controller;
        }

        /// <summary>지형 오버레이 + 호버 정보 렌더 진입점 (ShowTerrainDebug true일 때만)</summary>
        public void Draw()
        {
            DrawTerrainOverlay();
            DrawTerrainHoverInfo();
        }

        /// <summary>F1 토글: 각 셀에 지형 한 글자 라벨 그리기 (Open 제외)</summary>
        private void DrawTerrainOverlay()
        {
            var grid = controller.Grid;
            var mainCam = controller.MainCam;
            if (grid == null || mainCam == null) return;

            var style = new GUIStyle();
            style.fontSize = 13;
            style.alignment = TextAnchor.MiddleCenter;
            style.fontStyle = FontStyle.Bold;

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    var cell = grid.GetCell(new Vector2Int(x, y));
                    if (cell == null || cell.Terrain == TerrainType.Open) continue;

                    Vector3 world = grid.GridToWorld(new Vector2Int(x, y));
                    Vector3 screen = mainCam.WorldToScreenPoint(world);
                    if (screen.z < 0) continue;

                    float sx = screen.x / hud.UIScale;
                    float sy = (Screen.height - screen.y) / hud.UIScale;
                    var rect = new Rect(sx - 13, sy - 10, 26, 20);

                    var prev = GUI.color;
                    GUI.color = new Color(0f, 0f, 0f, 0.7f);
                    GUI.DrawTexture(rect, Texture2D.whiteTexture);
                    GUI.color = prev;

                    style.normal.textColor = TerrainLabelColor(cell.Terrain);
                    GUI.Label(rect, TerrainLetter(cell.Terrain), style);
                }
            }
        }

        /// <summary>마우스가 올라간 셀의 지형 상세 정보 박스 — 화면 상단 중앙</summary>
        private void DrawTerrainHoverInfo()
        {
            var grid = controller.Grid;
            var mainCam = controller.MainCam;
            if (grid == null || mainCam == null) return;

            Vector3 mouseWorld = mainCam.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int cellPos = grid.WorldToGrid(mouseWorld);
            if (!grid.IsInBounds(cellPos)) return;

            var cell = grid.GetCell(cellPos);
            if (cell == null) return;

            var terrain = cell.Terrain;
            int moveCost = TerrainData.MoveCost(terrain);
            int elev = TerrainData.Elevation(terrain);
            int concealment = TerrainData.Concealment(terrain);
            float intrinsicCov = TerrainData.IntrinsicCoverRate(terrain);
            bool groundPass = TerrainData.GroundPassable(terrain);
            bool blocksLOS = TerrainData.BlocksLOS(terrain);
            string moveCostStr = moveCost == int.MaxValue ? "∞" : moveCost.ToString();

            float boxW = 260;
            float boxH = 112;
            float bx = (hud.ScaledW - boxW) * 0.5f;
            float by = 50;

            hud.DrawBox(new Rect(bx, by, boxW, boxH));

            var title = hud.GetLabelStyleUI();
            title.fontSize = 15;
            title.fontStyle = FontStyle.Bold;
            title.normal.textColor = TerrainLabelColor(terrain);
            GUI.Label(new Rect(bx + 12, by + 6, boxW - 24, 20),
                $"{TerrainData.Label(terrain)} @ ({cellPos.x},{cellPos.y})", title);

            var row = hud.GetLabelStyleUI();
            row.fontSize = 13;
            row.normal.textColor = new Color(0.9f, 0.9f, 0.9f);

            float ly = by + 28;
            GUI.Label(new Rect(bx + 12, ly, boxW - 24, 18),
                $"이동 {moveCostStr} AP  |  고도 {(elev >= 0 ? "+" : "")}{elev}", row);
            ly += 17;
            GUI.Label(new Rect(bx + 12, ly, boxW - 24, 18),
                $"은엄폐 {concealment}%  |  지형엄폐 {intrinsicCov:P0}", row);
            ly += 17;
            string pass = groundPass ? "지상 통과" : "지상 차단";
            string los = blocksLOS ? "LOS 차단" : "LOS 통과";
            GUI.Label(new Rect(bx + 12, ly, boxW - 24, 18), $"{pass}  |  {los}", row);

            ly += 17;
            if (cell.HasCover && cell.Cover != null && !cell.Cover.IsDestroyed)
            {
                var covRow = new GUIStyle(row);
                covRow.normal.textColor = new Color(1f, 0.8f, 0.4f);
                GUI.Label(new Rect(bx + 12, ly, boxW - 24, 18),
                    $"엄폐물: {cell.Cover.coverName} ({cell.Cover.CoverRate:P0})", covRow);
            }
            else if (cell.HasSmoke)
            {
                var sm = new GUIStyle(row);
                sm.normal.textColor = new Color(0.7f, 0.9f, 1f);
                GUI.Label(new Rect(bx + 12, ly, boxW - 24, 18),
                    $"연막 {cell.SmokeTurnsLeft}턴 잔존", sm);
            }
        }

        /// <summary>지형 타입 → 한 글자 라벨</summary>
        private static string TerrainLetter(TerrainType t) => t switch
        {
            TerrainType.Road             => "로",
            TerrainType.Mud              => "진",
            TerrainType.Woods            => "숲",
            TerrainType.Rubble           => "편",
            TerrainType.Crater           => "탄",
            TerrainType.Hill             => "언",
            TerrainType.Building         => "건",
            TerrainType.ElevatedBuilding => "고",
            TerrainType.Water            => "물",
            _ => ""
        };

        /// <summary>지형 타입 → 레이블 색상</summary>
        private static Color TerrainLabelColor(TerrainType t) => t switch
        {
            TerrainType.Road             => new Color(0.95f, 0.95f, 0.75f),
            TerrainType.Mud              => new Color(0.95f, 0.75f, 0.45f),
            TerrainType.Woods            => new Color(0.50f, 1.00f, 0.50f),
            TerrainType.Rubble           => new Color(0.90f, 0.80f, 0.65f),
            TerrainType.Crater           => new Color(1.00f, 0.80f, 0.40f),
            TerrainType.Hill             => new Color(1.00f, 0.95f, 0.50f),
            TerrainType.Building         => new Color(0.65f, 0.75f, 1.00f),
            TerrainType.ElevatedBuilding => new Color(1.00f, 0.70f, 1.00f),
            TerrainType.Water            => new Color(0.50f, 0.80f, 1.00f),
            _ => Color.white
        };
    }
}
