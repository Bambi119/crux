using System.Collections.Generic;
using UnityEngine;
using Crux.Grid;
using Crux.Unit;
using Crux.Data;
using Crux.Combat;
using Crux.Core;
using TerrainData = Crux.Core.TerrainData;

namespace Crux.UI
{
    /// <summary>턴제 전투 HUD — OnGUI 렌더링 전담</summary>
    public class BattleHUD : MonoBehaviour
    {
        private Crux.Core.BattleController controller;

        // UI 상태
        private string bannerText;
        private Color bannerColor;
        private float bannerEndTime;

        // 경고 마커 (공격자 머리 위 "!") — OnGUI에서 월드→스크린 변환 후 렌더
        private Vector3? alertWorldPos;
        private float alertEndTime;

        // UI 스케일
        private float uiScale = 1f;

        // UI 텍스처 캐시
        private Dictionary<Color, Texture2D> texCache = new();

        // GUIStyle 캐시
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;

        // 헬퍼 컴포넌트
        private BattleHUDFirePreview firePreview;
        private BattleHUDTerrainOverlay terrainOverlay;
        private BattleHUDUnitPanel unitPanel;
        private BattleHUDModulePanel modulePanel;

        public void Initialize(Crux.Core.BattleController controller)
        {
            this.controller = controller;
            uiScale = Screen.height / 1080f;

            // 헬퍼 컴포넌트 초기화
            firePreview = new BattleHUDFirePreview(this, controller);
            terrainOverlay = new BattleHUDTerrainOverlay(this, controller);
            unitPanel = new BattleHUDUnitPanel(this, controller);
            modulePanel = new BattleHUDModulePanel(this, controller);
        }

        public void Draw()
        {
            if (controller == null || unitPanel == null || firePreview == null || modulePanel == null || terrainOverlay == null) return;

            // UI 스케일링 — 모든 좌표/크기에 자동 적용
            var prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));

            DrawTurnInfo();
            DrawUnitInfo();
            modulePanel.Draw();
            DrawInputModeInfo();
            DrawControls();
            DrawBanner();
            DrawReactionAlert();
            DrawGameResult();

            if (controller.ShowTerrainDebug)
            {
                terrainOverlay.Draw();
            }

