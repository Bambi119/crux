using System.Collections.Generic;
using UnityEngine;

namespace Crux.Grid
{
    /// <summary>육각 그리드 좌표 유틸 — flat-top, odd-q offset 저장</summary>
    /// <remarks>
    /// 저장: Vector2Int(col, row) odd-q offset coordinates.
    /// 수학 연산: 축좌표(axial) 변환 후 cube distance.
    /// 참고: https://www.redblobgames.com/grids/hexagons/
    /// </remarks>
    public static class HexCoord
    {
        /// <summary>6방향 나침반 — 플랫탑 인접 방향</summary>
        public enum HexDir
        {
            N = 0,   // 북
            NE = 1,  // 북동
            SE = 2,  // 남동
            S = 3,   // 남
            SW = 4,  // 남서
            NW = 5   // 북서
        }

        public const int DirCount = 6;

        // ===== 이웃 오프셋 (odd-q flat-top) =====
        // 홀수 열은 짝수 열보다 아래로 0.5 셀 밀려 있음
        // 순서: N → NE → SE → S → SW → NW

        private static readonly Vector2Int[] EvenColNeighbors =
        {
            new(0, -1),   // N
            new(1, -1),   // NE
            new(1, 0),    // SE
            new(0, 1),    // S
            new(-1, 0),   // SW
            new(-1, -1),  // NW
        };

        private static readonly Vector2Int[] OddColNeighbors =
        {
            new(0, -1),   // N
            new(1, 0),    // NE
            new(1, 1),    // SE
            new(0, 1),    // S
            new(-1, 1),   // SW
            new(-1, 0),   // NW
        };

        /// <summary>셀의 특정 방향 이웃 오프셋 좌표</summary>
        public static Vector2Int Neighbor(Vector2Int offset, HexDir dir)
        {
            bool oddCol = (offset.x & 1) == 1;
            var delta = (oddCol ? OddColNeighbors : EvenColNeighbors)[(int)dir];
            return offset + delta;
        }

        /// <summary>셀의 6방향 이웃 전체</summary>
        public static IEnumerable<Vector2Int> Neighbors(Vector2Int offset)
        {
            bool oddCol = (offset.x & 1) == 1;
            var table = oddCol ? OddColNeighbors : EvenColNeighbors;
            for (int i = 0; i < DirCount; i++)
                yield return offset + table[i];
        }

        // ===== 거리 (축좌표 기반) =====

        /// <summary>odd-q offset → axial (q, r)</summary>
        public static Vector2Int OffsetToAxial(Vector2Int offset)
        {
            int q = offset.x;
            int r = offset.y - (offset.x - (offset.x & 1)) / 2;
            return new Vector2Int(q, r);
        }

        /// <summary>axial → odd-q offset</summary>
        public static Vector2Int AxialToOffset(Vector2Int axial)
        {
            int col = axial.x;
            int row = axial.y + (axial.x - (axial.x & 1)) / 2;
            return new Vector2Int(col, row);
        }

        /// <summary>두 셀 사이 육각 거리 (cube distance)</summary>
        public static int Distance(Vector2Int a, Vector2Int b)
        {
            var axA = OffsetToAxial(a);
            var axB = OffsetToAxial(b);
            int dq = axA.x - axB.x;
            int dr = axA.y - axB.y;
            int ds = -dq - dr;
            return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(ds)) / 2;
        }

        // ===== 라인/LOS (hex linear interpolation) =====

