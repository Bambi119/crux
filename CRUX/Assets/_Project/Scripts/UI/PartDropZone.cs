using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Crux.Data;

namespace Crux.UI
{
    /// <summary>
    /// 장착 슬롯 행의 드롭 존.
    /// PartDragHandler의 드롭을 감지하고 OnPartDropped 콜백 실행.
    /// 호버 시 배경색 변경으로 드롭 가능 상태 표시.
    /// </summary>
    public class PartDropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public System.Action<PartInstance, TankInstance> OnPartDropped { get; set; }

        private Image _bgImage;
        private Color _normalColor = new Color(1f, 1f, 1f, 0.15f);
        private Color _hoverColor = new Color(0.3f, 0.8f, 0.3f, 0.5f);

        private void Awake()
        {
            _bgImage = GetComponent<Image>();
            if (_bgImage != null)
                _bgImage.color = _normalColor;
        }

        public void OnDrop(PointerEventData e)
        {
            var dragHandler = e.pointerDrag?.GetComponent<PartDragHandler>();
            if (dragHandler == null || dragHandler.Part == null || dragHandler.Tank == null)
                return;

            OnPartDropped?.Invoke(dragHandler.Part, dragHandler.Tank);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (_bgImage != null)
                _bgImage.color = _hoverColor;
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (_bgImage != null)
                _bgImage.color = _normalColor;
        }
    }
}