            GUI.matrix = prevMatrix;
        }

        // ===== 배너 및 경고 =====

        public void ShowBanner(string text, Color color, float duration)
        {
            bannerText = text;
            bannerColor = color;
            bannerEndTime = Time.time + duration;
        }

        public void ShowAlert(Vector3 worldPos, float duration)
        {
            alertWorldPos = worldPos;
            alertEndTime = Time.time + duration;
        }

        private void DrawBanner()
        {
            if (string.IsNullOrEmpty(bannerText)) return;
            float remaining = bannerEndTime - Time.time;
            if (remaining <= 0)
            {
                bannerText = null;
                return;
            }

            float alpha = Mathf.Clamp01(remaining / 0.6f);
            float bw = 520, bh = 56;
            float bx = ScaledW / 2 - bw / 2;
            float by = 70;

            var bg = new GUIStyle(GetBoxStyle());
            bg.normal.background = GetTex(new Color(0, 0, 0, 0.75f * alpha));
            GUI.Box(new Rect(bx, by, bw, bh), "", bg);

            var s = new GUIStyle(GetLabelStyle());
            s.fontSize = 26;
            s.alignment = TextAnchor.MiddleCenter;
            s.normal.textColor = new Color(bannerColor.r, bannerColor.g, bannerColor.b, alpha);
            GUI.Label(new Rect(bx, by, bw, bh), bannerText, s);
        }

        /// <summary>반응 사격 공격자 머리 위 "!" 마커 — OnGUI에서 월드→스크린 변환</summary>
        private void DrawReactionAlert()
        {
            var mainCam = controller.MainCam;
            if (!alertWorldPos.HasValue || mainCam == null) return;
            float remaining = alertEndTime - Time.time;
            if (remaining <= 0f)
            {
                alertWorldPos = null;
                return;
            }

            Vector3 sp = mainCam.WorldToScreenPoint(alertWorldPos.Value + Vector3.up * 0.6f);
            if (sp.z < 0) return;

            float x = sp.x / uiScale;
            float y = (Screen.height - sp.y) / uiScale;

            // 깜빡 패턴 — sin^2 펄스
            float pulse = Mathf.Abs(Mathf.Sin(Time.time * 22f));
            float alpha = 0.35f + 0.65f * pulse;

            var s = new GUIStyle(GetLabelStyle());
            s.fontSize = 44;
            s.fontStyle = FontStyle.Bold;
            s.alignment = TextAnchor.MiddleCenter;

            // 검은 외곽선
            var shadow = new GUIStyle(s);
            shadow.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.9f);
            GUI.Label(new Rect(x - 29, y - 29, 60, 60), "!", shadow);
            GUI.Label(new Rect(x - 31, y - 31, 60, 60), "!", shadow);

            s.normal.textColor = new Color(1f, 0.3f, 0.15f, alpha);
            GUI.Label(new Rect(x - 30, y - 30, 60, 60), "!", s);
        }

        // ===== 턴/유닛 정보 =====

        private void DrawTurnInfo()
        {
            string phaseKR = controller.CurrentPhase switch
            {
                Crux.Core.TurnPhase.PlayerTurn => "플레이어",
                Crux.Core.TurnPhase.EnemyTurn => "적 턴",
                Crux.Core.TurnPhase.Cinematic => "연출 중",
                Crux.Core.TurnPhase.GameOver => "패배",
                Crux.Core.TurnPhase.Victory => "승리",
                _ => controller.CurrentPhase.ToString()
            };
            var tstyle = new GUIStyle(GetLabelStyle());
            tstyle.fontSize = 20;
            tstyle.normal.textColor = Color.white;
            GUI.Box(new Rect(10, 10, 240, 35), "", GetBoxStyle());
            GUI.Label(new Rect(15, 14, 230, 25),
                $"턴 {controller.TurnCount}  |  {phaseKR}", tstyle);
        }

        private void DrawUnitInfo()
        {
            // 조회 대상: 적을 클릭했으면 적, 아니면 선택된 아군
            var info = controller.InspectedUnit != null && !controller.InspectedUnit.IsDestroyed
                       ? controller.InspectedUnit : controller.SelectedUnit;
            if (info != null)
                unitPanel.Draw(10f, 55f, info);

            // Fire 모드 호버 / WeaponSelect 모드 → 사격 프리뷰
            GridTankUnit previewTarget = null;
            if (controller.CurrentInputMode == Crux.Core.BattleController.InputModeEnum.WeaponSelect && controller.PendingTarget != null)
                previewTarget = controller.PendingTarget;
            else if (controller.CurrentInputMode == Crux.Core.BattleController.InputModeEnum.Fire && controller.HoveredTarget != null)
                previewTarget = controller.HoveredTarget;

            if (previewTarget != null)
                firePreview.Draw(previewTarget, controller.SelectedWeapon);
        }

        // ===== 입력 모드 UI =====

        private void DrawInputModeInfo()
        {
            // 무기 선택 모드 — 특별 UI
            if (controller.CurrentInputMode == Crux.Core.BattleController.InputModeEnum.WeaponSelect && controller.PendingTarget != null)
            {
                DrawWeaponSelectUI();
                return;
            }

            // 방향 선택 모드 — 특별 UI
            if (controller.CurrentInputMode == Crux.Core.BattleController.InputModeEnum.MoveDirectionSelect)
            {
                DrawMoveDirectionUI();
                return;
            }

            string modeText = controller.CurrentInputMode switch
            {
                Crux.Core.BattleController.InputModeEnum.Move => "[이동 모드] 셀 클릭으로 이동",
                Crux.Core.BattleController.InputModeEnum.Fire => "[사격 모드] 적 유닛 클릭으로 사격",
                _ => "[대기] Q:이동  E:사격  Space:턴종료"
            };

            float iw = 360;
            GUI.Box(new Rect(ScaledW / 2 - iw / 2, ScaledH - 42, iw, 32), "", GetBoxStyle());
            var style = new GUIStyle(GetLabelStyle());
            style.fontSize = 17;
            style.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(ScaledW / 2 - iw / 2, ScaledH - 42, iw, 32), modeText, style);
        }

        private void DrawMoveDirectionUI()
        {
            var box = GetBoxStyle();
            var label = new GUIStyle(GetLabelStyle());
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;

            float panelW = 440, panelH = 96;
            float px = ScaledW / 2 - panelW / 2;
            float py = ScaledH / 2 + 60;

            GUI.Box(new Rect(px, py, panelW, panelH), "", box);

            // 6방향 라벨 (60° 단위, QWE/ASD 매핑)
            string dirName = controller.PendingFacingAngle switch
            {
                0f => "↑ 북 (W)",
                60f => "↗ 북동 (E)",
                120f => "↘ 남동 (D)",
                180f => "↓ 남 (S)",
                240f => "↙ 남서 (A)",
                300f => "↖ 북서 (Q)",
                _ => $"{controller.PendingFacingAngle:F0}°"
            };

            GUI.Label(new Rect(px, py + 5, panelW, 22),
                $"목적지: ({controller.PendingMoveTarget.x},{controller.PendingMoveTarget.y})  AP: {controller.PendingMoveCost}", label);
            GUI.Label(new Rect(px, py + 30, panelW, 22),
                $"방향: {dirName}", label);
            var hintStyle = new GUIStyle(label);
            hintStyle.fontSize = 16;
            hintStyle.normal.textColor = new Color(0.75f, 0.75f, 0.8f);
            GUI.Label(new Rect(px, py + 60, panelW, 22),
                "[Space] 확정   [클릭] 방향+확정   [Tab] 취소", hintStyle);
        }

        private void DrawWeaponSelectUI()
        {
            var box = GetBoxStyle();
            var label = GetLabelStyle();

            // 무기 선택 패널
            float panelW = 420, panelH = 138;
            float px = ScaledW / 2 - panelW / 2;
            float py = ScaledH / 2 + 40;

            GUI.Box(new Rect(px, py, panelW, panelH), "", box);

            var titleStyle = new GUIStyle(label);
            titleStyle.fontSize = 21;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(px, py + 5, panelW, 22), $"무기 선택 — 대상: {controller.PendingTarget.Data.tankName}", titleStyle);

            var normalCol = new Color(0.75f, 0.75f, 0.8f);
            var selCol = new Color(1f, 0.95f, 0.3f);

            void DrawItem(int slot, WeaponType wt, string text)
            {
                var st = new GUIStyle(label);
                st.fontSize = 18;
                st.normal.textColor = (controller.SelectedWeapon == wt) ? selCol : normalCol;
                string mark = (controller.SelectedWeapon == wt) ? "▶ " : "  ";
                GUI.Label(new Rect(px + 10, py + 30 + (slot - 1) * 23, panelW - 20, 20),
                    $"{mark}[{slot}] {text}", st);
            }

            // 1. 주포
            string mainAmmo = controller.SelectedUnit.currentAmmo != null
                ? (!string.IsNullOrEmpty(controller.SelectedUnit.currentAmmo.shortCode)
                    ? controller.SelectedUnit.currentAmmo.shortCode : controller.SelectedUnit.currentAmmo.ammoName)
                : "AP";
            int mainCal = controller.SelectedUnit.Data != null ? controller.SelectedUnit.Data.mainGunCaliber : 0;
            DrawItem(1, WeaponType.MainGun,
                $"주포 {mainCal}mm {mainAmmo}  AP:{GameConstants.FireCost}");

            // 2. 동축 기관총
            if (controller.CoaxialMGData != null)
                DrawItem(2, WeaponType.CoaxialMG,
                    $"{controller.CoaxialMGData.mgName} {controller.CoaxialMGData.burstCount}발  AP:{controller.CoaxialMGData.apCost}");

            // 3. 탑재 기관총
            if (controller.MountedMGData != null)
                DrawItem(3, WeaponType.MountedMG,
                    $"{controller.MountedMGData.mgName} {controller.MountedMGData.burstCount}발  AP:{controller.MountedMGData.apCost}");

            var hintStyle = new GUIStyle(label);
            hintStyle.fontSize = 15;
            hintStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            hintStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(px, py + 110, panelW, 18),
                "1/2/3: 선택   Space/클릭: 사격   Tab: 취소", hintStyle);
        }

        // ===== 모듈 상태 =====

        // ===== 컨트롤 + 게임 결과 =====

        private void DrawControls()
        {
            var style = new GUIStyle(GetLabelStyle());
            style.fontSize = 16;
            style.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            GUI.Box(new Rect(10, ScaledH - 115, 340, 98), "", GetBoxStyle());
            GUI.Label(new Rect(15, ScaledH - 112, 330, 18), "Q: 이동  |  E: 사격  |  Tab: 취소", style);
            GUI.Label(new Rect(15, ScaledH - 93, 330, 18), "Space: 확정/턴종료  |  1/2/3: 무기", style);
            GUI.Label(new Rect(15, ScaledH - 74, 330, 18), "C: 소화  |  V: 연막", style);
            GUI.Label(new Rect(15, ScaledH - 55, 330, 18),
                      $"O: 오버워치 (AP -{GameConstants.OverwatchCost})", style);
        }

        private void DrawGameResult()
        {
            // 승리/패배 체크는 BattleController에 맡김
            if (controller.CurrentPhase == Crux.Core.TurnPhase.GameOver || controller.CurrentPhase == Crux.Core.TurnPhase.Victory)
            {
                string msg = controller.CurrentPhase == Crux.Core.TurnPhase.Victory
                    ? "승리! 적 전멸!"
                    : "패배... 로시난테 파괴됨";

                var bigStyle = new GUIStyle(GetLabelStyle());
                bigStyle.fontSize = 36;
                bigStyle.alignment = TextAnchor.MiddleCenter;

                GUI.Box(new Rect(ScaledW / 2 - 180, ScaledH / 2 - 35, 360, 70), "", GetBoxStyle());
                GUI.Label(new Rect(ScaledW / 2 - 180, ScaledH / 2 - 35, 360, 70), msg, bigStyle);

                if (Input.GetKeyDown(KeyCode.R))
                    UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            }
        }


        // ===== 유틸 =====

        /// <summary>스케일 보정된 Screen 크기 (OnGUI 내에서 사용)</summary>
        public float ScaledW => Screen.width / uiScale;
        public float ScaledH => Screen.height / uiScale;

        /// <summary>UI 스케일 (헬퍼 클래스에서 접근 가능)</summary>
        public float UIScale => uiScale;

        /// <summary>전차 분류 라벨</summary>
        public static string GetTankClassLabelStatic(TankClass cls) => cls switch
        {
            TankClass.Vehicle => "차량",
            TankClass.Light => "경전차",
            TankClass.Medium => "중형전차",
            TankClass.Heavy => "중전차",
            _ => ""
        };

        /// <summary>전차 분류 라벨 (내부용)</summary>
        private static string GetTankClassLabel(TankClass cls) => GetTankClassLabelStatic(cls);

        /// <summary>나침반 각도 → N/NE/E/SE/S/SW/W/NW</summary>
        public static string GetCompassLabelStatic(float compassAngle)
        {
            float a = ((compassAngle % 360f) + 360f) % 360f;
            int idx = Mathf.RoundToInt(a / 45f) % 8;
            return idx switch
            {
                0 => "↑N",
                1 => "↗NE",
                2 => "→E",
                3 => "↘SE",
                4 => "↓S",
                5 => "↙SW",
                6 => "←W",
                7 => "↖NW",
                _ => ""
            };
        }

        private static string GetCompassLabel(float compassAngle) => GetCompassLabelStatic(compassAngle);

        /// <summary>6면 방호 플래그 → "북/북동/남동" 식 라벨</summary>
        public static string GetFacetLabelStatic(HexFacet facets)
        {
            if (facets == HexFacet.None) return "—";
            var list = new List<string>();
            foreach (var d in facets.Enumerate())
                list.Add(HexCoord.DirLabel(d));
            return string.Join("/", list);
        }

        /// <summary>6면 방호 플래그 → "북/북동/남동" 식 라벨 (내부용)</summary>
        private static string GetFacetLabel(HexFacet facets) => GetFacetLabelStatic(facets);

        public void DrawBox(Rect rect)
        {
            GUI.Box(rect, "", GetBoxStyle());
        }

        public GUIStyle GetLabelStyleUI()
        {
            return new GUIStyle(GetLabelStyle());
        }

        private GUIStyle GetBoxStyle()
        {
            if (_boxStyle != null) return _boxStyle;
            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = GetTex(new Color(0, 0, 0, 0.7f));
            return _boxStyle;
        }

        private GUIStyle GetLabelStyle()
        {
            if (_labelStyle != null) return _labelStyle;
            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = 20;
            _labelStyle.normal.textColor = Color.white;
            _labelStyle.fontStyle = FontStyle.Bold;
            return _labelStyle;
        }

        private Texture2D GetTex(Color col)
        {
            if (texCache.TryGetValue(col, out var cached)) return cached;
            var tex = new Texture2D(2, 2);
            var pixels = new Color[4];
            for (int i = 0; i < 4; i++) pixels[i] = col;
            tex.SetPixels(pixels);
            tex.Apply();
            texCache[col] = tex;
            return tex;
        }
    }
}
