using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm.Irrigation.UI
{
    /// <summary>
    /// Top-level shell for the Smart Irrigation Tablet.
    ///
    /// Provides:
    ///   • Tab navigation between Overview / Zones / Analytics / Alerts pages.
    ///   • Live header status (current weather, active zone count, alert count).
    ///   • Pin / unpin to a desk anchor (re-uses the existing tablet ergonomics).
    ///
    /// Pages are simple GameObjects; this controller toggles their active state
    /// and tints the active tab so the visual focus is always clear.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/UI/Smart Irrigation Tablet Controller")]
    public class SmartIrrigationTabletAppController : MonoBehaviour
    {
        [Header("Manager")]
        [SerializeField] private SmartIrrigationTabletManager manager;

        [Header("Header")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text headerStatusText;
        [SerializeField] private Image    statusLed;

        [Header("Tabs")]
        [SerializeField] private Button overviewTabButton;
        [SerializeField] private Button zonesTabButton;
        [SerializeField] private Button analyticsTabButton;
        [SerializeField] private Button alertsTabButton;

        [Header("Pages")]
        [SerializeField] private GameObject overviewPage;
        [SerializeField] private GameObject zonesPage;
        [SerializeField] private GameObject analyticsPage;
        [SerializeField] private GameObject alertsPage;

        [Header("Tab Colours")]
        [SerializeField] private Color activeTabColor   = new Color(0.20f, 0.78f, 0.45f, 1f);
        [SerializeField] private Color inactiveTabColor = new Color(0.10f, 0.18f, 0.22f, 1f);

        [Header("Anchoring")]
        [Tooltip("Optional desk anchor. When 'Snap To Desk On Start' is true the tablet teleports here on Play. Disabled by default so you can hand-place the tablet in the scene.")]
        [SerializeField] private Transform deskAnchor;
        [SerializeField] private bool      snapToDeskOnStart = false;

        private GameObject _activePage;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (manager == null)
                manager = SmartIrrigationTabletManager.Instance ?? FindFirstObjectByType<SmartIrrigationTabletManager>();
        }

        private void Start()
        {
            WireButtons();
            SetActivePage(overviewPage);
            UpdateTabColors();

            if (titleText != null && string.IsNullOrWhiteSpace(titleText.text))
                titleText.text = "Smart Irrigation Tablet";

            if (snapToDeskOnStart && deskAnchor != null)
            {
                transform.SetPositionAndRotation(deskAnchor.position, deskAnchor.rotation);
            }
        }

        private void OnEnable()
        {
            if (manager == null)
                manager = SmartIrrigationTabletManager.Instance ?? FindFirstObjectByType<SmartIrrigationTabletManager>();
            if (manager != null)
                manager.OnDashboardChanged += HandleDashboardChanged;
            ApplyHeader(manager != null ? manager.LatestSnapshot : default);
        }

        private void OnDisable()
        {
            if (manager != null) manager.OnDashboardChanged -= HandleDashboardChanged;
        }

        private void WireButtons()
        {
            if (overviewTabButton  != null) overviewTabButton.onClick.AddListener(()  => SetActivePage(overviewPage));
            if (zonesTabButton     != null) zonesTabButton.onClick.AddListener(()     => SetActivePage(zonesPage));
            if (analyticsTabButton != null) analyticsTabButton.onClick.AddListener(() => SetActivePage(analyticsPage));
            if (alertsTabButton    != null) alertsTabButton.onClick.AddListener(()    => SetActivePage(alertsPage));
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Tab handling
        // ─────────────────────────────────────────────────────────────────────

        public void SetActivePage(GameObject page)
        {
            if (page == null) return;
            if (_activePage == page) return;

            if (overviewPage  != null) overviewPage.SetActive(page == overviewPage);
            if (zonesPage     != null) zonesPage.SetActive(page == zonesPage);
            if (analyticsPage != null) analyticsPage.SetActive(page == analyticsPage);
            if (alertsPage    != null) alertsPage.SetActive(page == alertsPage);

            _activePage = page;
            UpdateTabColors();
        }

        private void UpdateTabColors()
        {
            SetButtonTint(overviewTabButton,  _activePage == overviewPage);
            SetButtonTint(zonesTabButton,     _activePage == zonesPage);
            SetButtonTint(analyticsTabButton, _activePage == analyticsPage);
            SetButtonTint(alertsTabButton,    _activePage == alertsPage);
        }

        private void SetButtonTint(Button button, bool active)
        {
            if (button == null) return;
            var img = button.targetGraphic as Image;
            if (img == null) return;
            img.color = active ? activeTabColor : inactiveTabColor;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Header
        // ─────────────────────────────────────────────────────────────────────

        private void HandleDashboardChanged(IrrigationDashboardSnapshot snap) => ApplyHeader(snap);

        private void ApplyHeader(IrrigationDashboardSnapshot snap)
        {
            if (headerStatusText != null)
            {
                string weather = snap.weather.ToString();
                string zonesPart = snap.totalZones > 0
                    ? $"{snap.activeZoneCount}/{snap.totalZones} zones active"
                    : "no zones";
                headerStatusText.text = $"<b>{weather}</b>  ·  {zonesPart}  ·  {snap.activeAlerts} alert{(snap.activeAlerts == 1 ? "" : "s")}";
            }

            if (statusLed != null)
            {
                if (snap.stormActive)
                    statusLed.color = new Color(0.95f, 0.30f, 0.30f, 1f);
                else if (snap.activeAlerts > 0)
                    statusLed.color = new Color(0.95f, 0.72f, 0.20f, 1f);
                else
                    statusLed.color = new Color(0.30f, 0.85f, 0.55f, 1f);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Wiring helpers (used by editor setup)
        // ─────────────────────────────────────────────────────────────────────

        public void SetManager(SmartIrrigationTabletManager mgr) => manager = mgr;
    }
}
