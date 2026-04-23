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

        // UnitInfoCard — C안 (상단뷰 실루엣 + 좌우 파츠 바 + 하단 뱃지)
        private GameObject unitCardRoot;

        // Header row (UnitName, HPText, APText)
        private TextMeshProUGUI unitNameText;
        private TextMeshProUGUI hpText;
        private TextMeshProUGUI apText;

        // Body row — Left bars (Hull, Engine, Track)
        private Image hullBarFill;
        private Image engineBarFill;
        private Image trackBarFill;
        private TextMeshProUGUI hullBarText;
        private TextMeshProUGUI engineBarText;
        private TextMeshProUGUI trackBarText;

        // Body row — Center silhouette
        private Image silhouetteTint;

        // Body row — Right bars (Turret, Barrel, MG)
        private Image turretBarFill;
        private Image barrelBarFill;
        private Image mgBarFill;
        private TextMeshProUGUI turretBarText;
        private TextMeshProUGUI barrelBarText;
        private TextMeshProUGUI mgBarText;

        // Footer badges (Morale, Overwatch, Fire, Misses)
        private TextMeshProUGUI moraleBadge;
        private TextMeshProUGUI overwatchBadge;
        private TextMeshProUGUI fireBadge;
        private TextMeshProUGUI missBadge;

        // InputModePanel — 플레이어 입력 상태 표시
        private GameObject inputModePanel;
        private TextMeshProUGUI inputModeText;

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

            // UnitInfoCard 캐시 — C안 (320×110) 구조
            if (unitCard != null)
            {
                unitCardRoot = unitCard.gameObject;

                // HeaderRow (UnitNameText, HPText, APText) — 24px 높이
                var headerRow = unitCard.Find("HeaderRow");
                if (headerRow != null)
                {
                    unitNameText = headerRow.Find("UnitNameText")?.GetComponent<TextMeshProUGUI>();
                    hpText = headerRow.Find("HPText")?.GetComponent<TextMeshProUGUI>();
                    apText = headerRow.Find("APText")?.GetComponent<TextMeshProUGUI>();
                }

                // BodyRow — 64px 높이
                var bodyRow = unitCard.Find("BodyRow");
                if (bodyRow != null)
                {
                    // LeftBars (HullBar, EngineBar, TrackBar)
                    var leftBars = bodyRow.Find("LeftBars");
                    if (leftBars != null)
                    {
                        var hullBar = leftBars.Find("HullBar");
                        if (hullBar != null)
                        {
                            hullBarFill = hullBar.Find("HullBarFill")?.GetComponent<Image>();
                            hullBarText = hullBar.Find("HullBarText")?.GetComponent<TextMeshProUGUI>();
                        }

                        var engineBar = leftBars.Find("EngineBar");
                        if (engineBar != null)
                        {
                            engineBarFill = engineBar.Find("EngineBarFill")?.GetComponent<Image>();
                            engineBarText = engineBar.Find("EngineBarText")?.GetComponent<TextMeshProUGUI>();
                        }

                        var trackBar = leftBars.Find("TrackBar");
                        if (trackBar != null)
                        {
                            trackBarFill = trackBar.Find("TrackBarFill")?.GetComponent<Image>();
                            trackBarText = trackBar.Find("TrackBarText")?.GetComponent<TextMeshProUGUI>();
                        }
                    }

                    // CenterSilhouette (Image) — 140×64
                    var centerSilhouette = bodyRow.Find("CenterSilhouette");
                    if (centerSilhouette != null)
                    {
                        silhouetteTint = centerSilhouette.GetComponent<Image>();
                    }

                    // RightBars (TurretBar, BarrelBar, MGBar)
                    var rightBars = bodyRow.Find("RightBars");
                    if (rightBars != null)
                    {
                        var turretBar = rightBars.Find("TurretBar");
                        if (turretBar != null)
                        {
                            turretBarFill = turretBar.Find("TurretBarFill")?.GetComponent<Image>();
                            turretBarText = turretBar.Find("TurretBarText")?.GetComponent<TextMeshProUGUI>();
                        }

                        var barrelBar = rightBars.Find("BarrelBar");
                        if (barrelBar != null)
                        {
                            barrelBarFill = barrelBar.Find("BarrelBarFill")?.GetComponent<Image>();
                            barrelBarText = barrelBar.Find("BarrelBarText")?.GetComponent<TextMeshProUGUI>();
                        }

                        var mgBar = rightBars.Find("MGBar");
                        if (mgBar != null)
                        {
                            mgBarFill = mgBar.Find("MGBarFill")?.GetComponent<Image>();
                            mgBarText = mgBar.Find("MGBarText")?.GetComponent<TextMeshProUGUI>();
                        }
                    }
                }

                // FooterBadges (MoraleBadge, OverwatchBadge, FireBadge, MissBadge) — 20px 높이
                var footerBadges = unitCard.Find("FooterBadges");
                if (footerBadges != null)
                {
                    moraleBadge = footerBadges.Find("MoraleBadge")?.GetComponent<TextMeshProUGUI>();
                    overwatchBadge = footerBadges.Find("OverwatchBadge")?.GetComponent<TextMeshProUGUI>();
                    fireBadge = footerBadges.Find("FireBadge")?.GetComponent<TextMeshProUGUI>();
                    missBadge = footerBadges.Find("MissBadge")?.GetComponent<TextMeshProUGUI>();
                }
            }

            // InputModePanel 캐시 (선택적 — 있으면 사용, 없으면 무시)
            var inputPanel = turnCounter?.parent?.Find("InputModePanel");
            if (inputPanel != null)
            {
                inputModePanel = inputPanel.gameObject;
                inputModeText = inputPanel.Find("InputModeText")?.GetComponent<TextMeshProUGUI>();
                if (inputModeText == null)
                    inputModePanel = null; // Text 컴포넌트 없으면 전체 무시
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
            UpdateInputMode();
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
            if (bannerPanel == null || controller == null) return;

            // 현재 배너 타이머 확인 — 만료 시 다음 항목으로
            if (bannerEndTime > 0f && Time.time >= bannerEndTime)
            {
                bannerPanel.SetActive(false);
                bannerEndTime = 0f;
            }

            // 큐 폴링 — 배너가 표시되지 않고 있을 때만 다음 항목 꺼냄
            if (bannerEndTime <= 0f && controller.BannerQueue.Count > 0)
            {
                var entry = controller.BannerQueue[0];
                controller.BannerQueue.RemoveAt(0);

                // 배너 렌더링
                bannerPanel.SetActive(true);
                if (bannerText != null)
                {
                    bannerText.text = entry.text;
                    bannerText.color = entry.color;
                }
                if (bannerLegacyText != null)
                {
                    bannerLegacyText.text = entry.text;
                    bannerLegacyText.color = entry.color;
                }
                if (bannerBackground != null)
                    bannerBackground.color = new Color(entry.color.r, entry.color.g, entry.color.b, 0.7f);

                bannerEndTime = entry.endTime;
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

            // ===== Header Row =====
            if (unitNameText != null)
                unitNameText.text = selectedUnit.Data != null ? selectedUnit.Data.tankName : "Unknown";

            int currentHPInt = Mathf.CeilToInt(selectedUnit.CurrentHP);
            int maxHPInt = Mathf.CeilToInt(selectedUnit.Data != null ? selectedUnit.Data.maxHP : 100);

            if (hpText != null)
                hpText.text = $"HP: {currentHPInt}/{maxHPInt}";

            if (apText != null)
                apText.text = $"AP: {selectedUnit.CurrentAP}/{selectedUnit.MaxAP}";

            // ===== Body Row — Parts Progress Bars =====
            // NOTE: ModuleType enum has no Hull/Track — using Engine and CaterpillarLeft as placeholders
            UpdatePartsBar(hullBarFill, hullBarText, selectedUnit, ModuleType.Engine, "Engine");
            UpdatePartsBar(engineBarFill, engineBarText, selectedUnit, ModuleType.Engine, "Engine");
            UpdatePartsBar(trackBarFill, trackBarText, selectedUnit, ModuleType.CaterpillarLeft, "Track");
            UpdatePartsBar(turretBarFill, turretBarText, selectedUnit, ModuleType.TurretRing, "Turret");
            UpdatePartsBar(barrelBarFill, barrelBarText, selectedUnit, ModuleType.Barrel, "Barrel");
            UpdatePartsBar(mgBarFill, mgBarText, selectedUnit, ModuleType.MachineGun, "MG");

            // Silhouette tint — overall unit HP %
            if (silhouetteTint != null)
            {
                float hpPercent = maxHPInt > 0 ? selectedUnit.CurrentHP / maxHPInt : 0f;
                Color tintColor = GetHealthTintColor(hpPercent);
                silhouetteTint.color = tintColor;
            }

            // ===== Footer Badges =====
            // Morale badge
            if (moraleBadge != null)
            {
                moraleBadge.text = selectedUnit.Crew != null ? GetMoraleBandLabel(selectedUnit.Crew.Band) : "—";
                moraleBadge.color = selectedUnit.Crew != null ? GetMoraleColor(selectedUnit.Crew.Band) : Color.white;
            }

            // Overwatch badge (조건부)
            if (overwatchBadge != null)
            {
                overwatchBadge.gameObject.SetActive(selectedUnit.IsOverwatching);
                if (selectedUnit.IsOverwatching)
                    overwatchBadge.text = "反击";
            }

            // Fire badge (조건부 — Fire turns remaining)
            if (fireBadge != null)
            {
                bool hasFireTurns = selectedUnit.FireTurnsLeft > 0;
                fireBadge.gameObject.SetActive(hasFireTurns);
                if (hasFireTurns)
                    fireBadge.text = $"Fire {selectedUnit.FireTurnsLeft}T";
            }

            // Misses badge (조건부 — consecutive misses)
            if (missBadge != null)
            {
                bool hasMisses = selectedUnit.ConsecutiveMisses > 0;
                missBadge.gameObject.SetActive(hasMisses);
                if (hasMisses)
                    missBadge.text = $"Miss ×{selectedUnit.ConsecutiveMisses}";
            }
        }

        private void UpdatePartsBar(Image fillImage, TextMeshProUGUI labelText, GridTankUnit unit, ModuleType moduleType, string label)
        {
            if (fillImage == null) return;

            var module = unit.Modules.Get(moduleType);
            float hpPercent = 0f;

            if (module != null && module.maxHP > 0)
                hpPercent = Mathf.Clamp01(module.currentHP / (float)module.maxHP);

            // Bar fill amount (0~1 range)
            fillImage.fillAmount = hpPercent;

            // Bar color based on health %
            Color barColor = GetHealthTintColor(hpPercent);
            fillImage.color = barColor;

            // Label text (if text component exists)
            if (labelText != null)
                labelText.text = module != null ? $"{Mathf.CeilToInt(module.currentHP)}/{Mathf.CeilToInt(module.maxHP)}" : "0/0";
        }

        private Color GetHealthTintColor(float healthPercent)
        {
            // Green (>60%) → Orange (30~60%) → Red (≤30%)
            if (healthPercent > 0.6f)
                return new Color(0.3f, 0.9f, 0.3f); // Green
            else if (healthPercent > 0.3f)
                return new Color(1f, 0.6f, 0.2f); // Orange
            else
                return new Color(1f, 0.2f, 0.2f); // Red
        }


        /// <summary>사기 대역 라벨 반환</summary>
        private string GetMoraleBandLabel(MoraleBand band)
        {
            return band switch
            {
                MoraleBand.High => "양호",
                MoraleBand.Normal => "정상",
                MoraleBand.Shaken => "동요",
                MoraleBand.Panic => "공황",
                _ => "?"
            };
        }

        /// <summary>사기 대역에 따른 색상 반환</summary>
        private Color GetMoraleColor(MoraleBand band)
        {
            return band switch
            {
                MoraleBand.High => new Color(0.2f, 0.8f, 0.3f), // 녹색
                MoraleBand.Normal => new Color(0.9f, 0.9f, 0.3f), // 노란색
                MoraleBand.Shaken => new Color(1f, 0.6f, 0.2f), // 주황색
                MoraleBand.Panic => new Color(1f, 0.2f, 0.2f), // 빨간색
                _ => Color.white
            };
        }

        private void UpdateInputMode()
        {
            if (inputModePanel == null || inputModeText == null) return;

            var mode = controller.CurrentInputMode;
            string modeLabel = mode switch
            {
                BattleController.InputModeEnum.Select => "선택 대기",
                BattleController.InputModeEnum.Move => "이동 선택",
                BattleController.InputModeEnum.MoveDirectionSelect => "방향 확인",
                BattleController.InputModeEnum.Fire => "사격 준비",
                BattleController.InputModeEnum.WeaponSelect => "무기 선택",
                _ => "?"
            };

            inputModeText.text = modeLabel;

            // 모드별 색상
            Color modeColor = mode switch
            {
                BattleController.InputModeEnum.Select => UIColorPalette.OnSurface,
                BattleController.InputModeEnum.Move => UIColorPalette.PrimaryContainer,
                BattleController.InputModeEnum.MoveDirectionSelect => UIColorPalette.PrimaryContainer,
                BattleController.InputModeEnum.Fire => UIColorPalette.TertiaryContainer,
                BattleController.InputModeEnum.WeaponSelect => UIColorPalette.SecondaryContainer,
                _ => UIColorPalette.OnSurfaceVariant
            };

            inputModeText.color = modeColor;
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
            if (unitNameText != null && unitNameText.font != null)
                apCostPreviewText.font = unitNameText.font;

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
