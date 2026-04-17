using UnityEngine;

namespace Crux.Cinematic
{
    /// <summary>
    /// VFXTestScene 전용 파티클 스폰 테스터.
    /// - Space: 우측에서 피격(파편 왼쪽으로 비산)
    /// - 1/2/3/4: 상/하/좌/우 방향 피격 프리셋
    /// - 마우스 좌클릭: 클릭 위치에 스폰 (피격 방향 = runner → 클릭지점)
    /// </summary>
    public class VFXTestRunner : MonoBehaviour
    {
        [SerializeField] private GameObject impactVFXPrefab;
        [SerializeField] private UnityEngine.Camera mainCam;

        private void Update()
        {
            if (impactVFXPrefab == null) return;

            // Space — 기본 우측 피격
            if (Input.GetKeyDown(KeyCode.Space))
                SpawnAt(transform.position, Vector2.right);

            // 방향 프리셋
            if (Input.GetKeyDown(KeyCode.Alpha1)) SpawnAt(transform.position, Vector2.up);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SpawnAt(transform.position, Vector2.down);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SpawnAt(transform.position, Vector2.left);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SpawnAt(transform.position, Vector2.right);

            // 마우스 좌클릭 — 클릭 위치 스폰, 방향 = runner → 클릭
            if (Input.GetMouseButtonDown(0) && mainCam != null)
            {
                Vector3 wp = mainCam.ScreenToWorldPoint(Input.mousePosition);
                wp.z = 0;
                Vector2 dir = ((Vector2)(wp - transform.position)).normalized;
                if (dir == Vector2.zero) dir = Vector2.right;
                SpawnAt(wp, dir);
            }
        }

        /// <summary>
        /// 파티클 스폰 + 피격 방향으로 Transform rotation 적용.
        /// hitFromDir = 탄이 날아온 방향 (파편은 그 반대로 비산).
        /// Cone forward는 +Z (transform.forward). 2D에서 z-rotation으로 +X가 forward 되게 조정.
        /// </summary>
        private void SpawnAt(Vector3 pos, Vector2 hitFromDir)
        {
            // 파편이 튈 방향 = 탄이 온 반대 방향
            Vector2 splashDir = -hitFromDir.normalized;
            // +X축 기준 각도. Cone forward가 splashDir 향하도록 Transform 회전
            float angleDeg = Mathf.Atan2(splashDir.y, splashDir.x) * Mathf.Rad2Deg;

            // Unity 2D에서 Cone forward(+Z)를 XY 평면에 쓰려면 회전 조합 필요:
            // 먼저 X축 -90도 회전 (Z축 → Y축) 후 Z축으로 angleDeg 회전
            // 간단 대안: Cone을 Circle로 변경하거나 Shape.rotation 사용
            // 실용: Transform.rotation을 Z 회전 + X 90도 조합
            Quaternion rot = Quaternion.Euler(-90f, 0f, angleDeg);

            Instantiate(impactVFXPrefab, pos, rot);
            Debug.Log($"[VFX] ConcreteImpact @ {pos} hitFrom={hitFromDir} → splashDir={splashDir}");
        }
    }
}
