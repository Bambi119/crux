using UnityEngine;

namespace Crux.Camera
{
    /// <summary>전투 씬 카메라 제어 — 줌/팬/초기 프레이밍/반응 사격 시퀀스 지원</summary>
    public class BattleCamera : MonoBehaviour
    {
        private UnityEngine.Camera cam;
        private Vector3 targetPos;
        private float targetSize;

        private const float ZoomSpeed = 5f;
        private const float MinSizeConst = 3f;
        private const float MaxSizeConst = 8f;
        private const float PanSpeed = 8f;
        private const int EdgePanMargin = 15;

        private Vector3 savedPos;
        private float savedSize;

        // Pan bounds (grid 기준 절대 좌표)
        private float panMinX, panMaxX, panMinY, panMaxY;
        private bool boundsSet;

        public UnityEngine.Camera Cam => cam;
        public Vector3 TargetPos => targetPos;
        public float TargetSize => targetSize;
        public float MinSize => MinSizeConst;
        public float MaxSize => MaxSizeConst;

        /// <summary>Camera.main 획득 + 기본 설정</summary>
        public void Initialize()
        {
            cam = UnityEngine.Camera.main;
            if (cam != null) cam.orthographic = true;
        }

        /// <summary>초기 프레이밍 — 중심 좌표와 orthographic size를 즉시 적용</summary>
        public void SetInitialFraming(Vector3 centerPos, float size)
        {
            if (cam == null) return;
            targetPos = centerPos;
            targetSize = Mathf.Clamp(size, MinSizeConst, MaxSizeConst);
            cam.transform.position = targetPos;
            cam.orthographicSize = targetSize;
        }

        /// <summary>팬 경계 설정 (grid 좌표계 기준 min/max)</summary>
        public void SetPanBounds(float minX, float maxX, float minY, float maxY)
        {
            panMinX = minX; panMaxX = maxX;
            panMinY = minY; panMaxY = maxY;
            boundsSet = true;
        }

        /// <summary>매 프레임 호출 — blocked=true면 입력 무시 (반응 사격 중 등)</summary>
        public void Tick(bool blocked)
        {
            if (cam == null || blocked) return;

            // 마우스 휠 줌
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0)
            {
                targetSize -= scroll * 0.5f;
                targetSize = Mathf.Clamp(targetSize, MinSizeConst, MaxSizeConst);
            }

            // 가장자리 팬
            Vector3 panDir = Vector3.zero;
            Vector2 mp = Input.mousePosition;
            if (mp.x < EdgePanMargin) panDir.x = -1f;
            else if (mp.x > Screen.width - EdgePanMargin) panDir.x = 1f;
            if (mp.y < EdgePanMargin) panDir.y = -1f;
            else if (mp.y > Screen.height - EdgePanMargin) panDir.y = 1f;

            if (panDir != Vector3.zero)
            {
                targetPos += panDir * PanSpeed * Time.deltaTime;
                if (boundsSet)
                {
                    float halfH = targetSize;
                    float halfW = targetSize * cam.aspect;
                    targetPos.x = Mathf.Clamp(targetPos.x, panMinX - halfW * 0.3f, panMaxX + halfW * 0.3f);
                    targetPos.y = Mathf.Clamp(targetPos.y, panMinY - halfH * 0.3f, panMaxY + halfH * 0.3f);
                }
                targetPos.z = -10f;
            }

            // 부드러운 보간
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, ZoomSpeed * Time.deltaTime);
            cam.transform.position = Vector3.Lerp(cam.transform.position, targetPos, ZoomSpeed * Time.deltaTime);
        }

        /// <summary>반응 사격 시퀀스용 — 현재 타깃 상태 저장</summary>
        public void SaveState()
        {
            savedPos = targetPos;
            savedSize = targetSize;
        }

        /// <summary>저장된 상태로 즉시 복귀</summary>
        public void RestoreState()
        {
            targetPos = savedPos;
            targetSize = savedSize;
            if (cam != null)
            {
                cam.transform.position = savedPos;
                cam.orthographicSize = savedSize;
            }
        }

        /// <summary>부드러운 이동 — targetPos만 갱신, Tick의 Lerp가 보간</summary>
        public void PanTo(Vector3 worldPos)
        {
            targetPos = new Vector3(worldPos.x, worldPos.y, -10f);
            if (boundsSet && cam != null)
            {
                float halfH = targetSize;
                float halfW = targetSize * cam.aspect;
                targetPos.x = Mathf.Clamp(targetPos.x, panMinX - halfW * 0.3f, panMaxX + halfW * 0.3f);
                targetPos.y = Mathf.Clamp(targetPos.y, panMinY - halfH * 0.3f, panMaxY + halfH * 0.3f);
            }
        }

        /// <summary>즉시 이동 (Lerp 바이패스) — closeup/wide 시퀀스용</summary>
        public void SnapTo(Vector3 pos, float size)
        {
            targetPos = pos;
            targetSize = size;
            if (cam != null)
            {
                cam.transform.position = pos;
                cam.orthographicSize = size;
            }
        }
    }
}
