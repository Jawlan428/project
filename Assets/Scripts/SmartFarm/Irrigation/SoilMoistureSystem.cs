using UnityEngine;

namespace SmartFarm.Irrigation
{
    /// <summary>
    /// Stateless helper for converting moisture percentages into UI-friendly
    /// labels and colours. Used by both the tablet and the alert system so the
    /// classification rules stay consistent in one place.
    /// </summary>
    public static class SoilMoistureSystem
    {
        // Display palette tuned to match the Smart Irrigation Tablet theme.
        public static readonly Color DryColor         = new Color(0.92f, 0.40f, 0.30f, 1f); // warm red/orange
        public static readonly Color MediumColor      = new Color(0.95f, 0.78f, 0.25f, 1f); // amber
        public static readonly Color HealthyColor     = new Color(0.30f, 0.85f, 0.60f, 1f); // mint green
        public static readonly Color OverwaterColor   = new Color(0.40f, 0.65f, 1.00f, 1f); // bright blue

        /// <summary>Returns a friendly label like "Dry", "Healthy", etc.</summary>
        public static string Label(SoilMoistureState state) => state switch
        {
            SoilMoistureState.Dry         => "Dry",
            SoilMoistureState.Medium      => "Medium",
            SoilMoistureState.Healthy     => "Healthy",
            SoilMoistureState.Overwatered => "Overwatered",
            _                             => "Unknown"
        };

        /// <summary>Returns the colour used for moisture indicators in the tablet UI.</summary>
        public static Color Color(SoilMoistureState state) => state switch
        {
            SoilMoistureState.Dry         => DryColor,
            SoilMoistureState.Medium      => MediumColor,
            SoilMoistureState.Healthy     => HealthyColor,
            SoilMoistureState.Overwatered => OverwaterColor,
            _                             => new Color(0.4f, 0.4f, 0.4f, 1f)
        };

        /// <summary>Friendly icon string (single character) used as a glyph fallback.</summary>
        public static string Icon(SoilMoistureState state) => state switch
        {
            SoilMoistureState.Dry         => "!",
            SoilMoistureState.Medium      => "~",
            SoilMoistureState.Healthy     => "\u2713", // ✓
            SoilMoistureState.Overwatered => "\u26A0", // ⚠
            _                             => "?"
        };
    }
}
