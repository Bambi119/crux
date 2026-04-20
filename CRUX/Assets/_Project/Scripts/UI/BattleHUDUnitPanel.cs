using UnityEngine;
using Crux.Grid;
using Crux.Unit;
using Crux.Data;
using Crux.Core;

namespace Crux.UI
{
    /// <summary>통합 유닛 정보 패널 — 아군/적 동일 레이아웃 (조회용, 프리뷰 제외)</summary>
    public class BattleHUDUnitPanel
    {
        private BattleHUD hud;
        private Crux.Core.BattleController controller;

        public BattleHUDUnitPanel(BattleHUD hud, Crux.Core.BattleController controller)
        {
            this.hud = hud;
            this.controller = controller;
        }

        public void Draw(float x, float y, GridTankUnit u)
        {
            float w = 350, h = 200;
            hud.DrawBox(new Rect(x, y, w, h));

            var style = hud.GetLabelStyleUI();
            style.fontSize = 17;

            float lineH = 20f;
            float cy = y + 5;
            float cx = x + 10;
            float innerW = w - 20;

            // 1행: 이름 + 전차타입 + 위치·방향
            string cls = BattleHUD.GetHullClassLabelStatic(u.Data != null ? u.Data.hullClass : HullClass.Assault);
            string facing = BattleHUD.GetCompassLabelStatic(u.HullAngle);
            string sideTag = u.side == PlayerSide.Player ? "" : "[적] ";
            GUI.Label(new Rect(cx, cy, innerW, lineH),
                $"{sideTag}{u.Data?.tankName}  ({cls})", style);
            var posStyle = new GUIStyle(style);
            posStyle.alignment = TextAnchor.UpperRight;
            posStyle.fontSize = 15;
            posStyle.normal.textColor = new Color(0.75f, 0.75f, 0.8f);
            GUI.Label(new Rect(cx, cy, innerW, lineH),
                $"({u.GridPosition.x},{u.GridPosition.y}) {facing}", posStyle);
            cy += lineH;

            // 2행: HP / AP
            float hpRatio = u.Data != null && u.Data.maxHP > 0 ? u.CurrentHP / u.Data.maxHP : 0f;
            var hpColor = hpRatio > 0.6f ? new Color(0.4f, 1f, 0.5f)
                       : hpRatio > 0.3f ? new Color(1f, 0.9f, 0.2f)
                                        : new Color(1f, 0.3f, 0.2f);
            var hpStyle = new GUIStyle(style);
            hpStyle.normal.textColor = hpColor;
            GUI.Label(new Rect(cx, cy, innerW, lineH),
                $"HP {u.CurrentHP:F0}/{u.Data?.maxHP}   AP {u.CurrentAP}/{u.MaxAP}", hpStyle);
            cy += lineH;

            // 3행: 이동 거리 (셀, 패널티 반영)
            int moveCost = u.GetMoveCostPerCell();
            int moveCells = moveCost > 0 ? u.CurrentAP / moveCost : 0;
            int baseMove = GameConstants.MoveCostPerCell;
            int penalty = moveCost - baseMove;
            string penStr = penalty > 0 ? $"-{penalty}" : "";
            GUI.Label(new Rect(cx, cy, innerW, lineH),
                $"이동 {moveCells}셀 (비용 {moveCost}/셀{penStr})", style);
            cy += lineH;

            // 4행: 주포 — {caliber}mm {AmmoCode} {count}/{max}
            int cal = u.Data != null ? u.Data.mainGunCaliber : 0;
            string ammoCode = u.currentAmmo != null
                ? (!string.IsNullOrEmpty(u.currentAmmo.shortCode) ? u.currentAmmo.shortCode : u.currentAmmo.ammoName)
                : "-";
            GUI.Label(new Rect(cx, cy, innerW, lineH),
                $"주포 {cal}mm  {ammoCode} {u.MainGunAmmoCount}/{u.MaxMainGunAmmo}", style);
            cy += lineH;

            // 5행: 기관총 — {caliber}mm {loaded}/{total}
            if (u.MGAmmoTotal > 0)
            {
                float mgCal = controller.CoaxialMGData != null ? controller.CoaxialMGData.caliber : 7.92f;
                GUI.Label(new Rect(cx, cy, innerW, lineH),
                    $"MG {mgCal:0.##}mm  {u.MGAmmoLoaded}/{u.MGAmmoTotal}", style);
            }
            else
            {
                GUI.Label(new Rect(cx, cy, innerW, lineH), "MG —", style);
            }
            cy += lineH;

            // 6행: 엄폐 상태 (이름(크기), 방호범위 N/E, -명중보정)
            var grid = controller.Grid;
            var cell = grid.GetCell(u.GridPosition);
            var coverStyle = new GUIStyle(style);
            coverStyle.fontSize = 16;
            if (cell != null && cell.HasCover && cell.Cover != null && !cell.Cover.IsDestroyed)
            {
                var cov = cell.Cover;
                string sizeLabel = cov.size switch
                {
                    CoverSize.Small => "소",
                    CoverSize.Medium => "중",
                    CoverSize.Large => "대",
                    _ => ""
                };
                string dirs = BattleHUD.GetFacetLabelStatic(cov.CurrentFacets);
                int hitPenalty = Mathf.RoundToInt(cov.CoverRate * 30f); // 명중률 보정치 %
                coverStyle.normal.textColor = new Color(0.3f, 1f, 0.5f);
                GUI.Label(new Rect(cx, cy, innerW, lineH),
                    $"엄폐 {cov.coverName}({sizeLabel}) {dirs}  명중-{hitPenalty}%", coverStyle);
            }
            else
            {
                coverStyle.normal.textColor = new Color(1f, 0.6f, 0.4f);
                GUI.Label(new Rect(cx, cy, innerW, lineH), "엄폐 — 개활지", coverStyle);
            }
            cy += lineH;

            // 7행: 상태이상
            string statusExtra = "";
            if (u.IsOnFire) statusExtra += "[화재] ";
            if (u.RemainingSmokeCharges > 0) statusExtra += $"연막:{u.RemainingSmokeCharges} ";
            if (cell != null && cell.HasSmoke) statusExtra += "[연막중] ";
            if (u.IsOverwatching) statusExtra += "[⌁반응대기]";
            if (statusExtra.Length == 0) statusExtra = "정상";
            var stStyle = new GUIStyle(style);
            stStyle.fontSize = 15;
            stStyle.normal.textColor = u.IsOnFire ? new Color(1f, 0.5f, 0.2f) : new Color(0.8f, 0.85f, 0.9f);
            GUI.Label(new Rect(cx, cy, innerW, lineH), $"상태: {statusExtra}", stStyle);
            cy += lineH;

            // 8행: 승무원 부상 상태 (부상자가 있을 때만 표시)
            var crew = u.Crew;
            if (crew != null)
            {
                var injuredMembers = new System.Collections.Generic.List<string>();
                AddInjuredMemberIfExists(crew.commander, "전차장", injuredMembers);
                AddInjuredMemberIfExists(crew.gunner, "포수", injuredMembers);
                AddInjuredMemberIfExists(crew.loader, "탄약수", injuredMembers);
                AddInjuredMemberIfExists(crew.driver, "조종수", injuredMembers);
                AddInjuredMemberIfExists(crew.mgMechanic, "기총사수", injuredMembers);

                if (injuredMembers.Count > 0)
                {
                    string crewStatus = string.Join(" ", injuredMembers);
                    var crewStyle = new GUIStyle(style);
                    crewStyle.fontSize = 14;
                    crewStyle.normal.textColor = new Color(1f, 0.7f, 0.4f);
                    GUI.Label(new Rect(cx, cy, innerW, lineH), $"크루: {crewStatus}", crewStyle);
                }
            }
        }

        /// <summary>승무원 부상 상태 추가 헬퍼</summary>
        private void AddInjuredMemberIfExists(Crux.Data.CrewMemberRuntime member, string role,
                                              System.Collections.Generic.List<string> injuredList)
        {
            if (member == null) return;
            var level = member.injuryState;
            if (level == Crux.Data.InjuryLevel.None) return;

            string label = level switch
            {
                Crux.Data.InjuryLevel.Minor => $"<color=#ffa040>부상:{role}</color>",
                Crux.Data.InjuryLevel.Severe => $"<color=#ff4040>중상:{role}</color>",
                Crux.Data.InjuryLevel.Fatal => $"<color=#888888>전사:{role}</color>",
                _ => ""
            };
            if (!string.IsNullOrEmpty(label))
                injuredList.Add(label);
        }
    }
}
