namespace SmartFarm
{
    /// <summary>
    /// Stateless AI decision engine for the Smart Irrigation System.
    /// No MonoBehaviour — pure logic evaluated each tick by SmartIrrigationManager.
    ///
    /// Decision priority (highest → lowest):
    ///   1. Storm            → always OFF  (safety)
    ///   2. Rainy weather    → always OFF  (natural moisture available)
    ///   3. Low soil moisture → ON         (primary trigger)
    ///   4. Low crop health  → ON          (secondary trigger)
    ///   5. Default          → OFF         (optimal conditions)
    /// </summary>
    public static class AIIrrigationDecision
    {
        /// <summary>
        /// Evaluate whether to irrigate.
        /// </summary>
        /// <param name="state">Current farm simulation snapshot.</param>
        /// <param name="weather">Current weather type from WeatherManager.</param>
        /// <param name="lowMoistureThreshold">
        ///   Soil moisture % below which irrigation is triggered (default 35 %).
        /// </param>
        /// <param name="lowHealthThreshold">
        ///   Crop health % below which irrigation is triggered (default 50 %).
        /// </param>
        /// <returns>
        ///   (shouldIrrigate, humanReadableReason) — the reason is displayed on the tablet UI.
        /// </returns>
        public static (bool shouldIrrigate, string reason) Evaluate(
            FarmSimulationState           state,
            WeatherManager.WeatherType    weather,
            float                         lowMoistureThreshold  = 35f,
            float                         lowHealthThreshold    = 50f)
        {
            // ── Safety: storm ─────────────────────────────────────────────────
            if (weather == WeatherManager.WeatherType.Storm)
                return (false, "Storm detected — irrigation disabled");

            // ── Natural moisture: rain ────────────────────────────────────────
            if (weather == WeatherManager.WeatherType.Rainy)
                return (false, "Rain detected — natural moisture sufficient");

            // ── Primary trigger: soil moisture ────────────────────────────────
            if (state.soilMoisturePercent < lowMoistureThreshold)
                return (true, $"Low soil moisture detected ({state.soilMoisturePercent:F0}%)");

            // ── Secondary trigger: crop health ────────────────────────────────
            if (state.cropHealthPercent < lowHealthThreshold)
                return (true, $"Low crop health detected ({state.cropHealthPercent:F0}%)");

            // ── Optimal conditions ────────────────────────────────────────────
            return (false, $"Conditions optimal — moisture {state.soilMoisturePercent:F0}%, health {state.cropHealthPercent:F0}%");
        }
    }
}