        /// <summary>두 hex 사이를 잇는 셀 라인 (양 끝 포함) — LOS·블라스트 판정용</summary>
        /// <remarks>
        /// 큐브 좌표 선형 보간 → 각 지점을 cube round로 가장 가까운 hex에 스냅.
        /// 참고: https://www.redblobgames.com/grids/hexagons/#line-drawing
        /// </remarks>
        public static List<Vector2Int> LineBetween(Vector2Int a, Vector2Int b)
        {
            int dist = Distance(a, b);
            var result = new List<Vector2Int>(dist + 1);
            if (dist == 0)
            {
                result.Add(a);
                return result;
            }

            var axA = OffsetToAxial(a);
            var axB = OffsetToAxial(b);
            // axial → cube: (x=q, z=r, y=-q-r)
            float ax = axA.x, az = axA.y, ay = -ax - az;
            float bx = axB.x, bz = axB.y, by = -bx - bz;

            // 경계 모호성 회피용 미세 오프셋 (redblobgames 권장)
            ax += 1e-6f; ay += 1e-6f; az -= 2e-6f;

            for (int i = 0; i <= dist; i++)
            {
                float t = (float)i / dist;
                float cx = Mathf.Lerp(ax, bx, t);
                float cy = Mathf.Lerp(ay, by, t);
                float cz = Mathf.Lerp(az, bz, t);
                var rounded = CubeRoundToAxial(cx, cy, cz);
                result.Add(AxialToOffset(rounded));
            }
            return result;
        }

        /// <summary>큐브 분수 좌표 → 가장 가까운 axial 정수 좌표</summary>
        private static Vector2Int CubeRoundToAxial(float x, float y, float z)
        {
            int rx = Mathf.RoundToInt(x);
            int ry = Mathf.RoundToInt(y);
            int rz = Mathf.RoundToInt(z);
            float dx = Mathf.Abs(rx - x);
            float dy = Mathf.Abs(ry - y);
            float dz = Mathf.Abs(rz - z);
            if (dx > dy && dx > dz) rx = -ry - rz;
            else if (dy > dz) ry = -rx - rz;
            else rz = -rx - ry;
            // axial (q=x, r=z)
            return new Vector2Int(rx, rz);
        }

        // ===== 월드 좌표 변환 (flat-top) =====

        /// <summary>오프셋 좌표 → 월드 좌표</summary>
        public static Vector3 OffsetToWorld(Vector2Int offset, float size)
        {
            float x = size * 1.5f * offset.x;
            float y = size * Mathf.Sqrt(3f) * (offset.y + 0.5f * (offset.x & 1));
            return new Vector3(x, y, 0f);
        }

        /// <summary>월드 좌표 → 오프셋 좌표 (가장 가까운 셀)</summary>
        public static Vector2Int WorldToOffset(Vector3 world, float size)
        {
            // 먼저 분수 axial 계산 후 cube round, 그다음 offset으로 변환
            float fracQ = (2f / 3f) * world.x / size;
            float fracR = (-1f / 3f * world.x + Mathf.Sqrt(3f) / 3f * world.y) / size;
            var axialRound = CubeRound(fracQ, fracR);
            return AxialToOffset(axialRound);
        }

        private static Vector2Int CubeRound(float q, float r)
        {
            float s = -q - r;
            int rq = Mathf.RoundToInt(q);
            int rr = Mathf.RoundToInt(r);
            int rs = Mathf.RoundToInt(s);
            float dq = Mathf.Abs(rq - q);
            float dr = Mathf.Abs(rr - r);
            float ds = Mathf.Abs(rs - s);
            if (dq > dr && dq > ds) rq = -rr - rs;
            else if (dr > ds) rr = -rq - rs;
            return new Vector2Int(rq, rr);
        }

        // ===== 방향 판정 =====

        /// <summary>공격자 → 대상 방향을 가장 가까운 HexDir로 스냅</summary>
        public static HexDir AttackDir(Vector2Int from, Vector2Int to, float size)
        {
            Vector3 fw = OffsetToWorld(from, size);
            Vector3 tw = OffsetToWorld(to, size);
            return NearestDir((Vector2)(tw - fw));
        }

        /// <summary>월드 공간 방향 벡터 → 가장 가까운 HexDir</summary>
        public static HexDir NearestDir(Vector2 worldDir)
        {
            if (worldDir.sqrMagnitude < 1e-5f) return HexDir.N;

            float best = -999f;
            HexDir bestDir = HexDir.N;
            Vector2 norm = worldDir.normalized;

            // 각 HexDir의 월드 방향과 dot product 비교
            for (int i = 0; i < DirCount; i++)
            {
                Vector2 dirVec = DirToWorld((HexDir)i);
                float dot = Vector2.Dot(norm, dirVec);
                if (dot > best)
                {
                    best = dot;
                    bestDir = (HexDir)i;
                }
            }
            return bestDir;
        }

