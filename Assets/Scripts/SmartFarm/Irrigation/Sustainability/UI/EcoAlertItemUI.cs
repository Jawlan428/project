using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm.Irrigation.Sustainability.UI
{
    /// <summary>
    /// Single row inside the scrolling Eco Alerts list. Used as a template that
    /// is cloned by <see cref="SustainabilityMonitorPageUI"/> when alerts are
    /// raised / resolved.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/Sustainability/UI/Eco Alert Item")]
    public class EcoAlertItemUI : MonoBehaviour
    {
        [SerializeField] private Image    accent;
        [SerializeField] private Image    background;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private TMP_Text timestampText;

        public void Bind(EcoAlert alert)
        {
            if (titleText     != null) titleText.text     = alert.title;
            if (messageText   != null) messageText.text   = alert.message;
            if (timestampText != null) timestampText.text = alert.timestampUtc.ToLocalTime().ToString("HH:mm");

            Color c = alert.GetColor();
            if (accent     != null) accent.color = c;
            if (background != null) background.color = new Color(c.r * 0.18f, c.g * 0.18f, c.b * 0.18f, 0.95f);
        }

        public void SetReferences(Image accentImg, Image bg, TMP_Text title, TMP_Text msg, TMP_Text ts)
        {
            accent        = accentImg;
            background    = bg;
            titleText     = title;
            messageText   = msg;
            timestampText = ts;
        }
    }
}
