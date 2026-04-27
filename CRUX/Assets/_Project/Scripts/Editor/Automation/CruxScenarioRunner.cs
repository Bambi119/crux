#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Crux.EditorTools.Automation
{
    /// <summary>
    /// 시나리오 자동 실행기.
    /// 메뉴: Crux/Test/Run Scenario (Pick) | (Last)
    /// 출력: CRUX/Temp/crux-scenario-{name}.log + crux-scenario-{name}-summary.json
    /// 스크린샷: CRUX/Temp/crux-scenario-{name}/{idx}_{label}.png
    ///
    /// 도메인 리로드 안전: SessionState로 상태 유지.
    /// </summary>
    [InitializeOnLoad]
    public static class CruxScenarioRunner
    {
        // ──────────────────────────────────────────────
        //  SessionState 키
        // ──────────────────────────────────────────────
        const string KeyActive      = "CruxScenario.active";
        const string KeyAssetPath   = "CruxScenario.assetPath";
        const string KeyStepIdx     = "CruxScenario.stepIdx";
        const string KeyState       = "CruxScenario.state";
        const string KeyStepEnterTime = "CruxScenario.stepEnterTime";
        const string KeyStartTime   = "CruxScenario.startTime";

        // 상태기계 단계
        const int StateWaitForPlay       = 0;
        const int StateWaitForController = 1;
        const int StateRunStep           = 2;
        const int StateWaitStep          = 3;
        const int StateFinish            = 4;

        // 결과 임시 보관 (도메인 리로드 시 손실 — 같은 플레이 세션 내에서만 유효)
        static readonly List<StepResult> s_results = new List<StepResult>();
        static double s_stepWaitUntil;

        static CruxScenarioRunner()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        static bool Active => SessionState.GetBool(KeyActive, false);

        // ──────────────────────────────────────────────
        //  메뉴 항목
        // ──────────────────────────────────────────────

        [MenuItem("Crux/Test/Run Scenario (Pick)")]
        public static void PickAndRun()
        {
            var path = EditorUtility.OpenFilePanel(
                "Pick Scenario Asset",
                "Assets/_Project/ScriptableObjects/Scenarios",
                "asset");
            if (string.IsNullOrEmpty(path)) return;

            // Unity 프로젝트 상대 경로로 변환
            var projectRoot = Path.GetFullPath(Application.dataPath + "/..").Replace('\\', '/');
            path = path.Replace('\\', '/');
            if (path.StartsWith(projectRoot))
                path = "Assets" + path.Substring(projectRoot.Length + 6); // +6 = "/Assets" skip

            var asset = AssetDatabase.LoadAssetAtPath<CruxScenarioAsset>(path);
            if (asset == null)
            {
                Debug.LogError($"[ScenarioRunner] 자산 로드 실패: {path}");
                return;
            }
            Start(asset, path);
        }

        [MenuItem("Crux/Test/Run Scenario (Last)")]
        public static void RunLast()
        {
            var path = SessionState.GetString(KeyAssetPath, "");
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[ScenarioRunner] 직전 시나리오 없음 — Pick 먼저 실행하세요");
                return;
            }
            var asset = AssetDatabase.LoadAssetAtPath<CruxScenarioAsset>(path);
            if (asset == null)
            {
                Debug.LogError($"[ScenarioRunner] 직전 자산 로드 실패: {path}");
                return;
            }
            Start(asset, path);
        }

        // ──────────────────────────────────────────────
        //  시작
        // ──────────────────────────────────────────────

        public static void Start(CruxScenarioAsset asset, string assetPath)
        {
            if (Active)
            {
                Debug.LogWarning("[ScenarioRunner] 이미 실행 중");
                return;
            }
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[ScenarioRunner] 이미 PlayMode — 중단 후 재시도");
                return;
            }

            s_results.Clear();
            s_stepWaitUntil = 0;

            SessionState.SetBool(KeyActive, true);
            SessionState.SetString(KeyAssetPath, assetPath);
            SessionState.SetInt(KeyStepIdx, 0);
            SessionState.SetInt(KeyState, StateWaitForPlay);
            SessionState.SetFloat(KeyStartTime, (float)EditorApplication.timeSinceStartup);

            AppendLog(asset, $"[ScenarioRunner] START scenario={asset.scenarioName} steps={asset.steps.Count}");

            EditorSceneManager.OpenScene(asset.scenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        // ──────────────────────────────────────────────
        //  Tick — 상태기계
        // ──────────────────────────────────────────────

        static void Tick()
        {
            if (!Active) return;

            var assetPath = SessionState.GetString(KeyAssetPath, "");
            var asset = AssetDatabase.LoadAssetAtPath<CruxScenarioAsset>(assetPath);
            if (asset == null) { ForceFinish("asset-lost"); return; }

            int state = SessionState.GetInt(KeyState, StateWaitForPlay);

            switch (state)
            {
                case StateWaitForPlay:
                    TickWaitForPlay();
                    break;
                case StateWaitForController:
                    TickWaitForController(asset);
                    break;
                case StateRunStep:
                    TickRunStep(asset);
                    break;
                case StateWaitStep:
                    TickWaitStep(asset);
                    break;
                case StateFinish:
                    FinishRun(asset);
                    break;
            }
        }

        static void TickWaitForPlay()
        {
            if (EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                SessionState.SetInt(KeyState, StateWaitForController);
        }

        static void TickWaitForController(CruxScenarioAsset asset)
        {
            var bc = CruxScenarioInputHelper.GetController();
            if (bc == null) return;

            // 1초 안정화 대기
            float enterTime = SessionState.GetFloat(KeyStepEnterTime, 0f);
            if (enterTime <= 0f)
            {
                SessionState.SetFloat(KeyStepEnterTime, (float)EditorApplication.timeSinceStartup);
                return;
            }
            if (EditorApplication.timeSinceStartup - enterTime < 1.0) return;

            AppendLog(asset, "[ScenarioRunner] BattleController 준비 완료 — 스텝 실행 시작");
            SessionState.SetFloat(KeyStepEnterTime, 0f);
            SessionState.SetInt(KeyState, StateRunStep);
        }

        static void TickRunStep(CruxScenarioAsset asset)
        {
            int idx = SessionState.GetInt(KeyStepIdx, 0);
            if (idx >= asset.steps.Count)
            {
                SessionState.SetInt(KeyState, StateFinish);
                return;
            }

            var step = asset.steps[idx];
            bool passed = true;
            string actual = "";
            string detail = "";
            string capturePath = null;

            try
            {
                if (step.action == ScenarioAction.AssertState)
                {
                    actual = CruxScenarioInputHelper.ReadState(step.expectedStateKey);
                    passed = CruxScenarioInputHelper.AssertEquals(actual, step.expectedStateValue);
                    detail = $"key={step.expectedStateKey} expected={step.expectedStateValue} actual={actual}";
                }
                else
                {
                    CruxScenarioInputHelper.ApplyAction(step);
                    detail = $"action={step.action}";
                }
            }
            catch (Exception ex)
            {
                passed = false;
                detail = ex.Message;
            }

            // 캡처 판단
            bool doCapture = step.capture == CapturePolicy.Always
                          || (step.capture == CapturePolicy.OnFail && !passed);
            if (doCapture)
            {
                var capDir = GetCaptureDir(asset);
                capturePath = CruxScenarioCapture.Capture(capDir, idx, step.label);
            }

            string status = passed ? "PASS" : "FAIL";
            AppendLog(asset, $"[{status}] {idx:D3} {step.label} — {detail}");

            s_results.Add(new StepResult
            {
                idx = idx,
                label = step.label,
                action = step.action.ToString(),
                status = status,
                expected = step.expectedStateValue,
                actual = actual,
                capturePath = capturePath ?? "",
            });

            // 다음 단계로
            float wait = step.waitSeconds > 0f ? step.waitSeconds : 0.2f;
            s_stepWaitUntil = EditorApplication.timeSinceStartup + wait;
            SessionState.SetInt(KeyStepIdx, idx + 1);
            SessionState.SetInt(KeyState, StateWaitStep);
        }

        static void TickWaitStep(CruxScenarioAsset asset)
        {
            if (EditorApplication.timeSinceStartup < s_stepWaitUntil) return;
            int idx = SessionState.GetInt(KeyStepIdx, 0);
            if (idx >= asset.steps.Count)
                SessionState.SetInt(KeyState, StateFinish);
            else
                SessionState.SetInt(KeyState, StateRunStep);
        }

        // ──────────────────────────────────────────────
        //  완료 처리
        // ──────────────────────────────────────────────

        static void FinishRun(CruxScenarioAsset asset)
        {
            float startTime = SessionState.GetFloat(KeyStartTime, 0f);
            float duration = (float)(EditorApplication.timeSinceStartup - startTime);

            int passed = 0, failed = 0;
            foreach (var r in s_results)
                if (r.status == "PASS") passed++; else failed++;

            AppendLog(asset, $"[ScenarioRunner] FINISH passed={passed} failed={failed} duration={duration:F1}s");

            WriteSummaryJson(asset, passed, failed, duration);

            Cleanup();
            EditorApplication.isPlaying = false;
        }

        static void ForceFinish(string reason)
        {
            Debug.LogWarning($"[ScenarioRunner] ForceFinish: {reason}");
            Cleanup();
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;
        }

        static void Cleanup()
        {
            SessionState.SetBool(KeyActive, false);
            SessionState.SetInt(KeyState, StateWaitForPlay);
            SessionState.SetFloat(KeyStepEnterTime, 0f);
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (!Active) return;
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                // PlayMode가 예상 외 종료
                var assetPath = SessionState.GetString(KeyAssetPath, "");
                var asset = AssetDatabase.LoadAssetAtPath<CruxScenarioAsset>(assetPath);
                if (asset != null) AppendLog(asset, "[ScenarioRunner] PlayMode 조기 종료 — 강제 완료");
                Cleanup();
            }
        }

        // ──────────────────────────────────────────────
        //  로그·JSON 출력
        // ──────────────────────────────────────────────

        static string GetLogPath(CruxScenarioAsset asset)
            => $"Temp/crux-scenario-{SanitizeName(asset.scenarioName)}.log";

        static string GetSummaryPath(CruxScenarioAsset asset)
            => $"Temp/crux-scenario-{SanitizeName(asset.scenarioName)}-summary.json";

        static string GetCaptureDir(CruxScenarioAsset asset)
            => $"Temp/crux-scenario-{SanitizeName(asset.scenarioName)}";

        static void AppendLog(CruxScenarioAsset asset, string line)
        {
            var path = GetLogPath(asset);
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss.fff}] {line}\n");
            }
            catch { }
            Debug.Log(line);
        }

        static void WriteSummaryJson(CruxScenarioAsset asset, int passed, int failed, float duration)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"scenario\": \"{EscapeJson(asset.scenarioName)}\",");
            sb.AppendLine($"  \"scenePath\": \"{EscapeJson(asset.scenePath)}\",");
            sb.AppendLine($"  \"total\": {asset.steps.Count},");
            sb.AppendLine($"  \"passed\": {passed},");
            sb.AppendLine($"  \"failed\": {failed},");
            sb.AppendLine($"  \"durationSec\": {duration:F2},");
            sb.AppendLine("  \"steps\": [");
            for (int i = 0; i < s_results.Count; i++)
            {
                var r = s_results[i];
                string comma = i < s_results.Count - 1 ? "," : "";
                sb.AppendLine($"    {{\"idx\":{r.idx},\"label\":\"{EscapeJson(r.label)}\",\"action\":\"{r.action}\",\"status\":\"{r.status}\",\"expected\":\"{EscapeJson(r.expected)}\",\"actual\":\"{EscapeJson(r.actual)}\",\"capturePath\":\"{EscapeJson(r.capturePath)}\"}}{comma}");
            }
            sb.AppendLine("  ]");
            sb.Append("}");

            var path = GetSummaryPath(asset);
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, sb.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ScenarioRunner] summary.json 쓰기 실패: {ex.Message}");
            }
        }

        static string SanitizeName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        static string EscapeJson(string s) => s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";

        // ──────────────────────────────────────────────
        //  데이터 구조
        // ──────────────────────────────────────────────

        struct StepResult
        {
            public int idx;
            public string label;
            public string action;
            public string status;
            public string expected;
            public string actual;
            public string capturePath;
        }
    }
}
#endif
