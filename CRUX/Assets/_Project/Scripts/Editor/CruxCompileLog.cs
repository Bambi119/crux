#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Crux.EditorTools
{
    /// <summary>
    /// 자율 컴파일 피드백 훅.
    ///
    /// 목적: Unity 자동 재컴파일 결과를 파일로 덤프. Claude/에이전트가 MCP 없이
    /// `CRUX/Temp/crux-compile-status.txt` 를 Read해 에러 유무·상세 확인 가능.
    ///
    /// 트리거: 모든 assembly 컴파일 완료 시 자동 (CompilationPipeline 이벤트).
    ///
    /// 파일 포맷:
    ///   [STATUS] OK (0 errors, N warnings) at HH:mm:ss — F.Fs
    ///   또는
    ///   [STATUS] FAIL N errors M warnings at HH:mm:ss — F.Fs
    ///   [UNITY] {unityVersion}
    ///   [ERROR] {file}:{line} {message}
    ///   [WARNING] {file}:{line} {message}
    ///   ...
    ///
    /// 파일은 매 컴파일마다 덮어쓰기. 이전 결과는 보존하지 않음.
    ///
    /// 메뉴:
    /// - Crux/Test/Show Compile Status — 파일 내용을 Unity Console 에 출력.
    /// - Crux/Test/Force Recompile — CompilationPipeline.RequestScriptCompilation() 호출.
    /// </summary>
    [InitializeOnLoad]
    public static class CruxCompileLog
    {
        const string StatusPath = "Temp/crux-compile-status.txt";

        static int totalErrors;
        static int totalWarnings;
        static readonly StringBuilder detailBuf = new StringBuilder();
        static DateTime startTime;

        static CruxCompileLog()
        {
            // 중복 구독 방지 — 도메인 리로드 시 InitializeOnLoad 재실행됨
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyFinished;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyFinished;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
        }

        static void OnCompilationStarted(object ctx)
        {
            totalErrors = 0;
            totalWarnings = 0;
            detailBuf.Clear();
            startTime = DateTime.Now;
        }

        static void OnAssemblyFinished(string assemblyPath, CompilerMessage[] messages)
        {
            if (messages == null) return;
            foreach (var m in messages)
            {
                bool isErr = m.type == CompilerMessageType.Error;
                bool isWarn = m.type == CompilerMessageType.Warning;
                if (!isErr && !isWarn) continue;

                if (isErr) totalErrors++;
                else totalWarnings++;

                string tag = isErr ? "[ERROR]" : "[WARNING]";
                string file = m.file ?? "?";
                int line = m.line;
                string msg = m.message != null ? m.message.Replace("\n", " ").Replace("\r", "") : "";
                detailBuf.AppendLine($"{tag} {file}:{line} {msg}");
            }
        }

        static void OnCompilationFinished(object ctx)
        {
            try
            {
                var dir = Path.GetDirectoryName(StatusPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var sb = new StringBuilder();
                string timeStr = DateTime.Now.ToString("HH:mm:ss");
                float elapsed = (float)(DateTime.Now - startTime).TotalSeconds;
                bool ok = totalErrors == 0;

                if (ok)
                    sb.AppendLine($"[STATUS] OK (0 errors, {totalWarnings} warnings) at {timeStr} — {elapsed:F1}s");
                else
                    sb.AppendLine($"[STATUS] FAIL {totalErrors} errors {totalWarnings} warnings at {timeStr} — {elapsed:F1}s");

                sb.AppendLine($"[UNITY] {Application.unityVersion}");
                sb.Append(detailBuf);

                File.WriteAllText(StatusPath, sb.ToString());
            }
            catch { /* 파일 잠금·권한 예외 무시 */ }
        }

        [MenuItem("Crux/Test/Show Compile Status")]
        public static void ShowStatus()
        {
            if (!File.Exists(StatusPath))
            {
                Debug.Log($"[COMPILE] no status file at {StatusPath} — 아직 컴파일된 적 없음");
                return;
            }
            Debug.Log(File.ReadAllText(StatusPath));
        }

        [MenuItem("Crux/Test/Force Recompile")]
        public static void ForceRecompile()
        {
            Debug.Log("[COMPILE] Force recompile requested");
            CompilationPipeline.RequestScriptCompilation();
        }
    }
}
#endif
