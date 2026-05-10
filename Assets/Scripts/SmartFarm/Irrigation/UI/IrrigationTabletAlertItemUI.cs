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
            if (titleText     != null) titleText.text     = alert.title;
            if (messageText   != null) messageText.text   = alert.message;
            if (timestampText != null) timestampText.text = alert.timestampUtc.ToLocalTime().ToString("HH:mm:ss");

            Color color = alert.GetColor();
            if (accent != null) accent.color = color;
            if (background != null)
            {
                Color bg = color;
                bg.r *= 0.18f; bg.g *= 0.22f; bg.b *= 0.32f; bg.a = 0.95f;
                background.color = new Color(0.07f, 0.12f, 0.17f, 0.95f);
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
