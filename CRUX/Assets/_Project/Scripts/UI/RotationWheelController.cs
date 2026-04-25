using System;
using UnityEngine;
using UnityEngine.UI;

namespace Crux.UI
{
    /// <summary>
    /// 원형 휠 형태 방향 선택기.
    ///
    /// 6방향(0°=N, 60°=NE, 120°=SE, 180°=S, 240°=SW, 300°=NW) 중 하나를 선택.
    /// CommandBox→Rotate 메뉴 및 post-move 방향 선택 양쪽에서 재사용.
    ///
    /// 참고: docs/10c §2.3 — 방향 전환 휠 입력 흐름
    /// hex flat-top 각도 규약: 0°=North(위), 시계방향 60° 단위
    /// </summary>
    public class RotationWheelController : MonoBehaviour
    {
        // ------------------------------------------------------------------ 이벤트
        /// <summary>방향 확정 — 절대각도(0=N, 60=NE, 120=SE, 180=S, 240=SW, 300=NW)</summary>
        public event Action<float> OnAngleSelected;

        /// <summary>취소 (Esc 또는 외부 취소)</summary>
        public event Action OnCanceled;

        // ------------------------------------------------------------------ SerializeField
        [Header("섹터 이미지 (SectorN~SectorNW 순서, 0°부터 60° 단위)")]
        [SerializeField] private Image[] sectorImages = new Image[6];

        [Header("현재 방향 인디케이터 (노란색 화살표)")]
        [SerializeField] private RectTransform currentIndicator;

        [Header("호버 강조 인디케이터")]
        [SerializeField] private RectTransform hoverIndicator;

        [Header("휠 배경")]
        [SerializeField] private Image wheelBackground;

        // ------------------------------------------------------------------ 색상 상수
        private static readonly Color SectorNormal  = new Color(0f,   0f,   0f,   0.5f); // 어두운 반투명
        private static readonly Color SectorHover   = new Color(1f,   1f,   1f,   0.8f); // 흰색 강조
        private static readonly Color IndicatorColor = new Color(1f,   0.85f, 0f,  1f);  // 노란색

        // 섹터 각도 (0°=N, 시계방향)
        private static readonly float[] SectorAngles = { 0f, 60f, 120f, 180f, 240f, 300f };

        // 키보드 fallback 매핑 (키 → 각도)
        // hex flat-top 기준: W=N(0), E=NE(60), D=SE(120), S=S(180), A=SW(240), Q=NW(300)
        // 과제 명세: Q=300, W=0, E=60, A=240, S=180, D=120
        private static readonly (KeyCode key, float angle)[] KeyMappings =
        {
            (KeyCode.W, 0f),
            (KeyCode.E, 60f),
            (KeyCode.D, 120f),
            (KeyCode.S, 180f),
            (KeyCode.A, 240f),
            (KeyCode.Q, 300f),
        };

        // ------------------------------------------------------------------ 런타임 상태
        private bool   isActive      = false;
        private int    hoveredIndex  = -1;   // -1 = 없음
        private float  currentAngle  = 0f;

        // ------------------------------------------------------------------ Unity 콜백
        private void Awake()
        {
            AutoBindChildrenIfNeeded();
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!isActive) return;

            HandleMouseHover();
            HandleKeyboardInput();
        }

        private void OnDisable()
        {
            // 이벤트 리스너 해제 없이 단순 비활성 → 누수 없음
            // (이벤트는 외부에서 -= 로 구독 해제)
            isActive = false;
        }

        // ------------------------------------------------------------------ 공개 API
        /// <summary>
        /// 휠을 특정 월드 위치에 표시.
        /// </summary>
        /// <param name="worldPos">월드 좌표 (유닛 위치)</param>
        /// <param name="currentAngleIn">현재 포탑/차체 각도 (인디케이터 표시용)</param>
        /// <param name="cam">사용할 카메라 — null이면 Camera.main 폴백</param>
        public void Show(Vector3 worldPos, float currentAngleIn, UnityEngine.Camera cam = null)
        {
            gameObject.SetActive(true);
            isActive    = true;
            currentAngle = currentAngleIn;
            hoveredIndex = -1;

            PositionAt(worldPos, cam);
            UpdateCurrentIndicator();
            UpdateHoverVisuals(-1);
        }

