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

        private void Start()
        {
            // 메뉴 버튼들을 인덱스와 일치하도록 정렬 (Move, Fire, Rotate, Skill, Wait, Cancel)
            // 예: 레이아웃 2×3 그리드라면 인덱스 순서 확인 필요
            ValidateMenuButtons();
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
