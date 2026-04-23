using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Crux.UI
{
    /// <summary>
    /// 부위 바 데미지-비율 깜빡임 애니메이터.
    /// 예상 데미지 구간만 하이라이트하며, 전체 부위 바 점멸은 금지.
    ///
    /// 구조:
    ///   PartBar (Image, fillAmount로 HP 표시)
    ///   └── DamageOverlay (Image 자식, 빨간 반투명)
    /// </summary>
    public class PartBarFlashAnimator : MonoBehaviour
    {
        [SerializeField] private Image damageOverlay; // 빨간 오버레이 Image
        [SerializeField] private float flashAlphaMax = 0.7f; // 깜빡임 최대 투명도
        [SerializeField] private float flashPeriod = 0.3f; // 깜빡임 주기 (초)

        private Coroutine flashCoroutine;
        private Image partBar; // 부위 바 자체 (fillAmount)
        private RectTransform overlayRT; // 오버레이 RectTransform (너비/위치 조정용)

        private void Awake()
        {
            // 부모가 부위 바 Image여야 함
            partBar = GetComponent<Image>();
            if (damageOverlay != null)
            {
                overlayRT = damageOverlay.GetComponent<RectTransform>();
            }

            // 초기 상태: 오버레이 숨김
            if (damageOverlay != null)
            {
                damageOverlay.enabled = false;
            }
        }

        /// <summary>
        /// 부위 바 하단에 damageRatio만큼 빨간 오버레이로 깜빡임.
        /// 현재 HP 지점에서 predictedDamage 구간만 하이라이트.
        /// </summary>
        /// <param name="currentHP">현재 부위 HP</param>
        /// <param name="maxHP">부위 최대 HP</param>
        /// <param name="predictedDamage">예상 데미지</param>
        public void StartFlash(float currentHP, float maxHP, float predictedDamage)
        {
            // 기존 플래시 중단
            StopFlash();

            if (damageOverlay == null || overlayRT == null)
            {
                Debug.LogWarning("[PartBarFlashAnimator] DamageOverlay not assigned");
                return;
            }

            // 데미지 비율 계산
            float damageRatio = Mathf.Clamp01(predictedDamage / maxHP);

            // 현재 HP 비율 (0~1)
            float currentHPRatio = Mathf.Clamp01(currentHP / maxHP);

            // 오버레이 너비 설정 (damageRatio 폭)
            RectTransform parentRT = (RectTransform)partBar.transform;
            float fillWidth = parentRT.rect.width;
            float overlayWidth = fillWidth * damageRatio;
            overlayRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, overlayWidth);

            // 오버레이 X 위치 설정
            // "현재 HP 지점에서 좌측으로 damageRatio 폭"
            // = (currentHPRatio - damageRatio) * fillWidth 의 왼쪽 끝에서 시작
            float overlayLeftEdge = (currentHPRatio - damageRatio) * fillWidth;
            overlayRT.offsetMin = new Vector2(overlayLeftEdge, overlayRT.offsetMin.y);

            // 오버레이 활성화 및 깜빡임 시작
            damageOverlay.enabled = true;
            flashCoroutine = StartCoroutine(FlashCoroutine());
        }

        /// <summary>
        /// 플래시 중단 + 오버레이 숨김.
        /// </summary>
        public void StopFlash()
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
                flashCoroutine = null;
            }

            if (damageOverlay != null)
            {
                damageOverlay.enabled = false;
                // 알파 리셋
                Color c = damageOverlay.color;
                c.a = 0f;
                damageOverlay.color = c;
            }
        }

        private IEnumerator FlashCoroutine()
        {
            while (true)
            {
                // 투명도 0 → flashAlphaMax (0.3초)
                yield return AlphaTransition(0f, flashAlphaMax, flashPeriod / 2f);
                // 투명도 flashAlphaMax → 0 (0.3초)
                yield return AlphaTransition(flashAlphaMax, 0f, flashPeriod / 2f);
            }
        }

        private IEnumerator AlphaTransition(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float alpha = Mathf.Lerp(from, to, t);

                Color c = damageOverlay.color;
                c.a = alpha;
                damageOverlay.color = c;

                yield return null;
            }

            // 최종 값 보장
            Color finalColor = damageOverlay.color;
            finalColor.a = to;
            damageOverlay.color = finalColor;
        }
    }
}
