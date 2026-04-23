using UnityEngine;
using UnityEngine.UI;
using UnityCamera = UnityEngine.Camera;

namespace Crux.UI
{
    /// <summary>
    /// World→Screen 경계 플립 시스템. 월드 좌표의 팝업을 화면 경계를 넘지 않도록 위치 조정.
    /// Canvas World Space 및 Screen Space Overlay 환경 지원.
    /// </summary>
    public static class PopupPositioner
    {
        private const float SafetyMargin = 20f; // 화면 경계 안전 마진 (픽셀)

        /// <summary>
        /// 월드 좌표 기준 팝업 위치를 계산하며, 화면 경계 플립 적용.
        /// </summary>
        /// <param name="worldPos">기준 월드 좌표 (유닛/타일 중심)</param>
        /// <param name="popupSize">팝업 크기 (픽셀)</param>
        /// <param name="preferredOffset">기본 오프셋. 양수=우측/상단, 음수=좌측/하단</param>
        /// <param name="canvas">RectTransform을 가져올 Canvas</param>
        /// <returns>팝업 RectTransform에 설정할 anchoredPosition</returns>
        public static Vector2 ComputeScreenPosition(Vector3 worldPos, Vector2 popupSize, Vector2 preferredOffset, Canvas canvas)
        {
            if (canvas == null)
            {
                Debug.LogError("[PopupPositioner] Canvas is null");
                return preferredOffset;
            }

            // 월드 → 스크린 변환
            UnityCamera mainCam = UnityCamera.main;
            if (mainCam == null)
            {
                Debug.LogError("[PopupPositioner] Camera.main is null");
                return preferredOffset;
            }
            Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);

            // Canvas 스케일 팩터 고려 (Screen Space Canvas의 경우)
            float scaleFactor = canvas.scaleFactor;
            Vector2 screenPosNormalized = new Vector2(screenPos.x / scaleFactor, screenPos.y / scaleFactor);

            // Canvas RectTransform 기준 뷰포트 크기
            RectTransform canvasRT = canvas.GetComponent<RectTransform>();
            float canvasWidth = canvasRT.rect.width;
            float canvasHeight = canvasRT.rect.height;

            // X 축 플립 판정
            float xPos = screenPosNormalized.x + preferredOffset.x;
            if (xPos + popupSize.x / 2 > canvasWidth - SafetyMargin)
            {
                // 우측 초과 → 좌측으로 플립
                xPos = screenPosNormalized.x - preferredOffset.x - popupSize.x;
            }
            else if (xPos - popupSize.x / 2 < SafetyMargin)
            {
                // 좌측 초과 → 우측으로 플립
                xPos = screenPosNormalized.x + popupSize.x;
            }

            // Y 축 플립 판정 (Canvas가 bottom-left 원점이므로 음수 방향이 하단)
            float yPos = screenPosNormalized.y + preferredOffset.y;
            if (yPos + popupSize.y / 2 > canvasHeight - SafetyMargin)
            {
                // 상단 초과 → 하단으로 플립
                yPos = screenPosNormalized.y - preferredOffset.y - popupSize.y;
            }
            else if (yPos - popupSize.y / 2 < SafetyMargin)
            {
                // 하단 초과 → 상단으로 플립
                yPos = screenPosNormalized.y + popupSize.y;
            }

            return new Vector2(xPos, yPos);
        }

        /// <summary>
        /// 간편 오버로드 — preferredOffset 기본값 (우측 10, 상단 -10).
        /// </summary>
        public static Vector2 ComputeScreenPosition(Vector3 worldPos, Vector2 popupSize, Canvas canvas)
        {
            return ComputeScreenPosition(worldPos, popupSize, new Vector2(10f, -10f), canvas);
        }
    }
}
