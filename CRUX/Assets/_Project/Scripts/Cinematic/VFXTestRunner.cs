using UnityEngine;
using Crux.Camera;

namespace Crux.Cinematic
{
    /// <summary>VFXTestScene 전용 파티클 스폰 테스터. Space 또는 마우스 좌클릭으로 재생.</summary>
    public class VFXTestRunner : MonoBehaviour
    {
        [SerializeField] private GameObject impactVFXPrefab;
        [SerializeField] private UnityEngine.Camera mainCam;

        private void Update()
        {
            if (impactVFXPrefab == null) return;

            if (Input.GetKeyDown(KeyCode.Space))
                SpawnAt(transform.position);

            if (Input.GetMouseButtonDown(0) && mainCam != null)
            {
                Vector3 wp = mainCam.ScreenToWorldPoint(Input.mousePosition);
                wp.z = 0;
                SpawnAt(wp);
            }
        }

        private void SpawnAt(Vector3 pos)
        {
            Instantiate(impactVFXPrefab, pos, Quaternion.identity);
            Debug.Log($"[VFX] ConcreteImpact @ {pos}");
        }
    }
}
