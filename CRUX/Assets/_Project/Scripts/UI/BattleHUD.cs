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
    /// <summary>Phase 3: OnGUI 폐기 — BattleHUDFirePreview/BattleHUDTerrainOverlay 헬퍼 메서드만 유지</summary>
    /// <remarks>배너/경고 렌더링은 BattleController의 큐에서 관리. Phase 4 uGUI로 이관 (TD-08)</remarks>
    public class BattleHUD : MonoBehaviour
    {
        private Crux.Core.BattleController controller;
        private float uiScale = 1f;

        // UI 텍스처 캐시
        private Dictionary<Color, Texture2D> texCache = new();

        // GUIStyle 캐시
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;

        // 헬퍼 컴포넌트 (Phase 4: 월드 공간 렌더링 이관까지 보존)
        private BattleHUDFirePreview firePreview;
        private BattleHUDTerrainOverlay terrainOverlay;

        public void Initialize(Crux.Core.BattleController controller)
        {
            this.controller = controller;
            uiScale = Screen.height / 1080f;
            // 헬퍼 컴포넌트: FirePreview/TerrainOverlay만 유지 (Phase 4 이관까지)
        }

        public void Draw()
        {
            // Phase 3: OnGUI 렌더링 폐기
        }

        public void ShowBanner(string text, Color color, float duration)
        {
            // Phase 3: 구현 이관 (TD-08)
        }

        public void ShowAlert(Vector3 worldPos, float duration)
        {
            // Phase 3: 구현 이관 (TD-08)
        }

        // ===== 유틸 — FirePreview/TerrainOverlay 용 헬퍼 =====

        /// <summary>스케일 보정된 Screen 크기</summary>
        public float ScaledW => Screen.width / uiScale;
        public float ScaledH => Screen.height / uiScale;

        /// <summary>UI 스케일</summary>
        public float UIScale => uiScale;

        /// <summary>차체 분류 라벨 — docs/05 §1</summary>
        public static string GetHullClassLabelStatic(HullClass cls) => cls switch
        {
            HullClass.Scout => "경전차",
            HullClass.Assault => "중형전차",
            HullClass.Support => "지원전차",
            HullClass.Heavy => "중전차",
            HullClass.Siege => "초중전차",
            _ => ""
        };

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

        /// <summary>6면 방호 플래그 → "북/북동/남동" 식 라벨</summary>
        public static string GetFacetLabelStatic(HexFacet facets)
        {
            if (facets == HexFacet.None) return "—";
            var list = new List<string>();
            foreach (var d in facets.Enumerate())
                list.Add(HexCoord.DirLabel(d));
            return string.Join("/", list);
        }

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
