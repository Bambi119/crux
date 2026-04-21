#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Crux.Core;
using Crux.Unit;
using Crux.Grid;

namespace Crux.EditorTools
{
    /// <summary>
    /// UI 시나리오 캡처 하네스.
    ///
    /// 목적: PlayMode 진입 → 시나리오가 BattleController 상태를 강제 →
    ///       UI Canvas 스크린샷을 PNG로 덤프 → PlayMode 종료.
    /// 호출: 메뉴 또는 MCP `execute_script McpRunner.CaptureUI<Name>`.
    /// 결과: `CRUX/Temp/crux-uitest.log` + `CRUX/Temp/ui-captures/&lt;name&gt;.png`.
    ///
    /// 구조:
    /// - 정적 오케스트레이터(이 파일): 로그·씬 오픈·PlayMode 토글·타임아웃
    /// - 런타임 드라이버(CruxUIScenarioDriver MB): 상태 강제·프레임 대기·캡처
    /// </summary>
    [InitializeOnLoad]
    public static class CruxUICapture
    {
        public const string LogPath = "Temp/crux-uitest.log";
        public const string CaptureDir = "Temp/ui-captures";
        const float DefaultTimeoutSec = 15f;

        const string KeyActive = "CruxUICapture.active";
        const string KeyScenario = "CruxUICapture.scenario";
        const string KeyStartTime = "CruxUICapture.startTime";
        const string KeyTimeout = "CruxUICapture.timeout";

        static CruxUICapture()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnStateChanged;
        }

        static bool Active => SessionState.GetBool(KeyActive, false);

        public static void Append(string line)
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

