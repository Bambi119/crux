using UnityEngine;

namespace Crux.Data
{
    /// <summary>HitEffects / MuzzleFlash VFX 렌더 상수 — SO로 외부화 (TD-05)</summary>
    [CreateAssetMenu(menuName = "Crux/Data/VfxRenderData", fileName = "VfxRenderData")]
    public class VfxRenderDataSO : ScriptableObject
    {
        // ─────────────────────────────────────────────
        //  Sorting Orders
        // ─────────────────────────────────────────────
        [Header("Sorting Orders")]
        public int sortSmoke        = 55;
        public int sortAfterGlow    = 56;
        public int sortDebrisLow    = 62;
        public int sortDebrisMid    = 64;
        public int sortSpark        = 67;
        public int sortHitAnim      = 68;
        public int sortStreakHit     = 73;
        public int sortFlash2       = 70;
        public int sortFlash1       = 71;
        public int sortFireball3    = 69;   // Impact outer
        public int sortFireball2    = 70;   // reuse flash2 slot — kept separate for clarity
        public int sortFireball1    = 71;
        public int sortFireballCore = 72;
        public int sortStreakExplosion = 74;
        public int sortExpFlash1    = 75;
        public int sortExpFlash2    = 76;   // AmmoRack core2

        // ─────────────────────────────────────────────
        //  Miss
        // ─────────────────────────────────────────────
        [Header("Miss")]
        public Color missDustColor1     = new Color(0.62f, 0.52f, 0.32f, 0.7f);
        public float missDustScale1     = 0.22f;
        public float missDustLife1      = 0.55f;
        public Color missDustColor2     = new Color(0.55f, 0.47f, 0.28f, 0.5f);
        public float missDustScale2     = 0.14f;
        public float missDustLife2      = 0.4f;
        public Color missChipColor      = new Color(0.6f, 0.5f, 0.3f, 0.85f);
        public float missChipScaleMin   = 0.04f;
        public float missChipScaleMax   = 0.07f;
        public float missChipSpeedMin   = 3f;
        public float missChipSpeedMax   = 7f;
        public float missChipLifeMin    = 0.3f;
        public float missChipLifeMax    = 0.5f;
        public float missChipDamping    = 4f;

