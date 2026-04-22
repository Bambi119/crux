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

        // New P-B battle status fields
        private TextMeshProUGUI moraleValue;
        private TextMeshProUGUI moraleband;
        private TextMeshProUGUI mgAmmoText;
        private TextMeshProUGUI fireTurnsText;
        private GameObject fireTurnsRow;
        private TextMeshProUGUI missesText;
        private GameObject missesRow;
        private Image overwatchBadge;
        private GameObject overwatchBadgeContainer;
        private TextMeshProUGUI crewStatusText;

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
                    // Engine, Barrel, MachineGun, TurretRing 순서와 실제 프리팹 셀 이름 매핑:
                    // Engine → EngineCell / Barrel(주포) → GunCell / MachineGun → TrackCell(기총 좌측) / TurretRing → TurretCell
                    string[] moduleCellNames = { "EngineCell", "GunCell", "TrackCell", "TurretCell" };
                    for (int i = 0; i < 4; i++)
                    {
                        var cell = moduleGrid.Find(moduleCellNames[i]);
                        if (cell != null)
                            moduleDots[i] = cell.Find($"{moduleCellNames[i].Replace("Cell", "Dot")}")?.GetComponent<Image>();
                    }
                }

                // P-B: 새로운 전투 상태 필드 생성 (프리팹에 없을 경우)
                EnsurePBattleStatusRows(unitCard);

                // P-B: 새로운 전투 상태 필드 캐싱 (Morale, MG Ammo, Fire Turns, Consecutive Misses, Overwatch, Crew Status)
                var moraleRow = unitCard.Find("MoraleRow");
                if (moraleRow != null)
                {
                    moraleValue = moraleRow.Find("MoraleValue")?.GetComponent<TextMeshProUGUI>();
                    moraleband = moraleRow.Find("MoraleBand")?.GetComponent<TextMeshProUGUI>();
                }

                var mgAmmoRow = unitCard.Find("MGAmmoRow");
                if (mgAmmoRow != null)
                {
                    mgAmmoText = mgAmmoRow.Find("MGAmmoText")?.GetComponent<TextMeshProUGUI>();
                }

                fireTurnsRow = unitCard.Find("FireTurnsRow")?.gameObject;
                if (fireTurnsRow != null)
                {
                    fireTurnsText = fireTurnsRow.transform.Find("FireTurnsText")?.GetComponent<TextMeshProUGUI>();
                }

                missesRow = unitCard.Find("MissesRow")?.gameObject;
                if (missesRow != null)
                {
                    missesText = missesRow.transform.Find("MissesText")?.GetComponent<TextMeshProUGUI>();
                }

                var overwatchRow = unitCard.Find("OverwatchRow");
                if (overwatchRow != null)
                {
                    overwatchBadgeContainer = overwatchRow.gameObject;
                    overwatchBadge = overwatchRow.Find("OverwatchBadge")?.GetComponent<Image>();
                }

                var crewStatusRow = unitCard.Find("CrewStatusRow");
                if (crewStatusRow != null)
                {
                    crewStatusText = crewStatusRow.Find("CrewStatusText")?.GetComponent<TextMeshProUGUI>();
                }

                // AP 이동 프리뷰 텍스트 — 런타임 생성 (APStatusRow 하단)
                EnsureAPCostPreviewText(unitCard);
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

            // P-B: 새로운 전투 상태 필드 바인딩
            UpdateBattleStatusFields(selectedUnit);

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

        /// <summary>
        /// P-B: 프리팹에서 누락된 전투 상태 행들을 런타임에 생성
        /// MoraleRow, MGAmmoRow, FireTurnsRow, MissesRow, OverwatchRow, CrewStatusRow가 없으면 생성
        /// </summary>
        private void EnsurePBattleStatusRows(Transform unitCard)
        {
            if (unitCard == null) return;

            // 각 행에 대해 이미 존재하면 스킵, 없으면 생성
            EnsureRow(unitCard, "MoraleRow", 2); // MoraleLabel, MoraleValue, MoraleBand
            EnsureRow(unitCard, "MGAmmoRow", 2); // MGAmmoLabel, MGAmmoText
            EnsureRow(unitCard, "FireTurnsRow", 2); // FireTurnsLabel, FireTurnsText
            EnsureRow(unitCard, "MissesRow", 2); // MissesLabel, MissesText
            EnsureRow(unitCard, "OverwatchRow", 1); // OverwatchBadge
            EnsureRow(unitCard, "CrewStatusRow", 2); // CrewStatusLabel, CrewStatusText
        }

        /// <summary>
        /// 행 생성 헬퍼 — 없으면 생성, 있으면 무시
        /// </summary>
        private void EnsureRow(Transform unitCard, string rowName, int childCount)
        {
            if (unitCard.Find(rowName) != null)
                return; // 이미 존재

            GameObject row = new GameObject(rowName);
            row.transform.SetParent(unitCard, false);

            // RectTransform 설정
            RectTransform rowRT = row.AddComponent<RectTransform>();
            rowRT.anchorMin = Vector2.zero;
            rowRT.anchorMax = new Vector2(1f, 0f); // 상단 고정, 너비는 부모 기준
            rowRT.offsetMin = Vector2.zero;
            rowRT.offsetMax = Vector2.zero;
            rowRT.sizeDelta = new Vector2(0f, 25f); // 높이 25, 너비는 부모 기준

            // LayoutGroup 추가
            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = false;
            hlg.spacing = 5f;
            hlg.padding = new RectOffset(5, 5, 0, 0);

            // 자식 생성 (label + value(s) 패턴)
            CreateRowChild(row.transform, $"{rowName}Label", true); // Label은 좌측, flexible width 최소
            for (int i = 1; i < childCount; i++)
            {
                CreateRowChild(row.transform, GetChildNameForRow(rowName, i), false); // 값 필드들
            }
        }

        /// <summary>
        /// 행의 자식 GameObject 생성 (TextMeshProUGUI 또는 Image)
        /// </summary>
        private void CreateRowChild(Transform parentRow, string childName, bool isLabel)
        {
            GameObject child = new GameObject(childName);
            child.transform.SetParent(parentRow, false);

            RectTransform childRT = child.AddComponent<RectTransform>();
            childRT.anchorMin = Vector2.zero;
            childRT.anchorMax = Vector2.one;
            childRT.offsetMin = Vector2.zero;
            childRT.offsetMax = Vector2.zero;

            // Label인 경우 위에 Image로 표시, 아니면 TextMeshProUGUI 또는 Image로
            if (childName.Contains("Badge"))
            {
                // Badge는 Image
                Image img = child.AddComponent<Image>();
                img.color = new Color(0.2f, 0.7f, 1f); // 파란 배지 기본색
            }
            else
            {
                // 나머지는 TextMeshProUGUI
                TextMeshProUGUI tmp = child.AddComponent<TextMeshProUGUI>();
                tmp.text = isLabel ? GetLabelText(childName) : "0";
                tmp.fontSize = 3;
                tmp.alignment = TextAlignmentOptions.BottomLeft;
                tmp.color = Color.white;
            }

            // LayoutElement — Label은 preferred width를 제한하여 공간 절약
            LayoutElement le = child.AddComponent<LayoutElement>();
            if (isLabel)
            {
                le.preferredWidth = 80f;
                le.flexibleWidth = 0f;
            }
            else
            {
                le.preferredWidth = -1f; // 자동
                le.flexibleWidth = 1f;
            }
            le.preferredHeight = 25f;
            le.flexibleHeight = 0f;
        }

        /// <summary>행 타입에 따른 자식 이름 생성</summary>
        private string GetChildNameForRow(string rowName, int index)
        {
            return rowName switch
            {
                "MoraleRow" => index == 1 ? "MoraleValue" : "MoraleBand",
                "MGAmmoRow" => "MGAmmoText",
                "FireTurnsRow" => "FireTurnsText",
                "MissesRow" => "MissesText",
                "OverwatchRow" => "OverwatchBadge",
                "CrewStatusRow" => "CrewStatusText",
                _ => $"Child{index}"
            };
        }

        /// <summary>라벨 텍스트 반환</summary>
        private string GetLabelText(string fieldName)
        {
            return fieldName switch
            {
                "MoraleRowLabel" => "사기",
                "MGAmmoRowLabel" => "기총탄",
                "FireTurnsRowLabel" => "사격대기",
                "MissesRowLabel" => "연속실탄",
                "OverwatchRowLabel" => "반격",
                "CrewStatusRowLabel" => "승무원",
                _ => ""
            };
        }

        /// <summary>
        /// P-B: 새로운 전투 상태 필드 바인딩 (사기, MG탄약, 사격대기, 연속실탄, 반격준비, 승무원 상태)
        /// </summary>
        private void UpdateBattleStatusFields(GridTankUnit unit)
        {
            if (unit?.Crew == null) return;

            // 사기 및 사기대 렌더링
            if (moraleValue != null)
            {
                moraleValue.text = unit.Crew.Morale.ToString();
                moraleValue.color = GetMoraleColor(unit.Crew.Band);
            }

            if (moraleband != null)
            {
                moraleband.text = GetMoraleBandLabel(unit.Crew.Band);
                moraleband.color = GetMoraleColor(unit.Crew.Band);
            }

            // MG 탄약 표시
            if (mgAmmoText != null)
            {
                mgAmmoText.text = $"{unit.MGAmmoLoaded}/{unit.MGAmmoTotal}";
            }

            // 사격 대기 턴 (조건부 표시)
            if (fireTurnsRow != null)
            {
                bool hasFireTurns = unit.FireTurnsLeft > 0;
                fireTurnsRow.SetActive(hasFireTurns);
                if (hasFireTurns && fireTurnsText != null)
                {
                    fireTurnsText.text = $"{unit.FireTurnsLeft}턴";
                }
            }

            // 연속 실탄 (조건부 표시)
            if (missesRow != null)
            {
                bool hasMisses = unit.ConsecutiveMisses > 0;
                missesRow.SetActive(hasMisses);
                if (hasMisses && missesText != null)
                {
                    missesText.text = $"{unit.ConsecutiveMisses}회";
                }
            }

            // 반격 준비 배지 (조건부 표시)
            if (overwatchBadgeContainer != null)
            {
                overwatchBadgeContainer.SetActive(unit.IsOverwatching);
            }

            // 승무원 상태 요약 (배치된 승무원 수 표시)
            if (crewStatusText != null)
            {
                int deployedCount = CountDeployedCrew(unit.Crew);
                int totalSlots = 5;
                crewStatusText.text = $"{deployedCount}/{totalSlots} 배치";
            }
        }

        /// <summary>배치된 전투 가능 승무원 수 반환</summary>
        private int CountDeployedCrew(TankCrew crew)
        {
            int count = 0;
            if (crew.commander != null && crew.commander.IsCombatReady) count++;
            if (crew.gunner != null && crew.gunner.IsCombatReady) count++;
            if (crew.loader != null && crew.loader.IsCombatReady) count++;
            if (crew.driver != null && crew.driver.IsCombatReady) count++;
            if (crew.mgMechanic != null && crew.mgMechanic.IsCombatReady) count++;
            return count;
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