        /// <summary>HexDir의 월드 공간 단위 방향 벡터 (flat-top)</summary>
        public static Vector2 DirToWorld(HexDir dir)
        {
            // flat-top에서 6방향의 월드 각도 (compass 0°=N, CW)
            // N=0°, NE=60°, SE=120°, S=180°, SW=240°, NW=300°
            float compassDeg = (int)dir * 60f;
            float rad = compassDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
        }

        /// <summary>HexDir → 나침반 각도 (0=N, 60=NE ...)</summary>
        public static float DirToCompass(HexDir dir) => (int)dir * 60f;

        /// <summary>HexDir → 한글 라벨</summary>
        public static string DirLabel(HexDir dir) => dir switch
        {
            HexDir.N => "북",
            HexDir.NE => "북동",
            HexDir.SE => "남동",
            HexDir.S => "남",
            HexDir.SW => "남서",
            HexDir.NW => "북서",
            _ => ""
        };

        /// <summary>HexDir → 화살표 문자</summary>
        public static string DirArrow(HexDir dir) => dir switch
        {
            HexDir.N => "↑",
            HexDir.NE => "↗",
            HexDir.SE => "↘",
            HexDir.S => "↓",
            HexDir.SW => "↙",
            HexDir.NW => "↖",
            _ => ""
        };

        // ===== 셀 경계 (월드 공간 변 계산) =====

        /// <summary>한 hex의 특정 방향 변 중점 (월드 공간)</summary>
        /// <remarks>엄폐물 벽 그래픽을 변 위에 배치할 때 사용</remarks>
        public static Vector3 EdgeMidpoint(Vector2Int offset, HexDir dir, float size)
        {
            Vector3 center = OffsetToWorld(offset, size);
            Vector2 dirVec = DirToWorld(dir);
            // 플랫탑 hex에서 한 변 중점까지 거리 = size * sqrt(3) / 2
            float edgeDist = size * Mathf.Sqrt(3f) / 2f;
            return center + new Vector3(dirVec.x, dirVec.y, 0f) * edgeDist;
        }

        /// <summary>한 hex의 6개 꼭짓점 (월드 공간, flat-top)</summary>
        public static Vector3[] Corners(Vector2Int offset, float size)
        {
            Vector3 c = OffsetToWorld(offset, size);
            var corners = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                // 플랫탑: 꼭짓점이 0°, 60°, 120° ... (x축 기준)
                float rad = i * 60f * Mathf.Deg2Rad;
                corners[i] = c + new Vector3(size * Mathf.Cos(rad), size * Mathf.Sin(rad), 0f);
            }
            return corners;
        }
    }

    /// <summary>엄폐 방호면 비트 플래그 — HexDir와 인덱스 일치</summary>
    [System.Flags]
    public enum HexFacet
    {
        None = 0,
        N = 1 << 0,
        NE = 1 << 1,
        SE = 1 << 2,
        S = 1 << 3,
        SW = 1 << 4,
        NW = 1 << 5,
        All = N | NE | SE | S | SW | NW
    }

    public static class HexFacetExtensions
    {
        public static HexFacet ToFacet(this HexCoord.HexDir dir) =>
            (HexFacet)(1 << (int)dir);

        public static bool Contains(this HexFacet facets, HexCoord.HexDir dir) =>
            (facets & dir.ToFacet()) != 0;

        public static int Count(this HexFacet facets)
        {
            int c = 0;
            int v = (int)facets;
            while (v != 0) { c += v & 1; v >>= 1; }
            return c;
        }

        /// <summary>포함된 방향 열거</summary>
        public static IEnumerable<HexCoord.HexDir> Enumerate(this HexFacet facets)
        {
            for (int i = 0; i < HexCoord.DirCount; i++)
            {
                var d = (HexCoord.HexDir)i;
                if (facets.Contains(d)) yield return d;
            }
        }
    }
}