        // ─────────────────────────────────────────────
        //  Ricochet
        // ─────────────────────────────────────────────
        [Header("Ricochet")]
        public Color ricoFlash1Color    = new Color(1f, 1f, 0.9f, 1f);
        public float ricoFlash1Scale    = 0.4f;
        public float ricoFlash1Life     = 0.04f;
        public Color ricoFlash2Color    = new Color(1f, 0.8f, 0.3f, 0.8f);
        public float ricoFlash2Scale    = 0.3f;
        public float ricoFlash2Life     = 0.12f;
        public int   ricoStreakBaseCount = 12;
        public float ricoStreakScaleY   = 0.05f;
        public float ricoStreakSpeedMin = 10f;
        public float ricoStreakSpeedMax = 22f;
        public float ricoStreakLenMin   = 0.18f;
        public float ricoStreakLenMax   = 0.45f;
        public float ricoStreakLifeMin  = 0.1f;
        public float ricoStreakLifeMax  = 0.25f;
        public float ricoStreakDamping  = 3f;
        public Color ricoStreakColorA   = new Color(1f, 0.95f, 0.6f, 0.95f);
        public Color ricoStreakColorB   = new Color(1f, 0.7f, 0.2f, 0.85f);
        public int   ricoSparkBaseCount = 14;
        public float ricoSparkSizeMin   = 0.025f;
        public float ricoSparkSizeMax   = 0.07f;
        public float ricoSparkSpeedMin  = 6f;
        public float ricoSparkSpeedMax  = 16f;
        public float ricoSparkLifeMin   = 0.12f;
        public float ricoSparkLifeMax   = 0.35f;
        public float ricoSparkDamping   = 2.5f;
        public Color ricoSparkColorWhite  = new Color(1f, 1f, 0.7f);
        public Color ricoSparkColorYellow = new Color(1f, 0.85f, 0.3f);
        public Color ricoSparkColorOrange = new Color(1f, 0.55f, 0.1f);
        public int   ricoEmberCount     = 8;
        public float ricoEmberSizeMin   = 0.02f;
        public float ricoEmberSizeMax   = 0.05f;
        public float ricoEmberSpeedMin  = 2f;
        public float ricoEmberSpeedMax  = 6f;
        public float ricoEmberLifeMin   = 0.25f;
        public float ricoEmberLifeMax   = 0.55f;
        public float ricoEmberDamping   = 4f;
        public Color ricoEmberColorA    = new Color(1f, 0.7f, 0.2f, 0.9f);
        public Color ricoEmberColorB    = new Color(0.9f, 0.4f, 0.1f, 0.7f);
        public int   ricoDebrisCount    = 4;
        public Color ricoDebrisColor    = new Color(0.45f, 0.4f, 0.35f);
        public float ricoDebrisSizeMin  = 0.04f;
        public float ricoDebrisSizeMax  = 0.07f;
        public float ricoDebrisSpeedMin = 3f;
        public float ricoDebrisSpeedMax = 8f;
        public float ricoDebrisLifeMin  = 0.4f;
        public float ricoDebrisLifeMax  = 0.8f;
        public float ricoDebrisDamping  = 5f;
        public int   ricoSmokeCount     = 3;
        public Color ricoSmokeColor     = new Color(0.25f, 0.23f, 0.2f, 0.35f);
        public float ricoSmokeSizeMin   = 0.1f;
        public float ricoSmokeSizeMax   = 0.2f;
        public float ricoSmokeLifeMin   = 0.4f;
        public float ricoSmokeLifeMax   = 0.8f;
        public float ricoSmokeDamping   = 1.5f;

        // ─────────────────────────────────────────────
        //  Impact (SpawnImpactLegacy)
        // ─────────────────────────────────────────────
        [Header("Impact")]
        public Color impactCoreColor    = new Color(1f, 1f, 1f, 1f);
        public float impactCoreScale    = 0.4f;
        public float impactCoreLife     = 0.05f;
        public Color impactGlow1Color   = new Color(1f, 1f, 0.85f, 1f);
        public float impactGlow1Scale   = 0.55f;
        public float impactGlow1Life    = 0.12f;
        public Color impactGlow2Color   = new Color(1f, 0.85f, 0.3f, 0.9f);
        public float impactGlow2Scale   = 0.7f;
        public float impactGlow2Life    = 0.25f;
        public Color impactOuterColor   = new Color(1f, 0.5f, 0.1f, 0.6f);
        public float impactOuterScale   = 0.9f;
        public float impactOuterLife    = 0.4f;
        public int   impactStreakBase    = 28;
        public float impactStreakScaleY = 0.06f;
        public float impactStreakLenMin = 0.25f;
        public float impactStreakLenMax = 0.9f;
        public float impactStreakSpeedMin = 10f;
        public float impactStreakSpeedMax = 24f;
        public float impactStreakLifeMin  = 0.12f;
        public float impactStreakLifeMax  = 0.35f;
        public float impactStreakDamping  = 2f;
        public Color impactStreakColorA   = new Color(1f, 0.97f, 0.75f, 0.95f);
        public Color impactStreakColorB   = new Color(1f, 0.85f, 0.35f, 0.85f);
        public Color impactStreakColorC   = new Color(1f, 0.6f, 0.15f, 0.75f);
        public int   impactDebrisBase    = 30;
        public float impactDebrisSizeMin = 0.015f;
        public float impactDebrisSizeMax = 0.05f;
        public float impactDebrisSpeedMin = 3f;
        public float impactDebrisSpeedMax = 18f;
        public float impactDebrisLifeMin  = 0.3f;
        public float impactDebrisLifeMax  = 0.9f;
        public float impactDebrisDamping  = 1.5f;
        public Color impactDebrisBlack    = new Color(0.1f, 0.08f, 0.06f);
        public Color impactDebrisBrown    = new Color(0.35f, 0.25f, 0.15f);
        public Color impactDebrisLight    = new Color(0.6f, 0.5f, 0.3f);
        public Color impactDebrisFire     = new Color(1f, 0.7f, 0.2f, 0.8f);
        public int   impactDustBase       = 6;
        public float impactDustSizeMin    = 0.2f;
        public float impactDustSizeMax    = 0.5f;
        public float impactDustSpeedMin   = 0.5f;
        public float impactDustSpeedMax   = 2.5f;
        public float impactDustLifeMin    = 0.6f;
        public float impactDustLifeMax    = 1.5f;
        public float impactDustDamping    = 0.8f;
        public Color impactDustBlack      = new Color(0.12f, 0.1f, 0.08f, 0.5f);
        public Color impactDustBrown      = new Color(0.3f, 0.25f, 0.2f, 0.4f);
        public Color impactDustLight      = new Color(0.45f, 0.4f, 0.35f, 0.3f);
        public Color impactAfterglowColor = new Color(1f, 0.4f, 0.1f, 0.4f);
        public float impactAfterglowScale = 0.5f;
        public float impactAfterglowLife  = 0.8f;

