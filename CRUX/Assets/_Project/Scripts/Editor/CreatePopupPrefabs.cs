#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Crux.Editor
{
    /// <summary>
    /// 3단계 팝업 UI 프리팹 생성 에디터 스크립트
    /// Crux/CreatePopupPrefabs 메뉴에서 실행
    /// </summary>
    public class CreatePopupPrefabs
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/BattleHUD/";

        [MenuItem("Crux/CreatePopupPrefabs/Create ContextMenu Prefab")]
        public static void CreateContextMenuPrefab()
        {
            var prefab = CreateContextMenuHierarchy();
            SavePrefab(prefab, PrefabPath + "ContextMenu.prefab");
            Debug.Log("[CRUX] ContextMenu 프리팹 생성 완료: " + PrefabPath + "ContextMenu.prefab");
        }

        [MenuItem("Crux/CreatePopupPrefabs/Create WeaponSelectPanel Prefab")]
        public static void CreateWeaponSelectPanelPrefab()
        {
            var prefab = CreateWeaponSelectPanelHierarchy();
            SavePrefab(prefab, PrefabPath + "WeaponSelectPanel.prefab");
            Debug.Log("[CRUX] WeaponSelectPanel 프리팹 생성 완료: " + PrefabPath + "WeaponSelectPanel.prefab");
        }

        [MenuItem("Crux/CreatePopupPrefabs/Create AmmoSelectPanel Prefab")]
        public static void CreateAmmoSelectPanelPrefab()
        {
            var prefab = CreateAmmoSelectPanelHierarchy();
            SavePrefab(prefab, PrefabPath + "AmmoSelectPanel.prefab");
            Debug.Log("[CRUX] AmmoSelectPanel 프리팹 생성 완료: " + PrefabPath + "AmmoSelectPanel.prefab");
        }

        private static GameObject CreateContextMenuHierarchy()
        {
            var root = new GameObject("ContextMenu");
            var canvas = root.AddComponent<CanvasGroup>();
            var layout = root.AddComponent<LayoutGroup>();

            CreateButton(root, "MoveButton", "Move");
            CreateButton(root, "AttackButton", "Attack");
            CreateButton(root, "WaitButton", "Wait");
            CreateButton(root, "CancelButton", "Cancel");

            return root;
        }

        private static GameObject CreateWeaponSelectPanelHierarchy()
        {
            var root = new GameObject("WeaponSelectPanel");
            var canvas = root.AddComponent<CanvasGroup>();
            var layout = root.AddComponent<LayoutGroup>();

            CreateButton(root, "MainGunButton", "Main Gun");
            CreateButton(root, "CoaxialMGButton", "Coaxial MG");
            CreateButton(root, "MountedMGButton", "Mounted MG");
            CreateButton(root, "BackButton", "Back");

            return root;
        }

        private static GameObject CreateAmmoSelectPanelHierarchy()
        {
            var root = new GameObject("AmmoSelectPanel");
            var canvas = root.AddComponent<CanvasGroup>();

            var ammoInfo = new GameObject("AmmoInfo");
            ammoInfo.transform.SetParent(root.transform, false);
            CreateTextElement(ammoInfo, "AmmoName", "AP - 철갑탄");
            CreateTextElement(ammoInfo, "AmmoStats", "관통력: 100mm | 데미지: 30\n폭발반경: 0칸");
            CreateTextElement(ammoInfo, "AmmoCount", "42 / 42");

            CreateButton(root, "ConfirmButton", "Confirm");
            CreateButton(root, "BackButton", "Back");

            return root;
        }

        private static void CreateButton(GameObject parent, string name, string label)
        {
            var btn = new GameObject(name);
            btn.transform.SetParent(parent.transform, false);
            var button = btn.AddComponent<Button>();
            var image = btn.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var text = new GameObject("Text");
            text.transform.SetParent(btn.transform, false);
            var tmp = text.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;

            Debug.Log($"[CRUX] 버튼 생성: {name}");
        }

        private static void CreateTextElement(GameObject parent, string name, string content)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = 4;

            Debug.Log($"[CRUX] 텍스트 요소 생성: {name}");
        }

        private static void SavePrefab(GameObject instance, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
        }
    }
}
#endif
