using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartFarm.MeetingRoom
{
    /// <summary>
    /// Categories of farming reports. Each report type drives a default visual
    /// style and a different source of live data inside <see cref="SmartFarmReportManager"/>.
    /// </summary>
    public enum SmartFarmReportType
    {
        CropHealth,
        Irrigation,
        WeatherForecast,
        HarvestPlanning,
        SoilAnalysis,
        WaterUsage,
        Custom
    }

    /// <summary>
    /// A single chart row that will be rendered as a horizontal bar inside the
    /// document. Designed to stay lightweight so the printout reads well in VR.
    /// </summary>
    [Serializable]
    public struct ReportMetric
    {
        [Tooltip("Short label shown on the left side of the bar (e.g. \"Soil Moisture\").")]
        public string label;

        [Tooltip("Suffix appended to the value (e.g. \"%\", \"L\", \"°C\").")]
        public string unit;

        [Tooltip("Numeric value between 0 and maxValue. Driven by live data when bound.")]
        public float value;

        [Tooltip("Maximum value the bar represents. Used to compute fill percentage.")]
        public float maxValue;

        [Tooltip("Fill colour for the metric bar.")]
        public Color color;

        [Tooltip("If true the value will be highlighted as critical (red tint).")]
        public bool isCritical;
    }

    /// <summary>
    /// ScriptableObject describing a single farming document on the meeting table.
    /// The data is purely descriptive: <see cref="SmartFarmReportManager"/> will
    /// populate the live values at runtime, and <see cref="VRDocumentInteractable"/>
    /// renders it on a world-space canvas.
    /// </summary>
    [CreateAssetMenu(menuName = "SmartFarm/Meeting Room/Smart Farm Report", fileName = "SmartFarmReport")]
    public class SmartFarmReportData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id used by the manager to route live data to this report.")]
        public string reportId = Guid.NewGuid().ToString();

        [Tooltip("Title shown at the top of the document.")]
        public string title = "Crop Health Report";

        [Tooltip("Sub-title shown directly below the title.")]
        public string subtitle = "Daily summary";

        [Tooltip("Logical category. Used by the manager to decide which live data feed to use.")]
        public SmartFarmReportType reportType = SmartFarmReportType.CropHealth;

        [Header("Body Text")]
        [TextArea(3, 8)]
        [Tooltip("Long-form summary. Lines starting with \"!\" are rendered as highlighted bullets.")]
        public string body = "Average crop health is stable.\nIrrigation is operating within nominal limits.";

        [TextArea(2, 6)]
        [Tooltip("Recommendations / next steps. Lines starting with \"!\" are highlighted.")]
        public string recommendations = "Continue current irrigation schedule.\n! Monitor evening temperature drop.";

        [Header("Charts / Metrics")]
        [Tooltip("Bar-chart rows. Values are overwritten at runtime when live data is bound.")]
        public List<ReportMetric> metrics = new List<ReportMetric>();

        [Header("Style")]
        [Tooltip("Background tint of the document page.")]
        public Color pageColor = new Color(0.96f, 0.94f, 0.86f, 1f);

        [Tooltip("Accent colour used for the header bar.")]
        public Color accentColor = new Color(0.15f, 0.45f, 0.25f, 1f);

        [Tooltip("Icon shown in the header (optional).")]
        public Sprite headerIcon;

        [Header("Reading Mode")]
        [Tooltip("Extra zoom multiplier applied while the document is being read.")]
        [Range(1f, 2.5f)] public float readingZoom = 1.35f;

        /// <summary>
        /// Generates a single multi-line string suitable for a plain TMP_Text fallback.
        /// Useful for analytics screens or any place where charts cannot be rendered.
        /// </summary>
        public string ToPlainText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(title);
            if (!string.IsNullOrWhiteSpace(subtitle))
                sb.AppendLine(subtitle);
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(body))
            {
                sb.AppendLine(body);
                sb.AppendLine();
            }

            if (metrics != null && metrics.Count > 0)
            {
                sb.AppendLine("— Metrics —");
                for (int i = 0; i < metrics.Count; i++)
                {
                    var m = metrics[i];
                    sb.AppendLine($"  • {m.label}: {m.value:0.##}{m.unit}");
                }
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(recommendations))
            {
                sb.AppendLine("— Recommendations —");
                sb.AppendLine(recommendations);
            }

            return sb.ToString();
        }
    }
}