        // ─────────────────────────────────────────────
        //  Explosion (SpawnExplosion)
        // ─────────────────────────────────────────────
        [Header("Explosion")]
        public Color expFlashCoreColor  = new Color(1f, 1f, 0.95f, 1f);
        public float expFlashCoreScale  = 0.5f;
        public float expFlashCoreLife   = 0.05f;
        public Color expFire1Color      = new Color(1f, 0.92f, 0.5f, 1f);
        public float expFire1Scale      = 0.8f;
        public float expFire1Life       = 0.18f;
        public Color expFire2Color      = new Color(1f, 0.5f, 0.12f, 0.9f);
        public float expFire2Scale      = 1.1f;
        public float expFire2Life       = 0.38f;
        public Color expFire3Color      = new Color(0.7f, 0.18f, 0.04f, 0.7f);
        public float expFire3Scale      = 1.3f;
        public float expFire3Life       = 0.65f;
        public int   expStreakCount      = 20;
        public float expStreakLenMin     = 0.3f;
        public float expStreakLenMax     = 0.85f;
        public float expStreakScaleY     = 0.06f;
        public float expStreakSpeedMin   = 9f;
        public float expStreakSpeedMax   = 22f;
        public float expStreakLifeMin    = 0.14f;
        public float expStreakLifeMax    = 0.38f;
        public float expStreakDamping    = 2f;
        public Color expStreakColorA     = new Color(1f, 0.88f, 0.4f, 0.9f);
        public Color expStreakColorB     = new Color(1f, 0.5f, 0.1f, 0.85f);
        public int   expDebrisCount      = 20;
        public Color expDebrisBlack      = new Color(0.1f, 0.08f, 0.06f);
        public Color expDebrisMid        = new Color(0.32f, 0.25f, 0.17f);
        public Color expDebrisLight      = new Color(0.55f, 0.45f, 0.3f);
        public float expDebrisSizeMin    = 0.03f;
        public float expDebrisSizeMax    = 0.09f;
        public float expDebrisSpeedMin   = 4f;
        public float expDebrisSpeedMax   = 16f;
        public float expDebrisLifeMin    = 0.3f;
        public float expDebrisLifeMax    = 0.9f;
        public float expDebrisDamping    = 1.6f;
        public int   expSmokeCount       = 5;
        public Color expSmokeColor       = new Color(0.08f, 0.07f, 0.05f, 0.72f);
        public float expSmokeSizeMin     = 0.35f;
        public float expSmokeSizeMax     = 0.65f;
        public float expSmokeSmokeRadius = 0.22f;
        public float expSmokeSpeedXMin   = -0.5f;
        public float expSmokeSpeedXMax   = 0.5f;
        public float expSmokeSpeedYMin   = 1f;
        public float expSmokeSpeedYMax   = 2.5f;
        public float expSmokeLifeMin     = 1.0f;
        public float expSmokeLifeMax     = 2.0f;
        public float expSmokeDamping     = 0.9f;
        public Color expGlowColor        = new Color(1f, 0.35f, 0.05f, 0.4f);
        public float expGlowScale        = 0.7f;
        public float expGlowLife         = 1.0f;

