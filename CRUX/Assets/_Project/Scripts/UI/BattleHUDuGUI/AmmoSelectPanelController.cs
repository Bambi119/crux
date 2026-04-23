using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Crux.Core;
using Crux.Unit;
using Crux.Data;

namespace Crux.UI
{
    /// <summary>
    /// AmmoSelectPanel (Depth 3) 컨트롤러 — 주포 탄종 선택.
    /// 현재 탑재된 탄약(currentAmmo)을 표시하고 확정/취소 선택.
    /// 향후 ammo inventory system 추가 시 다중 탄종 리스트로 확장 가능.
    /// </summary>
    public class AmmoSelectPanelController : MonoBehaviour
    {
        private BattleController controller;
        private Transform panelRoot;
        private TextMeshProUGUI ammoNameText;
        private TextMeshProUGUI ammoStatsText;
        private TextMeshProUGUI ammoCountText;
        private Button confirmButton;
        private Button backButton;
        private WeaponSelectPanelController weaponSelectPanel;

        private bool isShowing;
        private WeaponType currentWeaponType;

        public void Initialize(BattleController controller, Transform panel, WeaponSelectPanelController weaponSelect)
        {
            this.controller = controller;
            this.panelRoot = panel;
            this.weaponSelectPanel = weaponSelect;

            if (panelRoot == null)
            {
                Debug.LogError("[CRUX] AmmoSelectPanelController: panel Transform이 null입니다.");
                return;
            }

            // 텍스트 요소 찾기
            ammoNameText = panelRoot.Find("AmmoName")?.GetComponent<TextMeshProUGUI>();
            ammoStatsText = panelRoot.Find("AmmoStats")?.GetComponent<TextMeshProUGUI>();
            ammoCountText = panelRoot.Find("AmmoCount")?.GetComponent<TextMeshProUGUI>();

            // 버튼 찾기
            confirmButton = panelRoot.Find("ConfirmButton")?.GetComponent<Button>();
            backButton = panelRoot.Find("BackButton")?.GetComponent<Button>();

            if (confirmButton == null || backButton == null)
            {
                Debug.LogError("[CRUX] AmmoSelectPanelController: ConfirmButton 또는 BackButton을 찾을 수 없습니다.");
                return;
            }

            // 리스너
            confirmButton.onClick.AddListener(OnConfirmClicked);
            backButton.onClick.AddListener(OnBackClicked);

            // 초기 숨김
            panelRoot.gameObject.SetActive(false);
            isShowing = false;

            Debug.Log("[CRUX] AmmoSelectPanelController: 초기화 완료");
        }

        public void Show(WeaponType weaponType)
        {
            currentWeaponType = weaponType;
            isShowing = true;
            panelRoot.gameObject.SetActive(true);
            UpdateAmmoDisplay();
        }

        public void Hide()
        {
            isShowing = false;
            panelRoot.gameObject.SetActive(false);
        }

        private void UpdateAmmoDisplay()
        {
            if (controller.SelectedUnit == null) return;

            var unit = controller.SelectedUnit;
            var ammo = unit.currentAmmo;

            if (ammo == null)
            {
                if (ammoNameText != null) ammoNameText.text = "(탄약 미장전)";
                if (ammoCountText != null) ammoCountText.text = "0 / " + unit.MaxMainGunAmmo;
                if (ammoStatsText != null) ammoStatsText.text = "";
                return;
            }

            // 탄약 정보 표시
            if (ammoNameText != null)
                ammoNameText.text = $"{ammo.shortCode} - {ammo.ammoName}";

            if (ammoCountText != null)
                ammoCountText.text = $"{unit.MainGunAmmoCount} / {unit.MaxMainGunAmmo}";

            if (ammoStatsText != null)
                ammoStatsText.text = $"관통력: {ammo.penetration:F0}mm | 데미지: {ammo.damage:F0}\n" +
                                      $"폭발반경: {ammo.blastRadius:F1}칸";
        }

        private void OnConfirmClicked()
        {
            if (controller.SelectedUnit == null) return;

            // MainGun 탄약 확정 (이미 currentAmmo가 선택된 상태)
            controller.SelectMainGunAmmo(controller.SelectedUnit.currentAmmo);
            Hide();
            if (weaponSelectPanel != null)
                weaponSelectPanel.Hide();
        }

        private void OnBackClicked()
        {
            Hide();
            // WeaponSelectPanel로 돌아가기
            if (weaponSelectPanel != null)
                weaponSelectPanel.Show();
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
