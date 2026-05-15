using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm.Irrigation.UI
{
    /// <summary>
    /// One row in the tablet alerts list. Configured by
    /// <see cref="IrrigationAlertsPageUI"/> and re-bound on every refresh.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/UI/Alert Item")]
    public class IrrigationTabletAlertItemUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private TMP_Text timestampText;
        [SerializeField] private Image    accent;
        [SerializeField] private Image    background;

        public void Bind(IrrigationAlert alert)
        {
            BindRaw(alert.title, alert.message, alert.timestampUtc, alert.GetColor());
        }

        /// <summary>
        /// Generic bind path so the Alerts tab can display rows from either the
        /// <see cref="IrrigationAlertManager"/> (operational alerts) or the
        /// <c>EcoAlertManager</c> (eco / sustainability popups) with one item type.
        /// </summary>
        public void BindRaw(string title, string message, System.DateTime timestampUtc, Color tint)
        {
            if (titleText     != null) titleText.text     = title ?? string.Empty;
            if (messageText   != null) messageText.text   = message ?? string.Empty;
            if (timestampText != null) timestampText.text = timestampUtc.ToLocalTime().ToString("HH:mm:ss");

            if (accent != null) accent.color = tint;
            if (background != null)
            {
                // Subtle dark tint with a hint of the alert colour — keeps the
                // row legible against the tablet's dark theme.
                background.color = new Color(
                    0.07f + tint.r * 0.06f,
                    0.12f + tint.g * 0.06f,
                    0.17f + tint.b * 0.06f,
                    0.95f);
            }
        }

        public void SetReferences(TMP_Text title, TMP_Text message, TMP_Text timestamp, Image accentImg, Image bg)
        {
            titleText     = title;
            messageText   = message;
            timestampText = timestamp;
            accent        = accentImg;
            background    = bg;
        }
    }
}
