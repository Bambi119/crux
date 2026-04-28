#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Crux.EditorTools.Tests;

namespace Crux.EditorTools
{
    /// <summary>
    /// 자율 정적 테스트 러너.
    ///
    /// 목적: Unity MCP `execute_menu_item` 으로 호출해서 P2A/P2B/P2C 어설션 테스트를
    /// 한 번에 실행하고 전체 로그를 파일로 덤프. Claude/에이전트가 파일 Read로 PASS/FAIL 확인.
    ///
    /// 호출:
    /// - `Crux/Test/Run All Static` — P2A + P2B + P2C + P4B + P4C + P4D 순차 실행
    /// - `Crux/Test/Run P2A` / `P2B` / `P2C` / `P4B` / `P4C` / `P4D` — 개별 실행
    ///
    /// 결과 파일: `CRUX/Temp/crux-tests.log` — 실행 시각 + 각 테스트의 Debug.Log 전문
    /// 파일 끝에 `[RUNNER] result: passed=N failed=N` 요약 1줄.
    ///
    /// PASS 판정: 파일에 `FAIL` 문자열 없음 + `failed=0` 포함.
    ///
    /// 주의: CruxPlaySmoke 와 독립 — 이 러너는 EditMode에서만 동작, PlayMode 진입 없음.
    /// </summary>
    public static class CruxTestRunner
    {
        const string LogPath = "Temp/crux-tests.log";

        static bool capturing;
        static int passedCount;
        static int failedCount;

        static void OnLog(string condition, string stack, LogType type)
        {
            if (!capturing) return;
            Append($"[{type}] {condition}");
            if (type == LogType.Error || type == LogType.Exception) failedCount++;
            else if (condition.Contains("PASS") || condition.Contains(" OK ")) passedCount++;
        }

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

        static void BeginCapture(string testName)
        {
            passedCount = 0;
            failedCount = 0;
            capturing = true;
            Application.logMessageReceivedThreaded += OnLog;
            Append($"[RUNNER] ===== {testName} begin at={DateTime.Now:HH:mm:ss.fff} =====");
        }

        static void EndCapture(string testName)
        {
            Application.logMessageReceivedThreaded -= OnLog;
            capturing = false;
            Append($"[RUNNER] {testName} end — passed={passedCount} failed={failedCount}");
        }

        static void RunOne(string testName, Action testMethod)
        {
            BeginCapture(testName);
            try
            {
                testMethod?.Invoke();
            }
            catch (Exception ex)
            {
                Append($"[Exception] {ex.GetType().Name}: {ex.Message}");
                Append(ex.StackTrace ?? "");
                failedCount++;
            }
            EndCapture(testName);
        }

        static void ResetLog()
        {
            try { if (File.Exists(LogPath)) File.Delete(LogPath); } catch { }
        }

        // ===== 메뉴 항목 =====

        [MenuItem("Crux/Test/Run All Static")]
        public static void RunAllStatic()
        {
            ResetLog();
            Append($"[RUNNER] session start at={DateTime.Now:HH:mm:ss.fff} unity={Application.unityVersion}");

            int totalPassed = 0;
            int totalFailed = 0;

            RunOne("P2A", P2A_CrewRuntimeTest.Execute);
            totalPassed += passedCount; totalFailed += failedCount;

            RunOne("P2B", P2B_HullDataTest.Execute);
            totalPassed += passedCount; totalFailed += failedCount;

            RunOne("P2C", P2C_InitiativeTest.Execute);
            totalPassed += passedCount; totalFailed += failedCount;

            RunOne("P4B", P4B_CompatibilityTest.Execute);
            totalPassed += passedCount; totalFailed += failedCount;

            RunOne("P4C", P4C_TankInstanceTest.Execute);
            totalPassed += passedCount; totalFailed += failedCount;

            RunOne("P4D", P4D_ConvoyInventoryTest.Execute);
            totalPassed += passedCount; totalFailed += failedCount;

            RunOne("P6", P6_TraitEffectsTest.Execute);
            totalPassed += passedCount; totalFailed += failedCount;

            RunOne("P6B", P6B_TraitIntegrationTest.Execute);
            totalPassed += passedCount; totalFailed += failedCount;

            RunOne("P7", P7_CrewDeploymentTest.Execute);
            totalPassed += passedCount; totalFailed += failedCount;

            RunOne("LintCounterFireOrder", CruxStaticLint.LintCounterFireOrder);
            totalPassed += passedCount; totalFailed += failedCount;

            RunOne("LintFireExecutorClearSideEffect", CruxStaticLint.LintFireExecutorClearSideEffect);
            totalPassed += passedCount; totalFailed += failedCount;

            Append($"[RUNNER] ===== TOTAL passed={totalPassed} failed={totalFailed} =====");
            Debug.Log($"[RUNNER] wrote {LogPath} — passed={totalPassed} failed={totalFailed}");
        }

        [MenuItem("Crux/Test/Run P2A")]
        public static void RunP2A()
        {
            ResetLog();
            RunOne("P2A", P2A_CrewRuntimeTest.Execute);
            Debug.Log($"[RUNNER] wrote {LogPath} — P2A passed={passedCount} failed={failedCount}");
        }

        [MenuItem("Crux/Test/Run P2B")]
        public static void RunP2B()
        {
            ResetLog();
            RunOne("P2B", P2B_HullDataTest.Execute);
            Debug.Log($"[RUNNER] wrote {LogPath} — P2B passed={passedCount} failed={failedCount}");
        }

        [MenuItem("Crux/Test/Run P2C")]
        public static void RunP2C()
        {
            ResetLog();
            RunOne("P2C", P2C_InitiativeTest.Execute);
            Debug.Log($"[RUNNER] wrote {LogPath} — P2C passed={passedCount} failed={failedCount}");
        }

        [MenuItem("Crux/Test/Run P4B")]
        public static void RunP4B()
        {
            ResetLog();
            RunOne("P4B", P4B_CompatibilityTest.Execute);
            Debug.Log($"[RUNNER] wrote {LogPath} — P4B passed={passedCount} failed={failedCount}");
        }

        [MenuItem("Crux/Test/Run P4C")]
        public static void RunP4C()
        {
            ResetLog();
            RunOne("P4C", P4C_TankInstanceTest.Execute);
            Debug.Log($"[RUNNER] wrote {LogPath} — P4C passed={passedCount} failed={failedCount}");
        }

        [MenuItem("Crux/Test/Run P4D")]
        public static void RunP4D()
        {
            ResetLog();
            RunOne("P4D", P4D_ConvoyInventoryTest.Execute);
            Debug.Log($"[RUNNER] wrote {LogPath} — P4D passed={passedCount} failed={failedCount}");
        }

        [MenuItem("Crux/Test/Run P6")]
        public static void RunP6()
        {
            ResetLog();
            RunOne("P6", P6_TraitEffectsTest.Execute);
            Debug.Log($"[RUNNER] wrote {LogPath} — P6 passed={passedCount} failed={failedCount}");
        }

        [MenuItem("Crux/Test/Run P6B")]
        public static void RunP6B()
        {
            ResetLog();
            RunOne("P6B", P6B_TraitIntegrationTest.Execute);
            Debug.Log($"[RUNNER] wrote {LogPath} — P6B passed={passedCount} failed={failedCount}");
        }

        [MenuItem("Crux/Test/Run P7")]
        public static void RunP7()
        {
            ResetLog();
            RunOne("P7", P7_CrewDeploymentTest.Execute);
            Debug.Log($"[RUNNER] wrote {LogPath} — P7 passed={passedCount} failed={failedCount}");
        }
    }
}
#endif
