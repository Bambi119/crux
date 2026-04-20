using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Crux.Core;
using Crux.Grid;
using Crux.Unit;
using Crux.Data;

namespace Crux.UI
{
    /// <summary>사격 프리뷰 카드 — uGUI 프리팹을 실시간으로 채우는 바인더</summary>
    public class FirePreviewCardBinder : MonoBehaviour
    {
        private BattleController controller;
        private Transform cardRoot;

        // 캐시된 자식 참조
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI hpText;
        private Image hpFill;
        private Button tabAP, tabCoax, tabMount;
        private TextMeshProUGUI tabAPText, tabCoaxText, tabMountText;
        private Image tabAPBorder, tabCoaxBorder, tabMountBorder;
        private TextMeshProUGUI hitChanceText;
        private TextMeshProUGUI expDamageText;
        private TextMeshProUGUI outcomeText;
        private TextMeshProUGUI outcomeDetailText;
        private TextMeshProUGUI rangeValue;
        private TextMeshProUGUI hitZoneValue;
        private TextMeshProUGUI breakdownValue;
        private TextMeshProUGUI afterShotValue;
        private TextMeshProUGUI coverText;

        public void Initialize(BattleController controller, Transform cardRoot)
        {
            this.controller = controller;
            this.cardRoot = cardRoot;

            CacheChildReferences();
        }

        private void CacheChildReferences()
        {
            // Header
            titleText = cardRoot.Find("Header/TitleText")?.GetComponent<TextMeshProUGUI>();
            hpText = cardRoot.Find("Header/HPText")?.GetComponent<TextMeshProUGUI>();
            hpFill = cardRoot.Find("Header/HPBar/HPFill")?.GetComponent<Image>();

            // WeaponTabs
            tabAP = cardRoot.Find("WeaponTabs/TabAP")?.GetComponent<Button>();
            tabCoax = cardRoot.Find("WeaponTabs/TabCoax")?.GetComponent<Button>();
            tabMount = cardRoot.Find("WeaponTabs/TabMount")?.GetComponent<Button>();

            if (tabAP != null) tabAPText = tabAP.GetComponentInChildren<TextMeshProUGUI>();
            if (tabAP != null) tabAPBorder = tabAP.transform.Find("Border")?.GetComponent<Image>();

            if (tabCoax != null) tabCoaxText = tabCoax.GetComponentInChildren<TextMeshProUGUI>();
            if (tabCoax != null) tabCoaxBorder = tabCoax.transform.Find("Border")?.GetComponent<Image>();

            if (tabMount != null) tabMountText = tabMount.GetComponentInChildren<TextMeshProUGUI>();
            if (tabMount != null) tabMountBorder = tabMount.transform.Find("Border")?.GetComponent<Image>();

            // PrimaryMetrics
            hitChanceText = cardRoot.Find("PrimaryMetrics/HitChanceSection/HitChanceText")?.GetComponent<TextMeshProUGUI>();
            expDamageText = cardRoot.Find("PrimaryMetrics/ExpDamageSection/ExpDamageText")?.GetComponent<TextMeshProUGUI>();

            // OutcomeBadge
            outcomeText = cardRoot.Find("OutcomeBadge/OutcomeText")?.GetComponent<TextMeshProUGUI>();
            outcomeDetailText = cardRoot.Find("OutcomeBadge/OutcomeDetailText")?.GetComponent<TextMeshProUGUI>();

            // Details
            rangeValue = cardRoot.Find("Details/RangeRow/RangeValue")?.GetComponent<TextMeshProUGUI>();
            hitZoneValue = cardRoot.Find("Details/HitZoneRow/HitZoneValue")?.GetComponent<TextMeshProUGUI>();
            breakdownValue = cardRoot.Find("Details/BreakdownRow/BreakdownValue")?.GetComponent<TextMeshProUGUI>();
            afterShotValue = cardRoot.Find("Details/AfterShotRow/AfterShotValue")?.GetComponent<TextMeshProUGUI>();

            // CoverFooter
            coverText = cardRoot.Find("CoverFooter/CoverText")?.GetComponent<TextMeshProUGUI>();
        }

