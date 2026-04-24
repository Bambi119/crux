using UnityEngine;

namespace Crux.UI
{
    /// <summary>
    /// 팝업 위치 조정 — 화면 경계 플립.
    ///
    /// 선택된 유닛이 화면 경계에 가까우면 팝업(CommandBox, WeaponSelect 등)을
    /// 반대쪽으로 자동 배치하여 오프스크린 방지.
    ///
    /// 참고: docs/10c §2.1 — 화면 경계 플립 규칙
    /// </summary>
    public class PopupPositioner
    {
        /// <summary>팝업 크기 (예상값)</summary>
        public struct PopupSize
        {
            public float width;
            public float height;
        }

        /// <summary>경계 판정 마진 (px)</summary>
        private const float SafetyMargin = 20f;

        /// <summary>
        /// 화면 좌표 기준 팝업 위치 계산 — 경계 플립 자동 적용.
        ///
        /// 아군 유닛이 화면 경계에 가까우면 팝업을 반대쪽으로 배치.
        /// 예: 우측 근처 → 팝업을 좌측 배치
        /// </summary>
        public static Vector2 GetFlippedPosition(Vector3 unitWorldPos, UnityEngine.Camera mainCam, PopupSize popupSize)
        {
            if (mainCam == null) return Vector2.zero;

            // 유닛의 스크린 좌표 계산
            Vector3 screenPos = mainCam.WorldToScreenPoint(unitWorldPos);
            float screenX = screenPos.x;
            float screenY = Screen.height - screenPos.y; // GUI 좌표계로 변환

            float popupX = screenX;
            float popupY = screenY;

            // 수평 배치 판정 — 좌/우 경계
            float rightMargin = popupSize.width + SafetyMargin;
            float leftMargin = SafetyMargin;

            if (screenX + rightMargin > Screen.width)
            {
                // 우측 경계에 가까움 → 팝업을 좌측 배치
                popupX = screenX - popupSize.width - 10f;
            }
            else if (screenX - leftMargin < 0)
            {
                // 좌측 경계에 가까움 → 팝업을 우측 배치
                popupX = screenX + 10f;
            }
            else
            {
                // 여유 있음 — 기본값 (유닛 우측)
                popupX = screenX + 10f;
            }

            // 수직 배치 판정 — 상/하 경계
            float bottomMargin = popupSize.height + SafetyMargin;
            float topMargin = SafetyMargin;

            if (screenY + bottomMargin > Screen.height)
            {
                // 하단 경계에 가까움 → 팝업을 상단 배치
                popupY = screenY - popupSize.height - 10f;
            }
            else if (screenY - topMargin < 0)
            {
                // 상단 경계에 가까움 → 팝업을 하단 배치
                popupY = screenY + 10f;
            }
            else
            {
                // 여유 있음 — 기본값 (유닛 하측)
                popupY = screenY + 10f;
            }

            // 최종 위치 — 화면 내 클램핑 (추가 안전장치)
            popupX = Mathf.Clamp(popupX, 0, Screen.width - popupSize.width);
            popupY = Mathf.Clamp(popupY, 0, Screen.height - popupSize.height);

            return new Vector2(popupX, popupY);
        }

        /// <summary>
        /// 팝업이 화면 내에 완전히 포함되는지 판정.
        /// </summary>
        public static bool IsFullyOnScreen(Rect popupRect)
        {
            return popupRect.xMin >= 0 && popupRect.xMax <= Screen.width &&
                   popupRect.yMin >= 0 && popupRect.yMax <= Screen.height;
        }
    }
}
