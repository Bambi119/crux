using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Crux.Data;

namespace Crux.UI
{
    /// <summary>
    /// 여분 파츠 행의 드래그 핸들러.
    /// 드래그 시작 시 반투명 고스트 생성, 종료 시 삭제.
    /// PartDropZone이 OnPartDropped 콜백으로 교체를 실행.
    /// </summary>
    public class PartDragHandler : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public PartInstance Part { get; private set; }
        public TankInstance Tank { get; private set; }

        private GameObject _ghost;
        private CanvasGroup _canvasGroup;
        private Canvas _rootCanvas;

        public void Init(PartInstance part, TankInstance tank, Canvas rootCanvas)
        {
            Part = part;
            Tank = tank;
            _rootCanvas = rootCanvas;
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (Part == null || _rootCanvas == null) return;

            // 고스트 GameObject 생성
            _ghost = new GameObject("DragGhost");
            _ghost.transform.SetParent(_rootCanvas.transform, false);
            _ghost.GetComponent<RectTransform>().SetAsLastSibling();

            // Image 추가 — 반투명 배경
            var img = _ghost.AddComponent<Image>();
            img.color = new Color(1f, 1f, 0.6f, 0.75f);
            var rt = _ghost.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160f, 28f);

            // 텍스트 라벨 자식 생성
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(_ghost.transform, false);
            var labelRt = labelObj.AddComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var txt = labelObj.AddComponent<Text>();
            txt.font = HangarButtonHelpers.GetKoreanFont();
            txt.text = Part?.data?.partName ?? "???";
            txt.fontSize = 12;
            txt.color = Color.black;
            txt.alignment = TextAnchor.MiddleCenter;

            // 원본 반투명화 + raycasting 비활성화
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0.4f;
            _canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData e)
        {
            if (_ghost == null) return;

            // 화면 좌표 → Canvas 로컬 좌표 변환
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rootCanvas.GetComponent<RectTransform>(),
                e.position,
                e.pressEventCamera,
                out var localPoint);

            _ghost.GetComponent<RectTransform>().localPosition = localPoint;
        }

        public void OnEndDrag(PointerEventData e)
        {
            // 고스트 정소
            if (_ghost != null)
            {
                Destroy(_ghost);
                _ghost = null;
            }

            // 원본 복구
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
            }
        }
    }
}