        private void Update()
        {
            if (controller == null || cardRoot == null) return;

            // 대상 선택 여부 확인
            var target = controller.PendingTarget ?? controller.HoveredTarget;
            if (target == null || target.IsDestroyed || controller.SelectedUnit == null)
            {
                cardRoot.gameObject.SetActive(false);
                return;
            }

            cardRoot.gameObject.SetActive(true);

            // 사격 프리뷰 계산
            var weapon = controller.SelectedWeapon;
            var result = FirePreviewCalculator.Compute(controller, controller.SelectedUnit, target, weapon);

            // 각 텍스트/이미지 갱신
            UpdateHeader(target);
            UpdateHP(target);
            UpdateWeaponTabs(weapon);
            UpdatePrimaryMetrics(result);
            UpdateOutcomeBadge(result);
            UpdateDetails(target, result);
            UpdateAfterShot(target, result);
            UpdateCover(target, result);
        }

        private void UpdateHeader(GridTankUnit target)
        {
            if (titleText == null) return;
            string cls = BattleHUD.GetHullClassLabelStatic(target.Data?.hullClass ?? HullClass.Assault);
            titleText.text = $"{target.Data?.tankName ?? "대상"}  [{cls}]";
        }

        private void UpdateHP(GridTankUnit target)
        {
            if (hpText == null) return;
            hpText.text = $"{Mathf.CeilToInt(target.CurrentHP)}/{Mathf.CeilToInt(target.Data?.maxHP ?? 100)}";

            // HP Bar 갱신
            if (hpFill != null && target.Data != null && target.Data.maxHP > 0)
            {
                var rect = hpFill.GetComponent<RectTransform>();
                if (rect != null)
                {
                    float ratio = target.CurrentHP / target.Data.maxHP;
                    rect.anchorMax = new Vector2(ratio, rect.anchorMax.y);
                }
            }
        }

        private void UpdateWeaponTabs(WeaponType weapon)
        {
            // AP (MainGun) 하이라이트
            if (tabAP != null)
            {
                if (tabAPBorder != null)
                {
                    if (weapon == WeaponType.MainGun)
                        tabAPBorder.color = new Color(1f, 1f, 1f, 1f); // PrimaryContainer (White)
                    else
                        tabAPBorder.color = new Color(1f, 1f, 1f, 0.5f); // alpha 0.5
                }
            }

            // Coax 하이라이트
            if (tabCoax != null)
            {
                if (tabCoaxBorder != null)
                {
                    if (weapon == WeaponType.CoaxialMG)
                        tabCoaxBorder.color = new Color(1f, 1f, 1f, 1f);
                    else
                        tabCoaxBorder.color = new Color(1f, 1f, 1f, 0.5f);
                }
            }

            // Mount 하이라이트
            if (tabMount != null)
            {
                if (tabMountBorder != null)
                {
                    if (weapon == WeaponType.MountedMG)
                        tabMountBorder.color = new Color(1f, 1f, 1f, 1f);
                    else
                        tabMountBorder.color = new Color(1f, 1f, 1f, 0.5f);
                }
            }
        }

        private void UpdatePrimaryMetrics(FirePreviewCalculator.FirePreviewResult result)
        {
            if (hitChanceText != null)
                hitChanceText.text = $"{(result.finalHit * 100):F0}%";

            if (expDamageText != null)
            {
                if (result.isMG)
                    expDamageText.text = $"{result.totalExpected:F0}";
                else
                    expDamageText.text = $"{result.expectedDamagePerShot:F0}";
            }
        }

