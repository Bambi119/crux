#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Crux.EditorTools
{
    /// <summary>
    /// 자율 플레이 스모크 테스트 하네스.
    ///
    /// 목적: Unity MCP `execute_menu_item` 으로 호출해서 PlayMode 로그를 파일로 덤프.
    /// Claude/에이전트가 결과 파일을 Read로 읽어 검증 가능.
    ///
    /// 호출: 메뉴 `Crux/Test/PlaySmoke *` 또는 MCP `execute_menu_item(menuPath="Crux/Test/PlaySmoke TerrainTest")`.
    /// 결과: `CRUX/Temp/crux-playsmoke.log` — 시작·종료 마커 + 모든 PlayMode Debug.Log.
    ///
    /// 도메인 리로드 안전: SessionState로 active/startTime 유지, 로그는 즉시 append.
    /// </summary>
    [InitializeOnLoad]
    public static class CruxPlaySmoke
    {
        const string LogPath = "Temp/crux-playsmoke.log";
        const float DefaultDurationSec = 3f;

        const string KeyActive = "CruxPlaySmoke.active";
        const string KeyStartTime = "CruxPlaySmoke.startTime";
        const string KeyDuration = "CruxPlaySmoke.duration";

        static CruxPlaySmoke()
        {
            Application.logMessageReceivedThreaded += OnLog;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnStateChanged;
        }

        static bool Active => SessionState.GetBool(KeyActive, false);

        static void Append(string line)
        {
            try
            {
                var dir = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.AppendAllText(LogPath, line + "\n");
            }
            catch { /* 파일 잠금·권한 예외 무시 */ }
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            if (!Active) return;
            Append($"[{type}] {condition}");
        }

        static void OnStateChanged(PlayModeStateChange change)
        {
            if (!Active) return;
            Append($"[SMOKE] state={change} at={DateTime.Now:HH:mm:ss.fff}");
            if (change == PlayModeStateChange.EnteredEditMode)
                Finish("exited");
        }

        static void Tick()
        {
            if (!Active) return;
            if (!EditorApplication.isPlaying) return;

            float start = SessionState.GetFloat(KeyStartTime, 0f);
            if (start <= 0f)
            {
                SessionState.SetFloat(KeyStartTime, (float)EditorApplication.timeSinceStartup);
                return;
            }

            float duration = SessionState.GetFloat(KeyDuration, DefaultDurationSec);
            float elapsed = (float)EditorApplication.timeSinceStartup - start;
            if (elapsed < duration) return;

            Append($"[SMOKE] timeout elapsed={elapsed:F1}s duration={duration:F1}s — stopping");
            EditorApplication.isPlaying = false;
        }

        static void Finish(string reason)
        {
            Append($"[SMOKE] finished reason={reason} at={DateTime.Now:HH:mm:ss.fff}");
            SessionState.SetBool(KeyActive, false);
            SessionState.SetFloat(KeyStartTime, 0f);
        }

        static void Start(string scenePath, float duration)
        {
            if (Active)
            {
                Debug.LogWarning("[SMOKE] 이미 실행 중 — 무시");
                return;
            }
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[SMOKE] 이미 PlayMode — 취소");
                return;
            }
            if (!File.Exists(scenePath))
            {
                Debug.LogError($"[SMOKE] 씬 파일 없음: {scenePath}");
                return;
            }

            try { if (File.Exists(LogPath)) File.Delete(LogPath); } catch { }

            Append($"[SMOKE] start scene={scenePath} duration={duration:F1}s at={DateTime.Now:HH:mm:ss.fff}");
            Append($"[SMOKE] unity={Application.unityVersion}");

            SessionState.SetBool(KeyActive, true);
            SessionState.SetFloat(KeyStartTime, 0f);
            SessionState.SetFloat(KeyDuration, duration);

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        // ===== 메뉴 항목 =====

        [MenuItem("Crux/Test/PlaySmoke TerrainTest (3s)")]
        public static void SmokeTerrainTest3s()
            => Start("Assets/_Project/Scenes/TerrainTestScene.unity", 3f);

        [MenuItem("Crux/Test/PlaySmoke TerrainTest (8s)")]
        public static void SmokeTerrainTest8s()
            => Start("Assets/_Project/Scenes/TerrainTestScene.unity", 8f);

        [MenuItem("Crux/Test/PlaySmoke Strategy (3s)")]
        public static void SmokeStrategy()
            => Start("Assets/_Project/Scenes/StrategyScene.unity", 3f);

        [MenuItem("Crux/Test/PlaySmoke Abort")]
        public static void Abort()
        {
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;
            Finish("user-abort");
            Debug.Log("[SMOKE] 수동 중단");
        }
    }
}
#endif
