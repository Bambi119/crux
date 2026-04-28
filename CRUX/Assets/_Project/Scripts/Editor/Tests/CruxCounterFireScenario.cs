#if UNITY_EDITOR
// ============================================================
// CruxCounterFireScenario.cs — PlayMode 반격 시나리오 스모크 테스트
//
// 목적: AI 사격 → 반격 패널 출현 → 무기 확인 → Actions 큐 검증.
//       실제 키보드 입력 없이 reflection으로 Confirm() 직접 호출.
//
// 의존하는 private/internal 멤버 (시그니처 변경 시 갱신 필요):
//   - BattleController: field `currentPhase` (TurnPhase), method `StartProcessEnemyTurnFrom(int)`
//   - CounterFireUIPanel: method `EditorOnly_Confirm(WeaponType)` (internal, #if UNITY_EDITOR)
//   - FireActionContext: property `Actions` (static List<FireActionData>)
//
// 한계:
//   - 실제 키보드/마우스 입력 시뮬 불가 — UX 입력 자체는 사용자 수동 확인 잔존
//   - AI 강제 턴 진입은 BattleController reflection 의존
//   - FireActionScene 전환이 발생하지 않는 씬 설정에서는 Step 3 타임아웃
// ============================================================

using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Crux.EditorTools.Tests
{
    /// <summary>
    /// 반격 흐름 PlayMode 시나리오 테스트.
    /// 결과: <c>Temp/crux-counter-playsmoke.log</c>
    /// </summary>
    [InitializeOnLoad]
    public static class CruxCounterFireScenario
    {
        const string LogPath    = "Temp/crux-counter-playsmoke.log";
        const float  TotalTimeout = 8f;

        // SessionState 키
        const string KeyActive    = "CruxCounterFire.active";
        const string KeyStep      = "CruxCounterFire.step";
        const string KeyStartTime = "CruxCounterFire.startTime";
        const string KeyStepTime  = "CruxCounterFire.stepTime";
        const string KeyPass      = "CruxCounterFire.pass";
        const string KeyFail      = "CruxCounterFire.fail";

        // 단계별 타임아웃(초)
        const float TimeoutBattleReady  = 4f;
        const float TimeoutFireScene    = 5f;

        // ===== 초기화 =====

        static CruxCounterFireScenario()
        {
            Application.logMessageReceivedThreaded += OnLog;
            EditorApplication.update               += Tick;
            EditorApplication.playModeStateChanged += OnStateChanged;
        }

        static bool Active => SessionState.GetBool(KeyActive, false);
        static int  Step   => SessionState.GetInt(KeyStep, 0);

        // ===== 로그 =====

        static void Append(string line)
        {
            try
            {
                var dir = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.AppendAllText(LogPath, line + "\n");
            }
            catch { }
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            if (!Active) return;
            Append($"[{type}] {condition}");
        }

        // ===== 상태 변경 감지 =====

        static void OnStateChanged(PlayModeStateChange change)
        {
            if (!Active) return;
            Append($"[SCENARIO] state={change} at={DateTime.Now:HH:mm:ss.fff}");
            if (change == PlayModeStateChange.EnteredEditMode)
                Finalize("exited-play");
        }

        // ===== 업데이트 루프 =====

        static void Tick()
        {
            if (!Active) return;

            float elapsed = (float)EditorApplication.timeSinceStartup
                            - SessionState.GetFloat(KeyStartTime, 0f);

            // 전체 타임아웃
            if (elapsed > TotalTimeout)
            {
                Debug.LogError($"[SCENARIO] FAIL timeout — {elapsed:F1}s elapsed");
                ForceExit("timeout");
                return;
            }

            int step = Step;
            switch (step)
            {
                case 1: TickWaitBattleReady(); break;
                case 2: TickForceAITurn();     break;
                case 3: TickWaitFireScene();   break;
                case 4: TickAssertPanel();     break;
                case 5: TickTriggerConfirm();  break;
                case 6: TickAssertQueued();    break;
                case 7: TickExit();            break;
            }
        }

        // ===== 단계별 핸들러 =====

        // Step 1 — BattleController 발견 + grace 1.5s
        static void TickWaitBattleReady()
        {
            if (!EditorApplication.isPlaying) return;

            float stepElapsed = StepElapsed();

            var bc = UnityEngine.Object.FindObjectOfType<Crux.Core.BattleController>();
            if (bc == null)
            {
                if (stepElapsed > TimeoutBattleReady)
                {
                    Debug.LogError("[SCENARIO] FAIL WaitBattleReady — BattleController not found (timeout)");
                    ForceExit("step1-timeout");
                }
                return;
            }

            // grace 1.5s 후 진행
            if (stepElapsed < 1.5f) return;

            Append($"[SCENARIO] BattleController found after {stepElapsed:F2}s");
            AdvanceStep(2);
        }

        // Step 2 — AI 턴 강제 진입 (reflection)
        static void TickForceAITurn()
        {
            var bc = UnityEngine.Object.FindObjectOfType<Crux.Core.BattleController>();
            if (bc == null)
            {
                Debug.LogError("[SCENARIO] FAIL ForceAITurn — BattleController lost");
                ForceExit("step2-no-bc");
                return;
            }

            // currentPhase 필드 설정
            var phaseField = typeof(Crux.Core.BattleController)
                .GetField("currentPhase", BindingFlags.Instance | BindingFlags.NonPublic);
            if (phaseField == null)
            {
                Debug.LogError("[SCENARIO] FAIL ForceAITurn — reflection failed: field 'currentPhase' not found");
                ForceExit("step2-reflection-phase");
                return;
            }

            phaseField.SetValue(bc, Crux.Core.TurnPhase.EnemyTurn);

            // StartProcessEnemyTurnFrom(0) 호출
            var method = typeof(Crux.Core.BattleController)
                .GetMethod("StartProcessEnemyTurnFrom",
                           BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (method == null)
            {
                Debug.LogError("[SCENARIO] FAIL ForceAITurn — reflection failed: method 'StartProcessEnemyTurnFrom' not found");
                ForceExit("step2-reflection-method");
                return;
            }

            try
            {
                method.Invoke(bc, new object[] { 0 });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SCENARIO] FAIL ForceAITurn — StartProcessEnemyTurnFrom threw: {ex.InnerException?.Message ?? ex.Message}");
                ForceExit("step2-invoke-error");
                return;
            }

            Append("[SCENARIO] ForceAITurn — EnemyTurn set, ProcessEnemyTurn(0) started");
            AdvanceStep(3);
        }

        // Step 3 — FireActionScene 전환 대기
        static void TickWaitFireScene()
        {
            if (SceneManager.GetActiveScene().name == "FireActionScene")
            {
                Append("[SCENARIO] FireActionScene detected");
                AdvanceStep(4);
                return;
            }
            if (StepElapsed() > TimeoutFireScene)
            {
                Debug.LogError($"[SCENARIO] FAIL WaitFireScene — timeout {TimeoutFireScene}s (current={SceneManager.GetActiveScene().name})");
                ForceExit("step3-timeout");
            }
        }

        // Step 4 — CounterFireUIPanel 활성 확인
        static void TickAssertPanel()
        {
            var panel = UnityEngine.Object.FindObjectOfType<Crux.Cinematic.CounterFireUIPanel>();
            if (panel == null)
            {
                Debug.LogError("[SCENARIO] FAIL CounterFireUIPanel.Active — component not found");
                ForceExit("step4-no-panel");
                return;
            }

            if (!panel.gameObject.activeInHierarchy)
            {
                // 패널이 아직 초기화 중일 수 있으므로 최대 1s 대기
                if (StepElapsed() < 1f) return;
                Debug.LogError("[SCENARIO] FAIL CounterFireUIPanel.Active — gameObject not active (1s grace elapsed)");
                ForceExit("step4-panel-inactive");
                return;
            }

            Debug.Log("[SCENARIO] PASS CounterFireUIPanel.Active");
            AdvanceStep(5);
        }

        // Step 5 — EditorOnly_Confirm(MainGun) 호출
        static void TickTriggerConfirm()
        {
            var panel = UnityEngine.Object.FindObjectOfType<Crux.Cinematic.CounterFireUIPanel>();
            if (panel == null)
            {
                Debug.LogError("[SCENARIO] FAIL TriggerConfirm — CounterFireUIPanel lost");
                ForceExit("step5-no-panel");
                return;
            }

            // EditorOnly_Confirm(WeaponType.MainGun) — internal, 같은 빌드 내 reflection 접근
            var confirmMethod = typeof(Crux.Cinematic.CounterFireUIPanel)
                .GetMethod("EditorOnly_Confirm",
                           BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (confirmMethod == null)
            {
                Debug.LogError("[SCENARIO] FAIL TriggerConfirm — EditorOnly_Confirm method not found");
                ForceExit("step5-no-method");
                return;
            }

            try
            {
                confirmMethod.Invoke(panel, new object[] { Crux.Core.WeaponType.MainGun });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SCENARIO] FAIL TriggerConfirm — EditorOnly_Confirm threw: {ex.InnerException?.Message ?? ex.Message}");
                ForceExit("step5-invoke-error");
                return;
            }

            Append("[SCENARIO] TriggerConfirm — EditorOnly_Confirm(MainGun) called");
            AdvanceStep(6);
        }

        // Step 6 — FireActionContext.Actions.Count >= 2 검증
        static void TickAssertQueued()
        {
            // Actions가 채워지는 데 한 프레임 여유
            if (StepElapsed() < 0.1f) return;

            int count = Crux.Core.FireActionContext.Actions?.Count ?? 0;
            if (count >= 2)
            {
                Debug.Log($"[SCENARIO] PASS CounterFire.Queued — actions={count}");
            }
            else
            {
                Debug.LogError($"[SCENARIO] FAIL CounterFire.Queued — actions={count} (expected >=2)");
            }
            AdvanceStep(7);
        }

        // Step 7 — PlayMode 종료
        static void TickExit()
        {
            EditorApplication.isPlaying = false;
            AdvanceStep(8);
        }

        // ===== 유틸 =====

        static float StepElapsed()
            => (float)EditorApplication.timeSinceStartup - SessionState.GetFloat(KeyStepTime, 0f);

        static void AdvanceStep(int next)
        {
            SessionState.SetInt(KeyStep, next);
            SessionState.SetFloat(KeyStepTime, (float)EditorApplication.timeSinceStartup);
        }

        static void ForceExit(string reason)
        {
            Append($"[SCENARIO] ForceExit reason={reason}");
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;
            else
                Finalize(reason);
        }

        static void Finalize(string reason)
        {
            int pass = SessionState.GetInt(KeyPass, 0);
            int fail = SessionState.GetInt(KeyFail, 0);
            // pass/fail 집계: 로그 파싱 방식 대신 내부 카운터 없이 로그 파일 분석으로 위임
            // (CruxTestRunner와 동일하게 [SCENARIO] PASS / FAIL 마커 기반)
            Append($"[SCENARIO] === finished reason={reason} at={DateTime.Now:HH:mm:ss.fff} ===");
            Append("[SCENARIO] (pass/fail 집계: crux-counter-playsmoke.log 내 PASS/FAIL 마커 참조)");

            SessionState.SetBool(KeyActive, false);
            SessionState.SetInt(KeyStep, 0);
            SessionState.SetFloat(KeyStartTime, 0f);
            SessionState.SetFloat(KeyStepTime, 0f);
        }

        // ===== 메뉴 =====

        [MenuItem("Crux/Test/PlaySmoke CounterFire (8s)")]
        public static void RunCounterFireScenario()
        {
            if (Active)
            {
                Debug.LogWarning("[SCENARIO] 이미 실행 중 — 무시");
                return;
            }
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[SCENARIO] 이미 PlayMode — 취소");
                return;
            }

            const string scenePath = "Assets/_Project/Scenes/TerrainTestScene.unity";
            if (!File.Exists(scenePath))
            {
                Debug.LogError($"[SCENARIO] 씬 파일 없음: {scenePath}");
                return;
            }

            try { if (File.Exists(LogPath)) File.Delete(LogPath); } catch { }
            Append($"[SCENARIO] start at={DateTime.Now:HH:mm:ss.fff} timeout={TotalTimeout}s");
            Append($"[SCENARIO] unity={Application.unityVersion}");

            SessionState.SetBool(KeyActive, true);
            SessionState.SetInt(KeyStep, 1);
            SessionState.SetFloat(KeyStartTime, (float)EditorApplication.timeSinceStartup);
            SessionState.SetFloat(KeyStepTime,  (float)EditorApplication.timeSinceStartup);

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
    }
}
#endif
