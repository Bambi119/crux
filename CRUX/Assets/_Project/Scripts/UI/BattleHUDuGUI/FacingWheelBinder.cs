using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Crux.Core;

namespace Crux.UI
{
    /// <summary>
    /// 방향 선택 휠 — MoveDirectionSelect 모드에서 활성화.
    /// 6개 방향 버튼(N/NE/SE/S/SW/NW)을 클릭하면 각도 설정 후 이동 확정.
    /// </summary>
    public class FacingWheelBinder : MonoBehaviour
    {
        private BattleController controller;
        private Transform wheelRoot;

        // 방향 버튼 캐시 (인덱스: 0=N, 1=NE, 2=SE, 3=S, 4=SW, 5=NW)
        private Button[] dirButtons = new Button[6];
        private string[] dirNames = { "DirN", "DirNE", "DirSE", "DirS", "DirSW", "DirNW" };
        private float[] dirAngles = { 0f, 60f, 120f, 180f, 240f, 300f };

        // 각 버튼의 텍스트 컴포넌트
        private TextMeshProUGUI[] buttonTexts = new TextMeshProUGUI[6];

        // 색상 캐시
        private Color activeButtonColor;
        private Color activeTextColor;
        private Color inactiveButtonColor;
        private Color inactiveTextColor;

        // 마지막 적용한 각도 (dirty check)
        private float lastAppliedAngle = -1f;

        public void Initialize(BattleController controller, Transform wheelRoot)
        {
            this.controller = controller;
            this.wheelRoot = wheelRoot;

            CacheChildReferences();
            SetupColorPalette();
        }

        private void CacheChildReferences()
        {
            // 6개 방향 버튼 찾기 및 리스너 등록
            for (int i = 0; i < 6; i++)
            {
                var btn = wheelRoot.Find(dirNames[i])?.GetComponent<Button>();
                if (btn != null)
                {
                    dirButtons[i] = btn;
                    var text = btn.GetComponentInChildren<TextMeshProUGUI>();
                    if (text != null)
                        buttonTexts[i] = text;

                    // 클로저 문제 회피를 위해 로컬 변수 사용
                    int dirIndex = i;
                    btn.onClick.AddListener(() => OnDirectionClicked(dirIndex));
                }
            }

            Debug.Log("[CRUX] FacingWheelBinder: 6개 방향 버튼 캐싱 완료");
        }

        private void SetupColorPalette()
        {
            // UIColorPalette 불러오기 (또는 하드코드 스타일 사용)
            // 활성(amber): PrimaryContainer 계열 색상
            // 비활성(회색): SurfaceContainer 계열
            activeButtonColor = new Color(1f, 0.58f, 0.05f, 1f);     // Amber
            activeTextColor = new Color(0.2f, 0.1f, 0.05f, 1f);      // Dark amber text
            inactiveButtonColor = new Color(0.4f, 0.4f, 0.43f, 1f);  // Surface gray
            inactiveTextColor = new Color(1f, 0.58f, 0.05f, 0.6f);   // Dimmed amber
        }

        private void OnDirectionClicked(int dirIndex)
        {
            if (controller == null) return;

            // 각도 설정
            float angle = dirAngles[dirIndex];
            controller.SetPendingFacingAngle(angle);

            // 즉시 이동 확정
            controller.CommitMoveDirection();

            Debug.Log($"[CRUX] FacingWheelBinder: 방향 선택 {dirNames[dirIndex]} ({angle}°) → 이동 확정");
        }

        private void Update()
        {
            if (controller == null || wheelRoot == null) return;

            // 활성 조건: MoveDirectionSelect 모드
            bool shouldActive = controller.CurrentInputMode == BattleController.InputModeEnum.MoveDirectionSelect;
            if (wheelRoot.gameObject.activeSelf != shouldActive)
                wheelRoot.gameObject.SetActive(shouldActive);

            // 활성 상태에서만 색상 갱신
            if (shouldActive)
                UpdateButtonHighlight();
        }

        private void UpdateButtonHighlight()
        {
            float currentAngle = controller.PendingFacingAngle;

            // Dirty check — 각도 변경이 없으면 갱신 건너뛰기
            if (Mathf.Approximately(currentAngle, lastAppliedAngle))
                return;

            lastAppliedAngle = currentAngle;

            // 6개 버튼 색상 갱신
            for (int i = 0; i < 6; i++)
            {
                if (dirButtons[i] == null) continue;

                bool isSelected = Mathf.Approximately(currentAngle, dirAngles[i]);

                // 버튼 배경 색상
                var btnImage = dirButtons[i].GetComponent<Image>();
                if (btnImage != null)
                    btnImage.color = isSelected ? activeButtonColor : inactiveButtonColor;

                // 버튼 텍스트 색상
                if (buttonTexts[i] != null)
                    buttonTexts[i].color = isSelected ? activeTextColor : inactiveTextColor;
            }
        }
    }
}
