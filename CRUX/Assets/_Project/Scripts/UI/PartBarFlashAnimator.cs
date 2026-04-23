using UnityEngine;
using UnityEngine.UI;

namespace Crux.UI
{
    /// <summary>
    /// 부위 상태 바의 피격 예상 구간 깜빡임 애니메이션.
    ///
    /// 사격 시뮬레이션에서 특정 부위가 받을 예상 피해량을 시각화.
    /// 상태 바에서 해당 구간만 깜빡이는 하이라이트를 표시하여
    /// 플레이어가 예상 피해를 직관적으로 파악하도록 함.
    ///
    /// 참고: docs/10c §6.3 — 부위 바 피격 예상 하이라이트
    /// </summary>
    public class PartBarFlashAnimator : MonoBehaviour
    {
        /// <summary>깜빡임 애니메이션 상태</summary>
        public enum FlashState
        {
            Inactive,    // 미활성
            Flashing,    // 깜빡이는 중
            Done         // 완료
        }

        /// <summary>깜빡임 주기 (초)</summary>
        [SerializeField] private float flashDuration = 0.6f;

        /// <summary>한 사이클의 on/off 비율 (0.5 = 반반)</summary>
        [SerializeField] private float onOffRatio = 0.5f;

        /// <summary>피해 하이라이트용 Image (상태 바 위 오버레이)</summary>
        private Image damageHighlightImage;

        /// <summary>현재 상태</summary>
        private FlashState state = FlashState.Inactive;

        /// <summary>깜빡임 경과 시간</summary>
        private float flashElapsed = 0f;

        /// <summary>피해 예상값 (현재 HP 기준 비율, 0~1)</summary>
        private float expectedDamageRatio = 0f;

        private void Update()
        {
            if (state != FlashState.Flashing) return;

            flashElapsed += Time.deltaTime;
            if (flashElapsed >= flashDuration)
            {
                state = FlashState.Done;
                damageHighlightImage.enabled = false;
                return;
            }

            // 한 사이클 내 on/off 타이밍 계산
            float cycleTime = flashElapsed % flashDuration;
            float cycleRatio = cycleTime / flashDuration;
            bool isOn = cycleRatio < onOffRatio;

            damageHighlightImage.enabled = isOn;
        }

        /// <summary>
        /// 부위 바의 특정 구간을 깜빡임으로 표시.
        ///
        /// expectedDamageRatio는 현재 HP 대비 예상 피해량의 비율.
        /// 예: maxHP=100, expectedDamage=30 → ratio=0.3 → 바의 70~100 구간(0.7~1.0)을 하이라이트
        /// </summary>
        public void StartFlash(Image barImage, float maxHP, float expectedDamage)
        {
            if (barImage == null) return;

            // 피해 비율 계산 — 현재 HP 기준
            expectedDamageRatio = Mathf.Clamp01(expectedDamage / maxHP);

            // 하이라이트 이미지 설정 (바의 피해 예상 구간)
            // anchoredPosition과 sizeDelta를 조정하여 우측부터 덮음
            if (damageHighlightImage == null)
                damageHighlightImage = GetComponent<Image>();

            if (damageHighlightImage != null)
            {
                // 바의 전체 width를 기준으로 피해 구간 위치 계산
                RectTransform barRect = barImage.GetComponent<RectTransform>();
                RectTransform highlightRect = damageHighlightImage.GetComponent<RectTransform>();

                if (barRect != null && highlightRect != null)
                {
                    float barWidth = barRect.rect.width;
                    float damageWidth = barWidth * expectedDamageRatio;

                    // 우측 끝에서 피해 구간만큼 차지
                    highlightRect.sizeDelta = new Vector2(damageWidth, barRect.rect.height);
                    highlightRect.anchoredPosition = new Vector2(barWidth * (1f - expectedDamageRatio / 2f), 0);
                }

                damageHighlightImage.enabled = true;
            }

            state = FlashState.Flashing;
            flashElapsed = 0f;
        }

        /// <summary>깜빡임 중단</summary>
        public void StopFlash()
        {
            state = FlashState.Inactive;
            flashElapsed = 0f;
            if (damageHighlightImage != null)
                damageHighlightImage.enabled = false;
        }

        /// <summary>깜빡임 상태 조회</summary>
        public FlashState GetState() => state;
    }
}
