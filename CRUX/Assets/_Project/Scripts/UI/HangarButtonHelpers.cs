using UnityEngine;
using UnityEngine.UI;

namespace Crux.UI
{
    /// <summary>
    /// Hangar 전용 런타임 버튼 생성 헬퍼 — 순수 정적.
    /// HangarUI의 AttachSortieButton / AttachOpenPartsButton에서 추출.
    /// </summary>
    public static class HangarButtonHelpers
    {
        /// <summary>
        /// TopBar HorizontalLayoutGroup 하위에 "▶ 출격" 버튼 추가.
        /// 중복 생성 방지. 생성된 버튼의 onClick은 콜러 지정.
        /// </summary>
        public static void AttachSortieButton(Transform topBar, System.Action onClick)
        {
            if (topBar == null) return;
            if (topBar.Find("SortieButton") != null) return;

            var btnObj = new GameObject("SortieButton");
            btnObj.transform.SetParent(topBar, false);
            btnObj.AddComponent<RectTransform>();
            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.7f, 0.35f, 0.2f, 1f);
            var btn = btnObj.AddComponent<Button>();
            var le = btnObj.AddComponent<LayoutElement>();
            le.preferredWidth = 120;
            le.preferredHeight = 42;

            AddStretchLabel(btnObj.transform, "▶ 출격", 18);

            if (onClick != null)
                btn.onClick.AddListener(() => onClick());
        }

        /// <summary>
        /// CompositionTab 하위에 "파츠 인벤토리 열기" 버튼 추가.
        /// VerticalLayoutGroup 맨 아래에 자동 배치. 중복 생성 방지.
        /// </summary>
        public static void AttachOpenPartsButton(Transform compositionTab, System.Action onClick)
        {
            if (compositionTab == null) return;
            if (compositionTab.Find("OpenPartsButton") != null) return;

            var btnObj = new GameObject("OpenPartsButton");
            btnObj.transform.SetParent(compositionTab, false);
            btnObj.AddComponent<RectTransform>();
            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.3f, 0.35f, 0.42f, 1f);
            var btn = btnObj.AddComponent<Button>();
            var le = btnObj.AddComponent<LayoutElement>();
            le.preferredHeight = 36;
            le.flexibleWidth = 1;

            AddStretchLabel(btnObj.transform, "파츠 인벤토리 열기", 16);

            if (onClick != null)
                btn.onClick.AddListener(() => onClick());
        }

        /// <summary>
        /// 버튼 전체를 덮는 stretch 레이블 자식 Text 생성 — 공통 패턴.
        /// </summary>
        private static void AddStretchLabel(Transform parent, string label, int fontSize)
        {
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(parent, false);
            var labelRt = labelObj.AddComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var text = labelObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
        }
    }
}
