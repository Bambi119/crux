using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Crux.Core;
using Crux.Unit;
using Crux.Data;

namespace Crux.UI
{
    /// <summary>
    /// WeaponSelectPanel (Depth 2) 컨트롤러 — 주포/기관총 선택 팝업.
    /// MainGun은 항상 표시, CoaxialMG/MountedMG는 장착 여부에 따라 표시.
    /// 탄약 상태 표시: MainGun만 "보유/최대", MG는 간단히 "적재중".
    /// </summary>
    public class WeaponSelectPanelController : MonoBehaviour
    {
        private BattleController controller;
        private Transform panelRoot;
        private Button mainGunButton;
        private Button coaxialMGButton;
        private Button mountedMGButton;
        private Button backButton;
        private AmmoSelectPanelController ammoSelectPanel;
        private ContextMenuController contextMenu;

        // 상태
        private bool isShowing;
        private WeaponType selectedWeapon = WeaponType.MainGun;

        public void Initialize(BattleController controller, Transform panel, AmmoSelectPanelController ammoSelect, ContextMenuController context)
        {
            this.controller = controller;
            this.panelRoot = panel;
            this.ammoSelectPanel = ammoSelect;
            this.contextMenu = context;

            if (panelRoot == null)
            {
                Debug.LogError("[CRUX] WeaponSelectPanelController: panel Transform이 null입니다.");
                return;
            }

            // 버튼 찾기
            mainGunButton = panelRoot.Find("MainGunButton")?.GetComponent<Button>();
            coaxialMGButton = panelRoot.Find("CoaxialMGButton")?.GetComponent<Button>();
            mountedMGButton = panelRoot.Find("MountedMGButton")?.GetComponent<Button>();
            backButton = panelRoot.Find("BackButton")?.GetComponent<Button>();

            if (mainGunButton == null || backButton == null)
            {
                Debug.LogError("[CRUX] WeaponSelectPanelController: MainGunButton 또는 BackButton을 찾을 수 없습니다.");
                return;
            }

            // 리스너 등록
            mainGunButton.onClick.AddListener(() => SelectWeapon(WeaponType.MainGun));
            if (coaxialMGButton != null)
                coaxialMGButton.onClick.AddListener(() => SelectWeapon(WeaponType.CoaxialMG));
            if (mountedMGButton != null)
                mountedMGButton.onClick.AddListener(() => SelectWeapon(WeaponType.MountedMG));
            backButton.onClick.AddListener(OnBackClicked);

            // 초기 숨김
            panelRoot.gameObject.SetActive(false);
            isShowing = false;

            Debug.Log("[CRUX] WeaponSelectPanelController: 초기화 완료");
        }

        public void Show()
        {
            isShowing = true;
            panelRoot.gameObject.SetActive(true);
            UpdateWeaponButtons();
        }

        public void Hide()
        {
            isShowing = false;
            panelRoot.gameObject.SetActive(false);
            if (ammoSelectPanel != null)
                ammoSelectPanel.Hide();
        }

        private void UpdateWeaponButtons()
        {
            if (controller.SelectedUnit == null) return;

            var unit = controller.SelectedUnit;

            // CoaxialMG 표시 여부
            bool hasCoaxial = controller.CoaxialMGData != null;
            if (coaxialMGButton != null)
                coaxialMGButton.gameObject.SetActive(hasCoaxial);

            // MountedMG 표시 여부
            bool hasMounted = controller.MountedMGData != null;
            if (mountedMGButton != null)
                mountedMGButton.gameObject.SetActive(hasMounted);
        }

        private void SelectWeapon(WeaponType weaponType)
        {
            selectedWeapon = weaponType;

            // MainGun: AmmoSelectPanel로 진입 (Depth 3)
            if (weaponType == WeaponType.MainGun)
            {
                if (ammoSelectPanel != null)
                {
                    ammoSelectPanel.Show(weaponType);
                }
                return;
            }

            // MG: 직접 선택 확정 (Depth 3 스킵)
            controller.SelectMG(weaponType, null);  // ammo는 null (MG는 기본 탄약만 있음)
            Hide();
            contextMenu.HideContextMenu();
        }

        private void OnBackClicked()
        {
            Hide();
            // ContextMenu 재표시는 BattleController의 상태에 따라 자동 처리
        }

        private void Update()
        {
            // ESC 로컬 처리
            if (isShowing && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                OnBackClicked();
            }
        }
    }
}
