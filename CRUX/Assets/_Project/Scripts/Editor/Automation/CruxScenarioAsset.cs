#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Crux.EditorTools.Automation
{
    /// <summary>
    /// 자동화 시나리오 정의 ScriptableObject.
    /// 메뉴: Assets → Create → Crux → Test → Scenario
    /// 저장 위치: Assets/_Project/ScriptableObjects/Scenarios/
    /// </summary>
    [CreateAssetMenu(menuName = "Crux/Test/Scenario", fileName = "Scenario_New")]
    public class CruxScenarioAsset : ScriptableObject
    {
        [Tooltip("시나리오 식별 이름 (로그 파일명에 사용)")]
        public string scenarioName = "Unnamed";

        [Tooltip("실행할 Unity 씬 경로")]
        public string scenePath = "Assets/_Project/Scenes/TerrainTestScene.unity";

        [Tooltip("각 스텝의 기본 타임아웃(초) — 스텝 waitSeconds가 0이면 이 값 사용")]
        public float defaultStepTimeoutSec = 3f;

        [Tooltip("실행할 스텝 목록 (순서대로 실행)")]
        public List<CruxScenarioStep> steps = new List<CruxScenarioStep>();
    }
}
#endif
