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
}
