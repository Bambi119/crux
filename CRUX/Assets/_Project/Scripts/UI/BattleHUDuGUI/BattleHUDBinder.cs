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

        // BannerPanel — 턴 전환 알림
        private GameObject bannerPanel;
        private TextMeshProUGUI bannerText;
        private Text bannerLegacyText;
        private Image bannerBackground;
        private TurnPhase prevPhase;
        private bool phaseInitialized;
        private float bannerEndTime;
        private const float BannerDuration = 1.8f;

        // 이동 AP 프리뷰 (Canvas 루트 오버레이 — UnitInfoCard 위에 배지 형태)
        private GameObject apCostPreviewRoot;
        private TextMeshProUGUI apCostPreviewText;

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

            // BannerPanel 캐시 — TMP와 레거시 Text 모두 대응 (프리팹이 레거시 Text로 만들어졌을 가능성)
            if (banner != null)
            {
                bannerPanel = banner.gameObject;
                var bannerTextTr = banner.Find("BannerText");
                if (bannerTextTr != null)
                {
                    bannerText = bannerTextTr.GetComponent<TextMeshProUGUI>();
                    bannerLegacyText = bannerTextTr.GetComponent<Text>();
                }
                bannerBackground = banner.GetComponent<Image>();

                // 프리팹에 하드코딩된 기본 문구(예: "적 격파!") 제거 — 배너가 꺼지기 전까지 남지 않도록
                if (bannerText != null) bannerText.text = string.Empty;
                if (bannerLegacyText != null) bannerLegacyText.text = string.Empty;

                bannerPanel.SetActive(false);
            }

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

                // AP 이동 프리뷰 텍스트 — 런타임 생성 (APStatusRow 하단)
                EnsureAPCostPreviewText(unitCard);
            }

            Debug.Log("[CRUX] BattleHUDBinder: 초기화 완료");
        }

        private void Update()
        {
            if (controller == null) return;

            DetectPhaseChange();
            UpdateTurnCounter();
            UpdateBanner();
            UpdateAmmoCounter();
            UpdateUnitInfoCard();
        }

        // === 턴 배너 ===

        private void DetectPhaseChange()
        {
            var phase = controller.CurrentPhase;

            if (!phaseInitialized)
            {
                prevPhase = phase;
                phaseInitialized = true;
                // 초기 페이즈도 배너로 예고 (EnemyTurn 선공 등 상황 안내)
                ShowTurnBannerFor(phase);
                return;
            }

            if (phase != prevPhase)
            {
                ShowTurnBannerFor(phase);
                prevPhase = phase;
            }
        }

        private void ShowTurnBannerFor(TurnPhase phase)
        {
            switch (phase)
            {
                case TurnPhase.PlayerTurn:
                    ShowTurnBanner("아군 턴 시작", UIColorPalette.PrimaryContainer);
                    break;
                case TurnPhase.EnemyTurn:
                    ShowTurnBanner("적 턴 시작", UIColorPalette.TertiaryContainer);
                    break;
                case TurnPhase.Victory:
                    ShowTurnBanner("승리", UIColorPalette.SecondaryContainer);
                    break;
                case TurnPhase.GameOver:
                    ShowTurnBanner("패배", UIColorPalette.TertiaryContainer);
                    break;
                // Cinematic는 배너 스킵
            }
        }

        private void ShowTurnBanner(string message, Color accent)
        {
            if (bannerPanel == null) return;
            bannerPanel.SetActive(true);
            if (bannerText != null)
            {
                bannerText.text = message;
                bannerText.color = accent;
            }
            if (bannerLegacyText != null)
            {
                bannerLegacyText.text = message;
                bannerLegacyText.color = accent;
            }
            if (bannerBackground != null)
            {
                // 배경 살짝 강조 — 알파만 올림
                var col = bannerBackground.color;
                col.a = 0.92f;
                bannerBackground.color = col;
            }
            bannerEndTime = Time.time + BannerDuration;
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
            if (bannerPanel == null) return;
            if (bannerEndTime > 0f && Time.time >= bannerEndTime)
            {
                bannerPanel.SetActive(false);
                bannerEndTime = 0f;
            }
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

            // AP 도트 — 이동 프리뷰 적용 (플레이어 유닛 + Move/MoveDirectionSelect 모드에서만)
            int previewCost = 0;
            if (selectedUnit.side == Core.PlayerSide.Player)
                previewCost = ComputeMoveCostPreview(selectedUnit);

            int currentAP = selectedUnit.CurrentAP;
            int previewStart = Mathf.Max(0, currentAP - previewCost); // 이 인덱스부터 currentAP-1까지가 소모 예고

            for (int i = 0; i < 6; i++)
            {
                if (apDots[i] == null) continue;
                var img = apDots[i].GetComponent<Image>();
                if (img == null) continue;

                if (i < currentAP)
                {
                    // 활성 도트 — 소모 예고 구간은 Tertiary, 나머지는 Primary
                    bool willConsume = (previewCost > 0) && (i >= previewStart);
                    img.color = willConsume ? UIColorPalette.TertiaryContainer : UIColorPalette.PrimaryContainer;
                }
                else
                {
                    // 비활성 도트
                    img.color = UIColorPalette.SurfaceContainerLowest;
                }
                img.fillAmount = 1f;
            }

            UpdateAPCostPreviewText(currentAP, previewCost);

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

        // === 이동 AP 프리뷰 ===

        private int ComputeMoveCostPreview(GridTankUnit selected)
        {
            var mode = controller.CurrentInputMode;

            // 방향 확정 대기 — 이미 계산된 경로 비용
            if (mode == BattleController.InputModeEnum.MoveDirectionSelect)
                return Mathf.Max(0, controller.PendingMoveCost);

            // 이동 선택 중 — 마우스 hover 셀까지 경로 비용
            if (mode == BattleController.InputModeEnum.Move && selected != null)
            {
                var cam = controller.MainCam;
                var grid = controller.Grid;
                if (cam == null || grid == null) return 0;

                Vector3 world = cam.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
                world.z = 0f;
                Vector2Int hoverCell = grid.WorldToGrid(world);

                if (hoverCell == selected.GridPosition) return 0;

                var path = grid.FindPath(selected.GridPosition, hoverCell);
                if (path == null || path.Count <= 1) return 0;

                int cost = (path.Count - 1) * selected.GetMoveCostPerCell();
                return Mathf.Min(cost, selected.CurrentAP); // AP 초과는 클램프 (도트 표시 안전)
            }

            return 0;
        }

        private void EnsureAPCostPreviewText(Transform unitCard)
        {
            if (apCostPreviewRoot != null) return;
            if (unitCard == null || unitCard.parent == null) return;

            // Canvas 루트에 생성 — UnitInfoCard(좌하단 24,24)의 바로 위에 배치
            Transform canvasRoot = unitCard.parent;

            apCostPreviewRoot = new GameObject("APCostPreviewBadge", typeof(RectTransform));
            apCostPreviewRoot.transform.SetParent(canvasRoot, false);

            var rt = apCostPreviewRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            // UnitInfoCard = 24,24 + size 320x180 → 카드 바로 위에 8px 간격
            rt.anchoredPosition = new Vector2(24f, 212f);
            rt.sizeDelta = new Vector2(240f, 30f);

            // 배경 (반투명 패널 + 액센트)
            var bg = apCostPreviewRoot.AddComponent<Image>();
            var bgColor = UIColorPalette.SurfaceContainerHigh;
            bgColor.a = 0.92f;
            bg.color = bgColor;
            bg.raycastTarget = false;

            // 텍스트 자식 — 한글 없이 숫자/기호 중심
            var txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(apCostPreviewRoot.transform, false);
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(10f, 0f);
            txtRt.offsetMax = new Vector2(-10f, 0f);

            apCostPreviewText = txtGo.AddComponent<TextMeshProUGUI>();
            apCostPreviewText.fontSize = 16f;
            apCostPreviewText.alignment = TextAlignmentOptions.Left;
            apCostPreviewText.color = UIColorPalette.OnSurface;
            apCostPreviewText.text = string.Empty;
            apCostPreviewText.raycastTarget = false;

            // 기존 유닛명 폰트 상속 (있으면 SpaceGrotesk)
            if (unitName != null && unitName.font != null)
                apCostPreviewText.font = unitName.font;

            apCostPreviewRoot.SetActive(false);
        }

        private void UpdateAPCostPreviewText(int currentAP, int previewCost)
        {
            if (apCostPreviewRoot == null || apCostPreviewText == null) return;

            if (previewCost <= 0)
            {
                apCostPreviewRoot.SetActive(false);
                return;
            }

            apCostPreviewRoot.SetActive(true);
            int remaining = currentAP - previewCost;
            bool canAfford = remaining >= 0;

            string txt = canAfford
                ? $"MOVE  -{previewCost} AP   ({remaining}/{currentAP} left)"
                : $"LOW AP  ({previewCost} / {currentAP})";

            apCostPreviewText.text = txt;
            apCostPreviewText.color = canAfford
                ? UIColorPalette.OnSurfaceVariant
                : UIColorPalette.TertiaryContainer;
        }
    }
}
