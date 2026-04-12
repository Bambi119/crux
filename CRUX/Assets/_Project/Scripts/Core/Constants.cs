using UnityEngine;

namespace Crux.Core
{
    /// <summary>각도 변환 유틸 — 나침반 방식 (0°=북, CW). 스프라이트 기본 방향=오른쪽(→)</summary>
    public static class AngleUtil
    {
        /// <summary>나침반 각도 → Unity Euler Z (스프라이트가 →를 향할 때)</summary>
        public static float ToUnity(float compass) => 90f - compass;

        /// <summary>Unity Euler Z → 나침반 각도</summary>
        public static float FromUnity(float unity) => 90f - unity;

        /// <summary>방향 벡터 → 나침반 각도</summary>
        public static float FromDir(Vector2 dir) =>
            Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

        /// <summary>나침반 각도 → 방향 벡터</summary>
        public static Vector2 ToDir(float compass)
        {
            float rad = compass * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
        }

        /// <summary>임의 나침반 각도를 가장 가까운 60° 배수로 스냅</summary>
        public static float SnapTo60(float compass)
        {
            float a = ((compass % 360f) + 360f) % 360f;
            int idx = Mathf.RoundToInt(a / 60f) % 6;
            return idx * 60f;
        }
    }

    /// <summary>진영</summary>
    public enum PlayerSide { Player, Enemy }

    /// <summary>피격 부위 — 차체 6섹터 + 포탑</summary>
    /// <remarks>
    /// Front = 0° ± 30°, FrontRight = 60° ± 30°, RearRight = 120° ± 30°,
    /// Rear = 180° ± 30°, RearLeft = 240° ± 30°, FrontLeft = 300° ± 30°
    /// </remarks>
    public enum HitZone
    {
        Front,      // 전면
        FrontRight, // 우전
        RearRight,  // 우후
        Rear,       // 후면
        RearLeft,   // 좌후
        FrontLeft,  // 좌전
        Turret      // 포탑 (별도 판정)
    }

    /// <summary>탄종</summary>
    public enum AmmoType { AP, HE, HEAT, APCR }

    /// <summary>사격 결과 3단계</summary>
    public enum ShotOutcome
    {
        Miss,           // 빗나감
        Ricochet,       // 도탄 — 3% 데미지, 튕김 연출
        Hit,            // 피격 — 일반 데미지
        Penetration     // 관통 — 250% 데미지, 폭발 연출
    }

    // ModuleType은 Crux.Unit.ModuleSystem으로 이동

    /// <summary>적 유형</summary>
    public enum EnemyType { Light, Heavy }

    /// <summary>셀 타입</summary>
    public enum CellType { Empty, Cover, Impassable }

    /// <summary>엄폐물 크기</summary>
    public enum CoverSize { Small, Medium, Large }

    /// <summary>바닥 지형</summary>
    public enum TerrainType { Normal, Mud, Road }

    /// <summary>턴 페이즈</summary>
    public enum TurnPhase { PlayerTurn, EnemyTurn, Cinematic, GameOver, Victory }

    /// <summary>행동 타입</summary>
    public enum ActionType { Move, Fire, Overwatch }

    /// <summary>무기 타입</summary>
    public enum WeaponType
    {
        MainGun,        // 주포
        CoaxialMG,      // 동축 기관총
        MountedMG       // 탑재 기관총
    }

    /// <summary>게임 상수</summary>
    public static class GameConstants
    {
        // 그리드
        public const int GridWidth = 8;
        public const int GridHeight = 10;
        public const float CellSize = 1f;

        // AP
        public const int PlayerMaxAP = 6;
        public const int LightEnemyMaxAP = 5;
        public const int HeavyEnemyMaxAP = 4;
        public const int MoveCostPerCell = 1;
        public const int FireCost = 3;
        public const int OverwatchCost = 2;

        // 사거리
        public const int MaxFireRange = 8;

        // 관통 판정
        public const float AutoRicochetAngle = 70f;
        public const float PenetrationVariance = 0.1f;

        // 명중률
        public const float BaseAccuracy = 0.85f;
        public const float DistancePenaltyPerCell = 0.04f;
        public const float CoverAccuracyPenalty = 0.25f;

        // 연출
        public const float CinematicDuration = 5f;
    }
}
