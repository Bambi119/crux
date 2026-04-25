using System;
using System.Collections.Generic;
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
            WireButtonClickListeners();
        }

        /// <summary>각 버튼 클릭 시 해당 인덱스로 ConfirmSelection 발동 — 프리팹 onClick 미배선 보완</summary>
        private void WireButtonClickListeners()
        {
            if (menuButtons == null) return;
            for (int i = 0; i < menuButtons.Length; i++)
            {
                if (menuButtons[i] == null) continue;
                int idx = i; // 클로저 캡처
                menuButtons[i].onClick.RemoveAllListeners();
                menuButtons[i].onClick.AddListener(() =>
                {
                    selectedIndex = idx;
                    UpdateSelection();
                    ConfirmSelection();
                });
            }
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

        /// <summary>박스 활성화 및 선택 초기화 (전체 6개 항목 표시)</summary>
        public void ShowMenu()
        {
            visibleIndices = null; // 전체 모드
            gameObject.SetActive(true);
            isActive = true;
            selectedIndex = 0;
            UpdateSelection();
        }

        /// <summary>박스를 특정 월드 위치에 표시 (화면 경계 플립 자동 적용). 전체 6개 항목 표시.</summary>
        /// <param name="worldPos">월드 좌표</param>
        /// <param name="cam">사용할 카메라 — null이면 Camera.main 폴백</param>
        public void ShowMenuAt(Vector3 worldPos, UnityEngine.Camera cam = null)
        {
            visibleIndices = null; // 전체 모드
            selectedIndex = 0;
            ApplyPositionAndActivate(worldPos, cam);
        }

        // 필터 모드에서 visible 인덱스 캐시 (null이면 전체 표시 모드)
        private List<int> visibleIndices = null;

        /// <summary>지정한 메뉴 항목만 표시. 나머지는 SetActive(false). 위치는 worldPos 기준 화면 플립 적용.</summary>
        public void ShowMenuFiltered(MenuItem[] visibleItems, Vector3 worldPos, UnityEngine.Camera cam = null)
        {
            // 가시 인덱스 캐싱
            visibleIndices = new List<int>();
            foreach (var item in visibleItems)
                visibleIndices.Add((int)item);

            // 버튼 활성/비활성 설정
            for (int i = 0; i < menuButtons.Length; i++)
            {
                if (menuButtons[i] == null) continue;
                menuButtons[i].gameObject.SetActive(visibleIndices.Contains(i));
            }

            // 첫 번째 visible 항목으로 selectedIndex 초기화
            selectedIndex = visibleIndices.Count > 0 ? visibleIndices[0] : 0;

            // ShowMenuAt의 위치 계산 로직 재사용
            ApplyPositionAndActivate(worldPos, cam);
        }

        /// <summary>위치 계산 + 박스 활성화 (ShowMenuAt/ShowMenuFiltered 공유 helper)</summary>
        private void ApplyPositionAndActivate(Vector3 worldPos, UnityEngine.Camera cam)
        {
            var rectTransform = GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                gameObject.SetActive(true);
                isActive = true;
                UpdateSelection();
                return;
            }

            gameObject.SetActive(true);
            isActive = true;

            var size = rectTransform.sizeDelta;
            var parentCanvas = GetComponentInParent<Canvas>();
            var canvasRT = parentCanvas != null ? parentCanvas.GetComponent<RectTransform>() : null;

            var resolvedCam = cam ?? UnityEngine.Camera.main;
            if (canvasRT != null && resolvedCam != null)
            {
                Vector3 screenPos = resolvedCam.WorldToScreenPoint(worldPos);
                Vector2 localPos;

                UnityEngine.Camera uiCam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : resolvedCam;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRT, screenPos, uiCam, out localPos);

                float screenWidth = Screen.width;
                float xOffset = size.x * 0.5f + 50f;
                float rightMargin = size.x + 20f;
                if (screenPos.x + rightMargin > screenWidth)
                    localPos.x = localPos.x - xOffset;
                else
                    localPos.x = localPos.x + xOffset;

                float bottomMargin = size.y + 20f;
                if (screenPos.y < bottomMargin)
                    localPos.y = localPos.y + size.y + 20f;
                else
                    localPos.y = localPos.y - 20f;

                rectTransform.anchoredPosition = localPos;
            }

            UpdateSelection();
        }

        /// <summary>박스 비활성화</summary>
        public void HideMenu()
        {
            gameObject.SetActive(false);
            isActive = false;
            // 필터 상태 초기화 — 다음 ShowMenu/ShowMenuAt 호출 시 전체 표시 모드로 복귀
            visibleIndices = null;
            // 모든 버튼 다시 활성화 (다음 ShowMenu 시 올바른 상태)
            if (menuButtons != null)
                foreach (var btn in menuButtons)
                    if (btn != null) btn.gameObject.SetActive(true);
        }

        /// <summary>화살표 입력으로 선택 이동 (상/좌: -1, 하/우: +1). 필터 모드에서는 visible 항목만 순환.</summary>
        public void MoveSelection(int direction)
        {
            if (!isActive) return;

            if (visibleIndices != null && visibleIndices.Count > 0)
            {
                // 필터 모드: visible 인덱스 리스트 내에서 순환
                int pos = visibleIndices.IndexOf(selectedIndex);
                if (pos < 0) pos = 0;
                pos = (pos + direction + visibleIndices.Count) % visibleIndices.Count;
                selectedIndex = visibleIndices[pos];
            }
            else
            {
                // 전체 모드: 0~5 순환
                selectedIndex += direction;
                selectedIndex = (selectedIndex + 6) % 6;
            }
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

        /// <summary>UI 강조 업데이트 — 필터 모드에서는 visible 항목만 색상 처리</summary>
        private void UpdateSelection()
        {
            for (int i = 0; i < 6; i++)
            {
                if (menuButtons[i] == null) continue;
                // 비활성(필터 제외) 버튼은 색상 처리 생략
                if (!menuButtons[i].gameObject.activeSelf) continue;

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
