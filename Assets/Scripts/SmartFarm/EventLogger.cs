using UnityEngine;

namespace SmartFarm
{
    /// <summary>
    /// Central event logger for the Smart Collaborative VR Agriculture Platform.
    /// Connects to AuditLogger and the 3D recording/version system.
    /// All important actions should trigger EventLogger.LogEvent().
    /// </summary>
    public static class EventLogger
    {
        /// <summary>
        /// UI/history systems can subscribe for live event feed.
        /// (timestampUtc, message)
        /// </summary>
        public static event System.Action<System.DateTime, string> OnEventLogged;

        /// <summary>
        /// Logs an event to the audit/recording system.
        /// Connects to AuditLogger and can be extended for 3D recording version management.
        /// </summary>
        /// <param name="message">Human-readable event description (e.g., "Vote Opened", "Jawlan voted Option A")</param>
        public static void LogEvent(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            if (AuditLogger.Instance != null)
            {
                AuditLogger.Instance.Log(
                    AuditEventType.FARM_EVENT,
                    targetId: null,
                    zoneName: null,
                    position: null,
                    metaJson: $"{{\"message\":\"{EscapeJson(message)}\"}}"
                );
            }

            Debug.Log($"[SmartFarm] {message}");
            OnEventLogged?.Invoke(System.DateTime.UtcNow, message);
        }

        /// <summary>
        /// Logs an event with optional position for 3D recording context.
        /// </summary>
        public static void LogEvent(string message, Vector3? position)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            if (AuditLogger.Instance != null)
            {
                AuditLogger.Instance.Log(
                    AuditEventType.FARM_EVENT,
                    targetId: null,
                    zoneName: null,
                    position: position,
                    metaJson: $"{{\"message\":\"{EscapeJson(message)}\"}}"
                );
            }

            Debug.Log($"[SmartFarm] {message}");
            OnEventLogged?.Invoke(System.DateTime.UtcNow, message);
        }

        /// <summary>
        /// Logs a poll/vote-specific event.
        /// </summary>
        public static void LogVoteEvent(string voterName, string option, string question)
        {
            string msg = $"{voterName} voted {option}";
            if (!string.IsNullOrEmpty(question))
                msg += $" ({question})";

            LogEvent(msg);
        }

        /// <summary>
        /// Logs irrigation state change.
        /// </summary>
        public static void LogIrrigationChanged(bool enabled)
        {
            string msg = enabled ? "Irrigation Enabled" : "Irrigation Disabled";
            if (AuditLogger.Instance != null)
            {
                AuditLogger.Instance.Log(AuditEventType.FARM_IRRIGATION_CHANGED, targetId: enabled ? "ON" : "OFF");
            }
            LogEvent(msg);
        }

        /// <summary>
        /// Logs temperature change.
        /// </summary>
        public static void LogTemperatureChanged(float temperature)
        {
            string msg = $"Temperature changed to {temperature:F1}°C";
            if (AuditLogger.Instance != null)
            {
                AuditLogger.Instance.Log(AuditEventType.FARM_TEMPERATURE_CHANGED, targetId: temperature.ToString("F1"));
            }
            LogEvent(msg);
        }

        /// <summary>
        /// Logs plant stage progression.
        /// </summary>
        public static void LogPlantStageChanged(string plantId, int stageIndex, string stageName)
        {
            string msg = $"Plant #{plantId} reached {stageName} stage";
            if (AuditLogger.Instance != null)
            {
                AuditLogger.Instance.Log(
                    AuditEventType.FARM_PLANT_STAGE_CHANGED,
                    targetId: plantId,
                    metaJson: $"{{\"stageIndex\":{stageIndex},\"stageName\":\"{EscapeJson(stageName)}\"}}"
                );
            }
            LogEvent(msg);
        }

        private static string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}
