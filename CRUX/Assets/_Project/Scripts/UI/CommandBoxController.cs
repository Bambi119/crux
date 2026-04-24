using System;
using UnityEngine;
using UnityEngine.UI;

namespace Crux.UI
{
    /// <summary>
    /// 커맨드 박스(메인 메뉴) 내비게이션 제어.
    ///
    /// 6개 메뉴 항목(이동, 사격, 방향전환, 스킬, 대기, 취소) 중
    /// 화살표/WASD로 선택하고 Space/Enter로 확정.
    ///
    /// 참고: docs/10c §2.2 — 커맨드 박스 메뉴 항목 및 입력 흐름
    /// </summary>
    public class CommandBoxController : MonoBehaviour
    {
        /// <summary>메뉴 항목 정의</summary>
        public enum MenuItem
        {
            Move = 0,
            Fire = 1,
            Rotate = 2,
            Skill = 3,
            Wait = 4,
            Cancel = 5
        }

        /// <summary>메뉴 항목별 Button 참조</summary>
        [SerializeField] private Button[] menuButtons = new Button[6];

        /// <summary>선택된 항목 인덱스</summary>
        private int selectedIndex = 0;

        /// <summary>박스가 활성화되었는가</summary>
        private bool isActive = false;

        /// <summary>선택 확정 콜백</summary>
        public event Action<MenuItem> OnMenuSelected;

        /// <summary>박스 취소 콜백</summary>
        public event Action OnMenuCanceled;

        // 자식 이름 → MenuItem 인덱스 매핑
        private static readonly string[] ButtonNames =
            { "BtnMove", "BtnFire", "BtnRotate", "BtnSkill", "BtnWait", "BtnCancel" };

        private void Awake()
        {
            // SerializeField 배열이 비어 있을 때 자식 이름으로 자동 탐색
            AutoBindButtonsIfNeeded();
        }

        private void Start()
        {
            ValidateMenuButtons();
        }

        /// <summary>SerializeField 배열 미연결 시 자식 이름으로 Button을 자동 바인딩</summary>
        private void AutoBindButtonsIfNeeded()
        {
            bool needsBind = menuButtons == null || menuButtons.Length != 6;
            if (!needsBind)
            {
                foreach (var btn in menuButtons)
                    if (btn == null) { needsBind = true; break; }
            }

            if (!needsBind) return;

            menuButtons = new Button[6];
            for (int i = 0; i < ButtonNames.Length; i++)
            {
                Transform child = transform.Find(ButtonNames[i]);
                if (child != null)
                    menuButtons[i] = child.GetComponent<Button>();
                else
                    Debug.LogWarning($"[CommandBox] 자식 '{ButtonNames[i]}' 없음 — 프리팹 구조 확인 필요");
            }
        }

        private void ValidateMenuButtons()
        {
            if (menuButtons == null || menuButtons.Length != 6)
            {
                Debug.LogError("[CommandBox] 메뉴 버튼이 6개가 아님. 인스펙터에서 설정 필수.");
                return;
            }

            for (int i = 0; i < 6; i++)
            {
                if (menuButtons[i] == null)
                    Debug.LogWarning($"[CommandBox] 메뉴 항목 {i}({(MenuItem)i}) 버튼이 null");
            }
        }

        /// <summary>박스 활성화 및 선택 초기화</summary>
        public void ShowMenu()
        {
            gameObject.SetActive(true);
            isActive = true;
            selectedIndex = 0;
            UpdateSelection();
        }

        /// <summary>박스를 특정 월드 위치에 표시 (화면 경계 플립 자동 적용)</summary>
        public void ShowMenuAt(Vector3 worldPos)
        {
            var rectTransform = GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                ShowMenu();
                return;
            }

            gameObject.SetActive(true);
            isActive = true;
            selectedIndex = 0;

            // PopupPositioner로 화면 좌표 계산
            var size = rectTransform.sizeDelta;
            var canvasRT = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();

            if (canvasRT != null)
            {
                Vector3 screenPos = UnityEngine.Camera.main.WorldToScreenPoint(worldPos);
                Vector2 localPos;

                // Canvas의 좌표계로 변환
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRT, screenPos, UnityEngine.Camera.main, out localPos);

                // 화면 경계 플립 판정
                float screenWidth = Screen.width;
                float screenHeight = Screen.height;

                // 우측 경계 체크
                float rightMargin = size.x + 20f;
                if (screenPos.x + rightMargin > screenWidth)
                {
                    // 좌측 배치
                    localPos.x = localPos.x - size.x - 20f;
                }
                else
                {
                    // 우측 배치 (기본)
                    localPos.x = localPos.x + 80f;
                }

                // 하단 경계 체크
                float bottomMargin = size.y + 20f;
                if (screenPos.y + bottomMargin > screenHeight)
                {
                    // 상단 배치
                    localPos.y = localPos.y + size.y + 20f;
                }
                else
                {
                    // 하단 배치 (기본)
                    localPos.y = localPos.y - 20f;
                }

                rectTransform.anchoredPosition = localPos;
            }

            UpdateSelection();
        }

        /// <summary>박스 비활성화</summary>
        public void HideMenu()
        {
            gameObject.SetActive(false);
            isActive = false;
        }

        /// <summary>화살표 입력으로 선택 이동 (상/좌: -1, 하/우: +1)</summary>
        public void MoveSelection(int direction)
        {
            if (!isActive) return;

            selectedIndex += direction;
            selectedIndex = (selectedIndex + 6) % 6; // 순환 (0~5)
            UpdateSelection();
        }

        /// <summary>현재 선택 항목 확정</summary>
        public void ConfirmSelection()
        {
            if (!isActive) return;

            MenuItem selected = (MenuItem)selectedIndex;
            if (selected == MenuItem.Cancel)
            {
                OnMenuCanceled?.Invoke();
                HideMenu();
            }
            else
            {
                OnMenuSelected?.Invoke(selected);
                HideMenu();
            }
        }

        /// <summary>UI 강조 업데이트</summary>
        private void UpdateSelection()
        {
            for (int i = 0; i < 6; i++)
            {
                if (menuButtons[i] == null) continue;

                // 선택된 버튼은 색상 강조, 나머지는 연회색
                var colors = menuButtons[i].colors;
                if (i == selectedIndex)
                {
                    colors.normalColor = Color.white;
                    menuButtons[i].colors = colors;
                }
                else
                {
                    colors.normalColor = new Color(0.7f, 0.7f, 0.7f, 1f);
                    menuButtons[i].colors = colors;
                }
            }
        }

        /// <summary>선택된 메뉴 항목 조회</summary>
        public MenuItem GetSelectedMenuItem() => (MenuItem)selectedIndex;
    }
}
