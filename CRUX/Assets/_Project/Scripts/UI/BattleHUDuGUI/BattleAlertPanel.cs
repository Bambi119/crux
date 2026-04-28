using System.Collections;
using UnityEngine;

namespace Crux.UI
{
    /// <summary>
    /// TD-08: BattleController.ShowAlert 위임 대상 uGUI 패널.
    /// 월드 좌표를 스크린 좌표로 변환하여 alertIcon 위치에 적용하고,
    /// duration 경과 후 페이드 아웃한다.
    /// </summary>
    public class BattleAlertPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform alertIcon;
        [SerializeField] private CanvasGroup canvasGroup;

        private Coroutine _fadeCoroutine;

        /// <summary>경고 아이콘 표시 — worldPos를 스크린 좌표로 변환 후 duration 경과 후 페이드 아웃</summary>
        public void Show(Vector3 worldPos, float duration)
        {
            if (alertIcon == null || canvasGroup == null)
            {
                Debug.LogWarning("[CRUX] BattleAlertPanel: alertIcon 또는 canvasGroup이 null입니다.");
                return;
            }

            var cam = UnityEngine.Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[CRUX] BattleAlertPanel: Camera.main이 null입니다.");
                return;
            }

            // 월드 → 스크린 좌표 변환
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            // 카메라 뒤쪽이면 표시하지 않음
            if (screenPos.z < 0f)
                return;

            // 중복 코루틴 방지
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }

            // alertIcon.position은 스크린 좌표 직접 적용 (Overlay Canvas 전제)
            alertIcon.position = new Vector3(screenPos.x, screenPos.y, 0f);
            canvasGroup.alpha = 1f;
            gameObject.SetActive(true);

            _fadeCoroutine = StartCoroutine(FadeOutAfter(duration));
        }

        private IEnumerator FadeOutAfter(float duration)
        {
            yield return new WaitForSeconds(duration);

            // 0.3s 페이드 아웃
            const float fadeDuration = 0.3f;
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