        // ─────────────────────────────────────────────
        //  AmmoRack Explosion
        // ─────────────────────────────────────────────
        [Header("AmmoRack Explosion")]
        public Color ammoCore1Color     = new Color(1f, 1f, 0.98f, 1f);
        public float ammoCore1Scale     = 1.0f;
        public float ammoCore1Life      = 0.04f;
        public Color ammoCore2Color     = new Color(1f, 0.97f, 0.8f, 1f);
        public float ammoCore2Scale     = 1.6f;
        public float ammoCore2Life      = 0.07f;
        public int   ammoRingCount      = 4;
        public float ammoRingRadBase    = 0.3f;
        public float ammoRingRadStep    = 0.45f;
        public Color ammoRingColorBase  = new Color(1f, 0.92f, 0.65f, 0.7f);
        public float ammoRingLifeBase   = 0.1f;
        public float ammoRingLifeStep   = 0.04f;
        public Color ammoBall1Color     = new Color(1f, 0.9f, 0.45f, 1f);
        public float ammoBall1Scale     = 1.6f;
        public float ammoBall1Life      = 0.28f;
        public Color ammoBall2Color     = new Color(1f, 0.5f, 0.1f, 0.92f);
        public float ammoBall2Scale     = 2.2f;
        public float ammoBall2Life      = 0.55f;
        public Color ammoBall3Color     = new Color(0.65f, 0.15f, 0.04f, 0.75f);
        public float ammoBall3Scale     = 2.5f;
        public float ammoBall3Life      = 0.9f;
        public int   ammoStreakCount    = 36;
        public float ammoStreakLenMin   = 0.5f;
        public float ammoStreakLenMax   = 1.8f;
        public float ammoStreakScaleY   = 0.07f;
        public float ammoStreakSpeedMin = 16f;
        public float ammoStreakSpeedMax = 36f;
        public float ammoStreakLifeMin  = 0.18f;
        public float ammoStreakLifeMax  = 0.5f;
        public float ammoStreakDamping  = 1.5f;
        public Color ammoStreakColorA   = new Color(1f, 0.95f, 0.7f, 0.95f);
        public Color ammoStreakColorB   = new Color(1f, 0.65f, 0.18f, 0.9f);
        public Color ammoStreakColorC   = new Color(1f, 0.35f, 0.04f, 0.85f);
        public int   ammoDebrisCount    = 30;
        public float ammoDebrisSizeMin  = 0.05f;
        public float ammoDebrisSizeMax  = 0.14f;
        public float ammoDebrisSpeedMin = 7f;
        public float ammoDebrisSpeedMax = 24f;
        public float ammoDebrisLifeMin  = 0.5f;
        public float ammoDebrisLifeMax  = 1.3f;
        public float ammoDebrisDamping  = 1.2f;
        public Color ammoDebrisBlack    = new Color(0.08f, 0.06f, 0.04f);
        public Color ammoDebrisMid      = new Color(0.28f, 0.2f, 0.13f);
        public Color ammoDebrisLight    = new Color(0.5f, 0.4f, 0.28f);
        public Color ammoTurretColor    = new Color(0.22f, 0.2f, 0.17f);
        public float ammoTurretScale    = 0.22f;
        public float ammoTurretAngleMin = 20f;
        public float ammoTurretAngleMax = 160f;
        public float ammoTurretSpeedMin = 8f;
        public float ammoTurretSpeedMax = 16f;
        public float ammoTurretLifeMin  = 0.9f;
        public float ammoTurretLifeMax  = 1.6f;
        public float ammoTurretDamping  = 0.7f;
        public float ammoTurretAngVelMin = -280f;
        public float ammoTurretAngVelMax = 280f;
        public int   ammoSmokeCount     = 8;
        public Color ammoSmokeColor     = new Color(0.06f, 0.05f, 0.04f, 0.82f);
        public float ammoSmokeSizeMin   = 0.5f;
        public float ammoSmokeSizeMax   = 1.1f;
        public float ammoSmokeRadius    = 0.32f;
        public float ammoSmokeSpeedXMin = -0.8f;
        public float ammoSmokeSpeedXMax = 0.8f;
        public float ammoSmokeSpeedYMin = 1.8f;
        public float ammoSmokeSpeedYMax = 4.2f;
        public float ammoSmokeLifeMin   = 1.5f;
        public float ammoSmokeLifeMax   = 3.2f;
        public float ammoSmokeDamping   = 0.5f;
        public int   ammoEmberCount     = 15;
        public Color ammoEmberColorA    = new Color(1f, 0.6f, 0.1f, 0.8f);
        public Color ammoEmberColorB    = new Color(0.8f, 0.25f, 0.05f, 0.6f);
        public float ammoEmberSizeMin   = 0.03f;
        public float ammoEmberSizeMax   = 0.07f;
        public float ammoEmberSpeedMin  = 3f;
        public float ammoEmberSpeedMax  = 11f;
        public float ammoEmberLifeMin   = 0.5f;
        public float ammoEmberLifeMax   = 1.3f;
        public float ammoEmberDamping   = 2f;
        public float ammoEmberRadius    = 0.5f;
        public Color ammoAfterglowColor = new Color(1f, 0.28f, 0.04f, 0.5f);
        public float ammoAfterglowScale = 1.4f;
        public float ammoAfterglowLife  = 1.6f;

