using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm.Irrigation.UI
{
    /// <summary>
    /// Drop-in replacement for the template-cloning ZonesPageUI.
    ///
    /// Holds an explicit list of <see cref="ZoneCardWidgets"/> entries — one per
    /// zone you want shown on the ZONES tab. Each card has its own ON / OFF /
    /// AUTO buttons that drive a single zone in <see cref="IrrigationZoneManager"/>
    /// by id (e.g. "zone_corn", "zone_wheat").
    ///
    /// Avoids the VerticalLayoutGroup + ContentSizeFitter + template-clone
    /// chain that can silently fail in some scenes; every reference is wired
    /// explicitly by the editor builder.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/UI/Direct Zone Cards")]
    public class DirectZoneCardsUI : MonoBehaviour
    {
        [System.Serializable]
        public class ZoneCardWidgets
        {
            [Tooltip("Zone id this card drives (e.g. zone_corn / zone_wheat).")]
            public string zoneId;

            public TMP_Text zoneNameText;
            public TMP_Text cropTypeText;
            public TMP_Text moistureText;
            public TMP_Text healthText;
            public TMP_Text waterUsedText;
            public TMP_Text reasonText;
            public TMP_Text statusText;

            public Image statusLed;
            public Image moistureFill;
            public Image healthFill;
            public Image pillImage;
            public TMP_Text pillLabel;

            public AnimatedFlowBar flowBar;

            public Button onButton;
            public Button offButton;
            public Button autoButton;
        }

        [Header("Manager")]
        [SerializeField] private SmartIrrigationTabletManager manager;

        [Header("Cards (one per zone — wired explicitly)")]
        [SerializeField] private List<ZoneCardWidgets> cards = new List<ZoneCardWidgets>();

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
            // Wire each card's buttons to the matching zone id.
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card == null) continue;
                string id = card.zoneId;

                if (card.onButton != null)
                    card.onButton.onClick.AddListener(() => manager?.SetZoneMode(id, IrrigationZoneMode.On));
                if (card.offButton != null)
                    card.offButton.onClick.AddListener(() => manager?.SetZoneMode(id, IrrigationZoneMode.Off));
                if (card.autoButton != null)
                    card.autoButton.onClick.AddListener(() => manager?.SetZoneMode(id, IrrigationZoneMode.Auto));
            }
        }

        private void OnEnable()
        {
            if (manager == null)
                manager = SmartIrrigationTabletManager.Instance ?? FindFirstObjectByType<SmartIrrigationTabletManager>();

            if (manager != null)
            {
                manager.OnDashboardChanged += HandleDashboardChanged;
                RefreshAll();
            }
        }

        private void OnDisable()
        {
            if (manager != null) manager.OnDashboardChanged -= HandleDashboardChanged;
        }

        private void HandleDashboardChanged(IrrigationDashboardSnapshot _) => RefreshAll();

        // ─────────────────────────────────────────────────────────────────────
        //  Refresh
        // ─────────────────────────────────────────────────────────────────────

        public void RefreshAll()
        {
            if (manager == null || manager.Zones == null) return;
            var zones = manager.Zones.Zones;
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card == null || string.IsNullOrEmpty(card.zoneId)) continue;

                var zone = FindZone(zones, card.zoneId);
                if (zone == null) continue;

                Bind(card, zone.Snapshot(null));
            }
        }

        private static IrrigationZone FindZone(IReadOnlyList<IrrigationZone> zones, string id)
        {
            for (int i = 0; i < zones.Count; i++)
                if (zones[i] != null && zones[i].id == id) return zones[i];
            return null;
        }

        private static void Bind(ZoneCardWidgets card, IrrigationZoneSnapshot snap)
        {
            if (card.zoneNameText  != null) card.zoneNameText.text  = string.IsNullOrEmpty(snap.displayName) ? snap.id : snap.displayName;
            if (card.cropTypeText  != null) card.cropTypeText.text  = snap.cropType.ToString();
            if (card.moistureText  != null) card.moistureText.text  = $"<size=70%>Moisture</size>\n{Mathf.RoundToInt(snap.averageMoisture)}%";
            if (card.healthText    != null) card.healthText.text    = $"<size=70%>Health</size>\n{Mathf.RoundToInt(snap.averageHealth)}%";
            if (card.waterUsedText != null) card.waterUsedText.text = $"<size=70%>Water Used</size>\n{snap.totalWaterUsed:F0} u";
            if (card.reasonText    != null) card.reasonText.text    = string.IsNullOrEmpty(snap.lastReason) ? "" : snap.lastReason;
            if (card.statusText    != null) card.statusText.text    = snap.isFlowing ? "FLOWING" : "STANDBY";

            if (card.statusLed != null)
                card.statusLed.color = snap.isFlowing
                    ? new Color(0.30f, 0.85f, 0.55f, 1f)
                    : new Color(0.45f, 0.55f, 0.65f, 1f);

            if (card.moistureFill != null)
            {
                card.moistureFill.fillAmount = Mathf.Clamp01(snap.averageMoisture / 100f);
                card.moistureFill.color      = SoilMoistureSystem.Color(snap.moistureState);
            }
            if (card.healthFill != null)
            {
                card.healthFill.fillAmount = Mathf.Clamp01(snap.averageHealth / 100f);
                card.healthFill.color      = HealthColor(snap.averageHealth);
            }

            if (card.flowBar != null) card.flowBar.SetFlow(snap.flowRate);

            if (card.pillImage != null)
            {
                Color c = SoilMoistureSystem.Color(snap.moistureState);
                c.a = 0.85f;
                card.pillImage.color = c;
            }
            if (card.pillLabel != null) card.pillLabel.text = SoilMoistureSystem.Label(snap.moistureState);

            HighlightButton(card.onButton,   snap.mode == IrrigationZoneMode.On,   new Color(0.30f, 0.85f, 0.55f, 1f));
            HighlightButton(card.offButton,  snap.mode == IrrigationZoneMode.Off,  new Color(0.92f, 0.30f, 0.25f, 1f));
            HighlightButton(card.autoButton, snap.mode == IrrigationZoneMode.Auto, new Color(0.30f, 0.65f, 0.95f, 1f));
        }

        private static void HighlightButton(Button btn, bool active, Color activeColor)
        {
            if (btn == null) return;
            var img = btn.targetGraphic as Image;
            if (img == null) return;
            img.color = active ? activeColor : new Color(0.18f, 0.26f, 0.32f, 1f);
        }

        private static Color HealthColor(float pct)
        {
            if (pct < 30f) return new Color(0.92f, 0.30f, 0.25f, 1f);
            if (pct < 55f) return new Color(0.95f, 0.78f, 0.25f, 1f);
            return new Color(0.30f, 0.85f, 0.55f, 1f);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Editor wiring
        // ─────────────────────────────────────────────────────────────────────

        public void SetManager(SmartIrrigationTabletManager mgr) => manager = mgr;
        public void SetCards(List<ZoneCardWidgets> newCards)     => cards = newCards;
    }
}
