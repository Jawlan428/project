using TMPro;
using UnityEngine;

namespace SmartFarm
{
    public class HistoryListItemUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text timestampText;
        [SerializeField] private TMP_Text messageText;

        public void Bind(FarmHistoryItem item)
        {
            if (timestampText != null)
                timestampText.text = item.timestampUtc.ToLocalTime().ToString("HH:mm:ss");
            if (messageText != null)
                messageText.text = item.message;
        }
    }
}
