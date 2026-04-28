using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using Crux.Data;

namespace Crux.UI.Deployment
{
    /// <summary>
    /// 편성 화면에서 확정한 전차별 승무원 배치 저장 데이터.
    /// JSON 직렬화로 Application.persistentDataPath에 저장.
    /// </summary>
    [Serializable]
    public class DeploymentSaveData
    {
        public List<TankDeployment> tanks = new List<TankDeployment>();
    }

    /// <summary>
    /// 전차별 승무원 배치. SO GUID 기반 참조.
    /// </summary>
    [Serializable]
    public class TankDeployment
    {
        public string tankSOGuid = "";
        public string commanderGuid = "";
        public string gunnerGuid = "";
        public string loaderGuid = "";
        public string driverGuid = "";
        public string mgMechanicGuid = "";
    }

    /// <summary>
    /// 편성 상태 저장/복구 정적 저장소.
    /// BattleStateStorage 패턴 적용 (docs/04 §8.3).
    /// </summary>
    public static class DeploymentStorage
    {
        private static readonly string SavePath = Path.Combine(
            Application.persistentDataPath,
            "deployment.json"
        );

        private static DeploymentSaveData cachedData;
        public static bool HasSavedDeployment { get; private set; }

        /// <summary>편성 데이터 저장</summary>
        public static void Save(DeploymentSaveData data)
        {
            if (data == null) return;

            cachedData = data;
            string json = JsonUtility.ToJson(data, prettyPrint: true);

            try
            {
                File.WriteAllText(SavePath, json);
                HasSavedDeployment = true;
                Debug.Log($"[CRUX] Deployment saved to {SavePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[CRUX] Failed to save deployment: {ex.Message}");
            }
        }

        /// <summary>편성 데이터 로드</summary>
        public static DeploymentSaveData Load()
        {
            if (cachedData != null) return cachedData;

            if (!File.Exists(SavePath))
            {
                HasSavedDeployment = false;
                return null;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                cachedData = JsonUtility.FromJson<DeploymentSaveData>(json);
                HasSavedDeployment = cachedData != null && cachedData.tanks.Count > 0;
                Debug.Log($"[CRUX] Deployment loaded from {SavePath} ({cachedData?.tanks.Count ?? 0} tanks)");
                return cachedData;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[CRUX] Failed to load deployment: {ex.Message}");
                HasSavedDeployment = false;
                return null;
            }
        }

        /// <summary>편성 데이터 삭제</summary>
        public static void Clear()
        {
            cachedData = null;
            HasSavedDeployment = false;

            if (File.Exists(SavePath))
            {
                try
                {
                    File.Delete(SavePath);
                    Debug.Log("[CRUX] Deployment cleared");
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[CRUX] Failed to delete deployment file: {ex.Message}");
                }
            }
        }
    }
}
