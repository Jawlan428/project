using System.Collections.Generic;
using SmartFarm.Irrigation.Sustainability;
using TMPro;
using UnityEngine;

namespace SmartFarm.Irrigation.UI
{
    /// <summary>
    /// Renders the merged list of active alerts on the ALERTS tab:
    /// <list type="bullet">
    ///   <item><b>Irrigation alerts</b> from <see cref="IrrigationAlertManager"/>
    ///   (low moisture, overwatering, storm pause, etc.).</item>
    ///   <item><b>Eco alerts</b> from <see cref="EcoAlertManager"/>
    ///   (rainwater optimization, water savings milestones, water-waste warning…).</item>
    /// </list>
    /// Rows are sorted newest-first and re-bound on every refresh — no
    /// allocations per refresh.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/UI/Alerts Page")]
    public class IrrigationAlertsPageUI : MonoBehaviour
    {
        [Header("Managers")]
        [SerializeField] private SmartIrrigationTabletManager manager;

        [Tooltip("Optional. When wired, eco alerts from the Sustainability Monitor are merged into this tab.")]
        [SerializeField] private EcoAlertManager ecoAlerts;

        [Header("List")]
        [SerializeField] private RectTransform listRoot;
        [SerializeField] private IrrigationTabletAlertItemUI itemTemplate;
        [SerializeField] private TMP_Text emptyStateText;

        private readonly List<IrrigationTabletAlertItemUI> _items = new List<IrrigationTabletAlertItemUI>();

        // Shared row data so we can bind irrigation + eco alerts with the same UI.
        private struct UnifiedRow
        {
            public string title;
            public string message;
            public System.DateTime timestampUtc;
            public Color color;
        }
        private readonly List<UnifiedRow> _merged = new List<UnifiedRow>();

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
            if (ecoAlerts == null)
                ecoAlerts = FindFirstObjectByType<EcoAlertManager>();

            if (manager != null && manager.Alerts != null)
                manager.Alerts.OnActiveListChanged += HandleIrrigationAlertsChanged;
            if (ecoAlerts != null)
                ecoAlerts.OnActiveListChanged += HandleEcoAlertsChanged;

            Rebuild();
        }

        private void OnDisable()
        {
            if (manager != null && manager.Alerts != null)
                manager.Alerts.OnActiveListChanged -= HandleIrrigationAlertsChanged;
            if (ecoAlerts != null)
                ecoAlerts.OnActiveListChanged -= HandleEcoAlertsChanged;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Refresh
        // ─────────────────────────────────────────────────────────────────────

        private void HandleIrrigationAlertsChanged(IReadOnlyList<IrrigationAlert> _) => Rebuild();
        private void HandleEcoAlertsChanged(IReadOnlyList<EcoAlert> _)              => Rebuild();

        private void Rebuild()
        {
            _merged.Clear();

            if (manager != null && manager.Alerts != null)
            {
                var irrigation = manager.Alerts.ActiveAlerts;
                for (int i = 0; i < irrigation.Count; i++)
                {
                    var a = irrigation[i];
                    _merged.Add(new UnifiedRow
                    {
                        title        = a.title,
                        message      = a.message,
                        timestampUtc = a.timestampUtc,
                        color        = a.GetColor()
                    });
                }
            }

            if (ecoAlerts != null)
            {
                var eco = ecoAlerts.ActiveAlerts;
                for (int i = 0; i < eco.Count; i++)
                {
                    var e = eco[i];
                    _merged.Add(new UnifiedRow
                    {
                        title        = e.title,
                        message      = e.message,
                        timestampUtc = e.timestampUtc,
                        color        = e.GetColor()
                    });
                }
            }

            // Newest first
            _merged.Sort((a, b) => b.timestampUtc.CompareTo(a.timestampUtc));

            int count = _merged.Count;
            if (emptyStateText != null) emptyStateText.gameObject.SetActive(count == 0);

            if (itemTemplate == null || listRoot == null) return;

            while (_items.Count < count)
            {
                var clone = Instantiate(itemTemplate, listRoot);
                clone.gameObject.SetActive(true);
                _items.Add(clone);
            }
            for (int i = count; i < _items.Count; i++)
                if (_items[i] != null) _items[i].gameObject.SetActive(false);

            for (int i = 0; i < count; i++)
            {
                if (_items[i] == null) continue;
                _items[i].gameObject.SetActive(true);
                var row = _merged[i];
                _items[i].BindRaw(row.title, row.message, row.timestampUtc, row.color);
            }
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

        public void SetEcoAlertManager(EcoAlertManager eco) => ecoAlerts = eco;
    }
}
