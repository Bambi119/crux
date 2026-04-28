using UnityEngine;
using Crux.Data;

namespace Crux.UI.Deployment
{
    /// <summary>
    /// 편성 씬(Crew Deployment Scene) 부트스트랩.
    /// CrewDeploymentController 싱글톤 초기화 및 씬 진입 시 상태 복원.
    /// </summary>
    public class CrewDeploymentSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private CrewDeploymentController deploymentController;

        private void Start()
        {
            if (deploymentController == null)
            {
                Debug.LogError("[CRUX] CrewDeploymentSceneBootstrap: deploymentController not assigned");
                return;
            }

            // 저장된 편성 복원 시도
            DeploymentSaveData savedData = DeploymentStorage.Load();
            if (savedData != null && savedData.tanks.Count > 0)
            {
                TryLoadDeployment(savedData);
                Debug.Log("[CRUX] Deployment restored from save");
            }
            else
            {
                Debug.Log("[CRUX] No saved deployment; starting fresh");
            }
        }

        /// <summary>저장 데이터로부터 편성 복원</summary>
        private void TryLoadDeployment(DeploymentSaveData data)
        {
            foreach (var tankDeployment in data.tanks)
            {
                // TODO: tank GUID → TankDataSO 로드
                // TODO: crew GUID들 → CrewMemberSO 로드
                // TODO: deploymentController.RestoreDeployment(tank, crew배열) 호출
            }
        }

        /// <summary>씬 이탈 시 현재 편성 저장 (선택사항)</summary>
        private void OnDestroy()
        {
            // Optional: 편성 화면에서 나갈 때 현재 선택을 임시 저장
            // deploymentController.SaveTempState() 등
        }
    }
}
