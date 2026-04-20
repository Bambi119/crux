using UnityEngine;
using UnityEngine.UI;
using Crux.Data;

namespace Crux.UI
{
    /// <summary>
    /// 편성 탭(CompositionTab) 슬롯 데이터 바인딩 + 하단 [파츠 인벤토리 열기] 버튼 부착.
    /// HangarUI의 BindTankSlots / BindOneSlot에서 추출.
    ///
    /// 사용법: HangarUI가 OnEnable에 생성 + SelectTab(Composition)마다 Bind 호출.
    /// </summary>
    public class HangarCompositionBinder
    {
        private readonly HangarUI owner;
        private readonly ConvoyInventory convoy;

        public HangarCompositionBinder(HangarUI owner, ConvoyInventory convoy)
        {
            this.owner = owner;
            this.convoy = convoy;
        }

        /// <summary>
        /// 편성 탭 인스턴스 내 SortieGrid/StorageGrid 슬롯에 탱크 바인딩 + 라벨 카운트 갱신.
        /// 마지막에 [파츠 인벤토리 열기] 버튼 부착.
        /// </summary>
        public void Bind(GameObject compositionTabInstance, TankInstance selectedTank)
        {
            if (convoy == null || compositionTabInstance == null) return;

            Transform root = compositionTabInstance.transform;
            Transform sortieGrid = root.Find("SortieGrid");
            Transform storageGrid = root.Find("StorageGrid");

            var sortieTanks = convoy.tanks.FindAll(t => t.inSortie);
            var storageTanks = convoy.tanks.FindAll(t => !t.inSortie);

            // 카운트 라벨
            var sortieLabel = root.Find("SortieLabel")?.GetComponent<Text>();
            if (sortieLabel != null) sortieLabel.text = $"출격 ({sortieTanks.Count}/5)";
            var storageLabel = root.Find("StorageLabel")?.GetComponent<Text>();
            if (storageLabel != null) storageLabel.text = $"보관 ({storageTanks.Count}/5)";

            BindGrid(sortieGrid, sortieTanks, selectedTank);
            BindGrid(storageGrid, storageTanks, selectedTank);

            HangarButtonHelpers.AttachOpenPartsButton(root, () =>
            {
                // 클릭 시점의 selectedTank를 읽음 — 로컬 변수 캡처 회피
                var current = owner.SelectedTank;
                if (current != null) owner.OpenPartsInventory(current);
            });
        }

        private void BindGrid(Transform grid, System.Collections.Generic.List<TankInstance> tanks, TankInstance selectedTank)
        {
            if (grid == null) return;
            const int slotCount = 5;
            for (int i = 0; i < slotCount; i++)
            {
                if (i >= grid.childCount) break;
                Transform slot = grid.GetChild(i);
                TankInstance tank = (i < tanks.Count) ? tanks[i] : null;
                BindOneSlot(slot, tank, selectedTank);
            }
        }

        private void BindOneSlot(Transform slot, TankInstance tank, TankInstance selectedTank)
        {
            if (slot == null) return;

            Text label = slot.GetComponentInChildren<Text>();
            if (label != null)
                label.text = (tank != null) ? tank.tankName : "없음";

            Image bg = slot.GetComponent<Image>();
            if (bg != null)
            {
                bool isSelected = (tank != null && tank == selectedTank);
                bg.color = isSelected
                    ? new Color(0.55f, 0.45f, 0.25f, 1f)
                    : (tank != null
                        ? new Color(0.25f, 0.25f, 0.25f, 1f)
                        : new Color(0.18f, 0.18f, 0.18f, 1f));
            }

            Button btn = slot.GetComponent<Button>();
            if (btn == null)
                btn = slot.gameObject.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();

            if (tank != null)
            {
                var captured = tank;
                btn.onClick.AddListener(() => owner.OnUnitSelected(captured));
            }
        }
    }
}
