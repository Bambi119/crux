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
        private void Awake()
        {
            if (hangarUI == null)
                hangarUI = FindObjectOfType<HangarUI>();
        }

        [SerializeField] private Text nameText;
        [SerializeField] private Text hpText;
        [SerializeField] private Text armorText;

        [SerializeField] private Transform traitListRoot;
        [SerializeField] private Transform crewListRoot;
        [SerializeField] private GameObject listEntryPrefab;

        private TankInstance currentUnit;
        private HangarUI hangarUI;

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

            if (currentUnit.crew == null)
                return;

            // 모든 직책과 해당 승무원을 열거
            foreach (var (klass, crew) in currentUnit.crew.All())
            {
                CreateCrewEntry(klass, crew);
            }
        }

        private void CreateCrewEntry(CrewClass klass, CrewMemberRuntime crew)
        {
            GameObject entryObj = new GameObject($"CrewEntry_{klass}");
            entryObj.transform.SetParent(crewListRoot, false);

            entryObj.AddComponent<RectTransform>();

            // 텍스트 (raycastTarget=true로 Button의 targetGraphic 역할)
            Text textComponent = entryObj.AddComponent<Text>();
            textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComponent.fontSize = 14;
            textComponent.alignment = TextAnchor.MiddleLeft;
            textComponent.color = crew != null ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.5f, 0.5f, 0.5f);
            textComponent.raycastTarget = true;

            if (crew != null)
                textComponent.text = $"{klass}: {crew.DisplayName} (Aim {crew.BaseAim})";
            else
                textComponent.text = $"{klass}: (공석)";

            // 버튼 추가 — Text를 targetGraphic으로 사용
            var btn = entryObj.AddComponent<Button>();
            btn.targetGraphic = textComponent;
            var capturedKlass = klass;
            var capturedTank = currentUnit;

            if (crew != null)
            {
                // 장착된 크루 → 클릭 시 해제
                btn.onClick.AddListener(() => UnassignCrew(capturedKlass));
            }
            else
            {
                // 공석 → 클릭 시 풀 팝업
                btn.onClick.AddListener(() => RequestCrewPool(capturedKlass));
            }

            var le = entryObj.AddComponent<LayoutElement>();
            le.minHeight = 20;
            le.preferredHeight = 22;
        }

        private void UnassignCrew(CrewClass klass)
        {
            if (currentUnit == null || hangarUI == null) return;
            hangarUI.UnassignCrewAndRefresh(currentUnit, klass);
        }

        private void RequestCrewPool(CrewClass klass)
        {
            if (currentUnit == null || hangarUI == null) return;
            hangarUI.OpenCrewPool(currentUnit, klass);
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