        static void OnStateChanged(PlayModeStateChange change)
        {
            if (!Active) return;
            Append($"[UITEST] state={change} at={DateTime.Now:HH:mm:ss.fff}");

            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                AttachDriver();
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                Finish("exited");
            }
        }

        static void AttachDriver()
        {
            var scenario = SessionState.GetString(KeyScenario, "");
            if (string.IsNullOrEmpty(scenario))
            {
                Append("[UITEST] scenario empty — abort");
                EditorApplication.isPlaying = false;
                return;
            }

            var go = new GameObject("__CruxUIScenarioDriver__");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var driver = go.AddComponent<CruxUIScenarioDriver>();
            driver.Scenario = scenario;
            Append($"[UITEST] driver attached scenario={scenario}");
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

            float timeout = SessionState.GetFloat(KeyTimeout, DefaultTimeoutSec);
            float elapsed = (float)EditorApplication.timeSinceStartup - start;
            if (elapsed >= timeout)
            {
                Append($"[UITEST] timeout elapsed={elapsed:F1}s — stopping");
                EditorApplication.isPlaying = false;
            }
        }

        static void Finish(string reason)
        {
            Append($"[UITEST] finished reason={reason} at={DateTime.Now:HH:mm:ss.fff}");
            SessionState.SetBool(KeyActive, false);
            SessionState.SetFloat(KeyStartTime, 0f);
            SessionState.SetString(KeyScenario, "");
        }

        public static void Start(string scenarioName, string scenePath, float timeoutSec = DefaultTimeoutSec)
        {
            if (Active)
            {
                Debug.LogWarning("[UITEST] 이미 실행 중 — 무시");
                return;
            }
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[UITEST] 이미 PlayMode — 취소");
                return;
            }
            if (!File.Exists(scenePath))
            {
                Debug.LogError($"[UITEST] 씬 파일 없음: {scenePath}");
                return;
            }

            try { if (File.Exists(LogPath)) File.Delete(LogPath); } catch { }
            try
            {
                if (!Directory.Exists(CaptureDir)) Directory.CreateDirectory(CaptureDir);
            }
            catch { }

            Append($"[UITEST] start scenario={scenarioName} scene={scenePath} timeout={timeoutSec:F1}s at={DateTime.Now:HH:mm:ss.fff}");
            Append($"[UITEST] unity={Application.unityVersion}");

            SessionState.SetBool(KeyActive, true);
            SessionState.SetString(KeyScenario, scenarioName);
            SessionState.SetFloat(KeyStartTime, 0f);
            SessionState.SetFloat(KeyTimeout, timeoutSec);

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        public static void RequestExit()
        {
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;
        }

        // ===== 메뉴 항목 =====

        [MenuItem("Crux/Test/UICapture AP Preview")]
        public static void CaptureAPPreview()
            => Start("ap-preview", "Assets/_Project/Scenes/TerrainTestScene.unity", 20f);

        [MenuItem("Crux/Test/UICapture Idle HUD")]
        public static void CaptureIdleHUD()
            => Start("idle-hud", "Assets/_Project/Scenes/TerrainTestScene.unity", 25f);

        [MenuItem("Crux/Test/UICapture Abort")]
        public static void Abort()
        {
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;
            Finish("user-abort");
            Debug.Log("[UITEST] 수동 중단");
        }
    }

    /// <summary>
    /// PlayMode 내부에서 실행되는 시나리오 드라이버.
    /// Editor 어셈블리 소속이므로 빌드에 포함되지 않음.
    /// </summary>
    public class CruxUIScenarioDriver : MonoBehaviour
    {
        public string Scenario = "";

        void Start()
        {
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            CruxUICapture.Append($"[UITEST] driver Start scene={SceneManager.GetActiveScene().name}");

            // 1) Bootstrap + 초기화 대기 (최대 5초)
            float deadline = Time.realtimeSinceStartup + 5f;
            BattleController controller = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                controller = UnityEngine.Object.FindObjectOfType<BattleController>();
                if (controller != null) break;
                yield return null;
            }
            if (controller == null)
            {
                CruxUICapture.Append("[UITEST] ERROR BattleController not found");
                CruxUICapture.RequestExit();
                yield break;
            }
            CruxUICapture.Append("[UITEST] BattleController found");

            // 2) 시나리오 디스패치
            switch (Scenario)
            {
                case "ap-preview":
                    yield return RunAPPreview(controller);
                    break;
                case "idle-hud":
                    yield return RunIdleHUD(controller);
                    break;
                default:
                    CruxUICapture.Append($"[UITEST] ERROR unknown scenario={Scenario}");
                    break;
            }

            // 3) 1프레임 여유 후 캡처·종료
            yield return new WaitForEndOfFrame();
            CaptureAndSave(Scenario);

            yield return null;
            CruxUICapture.RequestExit();
        }

        IEnumerator RunIdleHUD(BattleController controller)
        {
            // 플레이어 턴 + SelectedUnit 세팅까지 대기 (최대 15초 — 적 선공 시 턴 소모 필요)
            float deadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (controller.CurrentPhase == TurnPhase.PlayerTurn &&
                    controller.SelectedUnit != null)
                    break;
                yield return null;
            }

            if (controller.SelectedUnit == null)
            {
                CruxUICapture.Append("[UITEST] idle-hud WARNING no SelectedUnit after 15s — 캡처는 진행");
            }
            else
            {
                CruxUICapture.Append($"[UITEST] idle-hud ready SelectedUnit={controller.SelectedUnit.name} AP={controller.SelectedUnit.CurrentAP}");
            }

            // 안정화 — HUD 바인더가 1프레임 지연 반영
            yield return new WaitForSeconds(0.3f);
        }

        IEnumerator RunAPPreview(BattleController controller)
        {
            // 플레이어 턴 시작 + SelectedUnit 세팅 대기 (최대 10초)
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (controller.CurrentPhase == TurnPhase.PlayerTurn &&
                    controller.SelectedUnit != null)
                    break;
                yield return null;
            }

            var unit = controller.SelectedUnit;
            if (unit == null)
            {
                CruxUICapture.Append("[UITEST] ERROR no SelectedUnit after 10s");
                yield break;
            }
            CruxUICapture.Append($"[UITEST] SelectedUnit={unit.name} pos={unit.GridPosition} AP={unit.CurrentAP}");

            // Move 모드 진입
            controller.TryEnterMoveMode();
            yield return null;

            // 도달 가능한 셀 찾기 — 현재 위치 인접 6방 중 하나
            var grid = controller.Grid;
            Vector2Int target = default;
            bool found = false;
            Vector2Int[] candidates = {
                new Vector2Int(unit.GridPosition.x + 1, unit.GridPosition.y),
                new Vector2Int(unit.GridPosition.x - 1, unit.GridPosition.y),
                new Vector2Int(unit.GridPosition.x,     unit.GridPosition.y + 1),
                new Vector2Int(unit.GridPosition.x,     unit.GridPosition.y - 1),
                new Vector2Int(unit.GridPosition.x + 1, unit.GridPosition.y + 1),
                new Vector2Int(unit.GridPosition.x - 1, unit.GridPosition.y + 1),
            };
            foreach (var c in candidates)
            {
                if (!grid.IsInBounds(c)) continue;
                var cell = grid.GetCell(c);
                if (cell == null || cell.Occupant != null) continue;
                var path = grid.FindPath(unit.GridPosition, c);
                if (path == null || path.Count <= 1) continue;
                target = c;
                found = true;
                break;
            }

            if (!found)
            {
                CruxUICapture.Append("[UITEST] ERROR no reachable adjacent cell");
                yield break;
            }
            CruxUICapture.Append($"[UITEST] move target={target}");

            // 클릭 시뮬레이션 — MoveDirectionSelect로 전환됨
            controller.HandleClickAt(target);
            yield return null; // Binder.Update 반영
            yield return null;

            CruxUICapture.Append($"[UITEST] mode={controller.CurrentInputMode} pendingCost={controller.PendingMoveCost}");
        }

        void CaptureAndSave(string scenario)
        {
            try
            {
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                byte[] png = tex.EncodeToPNG();
                UnityEngine.Object.Destroy(tex);

                string path = Path.Combine(CruxUICapture.CaptureDir, $"{scenario}.png");
                File.WriteAllBytes(path, png);
                CruxUICapture.Append($"[UITEST] captured path={path} bytes={png.Length}");
            }
            catch (Exception e)
            {
                CruxUICapture.Append($"[UITEST] ERROR capture: {e.Message}");
            }
        }
    }
}
#endif
