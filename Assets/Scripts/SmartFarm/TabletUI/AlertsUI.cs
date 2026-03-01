using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SmartFarm
{
    public class AlertsUI : MonoBehaviour
    {
        [SerializeField] private FarmDataManager dataManager;
        [SerializeField] private TMP_Text badgeCountText;
        [SerializeField] private GameObject badgeRoot;
        [SerializeField] private Transform listRoot;
        [SerializeField] private AlertListItemUI itemPrefab;
        [SerializeField] private GameObject emptyStateRoot;

        private readonly List<GameObject> _spawned = new List<GameObject>();

        private void OnEnable()
        {
            if (dataManager == null) dataManager = FindFirstObjectByType<FarmDataManager>();
            if (dataManager != null)
                dataManager.OnAlertsChanged += OnAlertsChanged;
        }

        private void OnDisable()
        {
            if (dataManager != null)
                dataManager.OnAlertsChanged -= OnAlertsChanged;
        }

        private void OnAlertsChanged(IReadOnlyList<FarmAlertItem> alerts, int unreadCount)
        {
            if (badgeRoot != null) badgeRoot.SetActive(unreadCount > 0);
            if (badgeCountText != null) badgeCountText.text = unreadCount.ToString();

            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Destroy(_spawned[i]);
            _spawned.Clear();

            if (emptyStateRoot != null) emptyStateRoot.SetActive(alerts == null || alerts.Count == 0);
            if (alerts == null || alerts.Count == 0 || itemPrefab == null || listRoot == null) return;

            for (int i = 0; i < alerts.Count; i++)
            {
                var go = Instantiate(itemPrefab.gameObject, listRoot);
                var ui = go.GetComponent<AlertListItemUI>();
                if (ui != null)
                    ui.Bind(alerts[i], id => dataManager?.AcknowledgeAlert(id));
                _spawned.Add(go);
            }
        }
    }
}
