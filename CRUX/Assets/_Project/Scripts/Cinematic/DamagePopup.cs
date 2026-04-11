using UnityEngine;

namespace Crux.Cinematic
{
    /// <summary>데미지 숫자 팝업 — 위로 떠오르며 페이드아웃</summary>
    public class DamagePopup : MonoBehaviour
    {
        private string text;
        private Color color;
        private float lifetime;
        private float elapsed;
        private Vector3 worldPos;
        private UnityEngine.Camera cam;

        private static GUIStyle _style;

        public static void Spawn(Vector3 worldPosition, float damage, Crux.Core.ShotOutcome outcome)
        {
            string text;
            Color color;

            switch (outcome)
            {
                case Core.ShotOutcome.Miss:
                    text = "MISS";
                    color = new Color(0.5f, 0.5f, 0.5f);
                    break;
                case Core.ShotOutcome.Ricochet:
                    text = $"{damage:F0}";
                    color = new Color(1f, 0.8f, 0.3f); // 노란색
                    break;
                case Core.ShotOutcome.Hit:
                    text = $"-{damage:F0}";
                    color = new Color(1f, 0.5f, 0.2f); // 주황색
                    break;
                case Core.ShotOutcome.Penetration:
                    text = $"-{damage:F0}!";
                    color = new Color(1f, 0.15f, 0.1f); // 빨간색
                    break;
                default:
                    text = $"{damage:F0}";
                    color = Color.white;
                    break;
            }

            var obj = new GameObject("DmgPopup");
            var popup = obj.AddComponent<DamagePopup>();
            popup.Initialize(worldPosition, text, color);
        }

        /// <summary>엄폐물 피격 — 시안 계열</summary>
        public static void SpawnCoverHit(Vector3 worldPosition, float damage, string coverName)
        {
            string text = $"{coverName} -{damage:F0}";
            Color color = new Color(0.3f, 0.85f, 0.9f); // 시안

            var obj = new GameObject("DmgPopupCover");
            var popup = obj.AddComponent<DamagePopup>();
            popup.Initialize(worldPosition, text, color);
        }

        /// <summary>기관총 연속 데미지용 — 작은 폰트</summary>
        public static void SpawnSmall(Vector3 worldPosition, float damage, bool hit)
        {
            string text = hit ? $"-{damage:F0}" : "miss";
            Color color = hit ? new Color(1f, 0.7f, 0.3f, 0.9f) : new Color(0.5f, 0.5f, 0.5f, 0.6f);

            // 약간 랜덤 오프셋
            worldPosition += (Vector3)(Random.insideUnitCircle * 0.3f);

            var obj = new GameObject("DmgPopupSm");
            var popup = obj.AddComponent<DamagePopup>();
            popup.Initialize(worldPosition, text, color, 0.8f, true);
        }

        public void Initialize(Vector3 pos, string txt, Color col, float life = 1.2f, bool small = false)
        {
            worldPos = pos;
            text = txt;
            color = col;
            lifetime = life;
            cam = UnityEngine.Camera.main;

            if (!small)
            {
                // 큰 데미지는 약간 위로 오프셋
                worldPos += Vector3.up * 0.3f;
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            worldPos += Vector3.up * Time.deltaTime * 0.8f; // 위로 떠오름

            if (elapsed >= lifetime)
                Destroy(gameObject);
        }

        private void OnGUI()
        {
            if (cam == null) return;

            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
            if (screenPos.z < 0) return;
            screenPos.y = Screen.height - screenPos.y;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label);
                _style.fontStyle = FontStyle.Bold;
                _style.alignment = TextAnchor.MiddleCenter;
            }

            // 크기와 투명도 애니메이션
            float t = elapsed / lifetime;
            float alpha = Mathf.Lerp(1f, 0f, t);
            float scale = Mathf.Lerp(1f, 1.5f, t * 0.5f);

            _style.fontSize = Mathf.RoundToInt(18 * scale);
            _style.normal.textColor = new Color(color.r, color.g, color.b, alpha);

            // 외곽선 효과 (검은색 뒤에)
            Color shadowColor = new Color(0, 0, 0, alpha * 0.8f);
            _style.normal.textColor = shadowColor;
            GUI.Label(new Rect(screenPos.x - 49, screenPos.y - 14, 100, 30), text, _style);
            GUI.Label(new Rect(screenPos.x - 51, screenPos.y - 16, 100, 30), text, _style);

            // 본 텍스트
            _style.normal.textColor = new Color(color.r, color.g, color.b, alpha);
            GUI.Label(new Rect(screenPos.x - 50, screenPos.y - 15, 100, 30), text, _style);
        }
    }
}
