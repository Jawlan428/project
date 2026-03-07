using System.Collections;
using TMPro;
using UnityEngine;

namespace Translation
{
    /// <summary>
    /// World-space subtitle panel that floats above a VR participant's head.
    /// Automatically faces the main camera (billboard effect).
    /// Auto-hides after a configurable duration.
    ///
    /// Created and managed by SubtitleUIController — do not add manually.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class SubtitleDisplay : MonoBehaviour
    {
        [Tooltip("How long the subtitle stays visible after the last update")]
        [SerializeField] private float displayDuration = 7f;

        private TMP_Text   _subtitleText;
        private TMP_Text   _speakerLabel;
        private Coroutine  _hideCoroutine;
        private Canvas     _canvas;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
        }

        // ── Internal wiring called by SubtitleUIController ────────────────────

        internal void Initialise(TMP_Text subtitleText, TMP_Text speakerLabel, float duration)
        {
            _subtitleText  = subtitleText;
            _speakerLabel  = speakerLabel;
            displayDuration = duration;
            gameObject.SetActive(false);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Show a translated subtitle for this participant.</summary>
        public void ShowSubtitle(TranslationEntry entry)
        {
            if (_subtitleText == null) return;

            _subtitleText.text = entry.translatedText;

            if (_speakerLabel != null)
                _speakerLabel.text = $"{entry.speakerName}  [{entry.sourceLanguage.ToShortLabel()}→{entry.targetLanguage.ToShortLabel()}]";

            gameObject.SetActive(true);

            if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
            _hideCoroutine = StartCoroutine(HideAfterDelay(displayDuration));
        }

        public void HideImmediate()
        {
            if (_hideCoroutine != null) { StopCoroutine(_hideCoroutine); _hideCoroutine = null; }
            gameObject.SetActive(false);
        }

        // ── Billboard ─────────────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (Camera.main == null) return;
            // Always face the camera — works correctly in VR with any head orientation
            transform.rotation = Quaternion.LookRotation(
                transform.position - Camera.main.transform.position,
                Vector3.up);
        }

        // ── Private ───────────────────────────────────────────────────────────

        private IEnumerator HideAfterDelay(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            gameObject.SetActive(false);
            _hideCoroutine = null;
        }
    }
}