        // ─────────────────────────────────────────────
        //  CoverHit
        // ─────────────────────────────────────────────
        [Header("CoverHit")]
        public Color coverFlash1Color   = new Color(0.95f, 0.92f, 0.85f, 1f);
        public float coverFlash1Scale   = 0.4f;
        public float coverFlash1Life    = 0.04f;
        public Color coverFlash2Color   = new Color(0.75f, 0.7f, 0.6f, 0.7f);
        public float coverFlash2Scale   = 0.55f;
        public float coverFlash2Life    = 0.15f;
        public int   coverStreakCount   = 12;
        public float coverStreakScaleY  = 0.09f;
        public float coverStreakLenMin  = 0.12f;
        public float coverStreakLenMax  = 0.38f;
        public float coverStreakSpeedMin = 5f;
        public float coverStreakSpeedMax = 13f;
        public float coverStreakLifeMin  = 0.15f;
        public float coverStreakLifeMax  = 0.32f;
        public float coverStreakDamping  = 4.5f;
        public Color coverStreakColorA   = new Color(0.88f, 0.85f, 0.78f, 0.9f);
        public Color coverStreakColorB   = new Color(0.62f, 0.58f, 0.5f, 0.85f);
        public Color coverStreakColorC   = new Color(0.42f, 0.38f, 0.32f, 0.8f);
        public int   coverChipCount1    = 18;
        public float coverChipSizeMin   = 0.03f;
        public float coverChipSizeMax   = 0.08f;
        public float coverChipSpeedMin  = 5f;
        public float coverChipSpeedMax  = 14f;
        public float coverChipLifeMin   = 0.15f;
        public float coverChipLifeMax   = 0.4f;
        public float coverChipDamping   = 2.8f;
        public Color coverChipColorA    = new Color(0.82f, 0.78f, 0.7f);
        public Color coverChipColorB    = new Color(0.58f, 0.54f, 0.46f);
        public Color coverChipColorC    = new Color(0.36f, 0.32f, 0.27f);
        public int   coverEmberCount    = 8;
        public float coverEmberSizeMin  = 0.02f;
        public float coverEmberSizeMax  = 0.06f;
        public float coverEmberSpeedMin = 1.5f;
        public float coverEmberSpeedMax = 5f;
        public float coverEmberLifeMin  = 0.3f;
        public float coverEmberLifeMax  = 0.65f;
        public float coverEmberDamping  = 5f;
        public Color coverEmberColorA   = new Color(0.7f, 0.66f, 0.58f, 0.9f);
        public Color coverEmberColorB   = new Color(0.48f, 0.44f, 0.38f, 0.75f);
        public int   coverChunkCount    = 4;
        public Color coverChunkColor    = new Color(0.65f, 0.62f, 0.55f);
        public float coverChunkSizeMin  = 0.08f;
        public float coverChunkSizeMax  = 0.14f;
        public float coverChunkSpeedMin = 2f;
        public float coverChunkSpeedMax = 6f;
        public float coverChunkLifeMin  = 0.45f;
        public float coverChunkLifeMax  = 0.9f;
        public float coverChunkDamping  = 6f;
        public int   coverDustCount     = 3;
        public Color coverDustColor     = new Color(0.72f, 0.68f, 0.6f, 0.45f);
        public float coverDustSizeMin   = 0.18f;
        public float coverDustSizeMax   = 0.35f;
        public float coverDustLifeMin   = 0.55f;
        public float coverDustLifeMax   = 1.1f;
        public float coverDustDamping   = 1.3f;
        public float coverDustSpeedMin  = 0.5f;
        public float coverDustSpeedMax  = 1.5f;