        /// <summary>휠 숨김</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
            isActive = false;
        }

        // ------------------------------------------------------------------ 위치 계산
        /// <summary>
        /// CommandBoxController.ShowMenuAt와 동일 패턴:
        /// worldPos → screenPos → ScreenPointToLocalPointInRectangle → 경계 플립.
        /// </summary>
        private void PositionAt(Vector3 worldPos, UnityEngine.Camera cam)
        {
            var rt = GetComponent<RectTransform>();
            if (rt == null) return;

            var parentCanvas = GetComponentInParent<Canvas>();
            var canvasRT     = parentCanvas != null ? parentCanvas.GetComponent<RectTransform>() : null;

            var resolvedCam = cam ?? UnityEngine.Camera.main;
            if (canvasRT == null || resolvedCam == null) return;

            Vector3 screenPos = resolvedCam.WorldToScreenPoint(worldPos);
            Vector2 size      = rt.sizeDelta;

            UnityEngine.Camera uiCam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : resolvedCam;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT, screenPos, uiCam, out Vector2 localPos);

            // 휠은 유닛 위에 중앙 표시 (약간 위 오프셋)
            // 화면 우측 가까우면 중앙 유지, 경계 초과 시 클램프
            float halfW = size.x * 0.5f;
            float halfH = size.y * 0.5f;

            // 수평 — 화면 경계 초과 시 플립
            if (screenPos.x + halfW > Screen.width)
                localPos.x -= halfW;
            else if (screenPos.x - halfW < 0)
                localPos.x += halfW;

            // 수직 — 기본은 유닛 위 중앙, 상단 클립 시 아래로
            localPos.y += halfH + 20f;
            if (screenPos.y + halfH + 20f > Screen.height)
                localPos.y -= size.y + 40f;

            rt.anchoredPosition = localPos;
        }

        // ------------------------------------------------------------------ 마우스 호버
        private void HandleMouseHover()
        {
            var rt = GetComponent<RectTransform>();
            if (rt == null) return;

            var parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null) return;

            UnityEngine.Camera uiCam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : UnityEngine.Camera.main;

            // 마우스 → Canvas 로컬 좌표
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rt, Input.mousePosition, uiCam, out Vector2 localMouse))
                return;

            // 중심에서의 방향 벡터
            Vector2 dir = localMouse; // 피벗 = 중심(0.5,0.5) 가정
            if (dir.sqrMagnitude < 1f) // 너무 가까우면 호버 없음
            {
                SetHoveredIndex(-1);
                return;
            }

            // 마우스 방향 → 각도 (Unity 2D: atan2(y,x), 오른쪽=0, 반시계)
            // hex flat-top: 위=0°, 시계방향 → 변환 필요
            // Unity atan2 기준 N(위) = 90°, 시계방향으로 변환
            float unityAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg; // -180~180
            // hex 각도로 변환: 위=0, 시계방향 → (90 - unityAngle)로 변환
            float hexAngle = (90f - unityAngle + 360f) % 360f;

            int nearest = NearestSectorIndex(hexAngle);
            SetHoveredIndex(nearest);

            // 좌클릭 확정
            if (Input.GetMouseButtonDown(0))
            {
                ConfirmAngle(SectorAngles[nearest]);
            }
        }

        // ------------------------------------------------------------------ 키보드 입력
        private void HandleKeyboardInput()
        {
            // 키보드 fallback: 각 키가 직접 각도 지정
            foreach (var (key, angle) in KeyMappings)
            {
                if (Input.GetKeyDown(key))
                {
                    int idx = AngleToSectorIndex(angle);
                    SetHoveredIndex(idx);
                    return;
                }
            }

            // Space/Enter: 현재 호버 확정
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                if (hoveredIndex >= 0)
                    ConfirmAngle(SectorAngles[hoveredIndex]);
                return;
            }

            // Esc: 취소
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnCanceled?.Invoke();
                Hide();
            }
        }

        // ------------------------------------------------------------------ 확정
        private void ConfirmAngle(float angle)
        {
            OnAngleSelected?.Invoke(angle);
            Hide();
        }

        // ------------------------------------------------------------------ 유틸
        /// <summary>hex 각도(0~360) → 가장 가까운 섹터 인덱스</summary>
        private static int NearestSectorIndex(float hexAngle)
        {
            // 각 섹터 중심: 0, 60, 120, 180, 240, 300
            // 거리 = min((hexAngle - sector + 360) % 360, (sector - hexAngle + 360) % 360)
            int best    = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < 6; i++)
            {
                float d = Mathf.Abs(Mathf.DeltaAngle(hexAngle, SectorAngles[i]));
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        private static int AngleToSectorIndex(float angle)
        {
            // SectorAngles 배열에서 정확 매칭
            for (int i = 0; i < SectorAngles.Length; i++)
                if (Mathf.Approximately(SectorAngles[i], angle)) return i;
            return NearestSectorIndex(angle);
        }

        // ------------------------------------------------------------------ 시각 업데이트
        private void SetHoveredIndex(int idx)
        {
            if (idx == hoveredIndex) return;
            hoveredIndex = idx;
            UpdateHoverVisuals(idx);
        }

        /// <summary>섹터 이미지 색상 및 호버 인디케이터 위치 갱신</summary>
        private void UpdateHoverVisuals(int hovIdx)
        {
            for (int i = 0; i < sectorImages.Length; i++)
            {
                if (sectorImages[i] == null) continue;
                sectorImages[i].color = (i == hovIdx) ? SectorHover : SectorNormal;
            }

            if (hoverIndicator != null)
            {
                hoverIndicator.gameObject.SetActive(hovIdx >= 0);
                if (hovIdx >= 0)
                {
                    // 인디케이터를 호버 섹터 방향으로 회전
                    // Unity Image 기본 "위=0°" 기준으로 hex 각도를 -Z 회전으로 변환
                    float uiRot = -SectorAngles[hovIdx]; // 시계방향 → Unity Z 음수
                    hoverIndicator.localRotation = Quaternion.Euler(0f, 0f, uiRot);
                }
            }
        }

        /// <summary>현재 방향 인디케이터 회전 갱신</summary>
        private void UpdateCurrentIndicator()
        {
            if (currentIndicator == null) return;
            float uiRot = -currentAngle;
            currentIndicator.localRotation = Quaternion.Euler(0f, 0f, uiRot);
        }

        // ------------------------------------------------------------------ 자동 바인딩
        /// <summary>
        /// SerializeField 미연결 시 자식 이름으로 자동 바인딩.
        /// RotationWheel.prefab 구조에 맞춰진 이름 규약 사용.
        /// </summary>
        private void AutoBindChildrenIfNeeded()
        {
            bool needsBind = sectorImages == null || sectorImages.Length != 6;
            if (!needsBind)
                foreach (var img in sectorImages)
                    if (img == null) { needsBind = true; break; }

            if (needsBind)
            {
                sectorImages = new Image[6];
                string[] names = { "SectorN", "SectorNE", "SectorSE", "SectorS", "SectorSW", "SectorNW" };
                Transform sectorsParent = transform.Find("Sectors");
                Transform searchRoot = sectorsParent != null ? sectorsParent : transform;

                for (int i = 0; i < names.Length; i++)
                {
                    Transform child = searchRoot.Find(names[i]);
                    if (child != null)
                        sectorImages[i] = child.GetComponent<Image>();
                    else
                        Debug.LogWarning($"[RotationWheel] 자식 '{names[i]}' 없음 — 프리팹 구조 확인 필요");
                }
            }

            if (currentIndicator == null)
            {
                Transform t = transform.Find("CurrentIndicator");
                if (t != null) currentIndicator = t.GetComponent<RectTransform>();
            }

            if (hoverIndicator == null)
            {
                Transform t = transform.Find("HoverIndicator");
                if (t != null) hoverIndicator = t.GetComponent<RectTransform>();
            }

            if (wheelBackground == null)
            {
                Transform t = transform.Find("WheelBackground");
                if (t != null) wheelBackground = t.GetComponent<Image>();
            }
        }
    }
}
