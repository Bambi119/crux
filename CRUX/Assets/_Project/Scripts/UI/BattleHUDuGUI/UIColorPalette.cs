namespace Crux.UI
{
    using UnityEngine;

    /// <summary>
    /// Stitch TCI palette — uGUI BattleHUD 색상 상수 정의.
    /// 모든 색상은 HEX → RGB(0~1) 정규화 후 소수점 6자리로 수렴.
    /// </summary>
    public static class UIColorPalette
    {
        // ─────────────────────────────────────────────────────────────
        // Surface (배경·기본 톤)
        // ─────────────────────────────────────────────────────────────
        public static readonly Color Surface = new Color(0.062745f, 0.078431f, 0.101961f, 1f); // #10141A
        public static readonly Color SurfaceContainer = new Color(0.109804f, 0.125490f, 0.149020f, 1f); // #1C2026
        public static readonly Color SurfaceContainerHigh = new Color(0.149020f, 0.164706f, 0.192157f, 1f); // #262A31
        public static readonly Color SurfaceContainerHighest = new Color(0.192157f, 0.207843f, 0.235294f, 1f); // #31353C
        public static readonly Color SurfaceContainerLow = new Color(0.094118f, 0.109804f, 0.133333f, 1f); // #181C22
        public static readonly Color SurfaceContainerLowest = new Color(0.039216f, 0.054902f, 0.078431f, 1f); // #0A0E14
        public static readonly Color SurfaceBright = new Color(0.207843f, 0.223529f, 0.250980f, 1f); // #353940
        public static readonly Color SurfaceDim = new Color(0.062745f, 0.078431f, 0.101961f, 1f); // #10141A
        public static readonly Color SurfaceVariant = new Color(0.192157f, 0.207843f, 0.235294f, 1f); // #31353C

        // ─────────────────────────────────────────────────────────────
        // Primary (Amber — 주요 강조색·HP·선택)
        // ─────────────────────────────────────────────────────────────
        public static readonly Color Primary = new Color(1f, 0.756863f, 0.454902f, 1f); // #FFC174
        public static readonly Color PrimaryContainer = new Color(0.960784f, 0.619608f, 0.058824f, 1f); // #F59E0B
        public static readonly Color PrimaryFixed = new Color(1f, 0.866667f, 0.721569f, 1f); // #FFDDB8
        public static readonly Color PrimaryFixedDim = new Color(1f, 0.725490f, 0.372549f, 1f); // #FFB95F
        public static readonly Color OnPrimary = new Color(0.278431f, 0.164706f, 0f, 1f); // #472A00
        public static readonly Color OnPrimaryContainer = new Color(0.380392f, 0.231373f, 0f, 1f); // #613B00

        // ─────────────────────────────────────────────────────────────
        // Secondary (Green — 부가 강조·AP·회피)
        // ─────────────────────────────────────────────────────────────
        public static readonly Color Secondary = new Color(0.290196f, 0.882353f, 0.462745f, 1f); // #4AE176
        public static readonly Color SecondaryContainer = new Color(0f, 0.725490f, 0.329412f, 1f); // #00B954
        public static readonly Color OnSecondary = new Color(0f, 0.223529f, 0.082353f, 1f); // #003915
        public static readonly Color OnSecondaryContainer = new Color(0f, 0.254902f, 0.098039f, 1f); // #004119

        // ─────────────────────────────────────────────────────────────
        // Tertiary (Red — 위험·피해·경고)
        // ─────────────────────────────────────────────────────────────
        public static readonly Color Tertiary = new Color(1f, 0.737255f, 0.717647f, 1f); // #FFBCB7
        public static readonly Color TertiaryContainer = new Color(1f, 0.576471f, 0.549020f, 1f); // #FF938C
        public static readonly Color OnTertiary = new Color(0.407843f, 0f, 0.039216f, 1f); // #68000A
        public static readonly Color OnTertiaryContainer = new Color(0.556863f, 0f, 0.070588f, 1f); // #8D0012

        // ─────────────────────────────────────────────────────────────
        // Text & Surface Interaction
        // ─────────────────────────────────────────────────────────────
        public static readonly Color OnSurface = new Color(0.874510f, 0.882353f, 0.921569f, 1f); // #DFE2EB
        public static readonly Color OnSurfaceVariant = new Color(0.847059f, 0.764706f, 0.678431f, 1f); // #D8C3AD
        public static readonly Color OnBackground = new Color(0.874510f, 0.882353f, 0.921569f, 1f); // #DFE2EB

        // ─────────────────────────────────────────────────────────────
        // Outline & Border
        // ─────────────────────────────────────────────────────────────
        public static readonly Color Outline = new Color(0.627451f, 0.556863f, 0.478431f, 1f); // #A08E7A
        public static readonly Color OutlineVariant = new Color(0.325490f, 0.266667f, 0.262745f, 1f); // #534434

        // ─────────────────────────────────────────────────────────────
        // Error (시스템 에러·불가능 상태)
        // ─────────────────────────────────────────────────────────────
        public static readonly Color Error = new Color(1f, 0.705882f, 0.670588f, 1f); // #FFB4AB
        public static readonly Color ErrorContainer = new Color(0.576471f, 0f, 0.039216f, 1f); // #93000A

        // ─────────────────────────────────────────────────────────────
        // Helper: Glass Background (HUD 패널 반투명 배경)
        // ─────────────────────────────────────────────────────────────
        public static readonly Color GlassBackground = new Color(0.109804f, 0.125490f, 0.149020f, 0.85f); // SurfaceContainer + 85% alpha
    }
}
