using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm
{
    public class AlertListItemUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text severityText;
        [SerializeField] private TMP_Text timestampText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button acknowledgeButton;
        [SerializeField] private Image background;

        [SerializeField] private Color infoColor = new Color(0.2f, 0.35f, 0.55f, 0.95f);
        [SerializeField] private Color warnColor = new Color(0.55f, 0.4f, 0.15f, 0.95f);
        [SerializeField] private Color criticalColor = new Color(0.55f, 0.16f, 0.16f, 0.95f);

        public void Bind(FarmAlertItem item, Action<string> acknowledgeCallback)
        {
            if (severityText != null) severityText.text = item.severity.ToString().ToUpperInvariant();
            if (timestampText != null) timestampText.text = item.timestampUtc.ToLocalTime().ToString("HH:mm:ss");
            if (messageText != null) messageText.text = item.message;

            if (background != null)
            {
                background.color = item.severity switch
                {
                    FarmAlertSeverity.Critical => criticalColor,
                    FarmAlertSeverity.Warning => warnColor,
                    _ => infoColor
                };
            }

            if (acknowledgeButton != null)
            {
                acknowledgeButton.onClick.RemoveAllListeners();
                acknowledgeButton.onClick.AddListener(() => acknowledgeCallback?.Invoke(item.id));
            }
        }
    }
}
