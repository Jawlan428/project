using System.Collections.Generic;
using UnityEngine;

namespace SmartFarm.Irrigation.UI
{
    /// <summary>
    /// Renders one card per irrigation zone using the configured prefab.
    /// Cards are bound on every dashboard refresh; the prefab is cloned once
    /// per zone so the UI doesn't re-instantiate at runtime.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/UI/Zones Page")]
    public class IrrigationZonesPageUI : MonoBehaviour
    {
        [Header("Manager")]
        [SerializeField] private SmartIrrigationTabletManager manager;

        [Header("List")]
        [SerializeField] private RectTransform listRoot;
        [SerializeField] private IrrigationZoneCardUI cardTemplate;

        [Header("Empty State")]
        [SerializeField] private GameObject emptyStateLabel;

        private readonly List<IrrigationZoneCardUI> _cards = new List<IrrigationZoneCardUI>();
        private bool _templatePrepared;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (manager == null)
                manager = SmartIrrigationTabletManager.Instance ?? FindFirstObjectByType<SmartIrrigationTabletManager>();
            PrepareTemplate();
        }

        private void OnEnable()
        {
            if (manager == null)
                manager = SmartIrrigationTabletManager.Instance ?? FindFirstObjectByType<SmartIrrigationTabletManager>();

            EnsureReferences();

            PrepareTemplate();

            if (manager != null)
            {
                manager.OnDashboardChanged += HandleDashboardChanged;
                RefreshZoneListFromManager();
            }

            StartCoroutine(RebuildLayoutEndOfFrame());
        }

        private System.Collections.IEnumerator RebuildLayoutEndOfFrame()
        {
            yield return null;
            RefreshZoneListFromManager();
            ForceLayout();
        }

        private void RefreshZoneListFromManager()
        {
            if (manager == null) return;

            var snaps = manager.ZoneSnapshots;
            if (snaps != null && snaps.Count > 0)
                Rebuild(snaps);
            else
                Rebuild(BuildFallbackSnapshots());

            ForceLayout();
        }

        private List<IrrigationZoneSnapshot> BuildFallbackSnapshots()
        {
            var list = new List<IrrigationZoneSnapshot>();
            var zm = manager.Zones;
            if (zm == null) return list;
            var zoneList = zm.Zones;
            for (int i = 0; i < zoneList.Count; i++)
            {
                var z = zoneList[i];
                if (z != null) list.Add(z.Snapshot(null));
            }
            return list;
        }

        private void EnsureReferences()
        {
            if (manager == null)
                manager = SmartIrrigationTabletManager.Instance ?? FindFirstObjectByType<SmartIrrigationTabletManager>();

            if (listRoot == null)
            {
                var t = transform.Find("ZoneScroll/Viewport/ListRoot");
                if (t != null) listRoot = t as RectTransform;
            }

            if (cardTemplate == null)
            {
                var t = transform.Find("ZoneCardTemplate");
                if (t != null)
                    cardTemplate = t.GetComponent<IrrigationZoneCardUI>();
            }

            if (emptyStateLabel == null)
            {
                var t = transform.Find("EmptyState");
                if (t != null) emptyStateLabel = t.gameObject;
            }
        }

        private static void ForceLayout(RectTransform list)
        {
            if (list == null) return;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(list);
            Canvas.ForceUpdateCanvases();
        }

        private void ForceLayout() => ForceLayout(listRoot);

        private void OnDisable()
        {
            if (manager != null) manager.OnDashboardChanged -= HandleDashboardChanged;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Refresh
        // ─────────────────────────────────────────────────────────────────────

        private void HandleDashboardChanged(IrrigationDashboardSnapshot _)
        {
            if (manager == null) return;
            var snaps = manager.ZoneSnapshots;
            if (snaps != null && snaps.Count > 0)
                Rebuild(snaps);
            else
                Rebuild(BuildFallbackSnapshots());
            ForceLayout();
        }

        private void PrepareTemplate()
        {
            if (_templatePrepared) return;
            if (cardTemplate == null) return;

            // Move the template OUT of the list so it never displays as a real card,
            // and never gets counted by the layout group. We keep it under our
            // own transform so it lives & dies with the page.
            if (listRoot != null && cardTemplate.transform.parent == listRoot)
                cardTemplate.transform.SetParent(transform, false);

            cardTemplate.gameObject.SetActive(false);
            _templatePrepared = true;
        }

        public void Rebuild(IReadOnlyList<IrrigationZoneSnapshot> zones)
        {
            EnsureReferences();
            PrepareTemplate();

            int zoneCount = zones != null ? zones.Count : 0;

            if (cardTemplate == null || listRoot == null)
            {
                if (emptyStateLabel != null)
                    emptyStateLabel.SetActive(true);
                Debug.LogWarning("[IrrigationZonesPageUI] Missing cardTemplate or listRoot — run Tools > Smart Farm > Rebuild Smart Irrigation Tablet.");
                return;
            }

            while (_cards.Count < zoneCount)
            {
                var clone = Instantiate(cardTemplate, listRoot);
                clone.gameObject.name = $"ZoneCard_{_cards.Count + 1}";
                clone.gameObject.SetActive(true);
                clone.SetManager(manager);
                _cards.Add(clone);
            }

            for (int i = zoneCount; i < _cards.Count; i++)
            {
                if (_cards[i] != null) _cards[i].gameObject.SetActive(false);
            }

            for (int i = 0; i < zoneCount; i++)
            {
                if (_cards[i] == null) continue;
                _cards[i].gameObject.SetActive(true);
                _cards[i].Bind(zones[i]);
            }

            if (emptyStateLabel != null)
                emptyStateLabel.SetActive(zoneCount == 0);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Wiring helpers
        // ─────────────────────────────────────────────────────────────────────

        public void SetReferences(SmartIrrigationTabletManager mgr, RectTransform list, IrrigationZoneCardUI template)
        {
            manager      = mgr;
            listRoot     = list;
            cardTemplate = template;
        }
    }
}
