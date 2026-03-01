using System.Collections;
using UnityEngine;

namespace SmartFarm
{
    /// <summary>
    /// Lightweight animation helper for page switching and modal pop.
    /// </summary>
    public class SimpleUIAnimationHelper : MonoBehaviour
    {
        [SerializeField] private float pageFadeDuration = 0.2f;
        [SerializeField] private float modalScaleDuration = 0.16f;

        public void SwitchPage(GameObject fromPage, GameObject toPage)
        {
            if (toPage == null) return;
            if (fromPage != null) fromPage.SetActive(false);
            toPage.SetActive(true);
            var cg = EnsureCanvasGroup(toPage);
            StartCoroutine(FadeCanvasGroup(cg, 0f, 1f, pageFadeDuration));
        }

        public void SetModalVisible(GameObject modal, bool visible)
        {
            if (modal == null) return;
            if (_modalRoutine != null) StopCoroutine(_modalRoutine);
            _modalRoutine = StartCoroutine(AnimateModal(modal, visible));
        }

        private Coroutine _modalRoutine;

        private IEnumerator AnimateModal(GameObject modal, bool visible)
        {
            var cg = EnsureCanvasGroup(modal);
            var rt = modal.GetComponent<RectTransform>();
            if (visible) modal.SetActive(true);

            float t = 0f;
            float startAlpha = cg.alpha;
            float targetAlpha = visible ? 1f : 0f;
            Vector3 startScale = rt != null ? rt.localScale : Vector3.one;
            Vector3 targetScale = visible ? Vector3.one : Vector3.one * 0.9f;

            while (t < modalScaleDuration)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(t / modalScaleDuration);
                cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, a);
                if (rt != null) rt.localScale = Vector3.Lerp(startScale, targetScale, a);
                yield return null;
            }

            cg.alpha = targetAlpha;
            if (rt != null) rt.localScale = targetScale;
            if (!visible) modal.SetActive(false);
            _modalRoutine = null;
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            return cg;
        }

        private static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
        {
            if (group == null) yield break;
            float t = 0f;
            group.alpha = from;
            while (t < duration)
            {
                t += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            group.alpha = to;
        }
    }
}
