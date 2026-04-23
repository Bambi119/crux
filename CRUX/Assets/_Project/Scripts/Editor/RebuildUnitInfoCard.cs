using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;

namespace Crux.Editor
{
    /// <summary>
    /// UnitInfoCard 프리팹 재구성 스크립트.
    /// 기존 세로 스택 구조 → 가로 320×110 C안 구조로 변환.
    /// </summary>
    public class RebuildUnitInfoCard
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/BattleHUD/UnitInfoCard.prefab";

        [MenuItem("Crux/UI/Rebuild UnitInfoCard Prefab")]
        public static void RebuildPrefab()
        {
            // 1. 프리팹 로드
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[RebuildUnitInfoCard] Prefab not found: " + PrefabPath);
                return;
            }

            // 2. 임시 인스턴스 생성
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                Debug.LogError("[RebuildUnitInfoCard] Failed to instantiate prefab");
                return;
            }

            try
            {
                // 3. 기존 자식 제거 (Text 자식들 보존 필요한 것만 확인 후 정리)
                var rt = instance.GetComponent<RectTransform>();
                // Header 자식들(UnitName, UnitID, HPValue) 임시 저장
                var headerTr = rt.Find("Header");
                TextMeshProUGUI savedUnitName = null, savedUnitID = null, savedHPValue = null;

                if (headerTr != null)
                {
                    savedUnitName = headerTr.Find("UnitName")?.GetComponent<TextMeshProUGUI>();
                    savedUnitID = headerTr.Find("UnitID")?.GetComponent<TextMeshProUGUI>();
                    savedHPValue = headerTr.Find("HPValue")?.GetComponent<TextMeshProUGUI>();
                }

                // 모든 자식 삭제
                for (int i = rt.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(rt.GetChild(i).gameObject);
                }

                // 4. RectTransform 세팅: 320×110
                rt.sizeDelta = new Vector2(320, 110);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);

                // 5. HeaderRow 생성 (UnitName, HPText, APText) — height 24
                var headerRow = CreateRow(rt, "HeaderRow", 320, 24, LayoutGroupType.HorizontalLayout);
                CreateText(headerRow, "UnitNameText", "Unit 1", 100, 24, TextAlignmentOptions.Left);
                CreateText(headerRow, "HPText", "HP: 80/100", 100, 24, TextAlignmentOptions.Center);
                CreateText(headerRow, "APText", "AP: 6/8", 80, 24, TextAlignmentOptions.Right);

                // 6. BodyRow 생성 (height 64)
                var bodyRow = CreateRow(rt, "BodyRow", 320, 64, LayoutGroupType.HorizontalLayout);

                // 6a. LeftBars (width 90) — Hull, Engine, Track
                var leftBars = CreateRow(bodyRow.transform, "LeftBars", 90, 64, LayoutGroupType.VerticalLayout);
                CreateBar(leftBars.transform, "HullBar", 90, 18, "HullBarFill", "HullBarText", "0/100");
                CreateBar(leftBars.transform, "EngineBar", 90, 18, "EngineBarFill", "EngineBarText", "100/100");
                CreateBar(leftBars.transform, "TrackBar", 90, 18, "TrackBarFill", "TrackBarText", "80/100");

                // 6b. CenterSilhouette (width 140, height 64) — hull_player 스프라이트
                var centerSilhouette = CreateGameObject(bodyRow.transform, "CenterSilhouette", 140, 64);
                var silhouetteImg = centerSilhouette.AddComponent<Image>();
                silhouetteImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/hull_player.png");
                silhouetteImg.color = new Color(1, 1, 1, 0.3f);

                // 6c. RightBars (width 90) — Turret, Barrel, MG
                var rightBars = CreateRow(bodyRow.transform, "RightBars", 90, 64, LayoutGroupType.VerticalLayout);
                CreateBar(rightBars.transform, "TurretBar", 90, 18, "TurretBarFill", "TurretBarText", "0/100");
                CreateBar(rightBars.transform, "BarrelBar", 90, 18, "BarrelBarFill", "BarrelBarText", "100/100");
                CreateBar(rightBars.transform, "MGBar", 90, 18, "MGBarFill", "MGBarText", "100/100");

                // 7. FooterBadges (height 20) — Morale, Overwatch, Fire, Misses
                var footerBadges = CreateRow(rt, "FooterBadges", 320, 20, LayoutGroupType.HorizontalLayout);
                CreateText(footerBadges, "MoraleBadge", "良好", 50, 20, TextAlignmentOptions.Center);
                CreateText(footerBadges, "OverwatchBadge", "反击", 50, 20, TextAlignmentOptions.Center);
                CreateText(footerBadges, "FireBadge", "Fire 2T", 50, 20, TextAlignmentOptions.Center);
                CreateText(footerBadges, "MissBadge", "Miss ×3", 50, 20, TextAlignmentOptions.Center);

                // 8. 프리팹 저장
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
                Debug.Log("[RebuildUnitInfoCard] Prefab rebuilt successfully: " + PrefabPath);

                // 9. 임시 인스턴스 정리
                Object.DestroyImmediate(instance);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[RebuildUnitInfoCard] Error: " + ex.Message);
                Object.DestroyImmediate(instance);
                throw;
            }
        }

        /// <summary>레이아웃 그룹이 있는 Row 생성</summary>
        private static GameObject CreateRow(Transform parent, string name, float width, float height, LayoutGroupType layoutType)
        {
            var go = CreateGameObject(parent, name, width, height);

            if (layoutType == LayoutGroupType.HorizontalLayout)
            {
                var hlg = go.AddComponent<HorizontalLayoutGroup>();
                hlg.padding = new RectOffset(2, 2, 2, 2);
                hlg.spacing = 2;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
            }
            else
            {
                var vlg = go.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(2, 2, 2, 2);
                vlg.spacing = 2;
                vlg.childForceExpandWidth = false;
                vlg.childForceExpandHeight = false;
            }

            return go;
        }

        /// <summary>일반 GameObject 생성 — RectTransform 포함</summary>
        private static GameObject CreateGameObject(Transform parent, string name, float width, float height)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            return go;
        }

        /// <summary>Text 생성</summary>
        private static TextMeshProUGUI CreateText(GameObject parent, string name, string text, float width, float height, TextAlignmentOptions align)
        {
            var go = CreateGameObject(parent.transform, name, width, height);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = align;
            tmp.fontSize = 12;
            tmp.color = Color.white;
            return tmp;
        }

        /// <summary>프로그래스 바 생성 (배경 + 채움 Image + 텍스트)</summary>
        private static void CreateBar(Transform parent, string name, float width, float height, string fillImageName, string textName, string labelText)
        {
            var barContainer = CreateGameObject(parent, name, width, height);

            // 배경 Image (채움 안 됨)
            var bgImage = barContainer.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // 채워지는 Image (Filled, Horizontal)
            var fillGo = CreateGameObject(barContainer.transform, fillImageName, width - 4, height - 4);
            var fillImage = fillGo.AddComponent<Image>();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 0.8f;
            fillImage.color = new Color(0.3f, 0.9f, 0.3f, 1f);

            // 텍스트
            var textGo = CreateGameObject(barContainer.transform, textName, width, height);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = labelText;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 10;
            tmp.color = Color.white;
        }

        private enum LayoutGroupType
        {
            HorizontalLayout,
            VerticalLayout
        }
    }
}
