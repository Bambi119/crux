using UnityEngine;

namespace Crux.Core
{
    /// <summary>지형 타입별 전술 속성 테이블 — MoveCost/Elevation/Concealment/LOS/Passability</summary>
    /// <remarks>
    /// AI·이동·LOS·명중률 계산의 단일 진실 원천.
    /// 값 조정은 이 파일만 수정하면 됨.
    /// 기획 레퍼런스: docs/12_enemy_ai.md §4.1~§4.2
    /// </remarks>
    public static class TerrainData
    {
        /// <summary>이동 AP 비용 — int.MaxValue = 통과 불가</summary>
        public static int MoveCost(TerrainType t) => t switch
        {
            TerrainType.Open             => 1,
            TerrainType.Road             => 1, // 추가 보너스(+1 이동)는 유닛측에서 처리
            TerrainType.Mud              => 2,
            TerrainType.Woods            => 1,
            TerrainType.Rubble           => 2,
            TerrainType.Crater           => 1,
            TerrainType.Hill             => 1,
            TerrainType.Building         => int.MaxValue,
            TerrainType.ElevatedBuilding => int.MaxValue, // 지상 전차 불가 (보병/드론 개별 판정)
            TerrainType.Water            => int.MaxValue, // 지상 불가 (비행 통과)
            _ => 1
        };

        /// <summary>도로 이동 AP 보너스 (도로→도로 이동 시 반환 AP)</summary>
        public static int RoadMoveBonus(TerrainType t) => t == TerrainType.Road ? 1 : 0;

        /// <summary>고도 (정수 스칼라, +3=언덕/고지) — 명중률·LOS 보너스에 사용</summary>
        public static int Elevation(TerrainType t) => t switch
        {
            TerrainType.Hill             => 3,
            TerrainType.ElevatedBuilding => 3,
            TerrainType.Crater           => -1,
            _ => 0
        };

        /// <summary>은엄폐 (%) — 피격 명중률 감소치</summary>
        public static int Concealment(TerrainType t) => t switch
        {
            TerrainType.Woods  => 30,
            TerrainType.Rubble => 10,
            _ => 0
        };

        /// <summary>지형 자체에서 발생하는 엄폐율 (엄폐물 오브젝트와 독립)</summary>
        public static float IntrinsicCoverRate(TerrainType t) => t switch
        {
            TerrainType.Rubble           => 0.20f,
            TerrainType.Crater           => 0.30f,
            TerrainType.ElevatedBuilding => 0.50f,
            _ => 0f
        };

        /// <summary>LOS 차단 여부 — 건물 외벽만 차단. 고도 차로 무력화 가능</summary>
        public static bool BlocksLOS(TerrainType t) => t == TerrainType.Building;

        /// <summary>지상 유닛 통과 가능 여부 (전차/차량 기준)</summary>
        public static bool GroundPassable(TerrainType t) =>
            t != TerrainType.Building
            && t != TerrainType.ElevatedBuilding
            && t != TerrainType.Water;

        /// <summary>비행 유닛 통과 여부 (드론) — 건물 외벽만 차단</summary>
        public static bool FlyingPassable(TerrainType t) => t != TerrainType.Building;

        /// <summary>보병 통과 여부 — 물 제외 전부 가능 (고지 건물 점유 포함)</summary>
        public static bool InfantryPassable(TerrainType t) =>
            t != TerrainType.Building && t != TerrainType.Water;

        /// <summary>사거리 보너스 — 고지에서 +1</summary>
        public static int RangeBonus(TerrainType t) => Elevation(t) > 0 ? 1 : 0;

        /// <summary>셀 틴트 색상 (D8) — 지형 시각 식별용</summary>
        /// <remarks>Tile_01 다크톤 레퍼런스 기준으로 채도 낮은 미세 오프셋</remarks>
        public static Color TintColor(TerrainType t) => t switch
        {
            TerrainType.Open             => new Color(1.00f, 1.00f, 1.00f, 1f),
            TerrainType.Road             => new Color(0.85f, 0.83f, 0.80f, 1f),
            TerrainType.Mud              => new Color(0.55f, 0.45f, 0.35f, 1f),
            TerrainType.Woods            => new Color(0.55f, 0.75f, 0.50f, 1f),
            TerrainType.Rubble           => new Color(0.75f, 0.70f, 0.65f, 1f),
            TerrainType.Crater           => new Color(0.45f, 0.40f, 0.35f, 1f),
            TerrainType.Hill             => new Color(1.05f, 0.98f, 0.85f, 1f), // warm highlight
            TerrainType.Building         => new Color(0.60f, 0.60f, 0.65f, 1f),
            TerrainType.ElevatedBuilding => new Color(1.10f, 1.00f, 0.80f, 1f), // warmer/brighter
            TerrainType.Water            => new Color(0.35f, 0.55f, 0.85f, 1f),
            _ => Color.white
        };

        /// <summary>한글 라벨 (UI/디버그용)</summary>
        public static string Label(TerrainType t) => t switch
        {
            TerrainType.Open             => "개활지",
            TerrainType.Road             => "도로",
            TerrainType.Mud              => "진창",
            TerrainType.Woods            => "수풀",
            TerrainType.Rubble           => "파편지대",
            TerrainType.Crater           => "탄흔",
            TerrainType.Hill             => "언덕",
            TerrainType.Building         => "건물",
            TerrainType.ElevatedBuilding => "고지 건물",
            TerrainType.Water            => "물",
            _ => "?"
        };
    }
}
