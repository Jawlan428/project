using UnityEngine;

/// <summary>
/// Auto-installer that ensures AuditSystem exists at runtime before any scene loads.
/// Uses RuntimeInitializeOnLoadMethod to run before scene initialization.
/// </summary>
public static class AuditAutoInstaller
{
    private static bool _hasInitialized = false;
    private static GameObject _auditSystemInstance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureAuditSystem()
    {
        // Prevent multiple initializations
        if (_hasInitialized)
            return;

        _hasInitialized = true;

        // Check if AuditSystem already exists in the scene
        GameObject existingSystem = GameObject.Find("AuditSystem");
        if (existingSystem != null)
        {
            // Ensure it has AuditBootstrap component
            if (existingSystem.GetComponent<AuditBootstrap>() == null)
            {
                existingSystem.AddComponent<AuditBootstrap>();
                Debug.Log("[AUDIT] Added AuditBootstrap to existing AuditSystem GameObject.");
            }

            // AUDIT INTEGRATION - Ensure it has AuditBoardBridge component
            if (existingSystem.GetComponent<AuditBoardBridge>() == null)
            {
                existingSystem.AddComponent<AuditBoardBridge>();
                Debug.Log("[AUDIT] Added AuditBoardBridge to existing AuditSystem GameObject.");
            }

            // Ensure it persists
            Object.DontDestroyOnLoad(existingSystem);
            _auditSystemInstance = existingSystem;
            Debug.Log("[AUDIT] Initialized (persistent) - Found existing AuditSystem in scene.");
            return;
        }

        // Create AuditSystem if it doesn't exist
        _auditSystemInstance = new GameObject("AuditSystem");
        _auditSystemInstance.AddComponent<AuditBootstrap>();
        _auditSystemInstance.AddComponent<AuditBoardBridge>(); // AUDIT INTEGRATION
        Object.DontDestroyOnLoad(_auditSystemInstance);

        Debug.Log("[AUDIT] Initialized (persistent) - Created AuditSystem automatically.");
    }

    /// <summary>
    /// Public method to manually ensure AuditSystem exists (for Editor menu tool).
    /// </summary>
    public static void EnsureAuditSystemManual()
    {
        if (_auditSystemInstance != null && _auditSystemInstance.activeInHierarchy)
            return;

        EnsureAuditSystem();
    }
}
