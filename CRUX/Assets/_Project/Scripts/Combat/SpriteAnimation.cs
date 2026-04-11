using System.Collections;
using UnityEngine;

namespace Crux.Combat
{
    /// <summary>스프라이트 프레임 애니메이션 — 1회 재생 후 자동 파괴</summary>
    public class SpriteAnimation : MonoBehaviour
    {
        private Sprite[] frames;
        private float frameDuration;
        private SpriteRenderer sr;

        /// <summary>스프라이트 시퀀스를 1회 재생하고 파괴</summary>
        public static GameObject Play(Vector3 position, Sprite[] frames, float totalDuration,
            float scale = 1f, int sortingOrder = 65, float rotation = 0f)
        {
            var obj = new GameObject("SpriteAnim");
            obj.transform.position = position;
            obj.transform.localScale = Vector3.one * scale;
            obj.transform.rotation = Quaternion.Euler(0, 0, rotation);

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            sr.sortingOrder = sortingOrder;

            var anim = obj.AddComponent<SpriteAnimation>();
            anim.frames = frames;
            anim.frameDuration = totalDuration / frames.Length;
            anim.sr = sr;
            anim.StartCoroutine(anim.PlaySequence());

            return obj;
        }

        private IEnumerator PlaySequence()
        {
            for (int i = 0; i < frames.Length; i++)
            {
                sr.sprite = frames[i];
                yield return new WaitForSeconds(frameDuration);
            }
            Destroy(gameObject);
        }
    }
}
