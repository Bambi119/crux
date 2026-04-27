#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Crux.EditorTools.Automation
{
    /// <summary>
    /// 시나리오 스텝 스크린샷 캡처 및 골든 이미지 비교 유틸리티.
    /// static 유틸리티, 인스턴스 없음.
    /// </summary>
    public static class CruxScenarioCapture
    {
        const int TargetWidth = 1280;
        const int TargetHeight = 720;

        // ──────────────────────────────────────────────
        //  스크린샷 캡처
        // ──────────────────────────────────────────────

        /// <summary>
        /// 현재 화면을 캡처하여 PNG로 저장한다.
        /// </summary>
        /// <param name="baseDir">저장 디렉토리 (예: CRUX/Temp/crux-scenario-Foo)</param>
        /// <param name="stepIdx">스텝 인덱스 (파일명 prefix)</param>
        /// <param name="label">스텝 레이블 (파일명에 포함)</param>
        /// <returns>저장된 파일 경로, 실패 시 null</returns>
        public static string Capture(string baseDir, int stepIdx, string label)
        {
            try
            {
                EnsureDir(baseDir);

                // 화면 캡처
                var srcTex = ScreenCapture.CaptureScreenshotAsTexture();
                var resized = ResizeTo(srcTex, TargetWidth, TargetHeight);
                UnityEngine.Object.DestroyImmediate(srcTex);

                var safeLabel = SanitizeLabel(label);
                var fileName = $"{stepIdx:D3}_{safeLabel}.png";
                var outPath = Path.Combine(baseDir, fileName).Replace('\\', '/');

                File.WriteAllBytes(outPath, resized.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(resized);

                return outPath;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ScenarioCapture] Capture 실패: {ex.Message}");
                return null;
            }
        }

        // ──────────────────────────────────────────────
        //  골든 이미지 비교
        // ──────────────────────────────────────────────

        /// <summary>
        /// 캡처 이미지를 골든 디렉토리의 동명 PNG와 픽셀 비교한다.
        /// 골든 파일 없으면 ok=true(비교 skip).
        /// </summary>
        /// <param name="capturedPath">Capture()가 반환한 경로</param>
        /// <param name="goldenDir">골든 PNG 디렉토리 (없으면 비교 skip)</param>
        /// <param name="threshold">허용 최대 픽셀 채널 차이 (기본 1/255)</param>
        /// <returns>(ok: 차이 없음, diffRatio: 차이 비율 0~1)</returns>
        public static (bool ok, float diffRatio) CompareToGolden(
            string capturedPath,
            string goldenDir,
            float threshold = 1f / 255f)
        {
            if (string.IsNullOrEmpty(capturedPath) || !File.Exists(capturedPath))
                return (true, 0f);

            if (!Directory.Exists(goldenDir))
                return (true, 0f);

            var goldenPath = Path.Combine(goldenDir, Path.GetFileName(capturedPath)).Replace('\\', '/');
            if (!File.Exists(goldenPath))
                return (true, 0f);

            try
            {
                var capTex = LoadPng(capturedPath);
                var goldenTex = LoadPng(goldenPath);

                if (capTex == null || goldenTex == null)
                    return (true, 0f);

                float diffRatio = CalculateDiffRatio(capTex, goldenTex, threshold,
                    out Texture2D diffTex);

                // diff 이미지 저장
                if (diffTex != null)
                {
                    var diffPath = capturedPath.Replace(".png", ".diff.png");
                    File.WriteAllBytes(diffPath, diffTex.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(diffTex);
                }

                UnityEngine.Object.DestroyImmediate(capTex);
                UnityEngine.Object.DestroyImmediate(goldenTex);

                bool ok = diffRatio <= threshold;
                return (ok, diffRatio);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ScenarioCapture] 골든 비교 실패: {ex.Message}");
                return (true, 0f);
            }
        }

        // ──────────────────────────────────────────────
        //  내부 유틸
        // ──────────────────────────────────────────────

        static Texture2D ResizeTo(Texture2D src, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Bilinear;
            Graphics.Blit(src, rt);

            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            var dst = new Texture2D(w, h, TextureFormat.ARGB32, false);
            dst.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            dst.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return dst;
        }

        static float CalculateDiffRatio(Texture2D a, Texture2D b, float threshold,
            out Texture2D diffTex)
        {
            int w = Mathf.Min(a.width, b.width);
            int h = Mathf.Min(a.height, b.height);
            var pixA = a.GetPixels32();
            var pixB = b.GetPixels32();

            int diffCount = 0;
            var diffPixels = new Color32[w * h];

            for (int i = 0; i < pixA.Length && i < pixB.Length; i++)
            {
                float dr = Mathf.Abs(pixA[i].r - pixB[i].r) / 255f;
                float dg = Mathf.Abs(pixA[i].g - pixB[i].g) / 255f;
                float db = Mathf.Abs(pixA[i].b - pixB[i].b) / 255f;

                if (dr > threshold || dg > threshold || db > threshold)
                {
                    diffCount++;
                    byte mag = (byte)Mathf.Clamp((dr + dg + db) * 85f, 0f, 255f);
                    diffPixels[i] = new Color32(mag, 0, 0, 255);
                }
                else
                {
                    diffPixels[i] = new Color32(pixA[i].r, pixA[i].g, pixA[i].b, 128);
                }
            }

            diffTex = new Texture2D(w, h, TextureFormat.ARGB32, false);
            diffTex.SetPixels32(diffPixels);
            diffTex.Apply();

            return (float)diffCount / (w * h);
        }

        static Texture2D LoadPng(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            return tex.LoadImage(bytes) ? tex : null;
        }

        static void EnsureDir(string dir)
        {
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        static string SanitizeLabel(string label)
        {
            // 파일명에 사용할 수 없는 문자를 _ 로 치환
            foreach (char c in Path.GetInvalidFileNameChars())
                label = label.Replace(c, '_');
            return label.Length > 40 ? label.Substring(0, 40) : label;
        }
    }
}
#endif
