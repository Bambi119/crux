using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Crux.Core;
using Crux.Unit;

namespace Crux.UI
{
    /// <summary>
    /// 작전 완료 모달 — Victory/GameOver 전환 시 표시.
    /// 격파 수, 명중률, 아군 손실 통계 및 유닛 목록 렌더링.
    /// </summary>
    public class MissionCompleteModalBinder : MonoBehaviour
    {
        private BattleController controller;
        private Transform modalRoot;

        // 캐시된 자식 참조
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI subtitleText;
        private Image amberBar;

        // 통계 행(Stat1~Stat3)
        private TextMeshProUGUI stat1Value, stat1Label;
        private TextMeshProUGUI stat2Value, stat2Label;
        private TextMeshProUGUI stat3Value, stat3Label;

        // 유닛 목록 컨테이너
        private Transform unitsList;

        // Return 버튼
        private Button returnButton;

        // 상태: dirty flag로 중복 갱신 방지
        private TurnPhase lastPhase = (TurnPhase)(-1);

        public void Initialize(BattleController controller, Transform modalRoot)
        {
            this.controller = controller;
            this.modalRoot = modalRoot;

            CacheChildReferences();
            RegisterReturnButton();
        }

        private void CacheChildReferences()
        {
            // Header
            titleText = modalRoot.Find("Panel/Header/Title")?.GetComponent<TextMeshProUGUI>();
            subtitleText = modalRoot.Find("Panel/Header/Subtitle")?.GetComponent<TextMeshProUGUI>();
            amberBar = modalRoot.Find("Panel/Header/AmberBar")?.GetComponent<Image>();

            // StatsRow
            var stat1Row = modalRoot.Find("Panel/StatsRow/Stat1");
            if (stat1Row != null)
            {
                stat1Value = stat1Row.Find("Value")?.GetComponent<TextMeshProUGUI>();
                stat1Label = stat1Row.Find("Label")?.GetComponent<TextMeshProUGUI>();
            }

            var stat2Row = modalRoot.Find("Panel/StatsRow/Stat2");
            if (stat2Row != null)
            {
                stat2Value = stat2Row.Find("Value")?.GetComponent<TextMeshProUGUI>();
                stat2Label = stat2Row.Find("Label")?.GetComponent<TextMeshProUGUI>();
            }

            var stat3Row = modalRoot.Find("Panel/StatsRow/Stat3");
            if (stat3Row != null)
            {
                stat3Value = stat3Row.Find("Value")?.GetComponent<TextMeshProUGUI>();
                stat3Label = stat3Row.Find("Label")?.GetComponent<TextMeshProUGUI>();
            }

            // UnitsList
            unitsList = modalRoot.Find("Panel/UnitsSection/UnitsList");

            // Return 버튼
            returnButton = modalRoot.Find("Panel/Footer/ReturnButton")?.GetComponent<Button>();
        }

        private void RegisterReturnButton()
        {
            if (returnButton == null) return;

            returnButton.onClick.AddListener(OnReturnClicked);
            Debug.Log("[CRUX] MissionCompleteModalBinder: Return 버튼 리스너 등록");
        }

        private void OnReturnClicked()
        {
            Debug.Log("[CRUX] MissionCompleteModalBinder: 격납고 복귀 요청");
            SceneManager.LoadScene("Hangar");
        }

        private void Update()
        {
            if (controller == null || modalRoot == null) return;

            TurnPhase currentPhase = controller.CurrentPhase;
            bool isTerminal = currentPhase == TurnPhase.Victory || currentPhase == TurnPhase.GameOver;

            // 상태 갱신 (dirty flag)
            if (isTerminal && lastPhase != currentPhase)
            {
                lastPhase = currentPhase;
                modalRoot.gameObject.SetActive(true);
                UpdateModalContent(currentPhase);
            }
            else if (!isTerminal && modalRoot.gameObject.activeSelf)
            {
                modalRoot.gameObject.SetActive(false);
            }
        }

        private void UpdateModalContent(TurnPhase phase)
        {
            // 제목 및 색상
            if (titleText != null)
                titleText.text = phase == TurnPhase.Victory ? "작전 완료" : "작전 실패";

            if (amberBar != null && phase == TurnPhase.GameOver)
                amberBar.color = new Color(0xFF / 255f, 0x93 / 255f, 0x8C / 255f, 1f);

            // 부제: 턴 수
            if (subtitleText != null)
                subtitleText.text = $"{controller.TurnCount}턴 만에 완료";

            // 통계
            UpdateStats();

            // 유닛 목록
            RebuildUnitsList();
        }

