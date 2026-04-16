using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Crux.Data;
using System.Collections.Generic;

namespace Crux.UI
{
    /// <summary>
    /// Hangar 편성 탭 컨트롤러.
    /// 출격 슬롯(3) + 보관 슬롯(5) 생성·관리.
    /// 슬롯 클릭 → HangarUI.OnUnitSelected 호출 → RightPanel 갱신.
    /// </summary>
    public class CompositionTabController : MonoBehaviour
    {
        [SerializeField] private Transform launchSlotsRow;
        [SerializeField] private Transform storageSlotsRow;
        [SerializeField] private GameObject tankSlotPrefab;

        [SerializeField] private HangarUI hangarRef;
        [SerializeField] private ConvoyInventory convoyRef;

        [SerializeField] private GameObject partInventoryPanel;
        [SerializeField] private Button openInventoryButton;
        [SerializeField] private Button compatibilityCheckButton;

        private const int LaunchSlotCount = 3;
        private const int StorageSlotCount = 5;

        // 슬롯별 TankInstance 보유 (추후 실제 데이터 구조로 전환)
        private TankInstance[] launchSlots;
        private TankInstance[] storageSlots;

        private void OnEnable()
        {
            launchSlots = new TankInstance[LaunchSlotCount];
            storageSlots = new TankInstance[StorageSlotCount];

            BuildSlots();
            RegisterButtonListeners();
        }

        private void BuildSlots()
        {
            BuildSlotRow(launchSlotsRow, launchSlots, true);
            BuildSlotRow(storageSlotsRow, storageSlots, false);
        }

        private void BuildSlotRow(Transform parent, TankInstance[] slots, bool isLaunch)
        {
            if (parent == null)
                return;

            for (int i = 0; i < slots.Length; i++)
            {
                GameObject slotObj = tankSlotPrefab != null
                    ? Instantiate(tankSlotPrefab, parent)
                    : new GameObject($"Slot_{i}");

                TMP_Text slotText = slotObj.GetComponentInChildren<TMP_Text>();
                if (slotText != null)
                    slotText.text = "—";  // 초기값: 빈 슬롯

                Button slotBtn = slotObj.GetComponent<Button>();
                if (slotBtn != null)
                {
                    int slotIndex = i;
                    bool isLaunchCopy = isLaunch;
                    slotBtn.onClick.AddListener(() => OnSlotClicked(slotIndex, isLaunchCopy));
                }
            }
        }

        private void OnSlotClicked(int slotIndex, bool isLaunch)
        {
            TankInstance tank = isLaunch
                ? (slotIndex < launchSlots.Length ? launchSlots[slotIndex] : null)
                : (slotIndex < storageSlots.Length ? storageSlots[slotIndex] : null);

            if (hangarRef != null)
            {
                if (tank != null)
                    hangarRef.OnUnitSelected(tank);
                else
                    hangarRef.OnUnitDeselected();
            }
        }

        private void RegisterButtonListeners()
        {
            if (openInventoryButton != null)
            {
                openInventoryButton.onClick.AddListener(() =>
                {
                    if (partInventoryPanel != null)
                        partInventoryPanel.SetActive(true);
                });
            }

            if (compatibilityCheckButton != null)
            {
                compatibilityCheckButton.onClick.AddListener(() =>
                {
                    Debug.Log("[HANGAR] Compatibility check requested (stub)");
                });
            }
        }

        private void OnDisable()
        {
            // 슬롯 버튼 리스너 해제
            if (launchSlotsRow != null)
                foreach (Transform child in launchSlotsRow)
                {
                    Button btn = child.GetComponent<Button>();
                    if (btn != null)
                        btn.onClick.RemoveAllListeners();
                    Destroy(child.gameObject);
                }

            if (storageSlotsRow != null)
                foreach (Transform child in storageSlotsRow)
                {
                    Button btn = child.GetComponent<Button>();
                    if (btn != null)
                        btn.onClick.RemoveAllListeners();
                    Destroy(child.gameObject);
                }

            // 메인 버튼 리스너 해제
            if (openInventoryButton != null)
                openInventoryButton.onClick.RemoveAllListeners();

            if (compatibilityCheckButton != null)
                compatibilityCheckButton.onClick.RemoveAllListeners();

            launchSlots = null;
            storageSlots = null;
        }
    }
}
