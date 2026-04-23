using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Crux.Core;
using Crux.Unit;

namespace Crux.UI
{
    /// <summary>
    /// ContextMenu (Depth 1) 팝업 관리자 — Move/Attack/Wait/Cancel 버튼 표시.
    /// 선택된 유닛 근처 월드 좌표에서 화면 좌표로 변환하여 배치.
    /// </summary>
    public class ContextMenuController : MonoBehaviour
    {
        private BattleController controller;
        private Transform contextMenuRoot;  // 팝업 루트 Panel
        private Button moveButton;
        private Button attackButton;
        private Button waitButton;
        private Button cancelButton;
        private RectTransform rectTransform;
        private WeaponSelectPanelController weaponSelectPanel;

        public void Initialize(BattleController controller, Transform contextMenu, WeaponSelectPanelController weaponSelect)
        {
            this.controller = controller;
            this.contextMenuRoot = contextMenu;
            this.weaponSelectPanel = weaponSelect;

            if (contextMenuRoot == null)
            {
                Debug.LogError("[CRUX] ContextMenuController: contextMenu Transform이 null입니다.");
                return;
            }

            rectTransform = contextMenuRoot.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                Debug.LogError("[CRUX] ContextMenuController: RectTransform 컴포넌트가 없습니다.");
                return;
            }

            // 버튼 찾기
            moveButton = contextMenuRoot.Find("MoveButton")?.GetComponent<Button>();
            attackButton = contextMenuRoot.Find("AttackButton")?.GetComponent<Button>();
            waitButton = contextMenuRoot.Find("WaitButton")?.GetComponent<Button>();
            cancelButton = contextMenuRoot.Find("CancelButton")?.GetComponent<Button>();

            if (moveButton == null || attackButton == null || waitButton == null || cancelButton == null)
            {
                Debug.LogError("[CRUX] ContextMenuController: 버튼을 찾을 수 없습니다. 예상: MoveButton, AttackButton, WaitButton, CancelButton");
                return;
            }

            // 버튼 클릭 리스너
            moveButton.onClick.AddListener(OnMoveClicked);
            attackButton.onClick.AddListener(OnAttackClicked);
            waitButton.onClick.AddListener(OnWaitClicked);
            cancelButton.onClick.AddListener(OnCancelClicked);

            // 초기 숨김
            contextMenuRoot.gameObject.SetActive(false);

            controller.OnSelectedUnitChanged += OnSelectedUnitChanged;

            Debug.Log("[CRUX] ContextMenuController: 초기화 완료");
        }

        private void OnEnable()
        {
            if (controller != null)
                controller.OnSelectedUnitChanged += OnSelectedUnitChanged;
        }

        private void OnDisable()
        {
            if (controller != null)
                controller.OnSelectedUnitChanged -= OnSelectedUnitChanged;
        }

        private void OnSelectedUnitChanged(GridTankUnit unit)
        {
            if (unit == null)
            {
                HideContextMenu();
                return;
            }

            // 아군 유닛만 표시 (플레이어 턴 중)
            if (unit.side != PlayerSide.Player)
            {
                HideContextMenu();
                return;
            }

            // 입력 모드가 Select일 때만 표시
            if (controller.CurrentInputMode != BattleController.InputModeEnum.Select)
            {
                HideContextMenu();
                return;
            }

            ShowContextMenuNearUnit(unit);
        }

        private void ShowContextMenuNearUnit(GridTankUnit unit)
        {
            // 유닛 월드 좌표 → 스크린 좌표 → 캔버스 로컬 좌표
            var worldPos = unit.transform.position;
            var screenPos = controller.MainCam.WorldToScreenPoint(worldPos);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                contextMenuRoot.parent as RectTransform,
                screenPos,
                null,  // ScreenSpace-Overlay 모드
                out Vector2 localPos
            );

            rectTransform.anchoredPosition = localPos + new Vector2(50f, 50f);  // 오프셋
            contextMenuRoot.gameObject.SetActive(true);
        }

        public void HideContextMenu()
        {
            contextMenuRoot.gameObject.SetActive(false);
        }

        private void OnMoveClicked()
        {
            HideContextMenu();
            controller.TryEnterMoveMode();
        }

        private void OnAttackClicked()
        {
            HideContextMenu();
            // Attack 시작 → WeaponSelectPanel 표시
            controller.TryEnterFireMode();
            if (weaponSelectPanel != null)
                weaponSelectPanel.Show();
        }

        private void OnWaitClicked()
        {
            HideContextMenu();
            controller.EndPlayerTurn();
        }

        private void OnCancelClicked()
        {
            HideContextMenu();
            controller.DeselectUnit();
        }

        private void Update()
        {
            // ESC 로컬 처리: ContextMenu 열려있으면 닫기
            if (contextMenuRoot.gameObject.activeSelf && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                HideContextMenu();
            }
        }
    }
}
