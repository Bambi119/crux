using System.Collections;
using UnityEngine;
using TMPro;

namespace Crux.UI
{
    /// <summary>
    /// TD-08: BattleController.ShowBanner 위임 대상 uGUI 패널.
    /// CanvasGroup 알파 페이드로 배너 연출을 처리한다.
    /// </summary>
    public class BattleBannerPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI bannerText;
        [SerializeField] private CanvasGroup canvasGroup;

        private Coroutine _fadeCoroutine;

        /// <summary>배너 표시 — duration 경과 후 0.4s 페이드 아웃</summary>
        public void Show(string message, Color color, float duration)
        {
            if (bannerText == null || canvasGroup == null)
            {
                Debug.LogWarning("[CRUX] BattleBannerPanel: bannerText 또는 canvasGroup이 null입니다.");
                return;
            }

            // 중복 코루틴 방지
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }

            bannerText.text = message;
            bannerText.color = color;
            canvasGroup.alpha = 1f;
            gameObject.SetActive(true);

            _fadeCoroutine = StartCoroutine(FadeOutAfter(duration));
        }

        private IEnumerator FadeOutAfter(float duration)
        {
            yield return new WaitForSeconds(duration);

            // 0.4s 페이드 아웃
            const float fadeDuration = 0.4f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
            _fadeCoroutine = null;
        }

        private void OnDisable()
        {
            // 씬 전환·오브젝트 비활성화 시 코루틴 누수 방지
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }
        }
    }
}