        private void UpdateStats()
        {
            // Stat1: 격파 수
            if (stat1Value != null)
            {
                int kills = 0;
                foreach (var enemy in controller.EnemyUnitsRef)
                {
                    if (enemy != null && enemy.IsDestroyed)
                        kills++;
                }
                stat1Value.text = kills.ToString();
            }

            // Stat2: 명중률 고정 (미집계)
            if (stat2Value != null)
                stat2Value.text = "-%";

            // Stat3: 아군 잔존 HP%
            if (stat3Value != null)
            {
                if (controller.PlayerUnitRef != null && !controller.PlayerUnitRef.IsDestroyed)
                {
                    float maxHP = controller.PlayerUnitRef.Data?.maxHP ?? 100f;
                    if (maxHP > 0)
                    {
                        int hpPercent = Mathf.RoundToInt(controller.PlayerUnitRef.CurrentHP * 100f / maxHP);
                        stat3Value.text = $"{hpPercent}%";
                    }
                    else
                        stat3Value.text = "-";
                }
                else
                {
                    stat3Value.text = "-";
                }
            }

            // Stat3 Label 변경
            if (stat3Label != null)
                stat3Label.text = "아군 잔존";
        }

        private void RebuildUnitsList()
        {
            if (unitsList == null) return;

            int expectedCount = 1 + controller.EnemyUnitsRef.Count; // 플레이어 + 적 개수
            int currentChildCount = unitsList.childCount;

            // 필요한 행 개수 맞추기
            if (currentChildCount < expectedCount)
            {
                // 부족하면 마지막 행 복제
                if (currentChildCount > 0)
                {
                    Transform templateRow = unitsList.GetChild(currentChildCount - 1);
                    for (int i = currentChildCount; i < expectedCount; i++)
                    {
                        Instantiate(templateRow, unitsList);
                    }
                }
            }
            else if (currentChildCount > expectedCount)
            {
                // 남는 행 비활성화
                for (int i = expectedCount; i < currentChildCount; i++)
                {
                    unitsList.GetChild(i).gameObject.SetActive(false);
                }
            }

            // 각 행 채우기
            int rowIndex = 0;

            // Row 0: 플레이어
            if (rowIndex < unitsList.childCount)
            {
                Transform row = unitsList.GetChild(rowIndex);
                row.gameObject.SetActive(true);
                FillUnitRow(row, controller.PlayerUnitRef, "로시난테");
                rowIndex++;
            }

            // Row 1+: 적들
            foreach (var enemy in controller.EnemyUnitsRef)
            {
                if (rowIndex >= unitsList.childCount)
                    break;

                Transform row = unitsList.GetChild(rowIndex);
                row.gameObject.SetActive(true);
                string enemyName = enemy != null ? (enemy.Data?.tankName ?? "적기") : "적기";
                FillUnitRow(row, enemy, enemyName);
                rowIndex++;
            }
        }

        private void FillUnitRow(Transform row, GridTankUnit unit, string displayName)
        {
            if (row == null || unit == null) return;

            // 유닛 이름
            var nameText = row.Find("UnitName")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
                nameText.text = displayName;

            // 상태 칩
            UpdateStatusChip(row, unit);
        }

        private void UpdateStatusChip(Transform row, GridTankUnit unit)
        {
            if (unit == null) return;

            var statusChip = row.Find("StatusChip");
            if (statusChip == null) return;

            string statusLabel;
            Color bgColor;
            Color borderColor;

            if (unit.IsDestroyed)
            {
                statusLabel = "격파";
                bgColor = new Color(0xFF / 255f, 0x93 / 255f, 0x8C / 255f, 0.2f);
                borderColor = new Color(0xFF / 255f, 0x93 / 255f, 0x8C / 255f, 0.3f);
            }
            else if (unit.CurrentHP <= unit.Data?.maxHP * 0.5f)
            {
                statusLabel = "손상";
                bgColor = new Color(0xF5 / 255f, 0x9E / 255f, 0x0B / 255f, 1f);
                borderColor = new Color(0xF5 / 255f, 0x9E / 255f, 0x0B / 255f, 1f);
            }
            else
            {
                statusLabel = "가동";
                bgColor = new Color(0x00 / 255f, 0xB9 / 255f, 0x54 / 255f, 1f);
                borderColor = new Color(0x00 / 255f, 0xB9 / 255f, 0x54 / 255f, 1f);
            }

            // 텍스트
            var statusText = statusChip.Find("Text")?.GetComponent<TextMeshProUGUI>();
            if (statusText != null)
                statusText.text = statusLabel;

            // 배경 색상
            var bgImage = statusChip.GetComponent<Image>();
            if (bgImage != null)
                bgImage.color = bgColor;

            // 테두리 색상 (별도 이미지)
            var borderImage = statusChip.Find("Border")?.GetComponent<Image>();
            if (borderImage != null)
                borderImage.color = borderColor;
        }
    }
}
