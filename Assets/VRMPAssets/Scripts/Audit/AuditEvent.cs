using System;
using UnityEngine;

/// <summary>
/// Serializable wrapper for Vector3 to support JSON serialization.
/// </summary>
[System.Serializable]
public class SerializableVector3
{
    public float x;
    public float y;
    public float z;

    public SerializableVector3() { }

    public SerializableVector3(Vector3 v)
    {
        x = v.x;
        y = v.y;
        z = v.z;
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}

/// <summary>
/// Represents a single audit event with all relevant metadata.
/// </summary>
[System.Serializable]
public class AuditEvent
{
    public string timestamp;           // ISO 8601 string
    public string sessionId;            // Session identifier
    public string playerName;           // Dynamic player name
    public string eventType;            // Enum as string
    public string targetId;             // Optional target identifier
    public string sceneName;            // Current scene name
    public string zoneName;             // Optional zone name
    public SerializableVector3 position; // Optional position
    public string metaJson;             // Optional small JSON metadata

    public AuditEvent()
    {
        timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        position = null;
    }

    /// <summary>
    /// Gets a formatted time string for display (HH:mm:ss in local time)
    /// </summary>
    public string GetFormattedTime()
    {
        if (DateTime.TryParse(timestamp, System.Globalization.CultureInfo.InvariantCulture, 
            System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime dt))
        {
            return dt.ToLocalTime().ToString("HH:mm:ss");
        }
        return "N/A";
    }

    /// <summary>
    /// Gets a summary string for display
    /// </summary>
    public string GetSummary()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append(eventType);
        
        if (!string.IsNullOrEmpty(playerName) && playerName != "Unknown")
            sb.Append($" | Player: {playerName}");
        
        if (!string.IsNullOrEmpty(targetId))
            sb.Append($" | Target: {targetId}");
        
        if (!string.IsNullOrEmpty(zoneName))
            sb.Append($" | Zone: {zoneName}");
        
        if (position != null)
            sb.Append($" | Pos: ({position.x:F1}, {position.y:F1}, {position.z:F1})");
        
        if (!string.IsNullOrEmpty(metaJson))
            sb.Append($" | Meta: {metaJson}");
        
        return sb.ToString();
    }
}
