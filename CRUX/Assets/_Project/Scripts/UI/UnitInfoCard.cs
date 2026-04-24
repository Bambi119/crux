using UnityEngine;
using UnityEngine.UI;
using Crux.Unit;

namespace Crux.UI
{
    /// <summary>
    /// 유닛 정보 카드 — 사격 타겟팅 단계에서 우측에 표시되는 적/아군 상세 정보 패널.
    ///
    /// 표시 항목: 유닛 이름, HP 바, AP, 주요 모듈 상태, 탄약 수.
    /// 무기 선택 단계에서 ShowFirePreview()로 명중률·예상 데미지 갱신.
    ///
    /// 참고: docs/10c §5.1, §5.2, §6.3
    /// </summary>
    public class UnitInfoCard : MonoBehaviour
    {
        // ===== 헤더 =====
        [Header("헤더 — 유닛 이름")]
        [SerializeField] private Text unitNameText;

        // ===== HP 섹션 =====
        [Header("HP 섹션")]
        [SerializeField] private Image hpBarFill;
        [SerializeField] private Text hpValueText;

        // ===== AP 섹션 =====
        [Header("AP 섹션")]
        [SerializeField] private Text apValueText;

        // ===== 탄약 섹션 =====
        [Header("탄약 섹션")]
        [SerializeField] private Text mainGunAmmoText;
        [SerializeField] private Text mgAmmoText;

        // ===== 모듈 상태 바 =====
        [Header("모듈 상태 바 (선택 사항)")]
        [SerializeField] private Image engineBarFill;
        [SerializeField] private Image barrelBarFill;
        [SerializeField] private Image caterpillarBarFill;   // 좌우 평균

        // ===== 사격 프리뷰 섹션 =====
        [Header("사격 프리뷰 — 무기 선택 단계")]
        [SerializeField] private GameObject firePreviewRoot;
        [SerializeField] private Text hitChanceText;
        [SerializeField] private Text expectedDamageText;

        // ===== PartBarFlashAnimator (선택 사항) =====
        [Header("피격 예상 깜빡임 애니메이터 (선택 사항)")]
        [SerializeField] private PartBarFlashAnimator flashAnimator;
        [SerializeField] private Image flashTargetBar;       // 깜빡일 대상 바

        // ===== 내부 상태 =====
        private GridTankUnit boundUnit;

        // ===== 공용 API =====

        /// <summary>유닛 정보를 바인딩하고 카드 표시</summary>
        public void Show(GridTankUnit unit)
        {
            if (unit == null)
            {
                Hide();
                return;
            }

            boundUnit = unit;
            gameObject.SetActive(true);

            HideFirePreview();
            UpdateFromUnit();
        }

        /// <summary>카드 숨기기 및 바인딩 해제</summary>
        public void Hide()
        {
            boundUnit = null;
            gameObject.SetActive(false);
            HideFirePreview();
        }

        /// <summary>바인딩된 유닛의 현재 상태로 UI 갱신</summary>
        public void UpdateFromUnit()
        {
            if (boundUnit == null) return;

            RefreshHeader();
            RefreshHP();
            RefreshAP();
            RefreshAmmo();
            RefreshModuleBars();
        }

        /// <summary>
        /// 사격 프리뷰 갱신 — 무기 선택 단계에서 호출.
        /// hitChance: 0~1 소수, expectedDamage: 기대 피해량.
        /// </summary>
        public void ShowFirePreview(float hitChance, float expectedDamage)
        {
            if (firePreviewRoot != null)
                firePreviewRoot.SetActive(true);

            if (hitChanceText != null)
                hitChanceText.text = $"명중률  {hitChance * 100f:F0}%";

            if (expectedDamageText != null)
                expectedDamageText.text = $"예상 피해  {expectedDamage:F0}";

            // 부위 바 깜빡임 — 바인딩된 유닛 HP 기준 (docs/10c §6.3)
            if (flashAnimator != null && flashTargetBar != null && boundUnit != null)
            {
                float maxHP = boundUnit.Data != null ? boundUnit.Data.maxHP : 100f;
                flashAnimator.StartFlash(flashTargetBar, maxHP, expectedDamage);
            }
        }

        /// <summary>사격 프리뷰 섹션 숨기기</summary>
        public void HideFirePreview()
        {
            if (firePreviewRoot != null)
                firePreviewRoot.SetActive(false);

            if (flashAnimator != null)
                flashAnimator.StopFlash();
        }

        // ===== 내부 갱신 =====

        private void RefreshHeader()
        {
            if (unitNameText == null) return;

            string displayName = boundUnit.Data != null && !string.IsNullOrEmpty(boundUnit.Data.tankName)
                ? boundUnit.Data.tankName
                : boundUnit.gameObject.name;

            unitNameText.text = displayName;
        }

        private void RefreshHP()
        {
            float maxHP = boundUnit.Data != null ? boundUnit.Data.maxHP : 100f;
            float currentHP = boundUnit.CurrentHP;
            float ratio = maxHP > 0f ? Mathf.Clamp01(currentHP / maxHP) : 0f;

            if (hpBarFill != null)
                hpBarFill.fillAmount = ratio;

            if (hpValueText != null)
                hpValueText.text = $"HP  {currentHP:F0} / {maxHP:F0}";
        }

        private void RefreshAP()
        {
            if (apValueText == null) return;
            apValueText.text = $"AP  {boundUnit.CurrentAP} / {boundUnit.MaxAP}";
        }

        private void RefreshAmmo()
        {
            if (mainGunAmmoText != null)
                mainGunAmmoText.text = $"포탄  {boundUnit.MainGunAmmoCount} / {boundUnit.MaxMainGunAmmo}";

            if (mgAmmoText != null)
                mgAmmoText.text = $"MG  {boundUnit.MGAmmoLoaded}";
        }

        private void RefreshModuleBars()
        {
            if (engineBarFill != null)
                engineBarFill.fillAmount = boundUnit.Modules.Get(ModuleType.Engine).HPRatio;

            if (barrelBarFill != null)
                barrelBarFill.fillAmount = boundUnit.Modules.Get(ModuleType.Barrel).HPRatio;

            if (caterpillarBarFill != null)
            {
                // 좌우 캐터필러 평균
                float left  = boundUnit.Modules.Get(ModuleType.CaterpillarLeft).HPRatio;
                float right = boundUnit.Modules.Get(ModuleType.CaterpillarRight).HPRatio;
                caterpillarBarFill.fillAmount = (left + right) * 0.5f;
            }
        }
    }
}
