using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SmartFarm.Irrigation.UI
{
    /// <summary>
    /// Renders the list of active irrigation alerts. Subscribes to
    /// <see cref="IrrigationAlertManager.OnActiveListChanged"/> and rebuilds
    /// using a pooled prefab list — no allocations per refresh.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/UI/Alerts Page")]
    public class IrrigationAlertsPageUI : MonoBehaviour
    {
        [Header("Manager")]
        [SerializeField] private SmartIrrigationTabletManager manager;

        [Header("List")]
        [SerializeField] private RectTransform listRoot;
        [SerializeField] private IrrigationTabletAlertItemUI itemTemplate;
        [SerializeField] private TMP_Text emptyStateText;

        private readonly List<IrrigationTabletAlertItemUI> _items = new List<IrrigationTabletAlertItemUI>();

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (manager == null)
                manager = SmartIrrigationTabletManager.Instance ?? FindFirstObjectByType<SmartIrrigationTabletManager>();
            if (itemTemplate != null) itemTemplate.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (manager == null)
                manager = SmartIrrigationTabletManager.Instance ?? FindFirstObjectByType<SmartIrrigationTabletManager>();
            if (manager != null && manager.Alerts != null)
            {
                manager.Alerts.OnActiveListChanged += HandleAlertsChanged;
                HandleAlertsChanged(manager.Alerts.ActiveAlerts);
            }
            else
            {
                ShowEmptyState();
            }
        }

        private void OnDisable()
        {
            if (manager != null && manager.Alerts != null)
                manager.Alerts.OnActiveListChanged -= HandleAlertsChanged;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Refresh
        // ─────────────────────────────────────────────────────────────────────

        private void HandleAlertsChanged(IReadOnlyList<IrrigationAlert> alerts)
        {
            int count = alerts != null ? alerts.Count : 0;
            if (emptyStateText != null) emptyStateText.gameObject.SetActive(count == 0);

            if (itemTemplate == null || listRoot == null) return;

            while (_items.Count < count)
            {
                var clone = Instantiate(itemTemplate, listRoot);
                clone.gameObject.SetActive(true);
                _items.Add(clone);
            }
            for (int i = count; i < _items.Count; i++)
            {
                if (_items[i] != null) _items[i].gameObject.SetActive(false);
            }
            for (int i = 0; i < count; i++)
            {
                if (_items[i] == null) continue;
                _items[i].gameObject.SetActive(true);
                _items[i].Bind(alerts[i]);
            }
        }

        private void ShowEmptyState()
        {
            if (emptyStateText != null) emptyStateText.gameObject.SetActive(true);
            for (int i = 0; i < _items.Count; i++)
                if (_items[i] != null) _items[i].gameObject.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Wiring helpers
        // ─────────────────────────────────────────────────────────────────────

        public void SetReferences(SmartIrrigationTabletManager mgr,
            RectTransform list, IrrigationTabletAlertItemUI template, TMP_Text emptyState)
        {
            manager        = mgr;
            listRoot       = list;
            itemTemplate   = template;
            emptyStateText = emptyState;
        }
    }
}
