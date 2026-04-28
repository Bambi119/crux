using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Crux.Data;

namespace Crux.UI.Deployment
{
    /// <summary>
    /// 편성 씬 UI와 CrewDeploymentController를 연결하는 바인더.
    /// 슬롯 UI, Roster 동적 생성, 사기 미리보기, 저장 등을 관리한다.
    /// </summary>
    public class CrewDeploymentBinder : MonoBehaviour
    {
        [SerializeField] private CrewDeploymentController controller;

        [SerializeField] private CrewSlotView commanderSlot;
        [SerializeField] private CrewSlotView gunnerSlot;
        [SerializeField] private CrewSlotView loaderSlot;
        [SerializeField] private CrewSlotView driverSlot;
        [SerializeField] private CrewSlotView mgMechanicSlot;

        [SerializeField] private Transform rosterContainer;
        [SerializeField] private GameObject rosterCardPrefab;

        [SerializeField] private TextMeshProUGUI moralePreviewText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backButton;

        [SerializeField] private GameObject noHullOverlay;

        private CrewSlotView[] allSlots;
        private CrewMemberSO selectedCrewInRoster;
        private List<GameObject> rosterCardInstances = new();

        private void Awake()
        {
            allSlots = new[] { commanderSlot, gunnerSlot, loaderSlot, driverSlot, mgMechanicSlot };

            if (controller == null)
                controller = GetComponent<CrewDeploymentController>();

            Debug.Log("[CRUX] CrewDeploymentBinder.Awake");
        }

        private void Start()
        {
            Debug.Log("[CRUX] CrewDeploymentBinder.Start");

            // 슬롯 초기화 (CrewClass와 레이블 매칭)
            commanderSlot.Initialize(CrewClass.Commander, "Commander");
            gunnerSlot.Initialize(CrewClass.Gunner, "Gunner");
            loaderSlot.Initialize(CrewClass.Loader, "Loader");
            driverSlot.Initialize(CrewClass.Driver, "Driver");
            mgMechanicSlot.Initialize(CrewClass.GunnerMech, "Gunner Mech");

            // 슬롯 클릭 이벤트 구독
            commanderSlot.OnSlotClickedEvent += OnSlotClicked;
            gunnerSlot.OnSlotClickedEvent += OnSlotClicked;
            loaderSlot.OnSlotClickedEvent += OnSlotClicked;
            driverSlot.OnSlotClickedEvent += OnSlotClicked;
            mgMechanicSlot.OnSlotClickedEvent += OnSlotClicked;

            // 이벤트 구독
            controller.OnTankSelectionChanged += OnTankSelectionChanged;
            controller.OnAssignmentChanged += OnAssignmentChanged;

            // 초기 UI 구성
            RefreshUI();

            // 버튼 연결
            if (confirmButton != null)
                confirmButton.onClick.AddListener(() => controller.ConfirmDeployment());
            if (backButton != null)
                backButton.onClick.AddListener(() => controller.Back());
        }

        private void OnTankSelectionChanged()
        {
            Debug.Log("[CRUX] OnTankSelectionChanged");
            RefreshUI();
        }

        private void OnAssignmentChanged()
        {
            Debug.Log("[CRUX] OnAssignmentChanged");
            RefreshUI();
        }

        private void RefreshUI()
        {
            // 선택된 전차 확인
            if (controller.SelectedTank == null)
            {
                ShowNoHullOverlay(true);
                return;
            }

            ShowNoHullOverlay(false);

            // 각 슬롯 업데이트
            RefreshSlots();

            // Roster 목록 업데이트
            RefreshRoster();

            // 사기 미리보기 업데이트
            RefreshMoralePreview();
        }

        private void RefreshSlots()
        {
            CrewSlotView[] slots = { commanderSlot, gunnerSlot, loaderSlot, driverSlot, mgMechanicSlot };
            CrewClass[] classes = { CrewClass.Commander, CrewClass.Gunner, CrewClass.Loader, CrewClass.Driver, CrewClass.GunnerMech };

            for (int i = 0; i < slots.Length; i++)
            {
                CrewMemberSO crew = controller.GetSelectedAssignment(classes[i]);
                if (crew != null)
                    slots[i].SetAssigned(crew);
                else
                    slots[i].SetVacant();
            }
        }

