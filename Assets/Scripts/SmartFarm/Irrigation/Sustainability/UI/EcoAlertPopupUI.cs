using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm.Irrigation.Sustainability.UI
{
    /// <summary>
    /// Small animated popup that shows the latest eco alert (e.g. "Rainwater
    /// Optimization Enabled") on the Sustainability Monitor.
    ///
    /// Subscribes to <see cref="EcoAlertManager.OnAlertRaised"/>; slides into view
    /// for a few seconds with a colour-coded accent strip, then slides back out.
    /// One coroutine, no allocations per frame.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/Sustainability/UI/Eco Alert Popup")]
    public class EcoAlertPopupUI : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private EcoAlertManager alertManager;

        [Header("UI Refs")]
        [SerializeField] private RectTransform root;
        [SerializeField] private CanvasGroup   canvasGroup;
        [SerializeField] private Image         accent;
        [SerializeField] private Image         background;
        [SerializeField] private TMP_Text      titleText;
        [SerializeField] private TMP_Text      messageText;

        [Header("Animation")]
        [SerializeField, Range(0.5f, 6f)] private float visibleSeconds = 3.5f;
        [SerializeField, Range(0.05f, 1f)] private float slideSeconds  = 0.30f;
        [SerializeField] private Vector2 hiddenOffset = new Vector2(0f, -30f);

        private Coroutine _routine;
        private Vector2   _baseAnchored;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (root        == null) root        = transform as RectTransform;
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            if (alertManager== null) alertManager= FindFirstObjectByType<EcoAlertManager>();

            if (root != null) _baseAnchored = root.anchoredPosition;
            HideImmediate();
        }

        private void OnEnable()
        {
            if (alertManager == null) alertManager = FindFirstObjectByType<EcoAlertManager>();
            if (alertManager != null) alertManager.OnAlertRaised += HandleAlert;
        }

        private void OnDisable()
        {
            if (alertManager != null) alertManager.OnAlertRaised -= HandleAlert;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Handling
        // ─────────────────────────────────────────────────────────────────────

        private void HandleAlert(EcoAlert alert)
        {
            if (titleText   != null) titleText.text   = alert.title;
            if (messageText != null) messageText.text = alert.message;
            Color c = alert.GetColor();
            if (accent     != null) accent.color = c;
            if (background != null) background.color = new Color(c.r * 0.18f, c.g * 0.18f, c.b * 0.18f, 0.96f);

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(PlayPopup());
        }

        private IEnumerator PlayPopup()
        {
            if (root == null || canvasGroup == null) yield break;

            // ─── slide in + fade ───
            float t = 0f;
            Vector2 start = _baseAnchored + hiddenOffset;
            Vector2 end   = _baseAnchored;
            canvasGroup.alpha = 0f;
            root.anchoredPosition = start;

            while (t < slideSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / slideSeconds);
                canvasGroup.alpha = k;
                root.anchoredPosition = Vector2.Lerp(start, end, k);
                yield return null;
            }
            canvasGroup.alpha = 1f;
            root.anchoredPosition = end;

            // ─── hold ───
            yield return new WaitForSeconds(visibleSeconds);

            // ─── slide out ───
            t = 0f;
            while (t < slideSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / slideSeconds);
                canvasGroup.alpha = 1f - k;
                root.anchoredPosition = Vector2.Lerp(end, start, k);
                yield return null;
            }
            HideImmediate();
            _routine = null;
        }

        private void HideImmediate()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (root != null)        root.anchoredPosition = _baseAnchored + hiddenOffset;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public wiring
        // ─────────────────────────────────────────────────────────────────────

        public void SetReferences(EcoAlertManager mgr, RectTransform rootRT, CanvasGroup cg,
            Image accentImg, Image bgImg, TMP_Text title, TMP_Text msg)
        {
            alertManager = mgr;
            root         = rootRT;
            canvasGroup  = cg;
            accent       = accentImg;
            background   = bgImg;
            titleText    = title;
            messageText  = msg;
        }
    }
}
