using UnityEngine;
using Crux.Grid;
using Crux.Unit;
using Crux.Data;
using Crux.Combat;
using Crux.Core;

namespace Crux.UI
{
    /// <summary>사격 프리뷰 패널 전담 — BattleHUD에서 위임받아 렌더링</summary>
    public class BattleHUDFirePreview
    {
        private BattleHUD hud;
        private Crux.Core.BattleController controller;

        // FirePreview 구조체
        private struct FirePreview
        {
            public int distance;
            public float baseHit;      // 거리 패널티 적용 후
            public float coverPenalty; // 엄폐에 의한 차감
            public float smokePenalty; // 연막에 의한 차감
            public float moraleBonus;  // 공격자 사기 명중 보정 (-0.15 ~ +0.05)
            public float finalHit;
            public HitZone hitZone;
            public float baseArmor;
            public float impactAngle;
            public float effectiveArmor;
            public float penetration;
            public ShotOutcome outcome;
            public float expectedDamagePerShot; // 판정 반영 데미지 (명중 시)
            public int shotsPerAction;           // 주포 1, 버스트 N
            public float totalExpected;          // shotsPerAction × finalHit × damagePerShot
            public bool coveredFromThisAngle;    // 현재 공격각에서 엄폐 유효
            public bool isMG;
        }

        public BattleHUDFirePreview(BattleHUD hud, Crux.Core.BattleController controller)
        {
            this.hud = hud;
            this.controller = controller;
        }

        /// <summary>사격 프리뷰 패널 렌더 진입점 (메인 Draw에서 호출)</summary>
        public void Draw(GridTankUnit target, WeaponType weapon)
        {
            DrawFireTargetPreview(target, weapon);
        }

        /// <summary>선택 무기 기준 사격 결과 기대값 계산</summary>
        private FirePreview ComputeFirePreview(GridTankUnit attacker, GridTankUnit target, WeaponType weapon)
        {
            var p = new FirePreview();
            var grid = controller.Grid;
            p.distance = grid.GetDistance(attacker.GridPosition, target.GridPosition);

            // 명중률 분해
            float chance = controller.CalculateHitChance(p.distance, target);
            chance -= attacker.Modules.GetAccuracyPenalty();

            // 지형 고도 차 (공격자 > 목표면 보너스)
            var aCell = grid.GetCell(attacker.GridPosition);
            var tCell = grid.GetCell(target.GridPosition);
            if (aCell != null && tCell != null)
            {
                int elevDelta = Crux.Core.TerrainData.Elevation(aCell.Terrain)
                              - Crux.Core.TerrainData.Elevation(tCell.Terrain);
                if (elevDelta > 0) chance += elevDelta * 0.05f;
            }
            p.baseHit = Mathf.Clamp01(chance);

            // 사기 보정 (P3-c) — 공격자 Band 기반 AimModifier
            p.moraleBonus = 0f;
            var atkCrew = attacker.Crew;
            if (atkCrew != null)
            {
                p.moraleBonus = MoraleSystem.AimModifier(atkCrew.Band) * 0.01f;
                p.baseHit = Mathf.Clamp01(p.baseHit + p.moraleBonus);
            }

            // 엄폐 보정 — 엄폐물 + 지형 고유 엄폐 합산
            p.coverPenalty = 0f;
            if (tCell != null && tCell.HasCover && tCell.Cover != null && !tCell.Cover.IsDestroyed)
            {
                var atkDir = HexCoord.AttackDir(attacker.GridPosition, target.GridPosition, GameConstants.CellSize);
                if (tCell.Cover.IsCovered(atkDir))
                {
                    p.coveredFromThisAngle = true;
                    p.coverPenalty = tCell.Cover.CoverRate * 0.3f;
                }
            }
            if (tCell != null)
            {
                float intrinsic = Crux.Core.TerrainData.IntrinsicCoverRate(tCell.Terrain);
                if (intrinsic > 0f) p.coverPenalty += intrinsic * 0.3f;
            }

            // 은엄폐 (수풀·파편)
            int concealmentPct = tCell != null ? Crux.Core.TerrainData.Concealment(tCell.Terrain) : 0;
            float concealmentPenalty = concealmentPct * 0.01f;

            // 연막 보정
            p.smokePenalty = (tCell != null && tCell.HasSmoke) ? 0.4f : 0f;
            // 은엄폐를 연막 페널티에 합산 (별도 필드 없이 기존 구조 유지)
            p.smokePenalty += concealmentPenalty;

            // 기총 기본 명중률 보정
            if (weapon == WeaponType.CoaxialMG && controller.CoaxialMGData != null)
            {
                p.baseHit = Mathf.Clamp01(p.baseHit + controller.CoaxialMGData.accuracyModifier
                                         - attacker.Modules.GetMGAccuracyPenalty());
            }
            else if (weapon == WeaponType.MountedMG && controller.MountedMGData != null)
            {
                p.baseHit = Mathf.Clamp01(p.baseHit + controller.MountedMGData.accuracyModifier
                                         - attacker.Modules.GetMGAccuracyPenalty());
            }

            p.finalHit = Mathf.Clamp01(p.baseHit - p.coverPenalty - p.smokePenalty);

            // 피격 위치·장갑 — 현재 위치 기준
            p.hitZone = PenetrationCalculator.DetermineHitZone(
                attacker.transform.position, target.transform.position, target.HullAngle);
            p.baseArmor = PenetrationCalculator.GetBaseArmor(target.Data.armor, p.hitZone);
            p.impactAngle = PenetrationCalculator.CalculateImpactAngleFromPositions(
                attacker.transform.position, target.transform.position, target.HullAngle, p.hitZone);
            p.effectiveArmor = PenetrationCalculator.CalculateEffectiveArmor(p.baseArmor, p.impactAngle);

            // 관통력 / 데미지 — 무기에 따라
            float basePenetration;
            float baseDamage;
            if (weapon == WeaponType.MainGun)
            {
                basePenetration = attacker.currentAmmo != null ? attacker.currentAmmo.penetration : 100f;
                baseDamage = attacker.currentAmmo != null ? attacker.currentAmmo.damage : 10f;
                p.shotsPerAction = 1;
                p.isMG = false;

                // 거리 감쇠
                if (attacker.currentAmmo != null && attacker.currentAmmo.penetrationDropPerCell > 0)
                    basePenetration = Mathf.Max(1f, basePenetration - attacker.currentAmmo.penetrationDropPerCell * p.distance);
            }
            else
            {
                var mg = weapon == WeaponType.CoaxialMG ? controller.CoaxialMGData : controller.MountedMGData;
                basePenetration = mg != null ? mg.penetration : 15f;
                baseDamage = mg != null ? mg.damagePerShot : 2f;
                p.shotsPerAction = mg != null
                    ? Mathf.Max(1, mg.burstCount - attacker.Modules.GetBurstPenalty())
                    : 1;
                p.isMG = true;
            }

            p.penetration = basePenetration;

            // 판정 예측 — 확률 경계는 기대값으로 대체 (ratio 기반 결정론 표기)
            p.outcome = PredictOutcome(basePenetration, p.effectiveArmor);

            // 데미지 계산 (판정별)
            float outcomeMult = p.outcome switch
            {
                ShotOutcome.Penetration => p.isMG ? 2f : 2.5f,
                ShotOutcome.Hit => 1f,
                ShotOutcome.Ricochet => 0.03f,
                _ => 0f
            };
            p.expectedDamagePerShot = baseDamage * outcomeMult;
            p.totalExpected = p.shotsPerAction * p.finalHit * p.expectedDamagePerShot;
            return p;
        }