        private void UpdateOutcomeBadge(FirePreviewCalculator.FirePreviewResult result)
        {
            if (outcomeText != null)
            {
                string label;
                Color color;
                switch (result.outcome)
                {
                    case ShotOutcome.Penetration:
                        label = "관통";
                        color = new Color(0.4f, 1f, 0.5f); // SecondaryContainer (Green)
                        break;
                    case ShotOutcome.Hit:
                        label = "명중";
                        color = new Color(1f, 1f, 0.4f); // PrimaryContainer (Yellow)
                        break;
                    case ShotOutcome.Ricochet:
                        label = "도탄";
                        color = new Color(1f, 0.4f, 0.3f); // Red
                        break;
                    default:
                        label = "실패";
                        color = new Color(0.5f, 0.5f, 0.5f); // Gray
                        break;
                }
                outcomeText.text = label;
                outcomeText.color = color;
            }

            if (outcomeDetailText != null)
                outcomeDetailText.text = $"관통력 {result.penetration:F0}mm 대 {result.effectiveArmor:F0}mm 유효장갑";
        }

        private void UpdateDetails(GridTankUnit target, FirePreviewCalculator.FirePreviewResult result)
        {
            if (rangeValue != null)
                rangeValue.text = $"{result.distance}셀";

            if (hitZoneValue != null)
            {
                string zoneLabel = result.hitZone switch
                {
                    HitZone.Front => "전면",
                    HitZone.FrontRight => "우전",
                    HitZone.RearRight => "우후",
                    HitZone.Rear => "후면",
                    HitZone.RearLeft => "좌후",
                    HitZone.FrontLeft => "좌전",
                    HitZone.Turret => "포탑",
                    _ => ""
                };
                hitZoneValue.text = $"{zoneLabel} 장갑 {result.baseArmor:F0}mm (유효 {result.effectiveArmor:F0}mm)";
            }

            if (breakdownValue != null)
            {
                string breakdown = $"기본 {(result.baseHit * 100):F0}%";

                if (result.moraleBonus != 0f)
                {
                    if (result.moraleBonus > 0)
                        breakdown += $"  <color=#64FF80>+사기 {(result.moraleBonus * 100f):F0}%</color>";
                    else
                        breakdown += $"  <color=#FF938C>−사기 {(Mathf.Abs(result.moraleBonus) * 100f):F0}%</color>";
                }

                if (result.coverPenalty > 0)
                    breakdown += $"  −엄폐 {(result.coverPenalty * 100f):F0}%";

                if (result.smokePenalty > 0)
                    breakdown += $"  −연막 {(result.smokePenalty * 100f):F0}%";

                breakdownValue.text = breakdown;
            }
        }

        private void UpdateAfterShot(GridTankUnit target, FirePreviewCalculator.FirePreviewResult result)
        {
            if (afterShotValue == null) return;

            float remainHP = Mathf.Max(0f, target.CurrentHP - result.totalExpected);
            bool kill = remainHP <= 0f && result.finalHit > 0.01f;

            if (kill)
                afterShotValue.text = $"{target.CurrentHP:F0} <color=#FF938C>→ 격파</color>";
            else
                afterShotValue.text = $"{target.CurrentHP:F0} <color=#F59E0B>→</color> {remainHP:F0}";
        }

        private void UpdateCover(GridTankUnit target, FirePreviewCalculator.FirePreviewResult result)
        {
            if (coverText == null) return;

            if (result.coveredFromThisAngle)
            {
                var grid = controller.Grid;
                var tc = grid.GetCell(target.GridPosition);
                if (tc.Cover != null)
                {
                    var cv = tc.Cover;
                    string sz = cv.size switch
                    {
                        CoverSize.Small => "소",
                        CoverSize.Medium => "중",
                        CoverSize.Large => "대",
                        _ => ""
                    };
                    string dirs = BattleHUD.GetFacetLabelStatic(cv.CurrentFacets);
                    coverText.text = $"엄폐 {cv.coverName}({sz}) {dirs}  유효";
                    coverText.color = new Color(0.4f, 1f, 0.5f);
                }
            }
            else
            {
                coverText.text = "엄폐  현재 각도에 무효";
                coverText.color = new Color(0.8f, 0.8f, 0.85f);
            }
        }
    }
}
