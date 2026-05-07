using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm
{
    /// <summary>
    /// Top-right animated popup that surfaces alerts raised by
    /// <see cref="CropMonitorAlertSystem"/>. Each alert slides in, holds for a
    /// configurable duration, then slides out. Multiple alerts queue up so the
    /// player never misses a critical event.
    ///
    /// The popup uses a single rect (configured by the editor setup) and animates
    /// position+alpha with a coroutine. Quest-friendly — no per-frame allocations.
    /// </summary>
    [AddComponentMenu("SmartFarm/Crops/Crop Monitor Alert Popup")]
    public class CropMonitorAlertPopupUI : MonoBehaviour
    {
        [Header("References (auto-found if empty)")]
        [SerializeField] private CropMonitorAlertSystem alertSystem;

        [Header("Popup")]
        [SerializeField] private RectTransform popupRoot;
        [SerializeField] private CanvasGroup   canvasGroup;
        [SerializeField] private Image         backgroundImage;
        [SerializeField] private Image         leftAccentImage;
        [SerializeField] private Image         iconImage;
        [SerializeField] private TMP_Text      titleText;
        [SerializeField] private TMP_Text      messageText;

        [Header("Animation")]
        [SerializeField, Range(0.05f, 1f)] private float slideDuration = 0.28f;
        [SerializeField, Range(0.5f, 12f)] private float holdDuration  = 4.5f;
        [SerializeField, Tooltip("Where the popup hides off-screen relative to its rest position. " +
                                 "Default = (0,80) → popup slides down from above the header.")]
        private Vector2 hideOffset = new Vector2(0f, 80f);

        [Header("Sound (optional)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip   popInClip;

        // ── State ─────────────────────────────────────────────────────────────

        private readonly Queue<CropMonitorAlert> _queue = new();
        private Coroutine _running;
        private Vector2   _restPosition;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (popupRoot   == null) popupRoot   = transform as RectTransform;
            if (canvasGroup == null && popupRoot != null)
            {
                canvasGroup = popupRoot.GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = popupRoot.gameObject.AddComponent<CanvasGroup>();
            }

            if (popupRoot != null)
            {
                _restPosition = popupRoot.anchoredPosition;
                popupRoot.anchoredPosition = _restPosition + hideOffset;
            }
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable   = false;
            }
        }

        private void OnEnable()
        {
            if (alertSystem == null)
                alertSystem = FindFirstObjectByType<CropMonitorAlertSystem>();
            if (alertSystem != null)
                alertSystem.OnAlertRaised += HandleAlertRaised;
        }

        private void OnDisable()
        {
            if (alertSystem != null)
                alertSystem.OnAlertRaised -= HandleAlertRaised;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Queue
        // ─────────────────────────────────────────────────────────────────────

        private void HandleAlertRaised(CropMonitorAlert alert)
        {
            _queue.Enqueue(alert);
            if (_running == null) _running = StartCoroutine(ConsumeQueue());
        }

        public void ShowAlert(CropMonitorAlert alert)
        {
            _queue.Enqueue(alert);
            if (_running == null) _running = StartCoroutine(ConsumeQueue());
        }

        private IEnumerator ConsumeQueue()
        {
            while (_queue.Count > 0)
            {
                var next = _queue.Dequeue();
                yield return PlayAlert(next);
            }
            _running = null;
        }

        private IEnumerator PlayAlert(CropMonitorAlert alert)
        {
            ApplyVisuals(alert);

            if (audioSource != null && popInClip != null)
                audioSource.PlayOneShot(popInClip);

            // Slide-in
            yield return Slide(_restPosition + hideOffset, _restPosition, 0f, 1f, slideDuration);

            // Hold (extend hold time on critical alerts)
            float wait = alert.level == CropAlertLevel.Critical ? holdDuration * 1.4f : holdDuration;
            yield return new WaitForSecondsRealtime(wait);

            // Slide-out
            yield return Slide(_restPosition, _restPosition + hideOffset, 1f, 0f, slideDuration);
        }

        private IEnumerator Slide(Vector2 from, Vector2 to, float fromAlpha, float toAlpha, float duration)
        {
            if (popupRoot == null || canvasGroup == null) yield break;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                popupRoot.anchoredPosition = Vector2.Lerp(from, to, k);
                canvasGroup.alpha          = Mathf.Lerp(fromAlpha, toAlpha, k);
                yield return null;
            }
            popupRoot.anchoredPosition = to;
            canvasGroup.alpha          = toAlpha;
        }

        private void ApplyVisuals(CropMonitorAlert alert)
        {
            Color color = CropMonitorAlertSystem.ColorFor(alert.level);

            if (backgroundImage != null) backgroundImage.color = WithAlpha(color, 0.2f);
            if (leftAccentImage != null) leftAccentImage.color = color;
            if (iconImage       != null) iconImage.color       = color;

            if (titleText   != null)
            {
                titleText.text  = alert.title;
                titleText.color = color;
            }
            if (messageText != null) messageText.text = alert.message;
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