        // ─────────────────────────────────────────────
        //  MuzzleFlash
        // ─────────────────────────────────────────────
        [Header("MuzzleFlash")]
        public Color muzzleFlashColor   = new Color(1f, 0.9f, 0.3f, 1f);
        public float muzzleFlashScale   = 0.5f;
        public float muzzleFlashOffset  = 0.1f;
        public float muzzleFlashLife    = 0.06f;
        public int   muzzleFlashSort    = 70;
        public Color muzzleFireballColor = new Color(1f, 0.6f, 0.15f, 0.9f);
        public float muzzleFireballScale = 0.35f;
        public float muzzleFireballOffset = 0.2f;
        public float muzzleFireballLife  = 0.15f;
        public int   muzzleFireballSort  = 68;
        public int   muzzleFlameCount   = 4;
        public Color muzzleFlameColor   = new Color(1f, 0.5f, 0.1f, 0.8f);
        public float muzzleFlameSizeMin = 0.06f;
        public float muzzleFlameSizeMax = 0.12f;
        public float muzzleFlameSpeedMin = 2f;
        public float muzzleFlameSpeedMax = 5f;
        public float muzzleFlameLife    = 0.2f;
        public float muzzleFlameDamping = 5f;
        public int   muzzleFlameSort    = 65;
        public int   muzzleSmokeCount   = 3;
        public float muzzleSmokeGrayMin = 0.3f;
        public float muzzleSmokeGrayMax = 0.5f;
        public float muzzleSmokeAlpha   = 0.35f;
        public float muzzleSmokeSizeMin = 0.15f;
        public float muzzleSmokeSizeMax = 0.3f;
        public float muzzleSmokeLifeMin = 0.5f;
        public float muzzleSmokeLifeMax = 1.0f;
        public float muzzleSmokeDamping = 1.5f;
        public int   muzzleSmokeSort    = 53;
        public float muzzleSmokeRadius  = 0.08f;
        public float muzzleSmokeSpeedMainMin = 0.3f;
        public float muzzleSmokeSpeedMainMax = 1.5f;
        public float muzzleSmokeSpeedSideMax = 0.4f;
    }
}
