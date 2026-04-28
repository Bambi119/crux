#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Crux.EditorTools.Automation
{
    /// <summary>
    /// PostMoveFlow 5-step PoC 시나리오 자산 자동 생성 헬퍼.
    /// 메뉴: Crux/Test/Create PostMoveFlow Scenario
    /// 생성 위치: Assets/_Project/ScriptableObjects/Scenarios/Scenario_PostMoveFlow.asset
    /// </summary>
    public static class CruxScenarioPoC
    {
        const string AssetDir  = "Assets/_Project/ScriptableObjects/Scenarios";
        const string AssetPath = AssetDir + "/Scenario_PostMoveFlow.asset";

        [MenuItem("Crux/Test/Create PostMoveFlow Scenario")]
        public static void CreatePostMoveFlowScenario()
        {
            // 이미 존재하면 Ping 후 종료
            var existing = AssetDatabase.LoadAssetAtPath<CruxScenarioAsset>(AssetPath);
            if (existing != null)
            {
                Debug.Log($"[ScenarioPoC] 이미 존재함 — {AssetPath}");
                EditorGUIUtility.PingObject(existing);
                return;
            }

            // 폴더 없으면 생성
            if (!AssetDatabase.IsValidFolder(AssetDir))
            {
                Directory.CreateDirectory(AssetDir);
                AssetDatabase.Refresh();
            }

            var asset = ScriptableObject.CreateInstance<CruxScenarioAsset>();
            asset.scenarioName = "PostMoveFlow";
            asset.scenePath    = "Assets/_Project/Scenes/TerrainTestScene.unity";
            asset.defaultStepTimeoutSec = 3f;
            asset.steps = BuildSteps();

            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ScenarioPoC] 생성 완료 — {AssetPath}");
            EditorGUIUtility.PingObject(asset);
        }

        static List<CruxScenarioStep> BuildSteps()
        {
            return new List<CruxScenarioStep>
            {
                // Step 0 — 이동 가능 셀 클릭 (인스펙터에서 조정)
                new CruxScenarioStep
                {
                    label       = "ClickMoveCell",
                    action      = ScenarioAction.ClickCell,
                    cellTarget  = new Vector2Int(4, 5), // placeholder — 인스펙터에서 수정
                    waitSeconds = 0.3f,
                    capture     = CapturePolicy.Always,
                },
                // Step 1 — MoveDirectionSelect 진입 확인
                new CruxScenarioStep
                {
                    label              = "AssertMoveDirSelect",
                    action             = ScenarioAction.AssertState,
                    expectedStateKey   = "CurrentInputMode",
                    expectedStateValue = "MoveDirectionSelect",
                    waitSeconds        = 0.2f,
                    capture            = CapturePolicy.OnFail,
                },
                // Step 2 — 60도 회전 방향 설정
                new CruxScenarioStep
                {
                    label       = "Rotate60",
                    action      = ScenarioAction.RotateAngle,
                    apiArg      = "60",
                    waitSeconds = 0.2f,
                    capture     = CapturePolicy.None,
                },
                // Step 3 — 이동 확정
                new CruxScenarioStep
                {
                    label       = "CommitMove",
                    action      = ScenarioAction.CommitMoveDirection,
                    waitSeconds = 0.4f,
                    capture     = CapturePolicy.Always,
                },
                // Step 4 — IsPostMoveContext 확인
                new CruxScenarioStep
                {
                    label              = "AssertPostMove",
                    action             = ScenarioAction.AssertState,
                    expectedStateKey   = "IsPostMoveContext",
                    expectedStateValue = "True",
                    waitSeconds        = 0.2f,
                    capture            = CapturePolicy.OnFail,
                },
            };
        }
    }
}
#endif
