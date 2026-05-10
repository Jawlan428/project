using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm.Irrigation.UI
{
    /// <summary>
    /// Renders a single irrigation zone as a rounded card on the Zones page.
    ///
    /// Shows live moisture %, crop health %, mode (Off/On/Auto), the soil
    /// moisture state with a colour pill, an animated flow bar and 3 mode
    /// toggle buttons. Driven entirely by <see cref="Bind"/> calls from
    /// <see cref="IrrigationZonesPageUI"/>.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/UI/Zone Card")]
    public class IrrigationZoneCardUI : MonoBehaviour
    {
        [Header("Manager")]
        [SerializeField] private SmartIrrigationTabletManager manager;

        [Header("Header")]
        [SerializeField] private TMP_Text zoneNameText;
        [SerializeField] private TMP_Text cropTypeText;
        [SerializeField] private Image    statusLed;
        [SerializeField] private TMP_Text statusText;

        [Header("Stats")]
        [SerializeField] private TMP_Text moistureText;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private TMP_Text waterUsedText;
        [SerializeField] private TMP_Text reasonText;

        [Header("Bars")]
        [SerializeField] private Image    moistureFill;
        [SerializeField] private Image    healthFill;
        [SerializeField] private AnimatedFlowBar flowBar;

        [Header("Soil State Pill")]
        [SerializeField] private Image    soilStatePill;
        [SerializeField] private TMP_Text soilStateLabel;

        [Header("Mode Buttons")]
        [SerializeField] private Button onButton;
        [SerializeField] private Button offButton;
        [SerializeField] private Button autoButton;

        private string _zoneId;

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
            if (onButton   != null) onButton.onClick.AddListener(()   => manager?.SetZoneMode(_zoneId, IrrigationZoneMode.On));
            if (offButton  != null) offButton.onClick.AddListener(()  => manager?.SetZoneMode(_zoneId, IrrigationZoneMode.Off));
            if (autoButton != null) autoButton.onClick.AddListener(() => manager?.SetZoneMode(_zoneId, IrrigationZoneMode.Auto));
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Bind / refresh
        // ─────────────────────────────────────────────────────────────────────

        public void Bind(IrrigationZoneSnapshot snap)
        {
            _zoneId = snap.id;

            if (zoneNameText != null) zoneNameText.text = string.IsNullOrEmpty(snap.displayName) ? snap.id : snap.displayName;
            if (cropTypeText != null) cropTypeText.text = snap.cropType.ToString();

            if (moistureText != null) moistureText.text = $"<size=70%>Moisture</size>\n{Mathf.RoundToInt(snap.averageMoisture)}%";
            if (healthText   != null) healthText.text   = $"<size=70%>Health</size>\n{Mathf.RoundToInt(snap.averageHealth)}%";
            if (waterUsedText != null) waterUsedText.text = $"<size=70%>Water Used</size>\n{snap.totalWaterUsed:F0} u";
            if (reasonText   != null) reasonText.text   = string.IsNullOrEmpty(snap.lastReason) ? "" : snap.lastReason;

            // Bar fills
            if (moistureFill != null)
            {
                moistureFill.fillAmount = Mathf.Clamp01(snap.averageMoisture / 100f);
                moistureFill.color      = SoilMoistureSystem.Color(snap.moistureState);
            }
            if (healthFill != null)
            {
                healthFill.fillAmount = Mathf.Clamp01(snap.averageHealth / 100f);
                healthFill.color      = HealthColor(snap.averageHealth);
            }
            if (flowBar != null) flowBar.SetFlow(snap.flowRate);

            // Soil state pill
            if (soilStatePill != null)
            {
                Color c = SoilMoistureSystem.Color(snap.moistureState);
                c.a = 0.85f;
                soilStatePill.color = c;
            }
            if (soilStateLabel != null)
                soilStateLabel.text = SoilMoistureSystem.Label(snap.moistureState);

            // Status header
            if (statusLed != null)
                statusLed.color = snap.isFlowing
                    ? new Color(0.30f, 0.85f, 0.55f, 1f)
                    : new Color(0.45f, 0.55f, 0.65f, 1f);
            if (statusText != null)
                statusText.text = snap.isFlowing ? "FLOWING" : "STANDBY";

            // Mode button highlight
            HighlightButton(onButton,   snap.mode == IrrigationZoneMode.On,   new Color(0.30f, 0.85f, 0.55f, 1f));
            HighlightButton(offButton,  snap.mode == IrrigationZoneMode.Off,  new Color(0.92f, 0.30f, 0.25f, 1f));
            HighlightButton(autoButton, snap.mode == IrrigationZoneMode.Auto, new Color(0.30f, 0.65f, 0.95f, 1f));
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
        //  Wiring helpers
        // ─────────────────────────────────────────────────────────────────────

        public void SetReferences(
            TMP_Text zoneName, TMP_Text cropType, Image led, TMP_Text status,
            TMP_Text moisture, TMP_Text health, TMP_Text waterUsed, TMP_Text reason,
            Image moistureF, Image healthF, AnimatedFlowBar flow,
            Image pill, TMP_Text pillLabel,
            Button on, Button off, Button autoBtn)
        {
            zoneNameText   = zoneName;
            cropTypeText   = cropType;
            statusLed      = led;
            statusText     = status;
            moistureText   = moisture;
            healthText     = health;
            waterUsedText  = waterUsed;
            reasonText     = reason;
            moistureFill   = moistureF;
            healthFill     = healthF;
            flowBar        = flow;
            soilStatePill  = pill;
            soilStateLabel = pillLabel;
            onButton       = on;
            offButton      = off;
            autoButton     = autoBtn;
        }

        public void SetManager(SmartIrrigationTabletManager mgr) => manager = mgr;
    }
}
