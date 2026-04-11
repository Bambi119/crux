#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace Crux.EditorTools
{
    /// <summary>체크무늬/어두운 배경을 투명으로 변환하는 에디터 도구</summary>
    public class SpriteAlphaRemover : EditorWindow
    {
        private float threshold = 0.15f;
        private bool removeCheckerboard = true;

        [MenuItem("CRUX/스프라이트 배경 제거")]
        public static void ShowWindow()
        {
            GetWindow<SpriteAlphaRemover>("배경 제거");
        }

        private void OnGUI()
        {
            GUILayout.Label("선택한 텍스처의 배경을 투명으로 변환", EditorStyles.boldLabel);
            GUILayout.Space(10);

            threshold = EditorGUILayout.Slider("어두운 픽셀 임계값", threshold, 0.05f, 0.5f);
            removeCheckerboard = EditorGUILayout.Toggle("체크무늬 패턴 제거", removeCheckerboard);

            GUILayout.Space(10);

            if (GUILayout.Button("선택한 텍스처에 적용 (Sprites 폴더 전체)"))
            {
                ProcessAllSprites();
            }
        }

        private void ProcessAllSprites()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project/Sprites" });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ProcessTexture(path);
            }

            AssetDatabase.Refresh();
            Debug.Log("[CRUX] 모든 스프라이트 배경 제거 완료!");
        }

        private void ProcessTexture(string assetPath)
        {
            // Read/Write 활성화
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            AssetDatabase.ImportAsset(assetPath);

            // 텍스처 로드
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null) return;

            // 복사본 생성 (원본은 읽기 전용일 수 있음)
            Texture2D writableTex = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            writableTex.SetPixels(tex.GetPixels());

            Color[] pixels = writableTex.GetPixels();
            int changed = 0;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];

                // 매우 어두운 픽셀 → 투명 (검은 배경)
                if (c.r < threshold && c.g < threshold && c.b < threshold)
                {
                    pixels[i] = Color.clear;
                    changed++;
                    continue;
                }

                // 체크무늬 패턴 감지 (회색 교차)
                if (removeCheckerboard)
                {
                    int x = i % tex.width;
                    int y = i / tex.width;

                    // 체크무늬: (x/8 + y/8) % 2 패턴의 회색
                    bool isCheckerDark = ((x / 8 + y / 8) % 2 == 0);
                    float gray = (c.r + c.g + c.b) / 3f;

                    // 체크무늬 색상 범위 (어두운 회색 0.2~0.3, 밝은 회색 0.35~0.45)
                    if ((gray > 0.18f && gray < 0.32f) || (gray > 0.33f && gray < 0.48f))
                    {
                        // 주변 픽셀도 비슷한 회색인지 확인 (실제 오브젝트가 아닌 배경)
                        float variation = Mathf.Abs(c.r - c.g) + Mathf.Abs(c.g - c.b);
                        if (variation < 0.05f) // 거의 무채색 = 체크무늬
                        {
                            pixels[i] = Color.clear;
                            changed++;
                        }
                    }
                }
            }

            writableTex.SetPixels(pixels);
            writableTex.Apply();

            // PNG로 저장
            byte[] pngData = writableTex.EncodeToPNG();
            string fullPath = Path.Combine(Application.dataPath, "..",  assetPath);
            File.WriteAllBytes(fullPath, pngData);

            Debug.Log($"[CRUX] {assetPath}: {changed}개 픽셀 투명 처리");
        }
    }
}
#endif
