#if UNITY_EDITOR
// ============================================================
// CruxStaticLint.cs  — 정적 코드 순서 검증 lint
//
// 목적: 반격 콜백 등록 순서 회귀(BattleController) 및
//       FireExecutor.Clear() 부수효과 명문화 여부를 텍스트
//       파싱으로 검증한다.
//
// 갱신 트리거 (패턴 변경 필요 조건):
//   - `bool playerCanCounter` 변수명 변경 시
//   - `fireExecutor.Execute(` 호출 시그니처 변경 시
//   - `FireActionContext.OnCounterWeaponSelected` 필드명 변경 시
//   - `public void Execute(` FireExecutor 메서드 시그니처 변경 시
//   - `FireActionContext.Clear()` 호출 위치 구조 변경 시
// ============================================================

using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Crux.EditorTools.Tests
{
    /// <summary>
    /// 정적 코드 패턴 lint — 반격 콜백 등록 순서 + FireExecutor 부수효과 명문화.
    /// CruxTestRunner.RunAllStatic() 에서도 호출된다.
    /// </summary>
    public static class CruxStaticLint
    {
        // BattleController 경로 (Assets-relative → 프로젝트 루트 기준)
        const string BattleControllerPath =
            "Assets/_Project/Scripts/Core/BattleController.cs";

        // FireExecutor 경로
        const string FireExecutorPath =
            "Assets/_Project/Scripts/Combat/FireExecutor.cs";

        // ===== 공개 lint 메서드 =====

        /// <summary>
        /// BattleController 내 반격 콜백 등록(OnCounterWeaponSelected)이
        /// fireExecutor.Execute() 호출보다 뒤에 있는지 검증.
        /// </summary>
        public static void LintCounterFireOrder()
        {
            string[] lines = ReadFile(BattleControllerPath);
            if (lines == null) return;

            // anchor: bool playerCanCounter = false;
            var reAnchor  = new Regex(@"bool\s+playerCanCounter\s*=\s*false\s*;");
            // Execute 호출
            var reExecute = new Regex(@"fireExecutor\.Execute\s*\(");
            // 콜백 setter
            var reSetter  = new Regex(@"FireActionContext\.OnCounterWeaponSelected\s*=");

            int anchorLine  = -1;
            int executeLine = -1;
            int setterLine  = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                int lineNo = i + 1; // 1-based

                if (anchorLine < 0 && reAnchor.IsMatch(lines[i]))
                {
                    anchorLine = lineNo;
                    continue;
                }

                // anchor 이후만 탐색
                if (anchorLine < 0) continue;

                if (executeLine < 0 && reExecute.IsMatch(lines[i]))
                    executeLine = lineNo;

                if (setterLine < 0 && reSetter.IsMatch(lines[i]))
                    setterLine = lineNo;

                if (executeLine > 0 && setterLine > 0) break;
            }

            if (anchorLine < 0)
            {
                Debug.LogError(
                    "[LINT] FAIL BattleController.CounterFireOrder — " +
                    "anchor 'bool playerCanCounter = false;' not found");
                return;
            }
            if (executeLine < 0)
            {
                Debug.LogError(
                    "[LINT] FAIL BattleController.CounterFireOrder — " +
                    "fireExecutor.Execute( not found after anchor");
                return;
            }
            if (setterLine < 0)
            {
                Debug.LogError(
                    "[LINT] FAIL BattleController.CounterFireOrder — " +
                    "FireActionContext.OnCounterWeaponSelected= not found after anchor");
                return;
            }

            if (setterLine > executeLine)
            {
                Debug.Log(
                    $"[LINT] PASS BattleController.CounterFireOrder — " +
                    $"setter@L{setterLine} > Execute@L{executeLine}");
            }
            else
            {
                Debug.LogError(
                    $"[LINT] FAIL BattleController.CounterFireOrder — " +
                    $"setter@L{setterLine} <= Execute@L{executeLine} " +
                    $"(콜백이 Execute 앞에 있음 — Clear()에 의해 wiped 위험)");
            }
        }

        /// <summary>
        /// FireExecutor.Execute() 진입 직후(5라인 이내) FireActionContext.Clear()를
        /// 호출하는지 검증 — 부수효과가 명문화·유지되고 있음을 보장.
        /// </summary>
        public static void LintFireExecutorClearSideEffect()
        {
            string[] lines = ReadFile(FireExecutorPath);
            if (lines == null) return;

            var reMethod = new Regex(@"public\s+void\s+Execute\s*\(");
            var reClear  = new Regex(@"FireActionContext\.Clear\s*\(\s*\)");

            int methodStartLine = -1;
            int clearLine       = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                int lineNo = i + 1;

                if (methodStartLine < 0 && reMethod.IsMatch(lines[i]))
                {
                    methodStartLine = lineNo;
                    continue;
                }

                if (methodStartLine < 0) continue;

                if (reClear.IsMatch(lines[i]))
                {
                    clearLine = lineNo;
                    break;
                }
            }

            if (methodStartLine < 0)
            {
                Debug.LogError(
                    "[LINT] FAIL FireExecutor.ClearSideEffect — " +
                    "public void Execute( not found");
                return;
            }
            if (clearLine < 0)
            {
                Debug.LogError(
                    "[LINT] FAIL FireExecutor.ClearSideEffect — " +
                    "FireActionContext.Clear() not found after Execute(");
                return;
            }

            int delta = clearLine - methodStartLine;
            if (delta <= 5)
            {
                Debug.Log(
                    $"[LINT] PASS FireExecutor.ClearSideEffect — " +
                    $"Execute@L{methodStartLine}, Clear@L{clearLine} (Δ={delta})");
            }
            else
            {
                Debug.LogError(
                    $"[LINT] FAIL FireExecutor.ClearSideEffect — " +
                    $"Execute@L{methodStartLine}, Clear@L{clearLine} (Δ={delta} > 5) " +
                    $"— Clear()가 진입 직후에 위치하지 않음. 부수효과 명문화 깨짐.");
            }
        }

        // ===== 단독 메뉴 =====

        [MenuItem("Crux/Test/Static Lint")]
        public static void RunStaticLint()
        {
            LintCounterFireOrder();
            LintFireExecutorClearSideEffect();
        }

        // ===== 헬퍼 =====

        static string[] ReadFile(string assetsRelativePath)
        {
            // Application.dataPath = .../Assets (끝에 슬래시 없음)
            string full = Path.Combine(Application.dataPath, "..", assetsRelativePath);
            full = Path.GetFullPath(full);

            if (!File.Exists(full))
            {
                Debug.LogError($"[LINT] FAIL {Path.GetFileName(assetsRelativePath)} — file not found: {full}");
                return null;
            }
            return File.ReadAllLines(full);
        }
    }
}
#endif
