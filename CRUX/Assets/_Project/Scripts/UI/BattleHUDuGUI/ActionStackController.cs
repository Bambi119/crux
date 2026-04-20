using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Crux.Core;

namespace Crux.UI
{
    /// <summary>
    /// ActionStack 버튼 컨트롤러 — Move, Fire, Smoke, Overwatch, EndTurn 버튼 관리.
    /// 현재 InputMode에 따라 활성 버튼 하이라이트.
    /// </summary>
    public class ActionStackController : MonoBehaviour
    {
        private BattleController controller;

        // 버튼 캐시
        private Button moveButton;
        private Button fireButton;
        private Button smokeButton;
        private Button overwatchButton;
        private Button endTurnButton;

        // 버튼별 하이라이트용 자식 요소
        private Image moveLeftAccent;
        private Image fireLeftAccent;
        private TextMeshProUGUI moveButtonText;
        private TextMeshProUGUI fireButtonText;

        public void Initialize(BattleController controller, Transform actionStack)
        {
            this.controller = controller;

            if (actionStack == null)
            {
                Debug.LogError("[CRUX] ActionStackController: actionStack Transform이 null입니다.");
                return;
            }

            // 버튼 찾기
            moveButton = actionStack.Find("MoveButton")?.GetComponent<Button>();
            fireButton = actionStack.Find("FireButton")?.GetComponent<Button>();
            smokeButton = actionStack.Find("SmokeButton")?.GetComponent<Button>();
            overwatchButton = actionStack.Find("OverwatchButton")?.GetComponent<Button>();
            endTurnButton = actionStack.Find("EndTurnButton")?.GetComponent<Button>();

            // 하이라이트 요소 찾기 (MoveButton과 FireButton만 구현)
            if (moveButton != null)
            {
                moveLeftAccent = moveButton.transform.Find("LeftAccent")?.GetComponent<Image>();
                moveButtonText = moveButton.GetComponentInChildren<TextMeshProUGUI>();
            }

            if (fireButton != null)
            {
                fireLeftAccent = fireButton.transform.Find("LeftAccent")?.GetComponent<Image>();
                fireButtonText = fireButton.GetComponentInChildren<TextMeshProUGUI>();
            }

            // onClick 리스너 등록
            if (moveButton != null)
                moveButton.onClick.AddListener(() => controller.TryEnterMoveMode());

            if (fireButton != null)
                fireButton.onClick.AddListener(() => controller.TryEnterFireMode());

            if (smokeButton != null)
                smokeButton.onClick.AddListener(() => controller.TryUseSmokeAction());

            if (overwatchButton != null)
                overwatchButton.onClick.AddListener(() => controller.TryActivateOverwatchAction());

            if (endTurnButton != null)
                endTurnButton.onClick.AddListener(() => controller.EndPlayerTurn());

            Debug.Log("[CRUX] ActionStackController: 초기화 완료");
        }

        private void Update()
        {
            if (controller == null) return;

            UpdateButtonHighlight();
        }

        private void UpdateButtonHighlight()
        {
            var inputMode = controller.CurrentInputMode;

            // Fire 모드
            if (fireLeftAccent != null)
                fireLeftAccent.gameObject.SetActive(inputMode == BattleController.InputModeEnum.Fire);

            if (fireButtonText != null)
            {
                fireButtonText.color = inputMode == BattleController.InputModeEnum.Fire
                    ? UIColorPalette.PrimaryContainer
                    : UIColorPalette.OnSurfaceVariant;
            }

            // Move 모드
            if (moveLeftAccent != null)
                moveLeftAccent.gameObject.SetActive(inputMode == BattleController.InputModeEnum.Move);

            if (moveButtonText != null)
            {
                moveButtonText.color = inputMode == BattleController.InputModeEnum.Move
                    ? UIColorPalette.PrimaryContainer
                    : UIColorPalette.OnSurfaceVariant;
            }
        }
    }
}
