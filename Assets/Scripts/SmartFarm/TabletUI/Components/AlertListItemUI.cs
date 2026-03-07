using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm
{
    /// <summary>
    /// Compact alert row in the Alerts list.
    /// Layout:  [SEVERITY badge] · [HH:mm] · [message preview …]  [▶]
    ///
    /// Tapping anywhere on the row calls onTapped(item) so AlertsUI can open
    /// the full-detail overlay.
    ///
    /// Backward-compatible: the old Action{string} overload still works so any
    /// existing callers are not broken.
    /// </summary>
    public class AlertListItemUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text severityText;
        [SerializeField] private TMP_Text timestampText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button   acknowledgeButton;  // kept for Inspector compat
        [SerializeField] private Image    background;

        [SerializeField] private Color infoColor     = new Color(0.15f, 0.30f, 0.52f, 0.95f);
        [SerializeField] private Color warnColor     = new Color(0.52f, 0.35f, 0.04f, 0.95f);
        [SerializeField] private Color criticalColor = new Color(0.58f, 0.09f, 0.09f, 0.95f);

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Bind this row to an alert item.
        /// onTapped receives the full FarmAlertItem so the caller can open a detail view.
        /// </summary>
        public void Bind(FarmAlertItem item, Action<FarmAlertItem> onTapped)
        {
            if (severityText  != null) severityText.text  = item.severity.ToString().ToUpperInvariant();
            if (timestampText != null) timestampText.text = item.timestampUtc.ToLocalTime().ToString("HH:mm");

            if (messageText != null)
            {
                string msg = item.message;
                if (!string.IsNullOrEmpty(msg) && msg.Length > 40)
                    msg = msg.Substring(0, 37) + "…";
                messageText.text = msg;
            }

            if (background != null)
            {
                background.color = item.severity switch
                {
                    FarmAlertSeverity.Critical => criticalColor,
                    FarmAlertSeverity.Warning  => warnColor,
                    _                          => infoColor
                };
            }

            // Make the entire row a button (added at runtime if the template has none)
            var rowBtn = GetComponent<Button>();
            if (rowBtn == null)
            {
                rowBtn = gameObject.AddComponent<Button>();
                if (background != null) rowBtn.targetGraphic = background;
            }
            rowBtn.onClick.RemoveAllListeners();
            rowBtn.onClick.AddListener(() => onTapped?.Invoke(item));

            // Legacy acknowledge button — also triggers the detail view
            if (acknowledgeButton != null)
            {
                acknowledgeButton.onClick.RemoveAllListeners();
                acknowledgeButton.onClick.AddListener(() => onTapped?.Invoke(item));
            }

            // Dim acknowledged rows so unread ones stand out
            var cg = GetComponent<CanvasGroup>();
            if (item.acknowledged)
            {
                if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0.45f;
            }
            else if (cg != null)
            {
                cg.alpha = 1f;
            }
        }

        /// <summary>
        /// Backward-compatible overload: receives just the alert id string.
        /// Calls the full-item overload internally.
        /// </summary>
        public void Bind(FarmAlertItem item, Action<string> acknowledgeCallback)
            => Bind(item, (FarmAlertItem _) => acknowledgeCallback?.Invoke(item.id));
    }
}