        /// <summary>결정론적 판정 예측 — JudgePenetration의 확률 구간을 단일값으로</summary>
        private static ShotOutcome PredictOutcome(float penetration, float effectiveArmor)
        {
            if (effectiveArmor >= float.MaxValue) return ShotOutcome.Ricochet;
            float ratio = penetration / effectiveArmor;
            if (ratio > 1.2f) return ShotOutcome.Penetration;
            if (ratio > 0.8f) return ShotOutcome.Hit;
            return ShotOutcome.Ricochet;
        }

        /// <summary>사격 프리뷰 패널 렌더 (154 LOC → 분할)</summary>
        private void DrawFireTargetPreview(GridTankUnit target, WeaponType weapon)
        {
            if (target == null || target.IsDestroyed || controller.SelectedUnit == null) return;

            var p = ComputeFirePreview(controller.SelectedUnit, target, weapon);

            float w = 360, h = 260;
            float x = hud.ScaledW - w - 10;
            float y = 55;
            hud.DrawBox(new Rect(x, y, w, h));

            var style = hud.GetLabelStyleUI();
            style.fontSize = 17;
            float cx = x + 10;
            float cy = y + 6;
            float lineH = 20f;
            float innerW = w - 20;

            // 제목
            DrawFirePreviewHeader(cx, cy, ref style, target);
            cy += 24;

            // HP + 구분선
            DrawFirePreviewHP(cx, cy, ref style, target, p);
            cy += lineH;

            cy = DrawFirePreviewSeparator(cx, cy, ref style, innerW);

            // 거리
            DrawFirePreviewDistance(cx, cy, ref style, p);
            cy += lineH;

            // 명중률 + 분해
            cy = DrawFirePreviewHitChance(cx, cy, ref style, innerW, p);

            // 피격 위치·장갑
            DrawFirePreviewZone(cx, cy, ref style, innerW, p);
            cy += lineH;

            // 관통력 vs 장갑 + 판정
            cy = DrawFirePreviewOutcome(cx, cy, ref style, innerW, p);

            // 예상 피해
            DrawFirePreviewDamage(cx, cy, ref style, innerW, p);
            cy += lineH;

            // 사격 후 예상 HP
            DrawFirePreviewAfterHP(cx, cy, ref style, innerW, target, p);
            cy += lineH;

            // 엄폐
            DrawFirePreviewCover(cx, cy, ref style, innerW, target, p);
        }

