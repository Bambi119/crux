using UnityEngine;
using UnityEngine.UI;
using Crux.Data;
using System.Collections.Generic;

namespace Crux.UI
{
    /// <summary>
    /// Hangar 우측 패널: 선택된 전차 정보 표시 (이름·HP·장갑·Trait·승무원).
    /// 슬롯 클릭 시 CompositionTabController가 OnUnitSelected를 호출해 업데이트.
    /// </summary>
    public class HangarRightPanel : MonoBehaviour
    {
        [SerializeField] private Text nameText;
        [SerializeField] private Text hpText;
        [SerializeField] private Text armorText;

        [SerializeField] private Transform traitListRoot;
        [SerializeField] private Transform crewListRoot;
        [SerializeField] private GameObject listEntryPrefab;

        private TankInstance currentUnit;

        public void SetUnit(TankInstance tank)
        {
            currentUnit = tank;
            if (tank == null)
            {
                Clear();
                return;
            }

            // 이름 표시
            if (nameText != null)
                nameText.text = tank.tankName ?? "—";

            // HP 표시
            if (hpText != null)
                hpText.text = $"HP {tank.CurrentHP}/{tank.MaxHP}";

            // 장갑 표시 (장착된 Armor 파츠 나열)
            if (armorText != null)
            {
                int armorCount = tank.armor?.FindAll(a => a != null).Count ?? 0;
                armorText.text = $"장갑: {armorCount}";
            }

            RefreshTraitList();
            RefreshCrewList();
        }

        public void Clear()
        {
            currentUnit = null;

            if (nameText != null)
                nameText.text = "—";
            if (hpText != null)
                hpText.text = "—";
            if (armorText != null)
                armorText.text = "—";

            ClearList(traitListRoot);
            ClearList(crewListRoot);
        }

        private void RefreshTraitList()
        {
            ClearList(traitListRoot);
            if (currentUnit == null || traitListRoot == null)
                return;

            // Trait 정보는 TankInstance에 직접 저장되지 않음 — 향후 crew/composition 정보와 통합
            // 지금은 플레이스홀더
        }

        private void RefreshCrewList()
        {
            ClearList(crewListRoot);
            if (currentUnit == null || crewListRoot == null)
                return;

            // 승무원 정보는 TankInstance에 직접 저장되지 않음 — 향후 crew 시스템과 통합
            // 지금은 플레이스홀더
        }

        private void ClearList(Transform listRoot)
        {
            if (listRoot == null)
                return;

            foreach (Transform child in listRoot)
            {
                Destroy(child.gameObject);
            }
        }
    }
}
