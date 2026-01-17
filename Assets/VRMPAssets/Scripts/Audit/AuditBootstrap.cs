using UnityEngine;

/// <summary>
/// Bootstrap component that initializes the audit system and logs session start/end.
/// This component is automatically attached by AuditAutoInstaller if missing.
/// </summary>
public class AuditBootstrap : MonoBehaviour
{
    private static bool _hasLoggedSessionStart = false;

    void Start()
    {
        // Initialize AuditLogger (creates singleton if needed)
        AuditLogger logger = AuditLogger.Instance;

        // Log session start only once per application run
        if (!_hasLoggedSessionStart)
        {
            logger.Log(AuditEventType.SESSION_START);
            _hasLoggedSessionStart = true;
        }
    }

    void OnDestroy()
    {
        // Log session end and flush to disk
        if (AuditLogger.Instance != null)
        {
            AuditLogger.Instance.Log(AuditEventType.SESSION_END);
            AuditLogger.Instance.Flush();
        }
    }

    void OnApplicationQuit()
    {
        // Ensure we flush on quit
        if (AuditLogger.Instance != null)
        {
            AuditLogger.Instance.Log(AuditEventType.SESSION_END);
            AuditLogger.Instance.Flush();
        }
    }
}
