using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Crux.Core;
using Crux.Unit;
using Crux.Data;

namespace Crux.UI
{
    /// <summary>
    /// BattleHUD uGUI 데이터 바인더 — 매 프레임 BattleController 상태를 UI에 반영.
    /// TurnCounterPanel, BannerPanel, AmmoCounterPanel, UnitInfoCard 담당.
    /// </summary>
    public class BattleHUDBinder : MonoBehaviour
    {
        private BattleController controller;

        // TurnCounterPanel
        private TextMeshProUGUI turnText;
        private TextMeshProUGUI playerTurnText;
        private Image statusDot;

        // BannerPanel (1차: 숨김 상태)
        private GameObject bannerPanel;

        // AmmoCounterPanel
        private TextMeshProUGUI ammoLabel;
        private TextMeshProUGUI ammoCount;
        private GameObject ammoPanelRoot;

        // UnitInfoCard
        private TextMeshProUGUI unitName;
        private TextMeshProUGUI unitID;
        private TextMeshProUGUI hpValue;
        private RectTransform hpFill;
        private GameObject[] apDots = new GameObject[6];
        private GameObject fireBadge;
        private GameObject smokeBadge;
        private Image[] moduleDots = new Image[4]; // Engine, Barrel, MachineGun, TurretRing
        private GameObject unitCardRoot;

        // 모듈 타입 맵 (순서: Engine, Barrel, MachineGun, TurretRing)
        private readonly ModuleType[] moduleTypesForUI = new[]
        {
            ModuleType.Engine,
            ModuleType.Barrel,
            ModuleType.MachineGun,
            ModuleType.TurretRing
        };

        public void Initialize(BattleController controller, Transform turnCounter, Transform banner, Transform ammo, Transform unitCard)
        {
            this.controller = controller;

            // TurnCounterPanel 캐시
            if (turnCounter != null)
            {
                turnText = turnCounter.Find("TurnText")?.GetComponent<TextMeshProUGUI>();
                var playerTurnLabel = turnCounter.Find("PlayerTurnLabel");
                if (playerTurnLabel != null)
                {
                    playerTurnText = playerTurnLabel.Find("PlayerTurnText")?.GetComponent<TextMeshProUGUI>();
                    statusDot = playerTurnLabel.Find("StatusDot")?.GetComponent<Image>();
                }
            }

            // BannerPanel 캐시
            if (banner != null)
                bannerPanel = banner.gameObject;

            // AmmoCounterPanel 캐시
            if (ammo != null)
            {
                ammoPanelRoot = ammo.gameObject;
                ammoLabel = ammo.Find("AmmoLabel")?.GetComponent<TextMeshProUGUI>();
                ammoCount = ammo.Find("AmmoCount")?.GetComponent<TextMeshProUGUI>();
            }

            // UnitInfoCard 캐시
            if (unitCard != null)
            {
                unitCardRoot = unitCard.gameObject;

                var header = unitCard.Find("Header");
                if (header != null)
                {
                    unitName = header.Find("UnitName")?.GetComponent<TextMeshProUGUI>();
                    unitID = header.Find("UnitID")?.GetComponent<TextMeshProUGUI>();
                }

                var hpContainer = unitCard.Find("HPContainer");
                if (hpContainer != null)
                {
                    var hpLabelRow = hpContainer.Find("HPLabelRow");
                    if (hpLabelRow != null)
                        hpValue = hpLabelRow.Find("HPValue")?.GetComponent<TextMeshProUGUI>();

                    var hpBar = hpContainer.Find("HPBar");
                    if (hpBar != null)
                        hpFill = hpBar.Find("HPFill")?.GetComponent<RectTransform>();
                }

                var apStatusRow = unitCard.Find("APStatusRow");
                if (apStatusRow != null)
                {
                    var apSection = apStatusRow.Find("APSection");
                    if (apSection != null)
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            apDots[i] = apSection.Find($"APDot{i + 1}")?.gameObject;
                        }
                    }

                    var statusBadgeSection = apStatusRow.Find("StatusBadgeSection");
                    if (statusBadgeSection != null)
                    {
                        fireBadge = statusBadgeSection.Find("FireBadge")?.gameObject;
                        smokeBadge = statusBadgeSection.Find("SmokeBadge")?.gameObject;
                    }
                }

                var moduleGrid = unitCard.Find("ModuleGrid");
                if (moduleGrid != null)
                {
                    // Engine, Barrel, MachineGun, TurretRing 순서
                    string[] moduleCellNames = { "EngineCell", "BarrelCell", "MachineGunCell", "TurretRingCell" };
                    for (int i = 0; i < 4; i++)
                    {
                        var cell = moduleGrid.Find(moduleCellNames[i]);
                        if (cell != null)
                            moduleDots[i] = cell.Find($"{moduleCellNames[i].Replace("Cell", "Dot")}")?.GetComponent<Image>();
                    }
                }
            }