        /// <summary>프리뷰 제목 (대상명·분류)</summary>
        private void DrawFirePreviewHeader(float cx, float cy, ref GUIStyle style, GridTankUnit target)
        {
            var titleStyle = new GUIStyle(style);
            titleStyle.fontSize = 19;
            titleStyle.normal.textColor = new Color(1f, 0.9f, 0.4f);
            string cls = BattleHUD.GetHullClassLabelStatic(target.Data != null ? target.Data.hullClass : HullClass.Assault);
            GUI.Label(new Rect(cx, cy, 340, 22),
                $"대상: {target.Data?.tankName}  [{cls}]", titleStyle);
        }

        /// <summary>프리뷰 HP 표시</summary>
        private void DrawFirePreviewHP(float cx, float cy, ref GUIStyle style, GridTankUnit target, FirePreview p)
        {
            float hpRatio = target.Data != null && target.Data.maxHP > 0
                            ? target.CurrentHP / target.Data.maxHP : 0f;
            var hpColor = hpRatio > 0.6f ? new Color(0.4f, 1f, 0.5f)
                       : hpRatio > 0.3f ? new Color(1f, 0.9f, 0.2f)
                                        : new Color(1f, 0.3f, 0.2f);
            var hpStyle = new GUIStyle(style);
            hpStyle.normal.textColor = hpColor;
            GUI.Label(new Rect(cx, cy, 340, 20),
                $"HP  {target.CurrentHP:F0}/{target.Data?.maxHP}", hpStyle);
        }

        /// <summary>프리뷰 구분선</summary>
        private float DrawFirePreviewSeparator(float cx, float cy, ref GUIStyle style, float innerW)
        {
            var sepStyle = new GUIStyle(style);
            sepStyle.normal.textColor = new Color(0.6f, 0.6f, 0.7f);
            GUI.Label(new Rect(cx, cy, innerW, 20), "────────────────────────", sepStyle);
            return cy + 20;
        }

        /// <summary>프리뷰 거리</summary>
        private void DrawFirePreviewDistance(float cx, float cy, ref GUIStyle style, FirePreview p)
        {
            GUI.Label(new Rect(cx, cy, 340, 20), $"거리  {p.distance}셀", style);
        }

        /// <summary>프리뷰 명중률 + 분해</summary>
        private float DrawFirePreviewHitChance(float cx, float cy, ref GUIStyle style, float innerW, FirePreview p)
        {
            // 명중률 표시
            var hitStyle = new GUIStyle(style);
            hitStyle.fontSize = 18;
            hitStyle.normal.textColor = new Color(1f, 0.95f, 0.4f);
            GUI.Label(new Rect(cx, cy, innerW, 20),
                $"명중률  {p.finalHit:P0}", hitStyle);
            cy += 20;

            // 분해 정보
            var breakStyle = new GUIStyle(style);
            breakStyle.fontSize = 14;
            breakStyle.normal.textColor = new Color(0.75f, 0.75f, 0.8f);
            string brk = $"   기본 {p.baseHit:P0}";
            if (p.moraleBonus != 0f)
            {
                if (p.moraleBonus > 0)
                {
                    breakStyle.normal.textColor = new Color(0.4f, 1f, 0.5f);
                    brk = $"   기본 {(p.baseHit - p.moraleBonus):P0}  +사기 {(p.moraleBonus * 100f):F0}%";
                }
                else
                {
                    breakStyle.normal.textColor = new Color(1f, 0.4f, 0.3f);
                    brk = $"   기본 {(p.baseHit - p.moraleBonus):P0}  −사기 {(Mathf.Abs(p.moraleBonus) * 100f):F0}%";
                }
            }
            else
            {
                breakStyle.normal.textColor = new Color(0.75f, 0.75f, 0.8f);
            }
            if (p.coverPenalty > 0) brk += $"  −엄폐 {(p.coverPenalty * 100f):F0}%";
            if (p.smokePenalty > 0) brk += $"  −연막 {(p.smokePenalty * 100f):F0}%";
            GUI.Label(new Rect(cx, cy, innerW, 18), brk, breakStyle);
            return cy + 18;
        }

