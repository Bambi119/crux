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

        // ===== 전술맵 타일 =====

        /// <summary>바닥 타일 (16x16) — 격자 무늬 + 지형 색상</summary>
        public static Sprite CreateFloorTile()
        {
            int s = 16;
            var tex = new Texture2D(s, s);
            tex.filterMode = FilterMode.Point;
            var px = new Color[s * s];

            Color floor = new Color(0.18f, 0.2f, 0.16f);       // 어두운 녹갈색
            Color floorAlt = new Color(0.16f, 0.18f, 0.14f);   // 약간 다른 톤
            Color edge = new Color(0.12f, 0.13f, 0.11f);       // 격자 테두리

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    if (x == 0 || y == 0)
                        px[y * s + x] = edge;
                    else if ((x + y) % 2 == 0)
                        px[y * s + x] = floor;
                    else
                        px[y * s + x] = floorAlt;
                }
            }

            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }

        /// <summary>엄폐물 테두리 타일 — 방호 방향 쪽에 벽 장식
        /// coverDir: 나침반 각도 (0=북에서 오는 공격을 막음 → 북쪽 테두리에 벽)
        /// size: Small/Medium/Large</summary>
        public static Sprite CreateCoverTile(CoverSize size, float coverDir)
        {
            int s = 16;
            var tex = new Texture2D(s, s);
            tex.filterMode = FilterMode.Point;
            var px = new Color[s * s];

            // 바닥 기본 (약간 밝은 톤 — 엄폐 셀 표시)
            Color floor = new Color(0.2f, 0.22f, 0.18f);
            for (int i = 0; i < px.Length; i++) px[i] = floor;

            // 벽 색상 — 크기별
            Color wall = size switch
            {
                CoverSize.Small  => new Color(0.5f, 0.45f, 0.35f),
                CoverSize.Medium => new Color(0.45f, 0.4f, 0.35f),
                CoverSize.Large  => new Color(0.38f, 0.35f, 0.3f),
                _ => new Color(0.45f, 0.4f, 0.35f)
            };
            Color wallDark = wall * 0.7f; wallDark.a = 1f;
            Color crack = wall * 0.55f; crack.a = 1f;

            // 벽 두께 — 크기별
            int thickness = size switch
            {
                CoverSize.Small => 2,
                CoverSize.Medium => 3,
                CoverSize.Large => 4,
                _ => 3
            };

            // 방호 방향 → 벽을 그릴 변(들)
            // coverDir=0° → 북쪽 변, 90° → 동쪽 변, 180° → 남쪽 변, 270° → 서쪽 변
            // Large(180°)는 전체 둘레의 절반을 커버하므로 3변
            float halfArc = size switch
            {
                CoverSize.Small => 45f,
                CoverSize.Medium => 67.5f,
                CoverSize.Large => 90f,
                _ => 67.5f
            };

            bool drawNorth = Mathf.Abs(Mathf.DeltaAngle(coverDir, 0f)) <= halfArc;
            bool drawEast  = Mathf.Abs(Mathf.DeltaAngle(coverDir, 90f)) <= halfArc;
            bool drawSouth = Mathf.Abs(Mathf.DeltaAngle(coverDir, 180f)) <= halfArc;
            bool drawWest  = Mathf.Abs(Mathf.DeltaAngle(coverDir, 270f)) <= halfArc;

            // 벽 그리기
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    bool isWall = false;

                    if (drawNorth && y >= s - thickness)            isWall = true;
                    if (drawSouth && y < thickness)                 isWall = true;
                    if (drawEast && x >= s - thickness)             isWall = true;
                    if (drawWest && x < thickness)                  isWall = true;

                    if (isWall)
                    {
                        Color c = wall;
                        // 균열 패턴
                        if ((x + y) % 7 == 0) c = crack;
                        // 내측 그림자
                        if (drawNorth && y == s - thickness) c = wallDark;
                        if (drawSouth && y == thickness - 1) c = wallDark;
                        if (drawEast && x == s - thickness) c = wallDark;
                        if (drawWest && x == thickness - 1) c = wallDark;

                        px[y * s + x] = c;
                    }
                }
            }

            // 모서리 강화 (벽이 만나는 곳)
            for (int i = 0; i < thickness; i++)
            {
                for (int j = 0; j < thickness; j++)
                {
                    if (drawNorth && drawWest) px[(s - 1 - i) * s + j] = wallDark;
                    if (drawNorth && drawEast) px[(s - 1 - i) * s + (s - 1 - j)] = wallDark;
                    if (drawSouth && drawWest) px[i * s + j] = wallDark;
                    if (drawSouth && drawEast) px[i * s + (s - 1 - j)] = wallDark;
                }
            }

            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }
    }
}
