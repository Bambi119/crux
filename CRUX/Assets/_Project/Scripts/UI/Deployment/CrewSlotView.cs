using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Crux.Data;

namespace Crux.UI.Deployment
{
    /// <summary>
    /// 5슬롯 중 하나를 관리하는 단일 슬롯 뷰.
    /// 크루 배치/미배치 상태를 표시하고, 클릭 이벤트를 발생시킨다.
    /// </summary>
    public class CrewSlotView : MonoBehaviour
    {
        [SerializeField] private Image slotBackground;
        [SerializeField] private TextMeshProUGUI slotLabelText;
        [SerializeField] private TextMeshProUGUI crewNameText;
        [SerializeField] private TextMeshProUGUI classLabelText;
        [SerializeField] private Image borderImage;
        [SerializeField] private Button slotButton;

        private CrewClass slotClass;
        private CrewMemberSO assignedCrew;

        public event Action<CrewSlotView> OnSlotClickedEvent;

        // 이 슬롯의 slot class를 초기화 (Inspector에서 할당하거나 코드로 설정)
        public void Initialize(CrewClass crewClass, string slotLabel)
        {
            slotClass = crewClass;
            if (slotLabelText != null)
                slotLabelText.text = slotLabel;
            SetVacant();
        }

        /// <summary>크루 배치</summary>
        public void SetAssigned(CrewMemberSO crew)
        {
            assignedCrew = crew;
            if (crewNameText != null)
                crewNameText.text = crew.displayName;
            if (classLabelText != null)
                classLabelText.text = crew.klass.ToString();

            // 테두리를 olive로 변경
            if (borderImage != null)
            {
                borderImage.color = new Color(107f / 255f, 120f / 255f, 86f / 255f, 1f); // #6b7856
            }
        }

        /// <summary>미배치 상태로 초기화</summary>
        public void SetVacant()
        {
            assignedCrew = null;
            if (crewNameText != null)
                crewNameText.text = "— VACANT —";
            if (classLabelText != null)
                classLabelText.text = "";

            // 테두리를 sepia alpha 20%로 변경
            if (borderImage != null)
            {
                borderImage.color = new Color(201f / 255f, 165f / 255f, 116f / 255f, 0.2f); // #c9a574 + alpha
            }
        }

        public CrewClass GetSlotClass() => slotClass;
        public CrewMemberSO GetAssignedCrew() => assignedCrew;

        private void OnEnable()
        {
            if (slotButton != null)
                slotButton.onClick.AddListener(OnSlotClicked);
        }

        private void OnDisable()
        {
            if (slotButton != null)
                slotButton.onClick.RemoveListener(OnSlotClicked);
        }

        private void OnSlotClicked()
        {
            OnSlotClickedEvent?.Invoke(this);
        }
    }
}
