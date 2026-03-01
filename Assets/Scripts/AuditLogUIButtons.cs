using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI button handlers for the Audit Log analytics canvas.
/// Provides buttons to generate test events, flush logs, and refresh the display.
/// </summary>
public class AuditLogUIButtons : MonoBehaviour
{
    [Header("Test Event Generation")]
    [Tooltip("Reference to the test generator (optional, will find automatically)")]
    public AuditLogTestGenerator testGenerator;
    
    [Header("UI Buttons (Optional)")]
    public Button generateTestEventsButton;
    public Button flushLogsButton;
    public Button refreshDisplayButton;

    private void Start()
    {
        // Find test generator if not assigned
        if (testGenerator == null)
        {
            testGenerator = FindFirstObjectByType<AuditLogTestGenerator>();
        }
        
        // Setup button listeners if assigned
        if (generateTestEventsButton != null)
        {
            generateTestEventsButton.onClick.AddListener(OnGenerateTestEvents);
        }
        
        if (flushLogsButton != null)
        {
            flushLogsButton.onClick.AddListener(OnFlushLogs);
        }
        
        if (refreshDisplayButton != null)
        {
            refreshDisplayButton.onClick.AddListener(OnRefreshDisplay);
        }
    }

    /// <summary>
    /// Button handler: Generate test events
    /// </summary>
    public void OnGenerateTestEvents()
    {
        Debug.Log("[AuditLogUIButtons] Generate Test Events button clicked");
        
        if (testGenerator != null)
        {
            testGenerator.GenerateTestEvents();
        }
        else
        {
            Debug.LogWarning("[AuditLogUIButtons] No AuditLogTestGenerator found in scene!");
            
            // Try to find it again
            testGenerator = FindFirstObjectByType<AuditLogTestGenerator>();
            if (testGenerator != null)
            {
                testGenerator.GenerateTestEvents();
            }
            else
            {
                // Create a temporary one
                GameObject tempGO = new GameObject("TempTestGenerator");
                testGenerator = tempGO.AddComponent<AuditLogTestGenerator>();
                testGenerator.generateOnStart = false;
                testGenerator.numberOfEvents = 5;
                testGenerator.GenerateTestEvents();
            }
        }
    }

    /// <summary>
    /// Button handler: Flush logs to disk
    /// </summary>
    public void OnFlushLogs()
    {
        Debug.Log("[AuditLogUIButtons] Flush Logs button clicked");
        
        if (AuditLogger.Instance != null)
        {
            AuditLogger.Instance.Flush();
            Debug.Log("[AuditLogUIButtons] Audit logs flushed to disk!");
        }
        else
        {
            Debug.LogWarning("[AuditLogUIButtons] AuditLogger instance not found!");
        }
    }

    /// <summary>
    /// Button handler: Refresh display (reload from files)
    /// </summary>
    public void OnRefreshDisplay()
    {
        Debug.Log("[AuditLogUIButtons] Refresh Display button clicked");
        Debug.Log("[AuditLogUIButtons] To refresh from files, restart the scene or reload the canvas controller.");
        
        // Note: A full refresh would require reloading the AnalyticsCanvasController
        // For now, just log a message
        Debug.Log("[AuditLogUIButtons] Current events are displayed in real-time from memory.");
    }
    
    /// <summary>
    /// Generate a single test event (for quick testing)
    /// </summary>
    public void OnGenerateSingleEvent()
    {
        Debug.Log("[AuditLogUIButtons] Generating single test event");
        
        // Set player name if not set
        if (PlayerIdentity.Instance != null && PlayerIdentity.Instance.PlayerName == "Unknown")
        {
            PlayerIdentity.Instance.SetPlayerName("TestUser");
        }
        
        // Generate a random event
        int eventType = Random.Range(0, 3);
        
        switch (eventType)
        {
            case 0:
                AuditLogger.Instance.Log(AuditEventType.JOIN_MEETING);
                break;
            case 1:
                AuditLogger.Instance.Log(
                    AuditEventType.ENTER_OFFICE,
                    zoneName: "Test Zone"
                );
                break;
            case 2:
                AuditLogger.Instance.Log(
                    AuditEventType.POLL_VOTE,
                    targetId: "Option A",
                    metaJson: "{\"question\":\"Test Question?\"}"
                );
                break;
        }
    }
}

