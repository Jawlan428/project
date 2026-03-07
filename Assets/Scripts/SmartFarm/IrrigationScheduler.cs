using System;
using UnityEngine;

namespace SmartFarm
{
    /// <summary>
    /// Defines a named irrigation time window (e.g. Morning 06:00–07:00).
    /// Configurable via the Unity Inspector.
    /// </summary>
    [Serializable]
    public struct IrrigationTimeWindow
    {
        public string presetName;
        [Range(0, 23)] public int startHour;
        [Range(0, 59)] public int startMinute;
        [Range(1, 240)] public int durationMinutes;
    }

    /// <summary>
    /// Manages scheduled irrigation time windows (Morning / Noon / Evening).
    /// Does NOT use Update() — state is queried on-demand by SmartIrrigationManager each tick.
    /// Uses System.DateTime.Now (local time) for schedule matching.
    /// Quest VR friendly: zero allocations per tick.
    /// </summary>
    public class IrrigationScheduler : MonoBehaviour
    {
        [Header("Schedule Presets")]
        [SerializeField]
        private IrrigationTimeWindow morningPreset = new IrrigationTimeWindow
        {
            presetName      = "Morning",
            startHour       = 6,
            startMinute     = 0,
            durationMinutes = 60
        };

        [SerializeField]
        private IrrigationTimeWindow noonPreset = new IrrigationTimeWindow
        {
            presetName      = "Noon",
            startHour       = 12,
            startMinute     = 0,
            durationMinutes = 60
        };

        [SerializeField]
        private IrrigationTimeWindow eveningPreset = new IrrigationTimeWindow
        {
            presetName      = "Evening",
            startHour       = 18,
            startMinute     = 0,
            durationMinutes = 60
        };

        // ── Public state ──────────────────────────────────────────────────────

        /// <summary>The name of the currently selected preset, or empty if none.</summary>
        public string ActivePreset { get; private set; } = "";

        // ── Private ───────────────────────────────────────────────────────────

        private IrrigationTimeWindow _activeWindow;
        private bool                 _presetSet;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Activate a preset by name (case-insensitive: Morning / Noon / Evening).
        /// </summary>
        public void SetPreset(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                ClearPreset();
                return;
            }

            if (name.Equals("Morning", StringComparison.OrdinalIgnoreCase))
            {
                _activeWindow = morningPreset;
                ActivePreset  = "Morning";
            }
            else if (name.Equals("Noon", StringComparison.OrdinalIgnoreCase))
            {
                _activeWindow = noonPreset;
                ActivePreset  = "Noon";
            }
            else if (name.Equals("Evening", StringComparison.OrdinalIgnoreCase))
            {
                _activeWindow = eveningPreset;
                ActivePreset  = "Evening";
            }
            else
            {
                Debug.LogWarning($"[IrrigationScheduler] Unknown preset: '{name}'. Use Morning, Noon or Evening.");
                return;
            }

            _presetSet = true;
        }

        /// <summary>Deactivate the current schedule preset.</summary>
        public void ClearPreset()
        {
            _presetSet   = false;
            ActivePreset = "";
        }

        /// <summary>
        /// Returns true when the current local time falls inside the active schedule window.
        /// Always returns false when no preset is set.
        /// </summary>
        public bool IsScheduledTimeActive()
        {
            if (!_presetSet) return false;

            int nowMinutes   = GetNowMinutes();
            int startMinutes = _activeWindow.startHour * 60 + _activeWindow.startMinute;
            int endMinutes   = startMinutes + _activeWindow.durationMinutes;

            return nowMinutes >= startMinutes && nowMinutes < endMinutes;
        }

        /// <summary>
        /// Human-readable info about when the active schedule will next fire,
        /// e.g. "Next: Morning in 2h 14m" or "Morning: Active now".
        /// Returns "No schedule set" when no preset is active.
        /// </summary>
        public string GetNextActivationInfo()
        {
            if (!_presetSet) return "No schedule set";

            if (IsScheduledTimeActive())
                return $"{ActivePreset}: Active now";

            int nowMinutes   = GetNowMinutes();
            int startMinutes = _activeWindow.startHour * 60 + _activeWindow.startMinute;

            int minutesUntil = startMinutes - nowMinutes;
            if (minutesUntil <= 0)
                minutesUntil += 1440; // wraps to next calendar day

            int h = minutesUntil / 60;
            int m = minutesUntil % 60;
            return h > 0
                ? $"Next: {ActivePreset} in {h}h {m}m"
                : $"Next: {ActivePreset} in {m}m";
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static int GetNowMinutes()
        {
            var now = DateTime.Now;
            return now.Hour * 60 + now.Minute;
        }
    }
}
