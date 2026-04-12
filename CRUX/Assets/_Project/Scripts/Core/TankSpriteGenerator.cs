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

        /// <summary>엄폐물 스프라이트 (12x12) — 연출 씬용 (기존 호환)</summary>
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
            int s = 32;
            var tex = new Texture2D(s, s);
            tex.filterMode = FilterMode.Point;
            var px = new Color[s * s];
            for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

            // 바닥
            Color floor = new Color(0.2f, 0.22f, 0.18f);
            Color edge = new Color(0.35f, 0.36f, 0.32f);

            // 벽 색상/두께
            Color wall = size switch
            {
                CoverSize.Small  => new Color(0.55f, 0.5f, 0.4f),
                CoverSize.Medium => new Color(0.48f, 0.43f, 0.37f),
                CoverSize.Large  => new Color(0.4f, 0.36f, 0.32f),
                _ => new Color(0.48f, 0.43f, 0.37f)
            };
            Color wallDark = wall * 0.65f; wallDark.a = 1f;

            float wallThickness = size switch
            {
                CoverSize.Small => 0.12f,
                CoverSize.Medium => 0.18f,
                CoverSize.Large => 0.24f,
                _ => 0.18f
            };

            float cx = (s - 1) * 0.5f;
            float cy = (s - 1) * 0.5f;
            float rx = s * 0.5f;
            float ry = s * 0.433f;

            // 각 픽셀에 대해: 1) hex 내부인가 2) 가장 가까운 변이 어느 facet인가
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float nx = (x - cx) / rx;
                    float ny = (y - cy) / ry;
                    float adx = Mathf.Abs(nx);
                    float ady = Mathf.Abs(ny);
                    bool inside = adx <= 1f && ady <= 1f && (2f * adx + ady) <= 2f;
                    if (!inside) continue;

                    // 내부 기본 색
                    px[y * s + x] = floor;

                    // 각 변까지의 거리 계산 (플랫탑 hex 6변의 법선 방향 dot)
                    // 변 법선 나침반: N=0°, NE=60°, SE=120°, S=180°, SW=240°, NW=300°
                    float px_world = (x - cx) / rx; // 정규화된 x (-1~1)
                    float py_world = (y - cy) / ry; // 정규화된 y (-1~1)
                    // y가 Unity 기준(+y=위)과 일치하려면 반전 필요 — 텍스처 y=0은 하단
                    // 여기서는 +y가 위라고 가정 (텍스처 위쪽 = 북쪽)

                    // 중심에서 외곽까지 비율 (hex 내부에서 0~1)
                    // 각 facet까지 거리를 구하고 가장 가까운 것이 내가 속한 변
                    float closest = float.MaxValue;
                    int closestFacet = -1;
                    for (int f = 0; f < 6; f++)
                    {
                        float compassDeg = f * 60f;
                        float rad = compassDeg * Mathf.Deg2Rad;
                        // facet 법선 방향 (월드)
                        float nFx = Mathf.Sin(rad);
                        float nFy = Mathf.Cos(rad);
                        // 해당 변까지 거리 = 1 - dot(px, n_facet)
                        float dot = px_world * nFx + py_world * nFy;
                        float distToEdge = 1f - dot;
                        if (distToEdge < closest)
                        {
                            closest = distToEdge;
                            closestFacet = f;
                        }
                    }

                    // 이 픽셀이 속한 변이 방호면이면 벽 색상
                    if (closestFacet >= 0)
                    {
                        var facetFlag = (Crux.Grid.HexFacet)(1 << closestFacet);
                        if ((facets & facetFlag) != 0 && closest <= wallThickness)
                        {
                            // 외곽 쪽은 진한 벽, 안쪽은 밝은 벽
                            px[y * s + x] = (closest <= wallThickness * 0.5f) ? wallDark : wall;
                        }
                    }

                    // 최외곽 테두리 — 약한 edge
                    bool atBoundary = adx > 0.92f || ady > 0.92f || (2f * adx + ady) > 1.85f;
                    if (atBoundary && px[y * s + x] == floor)
                        px[y * s + x] = edge;
                }
            }

            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }
    }
}