        private void RefreshRoster()
        {
            // 기존 카드 제거
            foreach (var card in rosterCardInstances)
            {
                Destroy(card);
            }
            rosterCardInstances.Clear();

            if (rosterCardPrefab == null)
            {
                Debug.LogWarning("[CRUX] rosterCardPrefab is null");
                return;
            }

            // Roster 목록을 순회하며 카드 생성
            foreach (var crew in controller.Roster)
            {
                var cardObj = Instantiate(rosterCardPrefab, rosterContainer);
                var button = cardObj.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(() => OnRosterCardClicked(crew));
                }

                // 카드 텍스트 업데이트
                var crewNameText = cardObj.transform.Find("CrewName")?.GetComponent<TextMeshProUGUI>();
                var classText = cardObj.transform.Find("ClassLabel")?.GetComponent<TextMeshProUGUI>();

                if (crewNameText != null)
                    crewNameText.text = crew.displayName;
                if (classText != null)
                    classText.text = crew.klass.ToString();

                // 할당 여부 시각화
                bool assignedElsewhere = controller.IsAssignedElsewhere(crew, controller.SelectedTankIndex);
                bool assignedHere = IsCrewAssignedToSelectedTank(crew);

                var canvasGroup = cardObj.GetComponent<CanvasGroup>() ?? cardObj.AddComponent<CanvasGroup>();
                canvasGroup.alpha = (assignedElsewhere || assignedHere) ? 0.4f : 1.0f;
                canvasGroup.interactable = !(assignedElsewhere || assignedHere);

                rosterCardInstances.Add(cardObj);
            }
        }

        private bool IsCrewAssignedToSelectedTank(CrewMemberSO crew)
        {
            return controller.GetSelectedAssignment(CrewClass.Commander) == crew
                || controller.GetSelectedAssignment(CrewClass.Gunner) == crew
                || controller.GetSelectedAssignment(CrewClass.Loader) == crew
                || controller.GetSelectedAssignment(CrewClass.Driver) == crew
                || controller.GetSelectedAssignment(CrewClass.GunnerMech) == crew;
        }

        private void OnRosterCardClicked(CrewMemberSO crew)
        {
            selectedCrewInRoster = crew;
            Debug.Log($"[CRUX] Selected crew: {crew.displayName}");
        }

        private void OnSlotClicked(CrewSlotView slot)
        {
            if (selectedCrewInRoster == null)
            {
                // 이미 배치된 슬롯을 클릭 → 제거
                if (slot.GetAssignedCrew() != null)
                {
                    controller.RemoveCrew(slot.GetSlotClass());
                }
                return;
            }

            // Roster에서 선택한 crew를 이 slot에 할당
            bool ok = controller.TryAssignCrew(slot.GetSlotClass(), selectedCrewInRoster);
            if (ok)
            {
                selectedCrewInRoster = null;
                RefreshRoster(); // 하이라이트 갱신
            }
            else
            {
                Debug.LogWarning($"[CRUX] Assignment refused: {selectedCrewInRoster.displayName} → {slot.GetSlotClass()}");
            }
        }

        private void RefreshMoralePreview()
        {
            if (moralePreviewText == null)
                return;

            var breakdown = controller.PreviewMoraleBreakdown();
            string preview = $"Base {breakdown.baseVal} | Cmdr +{breakdown.commanderMark} | Traits +{breakdown.traitFloor} = {breakdown.total}";
            moralePreviewText.text = preview;
        }

        private void ShowNoHullOverlay(bool show)
        {
            if (noHullOverlay != null)
                noHullOverlay.SetActive(show);
        }

        private void OnDestroy()
        {
            // 슬롯 이벤트 언구독
            commanderSlot.OnSlotClickedEvent -= OnSlotClicked;
            gunnerSlot.OnSlotClickedEvent -= OnSlotClicked;
            loaderSlot.OnSlotClickedEvent -= OnSlotClicked;
            driverSlot.OnSlotClickedEvent -= OnSlotClicked;
            mgMechanicSlot.OnSlotClickedEvent -= OnSlotClicked;

            // 컨트롤러 이벤트 언구독
            controller.OnTankSelectionChanged -= OnTankSelectionChanged;
            controller.OnAssignmentChanged -= OnAssignmentChanged;
        }
    }
}