        /// <summary>프리뷰 피격 위치·장갑</summary>
        private void DrawFirePreviewZone(float cx, float cy, ref GUIStyle style, float innerW, FirePreview p)
        {
            string zoneLabel = p.hitZone switch
            {
                HitZone.Front => "전면",
                HitZone.FrontRight => "우전",
                HitZone.RearRight => "우후",
                HitZone.Rear => "후면",
                HitZone.RearLeft => "좌후",
                HitZone.FrontLeft => "좌전",
                HitZone.Turret => "포탑",
                _ => ""
            };
            GUI.Label(new Rect(cx, cy, innerW, 20),
                $"피격  {zoneLabel}  장갑 {p.baseArmor:F0}mm (유효 {p.effectiveArmor:F0}mm)", style);
        }

        /// <summary>프리뷰 관통력 vs 장갑 + 판정</summary>
        private float DrawFirePreviewOutcome(float cx, float cy, ref GUIStyle style, float innerW, FirePreview p)
        {
            string outcomeLabel; Color outcomeColor;
            switch (p.outcome)
            {
                case ShotOutcome.Penetration:
                    outcomeLabel = "관통"; outcomeColor = new Color(0.4f, 1f, 0.5f); break;
                case ShotOutcome.Hit:
                    outcomeLabel = "명중"; outcomeColor = new Color(1f, 0.95f, 0.3f); break;
                case ShotOutcome.Ricochet:
                    outcomeLabel = "도탄"; outcomeColor = new Color(1f, 0.4f, 0.3f); break;
                default:
                    outcomeLabel = "실패"; outcomeColor = Color.gray; break;
            }
            var resStyle = new GUIStyle(style);
            resStyle.normal.textColor = outcomeColor;
            resStyle.fontSize = 18;
            GUI.Label(new Rect(cx, cy, innerW, 20),
                $"관통력 {p.penetration:F0}mm  →  {outcomeLabel}", resStyle);
            return cy + 20;
        }

        /// <summary>프리뷰 예상 피해</summary>
        private void DrawFirePreviewDamage(float cx, float cy, ref GUIStyle style, float innerW, FirePreview p)
        {
            string dmgLine;
            if (p.isMG)
                dmgLine = $"예상 피해  {p.totalExpected:F0}  ({p.shotsPerAction}발 기준)";
            else
                dmgLine = $"예상 피해  {p.expectedDamagePerShot:F0}  (명중 시)";
            var dmgStyle = new GUIStyle(style);
            dmgStyle.fontSize = 18;
            GUI.Label(new Rect(cx, cy, innerW, 20), dmgLine, dmgStyle);
        }

        /// <summary>프리뷰 사격 후 예상 HP</summary>
        private void DrawFirePreviewAfterHP(float cx, float cy, ref GUIStyle style, float innerW, GridTankUnit target, FirePreview p)
        {
            float remainHP = Mathf.Max(0f, target.CurrentHP - p.totalExpected);
            bool kill = remainHP <= 0f && p.finalHit > 0.01f;
            var afterStyle = new GUIStyle(style);
            afterStyle.fontSize = 16;
            afterStyle.normal.textColor = kill ? new Color(1f, 0.3f, 0.3f)
                                                : (remainHP / Mathf.Max(1f, target.Data.maxHP) < 0.5f
                                                    ? new Color(1f, 0.7f, 0.3f)
                                                    : new Color(0.85f, 0.85f, 0.9f));
            string afterLine = kill
                ? $"사격 후  {target.CurrentHP:F0} → 0  (격파)"
                : $"사격 후  {target.CurrentHP:F0} → {remainHP:F0}";
            GUI.Label(new Rect(cx, cy, innerW, 20), afterLine, afterStyle);
        }

        /// <summary>프리뷰 엄폐 상태</summary>
        private void DrawFirePreviewCover(float cx, float cy, ref GUIStyle style, float innerW, GridTankUnit target, FirePreview p)
        {
            var coverStyle = new GUIStyle(style);
            coverStyle.fontSize = 15;
            if (p.coveredFromThisAngle)
            {
                var grid = controller.Grid;
                var tc = grid.GetCell(target.GridPosition);
                var cv = tc.Cover;
                string sz = cv.size switch
                {
                    CoverSize.Small => "소",
                    CoverSize.Medium => "중",
                    CoverSize.Large => "대",
                    _ => ""
                };
                string dirs = BattleHUD.GetFacetLabelStatic(cv.CurrentFacets);
                coverStyle.normal.textColor = new Color(0.4f, 1f, 0.5f);
                GUI.Label(new Rect(cx, cy, innerW, 20),
                    $"엄폐 {cv.coverName}({sz}) {dirs}  유효", coverStyle);
            }
            else
            {
                coverStyle.normal.textColor = new Color(0.8f, 0.8f, 0.85f);
                GUI.Label(new Rect(cx, cy, innerW, 20), "엄폐  현재 각도에 무효", coverStyle);
            }
        }
    }
}
