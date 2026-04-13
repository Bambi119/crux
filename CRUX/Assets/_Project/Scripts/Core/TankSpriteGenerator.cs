using UnityEngine;

namespace Crux.Core
{
    /// <summary>탑뷰 전차 스프라이트 생성기 — 정면 = 오른쪽(angle 0)</summary>
    public static class TankSpriteGenerator
    {
        // 전차는 가로가 긴 형태 (정면이 오른쪽)
        // X+ = 전면, X- = 후면, Y+/- = 측면

        /// <summary>플레이어 전차 차체 스프라이트 (32x24)</summary>
        public static Sprite CreatePlayerHull()
        {
            int w = 32, h = 24;
            var tex = new Texture2D(w, h);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[w * h];

            Color hull = new Color(0.25f, 0.45f, 0.2f);       // 군녹색
            Color hullDark = new Color(0.18f, 0.35f, 0.15f);  // 어두운 녹색
            Color track = new Color(0.2f, 0.2f, 0.18f);       // 궤도 (진회색)
            Color trackDetail = new Color(0.28f, 0.26f, 0.22f);
            Color highlight = new Color(0.35f, 0.55f, 0.3f);  // 하이라이트
            Color engine = new Color(0.3f, 0.3f, 0.25f);      // 엔진부 (후면)

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c = Color.clear;

                    // 궤도 (상하 가장자리)
                    if (y <= 3 || y >= h - 4)
                    {
                        if (x >= 2 && x <= w - 3)
                        {
                            c = (x % 3 == 0) ? trackDetail : track;
                        }
                    }
                    // 차체
                    else if (y >= 4 && y <= h - 5 && x >= 3 && x <= w - 4)
                    {
                        c = hull;

                        // 전면 강조 (오른쪽 끝)
                        if (x >= w - 7)
                            c = highlight;

                        // 후면 엔진부 (왼쪽 끝)
                        if (x <= 7)
                            c = engine;

                        // 중앙 라인
                        if (y == h / 2)
                            c = hullDark;

                        // 상하 가장자리 어둡게
                        if (y == 4 || y == h - 5)
                            c = hullDark;
                    }

                    pixels[y * w + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 24);
        }

        /// <summary>플레이어 포탑 스프라이트 (16x14)</summary>
        public static Sprite CreatePlayerTurret()
        {
            int w = 16, h = 14;
            var tex = new Texture2D(w, h);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[w * h];

            Color turret = new Color(0.2f, 0.4f, 0.22f);
            Color turretLight = new Color(0.3f, 0.5f, 0.28f);
            Color turretDark = new Color(0.15f, 0.3f, 0.15f);

            int cx = w / 2, cy = h / 2;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c = Color.clear;

                    // 대략 타원형 포탑
                    float dx = (float)(x - cx) / (w * 0.45f);
                    float dy = (float)(y - cy) / (h * 0.45f);
                    float dist = dx * dx + dy * dy;

                    if (dist <= 1f)
                    {
                        c = turret;
                        if (dist < 0.5f) c = turretLight;
                        if (y == cy - 1 || y == cy + 1) c = turretDark; // 가로줄 디테일
                    }

                    pixels[y * w + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 24);
        }

        /// <summary>주포 포신 스프라이트 (20x4)</summary>
        public static Sprite CreateBarrel()
        {
            int w = 20, h = 4;
            var tex = new Texture2D(w, h);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[w * h];

            Color barrel = new Color(0.22f, 0.22f, 0.2f);
            Color barrelDark = new Color(0.15f, 0.15f, 0.13f);
            Color muzzle = new Color(0.3f, 0.3f, 0.28f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c = Color.clear;

                    if (y >= 1 && y <= h - 2)
                    {
                        c = barrel;
                        if (y == 1) c = barrelDark;
                        if (x >= w - 3) c = muzzle; // 총구
                    }

                    pixels[y * w + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0.5f), 24);
        }

        /// <summary>적 경량 전차 스프라이트 (28x20)</summary>
        public static Sprite CreateLightEnemy()
        {
            int w = 28, h = 20;
            var tex = new Texture2D(w, h);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[w * h];

            Color hull = new Color(0.5f, 0.2f, 0.15f);
            Color hullDark = new Color(0.35f, 0.15f, 0.1f);
            Color track = new Color(0.25f, 0.2f, 0.18f);
            Color glow = new Color(0.8f, 0.2f, 0.2f); // 바이오 코어 빛

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c = Color.clear;

                    // 궤도
                    if ((y <= 2 || y >= h - 3) && x >= 2 && x <= w - 3)
                        c = track;
                    // 차체
                    else if (y >= 3 && y <= h - 4 && x >= 3 && x <= w - 4)
                    {
                        c = hull;
                        if (x >= w - 6) c = hullDark; // 전면 어둡게
                        // 중앙 바이오 코어 빛
                        if (Mathf.Abs(x - w / 2) <= 2 && Mathf.Abs(y - h / 2) <= 2)
                            c = glow;
                    }

                    pixels[y * w + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 24);
        }