            Debug.Log("[CRUX] BattleHUDBinder: 초기화 완료");
        }

        private void Update()
        {
            if (controller == null) return;

            UpdateTurnCounter();
            UpdateBanner();
            UpdateAmmoCounter();
            UpdateUnitInfoCard();
        }

        private void UpdateTurnCounter()
        {
            if (turnText != null)
                turnText.text = $"턴 {controller.TurnCount}";

            if (playerTurnText != null)
                playerTurnText.text = controller.CurrentPhase == TurnPhase.PlayerTurn ? "플레이어 턴" : "적 턴";

            if (statusDot != null)
                statusDot.color = controller.CurrentPhase == TurnPhase.PlayerTurn
                    ? UIColorPalette.SecondaryContainer
                    : UIColorPalette.TertiaryContainer;
        }

        private void UpdateBanner()
        {
            // Phase 1: Banner은 숨김 상태로 둠. ShowBanner 큐 소비는 후속 단계에서 구현
            if (bannerPanel != null && bannerPanel.activeInHierarchy)
                bannerPanel.SetActive(false);
        }

        private void UpdateAmmoCounter()
        {
            var selectedUnit = controller.SelectedUnit;

            if (ammoPanelRoot == null) return;

            if (selectedUnit == null)
            {
                ammoPanelRoot.SetActive(false);
                return;
            }

            ammoPanelRoot.SetActive(true);

            if (ammoCount != null)
                ammoCount.text = $"×{selectedUnit.MainGunAmmoCount}";

            if (ammoLabel != null)
                ammoLabel.text = "AP탄";
        }

        private void UpdateUnitInfoCard()
        {
            var selectedUnit = controller.SelectedUnit;

            if (unitCardRoot == null) return;

            if (selectedUnit == null)
            {
                unitCardRoot.SetActive(false);
                return;
            }

            unitCardRoot.SetActive(true);

            // Unit 이름 및 ID
            if (unitName != null)
                unitName.text = selectedUnit.Data != null ? selectedUnit.Data.tankName : "Unknown";

            if (unitID != null)
                unitID.text = $"{(selectedUnit.side == Core.PlayerSide.Player ? "아군" : "적")}";

            // HP 표시 및 바 렌더링
            int currentHPInt = Mathf.CeilToInt(selectedUnit.CurrentHP);
            int maxHPInt = Mathf.CeilToInt(selectedUnit.Data != null ? selectedUnit.Data.maxHP : 100);

            if (hpValue != null)
                hpValue.text = $"HP {currentHPInt}/{maxHPInt}";

            if (hpFill != null)
            {
                // HPFill의 부모가 300 픽셀 너비라 가정. HPFill.sizeDelta.x 또는 anchorMax.x로 조절
                float hpRatio = maxHPInt > 0 ? selectedUnit.CurrentHP / selectedUnit.Data.maxHP : 0f;
                hpRatio = Mathf.Clamp01(hpRatio);

                // sizeDelta.x 방식 시도
                Vector2 sizeDelta = hpFill.sizeDelta;
                sizeDelta.x = 300f * hpRatio; // 부모가 300 너비 기준
                hpFill.sizeDelta = sizeDelta;
            }

            // AP 도트
            for (int i = 0; i < 6; i++)
            {
                if (apDots[i] == null) continue;

                if (i < selectedUnit.CurrentAP)
                {
                    // 활성 도트 — 색상 채우기
                    var img = apDots[i].GetComponent<Image>();
                    if (img != null)
                    {
                        img.color = UIColorPalette.PrimaryContainer;
                        img.fillAmount = 1f;
                    }
                }
                else
                {
                    // 비활성 도트 — 배경색 + outline
                    var img = apDots[i].GetComponent<Image>();
                    if (img != null)
                    {
                        img.color = UIColorPalette.SurfaceContainerLowest;
                        img.fillAmount = 1f;
                    }
                }
            }

            // 상태 배지
            if (fireBadge != null)
                fireBadge.SetActive(selectedUnit.IsOnFire);

            if (smokeBadge != null)
                smokeBadge.SetActive(selectedUnit.RemainingSmokeCharges == 0);

            // 모듈 도트
            UpdateModuleDots(selectedUnit);
        }

        private void UpdateModuleDots(GridTankUnit unit)
        {
            for (int i = 0; i < 4; i++)
            {
                if (moduleDots[i] == null) continue;

                var module = unit.Modules.Get(moduleTypesForUI[i]);
                Color dotColor = GetModuleColor(module?.state ?? ModuleState.Normal);
                moduleDots[i].color = dotColor;
            }
        }

        private Color GetModuleColor(ModuleState state)
        {
            return state switch
            {
                ModuleState.Normal => UIColorPalette.SecondaryContainer,
                ModuleState.Damaged => UIColorPalette.PrimaryContainer,
                ModuleState.Broken => UIColorPalette.TertiaryContainer,
                ModuleState.Destroyed => UIColorPalette.TertiaryContainer,
                _ => UIColorPalette.OnSurfaceVariant
            };
        }
    }
}