        /// <summary>적 중장갑 전차 스프라이트 (36x28)</summary>
        public static Sprite CreateHeavyEnemy()
        {
            int w = 36, h = 28;
            var tex = new Texture2D(w, h);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[w * h];

            Color hull = new Color(0.35f, 0.12f, 0.1f);
            Color hullDark = new Color(0.25f, 0.08f, 0.06f);
            Color track = new Color(0.2f, 0.18f, 0.15f);
            Color armor = new Color(0.4f, 0.15f, 0.12f);
            Color glow = new Color(1f, 0.15f, 0.3f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c = Color.clear;

                    // 넓은 궤도
                    if ((y <= 3 || y >= h - 4) && x >= 2 && x <= w - 3)
                        c = track;
                    // 차체
                    else if (y >= 4 && y <= h - 5 && x >= 3 && x <= w - 4)
                    {
                        c = hull;
                        // 증가 장갑 (측면 두꺼움)
                        if (y <= 6 || y >= h - 7) c = armor;
                        if (x >= w - 7) c = armor; // 전면 장갑
                        // 중앙 코어
                        if (Mathf.Abs(x - w / 2) <= 3 && Mathf.Abs(y - h / 2) <= 3)
                            c = glow;
                        if (y == h / 2) c = hullDark;
                    }

                    pixels[y * w + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 24);
        }

        /// <summary>바이오 코어 스프라이트 (40x40)</summary>
        public static Sprite CreateBioCore()
        {
            int size = 40;
            var tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[size * size];

            Color shell = new Color(0.3f, 0.1f, 0.2f);
            Color inner = new Color(0.8f, 0.1f, 0.5f);
            Color core = new Color(1f, 0.3f, 0.8f);
            Color outline = new Color(0.15f, 0.05f, 0.1f);

            int cx = size / 2, cy = size / 2;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    Color c = Color.clear;

                    if (dist <= 18) c = shell;
                    if (dist <= 14) c = inner;
                    if (dist <= 8) c = core;
                    if (dist > 17 && dist <= 19) c = outline;

                    pixels[y * size + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 24);
        }

        /// <summary>방어 포탑 스프라이트 (16x16)</summary>
        public static Sprite CreateDefenseTurret()
        {
            int size = 16;
            var tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[size * size];

            Color body = new Color(0.45f, 0.2f, 0.2f);
            Color dark = new Color(0.3f, 0.12f, 0.12f);

            int cx = size / 2, cy = size / 2;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    Color c = Color.clear;

                    if (dist <= 6) c = body;
                    if (dist <= 3) c = dark;

                    pixels[y * size + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 24);
        }

        /// <summary>엄폐물 스프라이트 (12x12) — 기존 호환 (간단한 벽)</summary>
        public static Sprite CreateCover()
        {
            int size = 12;
            var tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[size * size];

            Color wall = new Color(0.4f, 0.38f, 0.35f);
            Color wallDark = new Color(0.3f, 0.28f, 0.25f);
            Color crack = new Color(0.25f, 0.22f, 0.2f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color c = Color.clear;

                    if (x >= 1 && x <= size - 2 && y >= 1 && y <= size - 2)
                    {
                        c = wall;
                        if ((x + y) % 5 == 0) c = crack;
                        if (x == 1 || y == 1) c = wallDark;
                    }

                    pixels[y * size + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 12);
        }

        /// <summary>연출 씬 전용 엄폐 벽 스프라이트 — 크기별 벽/잔해/바리케이드</summary>
        /// <remarks>
        /// 직사각형 콘크리트·잔해 벽 비주얼. Fire Action Scene에서
        /// 전차와 적 사이에 놓이는 엄폐 형상 표현용.
        /// 소형: 낮은 잔해 더미, 중형: 콘크리트 벽, 대형: 두꺼운 장벽
        /// </remarks>
        public static Sprite CreateCinematicCover(CoverSize size)
        {
            // 벽의 픽셀 치수 (ppu 32 → 1 unit)
            int w, h;
            switch (size)
            {
                case CoverSize.Small:  w = 40; h = 14; break;   // 잔해 더미 — 낮고 가로로 넓음
                case CoverSize.Medium: w = 48; h = 22; break;   // 콘크리트 벽
                case CoverSize.Large:  w = 56; h = 32; break;   // 강화 장벽 — 두껍고 높음
                default:               w = 48; h = 22; break;
            }

            // 가운데 정렬을 위해 스프라이트 캔버스는 너비·높이 2의 배수로
            var tex = new Texture2D(w, h);
            tex.filterMode = FilterMode.Point;
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

            // 색 팔레트 — 크기별 톤
            Color wallBase, wallDark, wallLight, crackCol;
            switch (size)
            {
                case CoverSize.Small:
                    wallBase = new Color(0.55f, 0.48f, 0.38f);  // 흙/잔해 갈색
                    wallDark = new Color(0.32f, 0.28f, 0.22f);
                    wallLight = new Color(0.68f, 0.6f, 0.48f);
                    crackCol = new Color(0.22f, 0.18f, 0.14f);
                    break;
                case CoverSize.Large:
                    wallBase = new Color(0.42f, 0.42f, 0.42f);  // 짙은 콘크리트
                    wallDark = new Color(0.22f, 0.22f, 0.22f);
                    wallLight = new Color(0.58f, 0.58f, 0.58f);
                    crackCol = new Color(0.14f, 0.14f, 0.15f);
                    break;
                default: // Medium
                    wallBase = new Color(0.5f, 0.48f, 0.42f);   // 회갈색 콘크리트
                    wallDark = new Color(0.28f, 0.26f, 0.22f);
                    wallLight = new Color(0.65f, 0.62f, 0.54f);
                    crackCol = new Color(0.18f, 0.16f, 0.14f);
                    break;
            }

            // 벽 본체 — 위쪽 1픽셀, 아래쪽 1픽셀, 좌우 1픽셀 제외한 영역
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // 외곽 1픽셀 그림자
                    bool outer = (x == 0 || x == w - 1 || y == 0);
                    bool bottomShadow = (y == 0); // 바닥 그림자
                    bool topHighlight = (y == h - 1 && x > 0 && x < w - 1);

                    if (outer)
                    {
                        if (bottomShadow) px[y * w + x] = new Color(0, 0, 0, 0.35f);
                        continue;
                    }

                    Color c = wallBase;

                    // 상단 하이라이트
                    if (topHighlight) c = wallLight;
                    // 최상단 두 줄은 살짝 밝게
                    else if (y == h - 2) c = Color.Lerp(wallBase, wallLight, 0.5f);
                    // 바닥 쪽 어둡게
                    else if (y <= 2) c = wallDark;
                    else if (y <= 4) c = Color.Lerp(wallDark, wallBase, 0.5f);

                    // 균열·변형 패턴
                    int seed = (x * 13 + y * 7) % 17;
                    if (seed == 0) c = crackCol;
                    else if (seed == 3) c = wallDark;

                    // 대형 엄폐물은 수직 이음선 추가
                    if (size == CoverSize.Large && (x == w / 3 || x == 2 * w / 3) && y < h - 2 && y > 1)
                        c = wallDark;

                    // 소형은 위쪽이 우둘투둘 — 꼭대기 랜덤 결손
                    if (size == CoverSize.Small && y >= h - 2)
                    {
                        int bump = (x * 11) % 7;
                        if (bump < 2) c = Color.clear;
                    }

                    px[y * w + x] = c;
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            // pivot: 중앙 — 회전 시 좌우 대칭 유지
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32);
        }

        // ===== 전술맵 타일 (육각) =====

        /// <summary>육각 바닥 타일 (flat-top, 32x32) — 내부 채움 + 테두리</summary>
        public static Sprite CreateFloorTile()
        {
            int s = 32;
            var tex = new Texture2D(s, s);
            tex.filterMode = FilterMode.Point;
            var px = new Color[s * s];
            for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

            Color floor = new Color(0.18f, 0.2f, 0.16f);
            Color floorAlt = new Color(0.16f, 0.18f, 0.14f);
            Color edge = new Color(0.35f, 0.36f, 0.32f);

            float cx = (s - 1) * 0.5f;
            float cy = (s - 1) * 0.5f;
            // flat-top hex: 가로 반지름 = s/2, 세로 반지름 = s * sqrt(3)/4
            float rx = s * 0.5f;
            float ry = s * 0.433f; // sqrt(3)/4 ≈ 0.433

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    // 정규화 좌표
                    float dx = Mathf.Abs(x - cx) / rx;
                    float dy = Mathf.Abs(y - cy) / ry;
                    // flat-top hex inside test: dx <= 1 && dy <= 1 && (2*dx + dy) <= 2
                    bool inside = dx <= 1f && dy <= 1f && (2f * dx + dy) <= 2f;
                    if (!inside) continue;

                    // 테두리: 경계 근처
                    bool isEdge = dx > 0.92f || dy > 0.92f || (2f * dx + dy) > 1.85f;
                    if (isEdge) px[y * s + x] = edge;
                    else px[y * s + x] = ((x + y) % 2 == 0) ? floor : floorAlt;
                }
            }

            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }

        /// <summary>육각 엄폐물 타일 — 방호 방향 변(들)에 두꺼운 테두리</summary>
        /// <remarks>HexFacet 플래그로 지정된 변에만 벽이 그려짐. 플랫탑 hex 6변:
        /// N(상), NE(우상), SE(우하), S(하), SW(좌하), NW(좌상)</remarks>
        public static Sprite CreateCoverTile(CoverSize size, Crux.Grid.HexFacet facets)
        {
            int s = 48;
            var tex = new Texture2D(s, s);
            tex.filterMode = FilterMode.Point;
            var px = new Color[s * s];
            for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

            // 바닥
            Color floor = new Color(0.2f, 0.22f, 0.18f);
            Color edgeCol = new Color(0.35f, 0.36f, 0.32f);

            // 벽 색상/두께 (픽셀 단위)
            Color wall = size switch
            {
                CoverSize.Small  => new Color(0.65f, 0.58f, 0.42f),
                CoverSize.Medium => new Color(0.55f, 0.48f, 0.38f),
                CoverSize.Large  => new Color(0.45f, 0.4f, 0.32f),
                _ => new Color(0.55f, 0.48f, 0.38f)
            };
            Color wallDark = wall * 0.6f; wallDark.a = 1f;
            Color wallLight = Color.Lerp(wall, Color.white, 0.25f); wallLight.a = 1f;
            Color crack = wallDark * 0.7f; crack.a = 1f;

            // 육각 기하 — 픽셀 공간
            float cx = (s - 1) * 0.5f;
            float cy = (s - 1) * 0.5f;
            float radius = s * 0.5f - 1f;                    // 반지름 (픽셀)
            float apothem = radius * Mathf.Sqrt(3f) * 0.5f;  // 중심→변 수직거리

            // 벽 두께 — 반지름 대비 비율 × 픽셀 단위
            float wallPx = size switch
            {
                CoverSize.Small  => radius * 0.20f,
                CoverSize.Medium => radius * 0.28f,
                CoverSize.Large  => radius * 0.36f,
                _ => radius * 0.28f
            };

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;

                    // 6변에 대한 부호 거리: apothem - dot(p, n_facet)
                    // 0 = 변 위, 양수 = 내부, 음수 = 외부
                    float minDist = float.MaxValue;
                    int closestFacet = -1;
                    for (int f = 0; f < 6; f++)
                    {
                        float compassDeg = f * 60f;
                        float rad = compassDeg * Mathf.Deg2Rad;
                        float nFx = Mathf.Sin(rad); // compass → world x
                        float nFy = Mathf.Cos(rad); // compass → world y
                        float signed = apothem - (dx * nFx + dy * nFy);
                        if (signed < minDist)
                        {
                            minDist = signed;
                            closestFacet = f;
                        }
                    }

                    // hex 외부 (가장 가까운 변 거리가 음수 = 그 변 너머)
                    if (minDist < 0) continue;

                    // 기본 floor
                    px[y * s + x] = floor;

                    // 방호면 벽 체크
                    if (closestFacet >= 0)
                    {
                        var facetFlag = (Crux.Grid.HexFacet)(1 << closestFacet);
                        bool isProtected = (facets & facetFlag) != 0;
                        if (isProtected && minDist <= wallPx)
                        {
                            // 변에 가까울수록(minDist 작을수록) 진함, 안쪽은 밝음
                            float t = minDist / wallPx; // 0 = 변, 1 = 벽 안쪽 경계
                            Color c = Color.Lerp(wallDark, wallLight, t);
                            // 균열 패턴
                            if ((x * 7 + y * 13) % 11 == 0) c = crack;
                            px[y * s + x] = c;
                        }
                    }

                    // 최외곽 1픽셀 테두리 — 모든 변에 얇게
                    if (minDist <= 1.0f && px[y * s + x] == floor)
                        px[y * s + x] = edgeCol;
                }
            }

            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }
    }
}
