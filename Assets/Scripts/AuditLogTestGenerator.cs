using UnityEngine;
using System.Collections;

/// <summary>
/// Helper script to generate test audit events for testing the analytics canvas display.
/// Attach this to any GameObject in the scene and call GenerateTestEvents() to create sample events.
/// </summary>
public class AuditLogTestGenerator : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Automatically generate test events on start")]
    public bool generateOnStart = true;
    
    [Tooltip("Number of test events to generate")]
    public int numberOfEvents = 10;
    
    [Tooltip("Delay between events (seconds)")]
    public float delayBetweenEvents = 0.5f;

    private void Start()
    {
        if (generateOnStart)
        {
            StartCoroutine(GenerateTestEventsCoroutine());
        }
    }

    /// <summary>
    /// Generates test audit events with a delay between each
    /// </summary>
    public void GenerateTestEvents()
    {
        StartCoroutine(GenerateTestEventsCoroutine());
    }

    private IEnumerator GenerateTestEventsCoroutine()
    {
        yield return new WaitForSeconds(1f); // Wait for system initialization
        
        Debug.Log($"[AuditLogTestGenerator] Generating {numberOfEvents} test events...");
        
        // Set a test player name
        if (PlayerIdentity.Instance != null)
        {
            PlayerIdentity.Instance.SetPlayerName("TestPlayer");
        }
        
        // Generate diverse test events
        string[] testPlayers = { "Alice", "Bob", "Charlie", "Diana", "Eve" };
        string[] zones = { "Office", "Meeting Room", "Kitchen", "Garden", "Lounge" };
        string[] appleTypes = { "Red Apple", "Green Apple", "Golden Apple", "Blue Apple" };
        
        for (int i = 0; i < numberOfEvents; i++)
        {
            // Randomly select event type
            int eventType = Random.Range(0, 7);
            
            switch (eventType)
            {
                case 0:
                    AuditLogger.Instance.Log(
                        AuditEventType.SESSION_START
                    );
                    break;
                    
                case 1:
                    AuditLogger.Instance.Log(
                        AuditEventType.JOIN_MEETING
                    );
                    break;
                    
                case 2:
                    AuditLogger.Instance.Log(
                        AuditEventType.ENTER_OFFICE,
                        zoneName: zones[Random.Range(0, zones.Length)],
                        position: GetRandomPosition()
                    );
                    break;
                    
                case 3:
                    string apple = appleTypes[Random.Range(0, appleTypes.Length)];
                    AuditLogger.Instance.Log(
                        AuditEventType.APPLE_PICKED,
                        targetId: apple,
                        zoneName: "Orchard",
                        position: GetRandomPosition()
                    );
                    break;
                    
                case 4:
                    string droppedApple = appleTypes[Random.Range(0, appleTypes.Length)];
                    AuditLogger.Instance.Log(
                        AuditEventType.APPLE_DROPPED,
                        targetId: droppedApple,
                        zoneName: "Orchard",
                        position: GetRandomPosition()
                    );
                    break;
                    
                case 5:
                    string inventoryApple = appleTypes[Random.Range(0, appleTypes.Length)];
                    AuditLogger.Instance.Log(
                        AuditEventType.APPLE_ADDED_TO_INVENTORY,
                        targetId: "Inventory",
                        metaJson: $"{{\"appleName\":\"{inventoryApple}\",\"slot\":{Random.Range(0, 5)}}}"
                    );
                    break;
                    
                case 6:
                    string[] pollOptions = { "Option A", "Option B", "Option C", "Option D" };
                    string chosenOption = pollOptions[Random.Range(0, pollOptions.Length)];
                    AuditLogger.Instance.Log(
                        AuditEventType.POLL_VOTE,
                        targetId: chosenOption,
                        metaJson: $"{{\"question\":\"Test Poll Question?\",\"chosenOption\":\"{chosenOption}\"}}"
                    );
                    break;
            }
            
            Debug.Log($"[AuditLogTestGenerator] Generated event {i + 1}/{numberOfEvents}");
            
            if (i < numberOfEvents - 1)
            {
                yield return new WaitForSeconds(delayBetweenEvents);
            }
        }
        
        Debug.Log("[AuditLogTestGenerator] Test event generation complete!");
    }
    
    /// <summary>
    /// Generates a random position for test events
    /// </summary>
    private Vector3 GetRandomPosition()
    {
        return new Vector3(
            Random.Range(-10f, 10f),
            Random.Range(0f, 2f),
            Random.Range(-10f, 10f)
        );
    }
    
    /// <summary>
    /// Clears all audit logs (for testing)
    /// </summary>
    public void ClearAuditLogs()
    {
        Debug.Log("[AuditLogTestGenerator] Note: Clearing audit logs would require restarting the session.");
        Debug.Log("[AuditLogTestGenerator] Current events are stored in AuditLogger's memory.");
    }
    
    /// <summary>
    /// Flushes current events to disk
    /// </summary>
    public void FlushAuditLogs()
    {
        if (AuditLogger.Instance != null)
        {
            AuditLogger.Instance.Flush();
            Debug.Log("[AuditLogTestGenerator] Audit logs flushed to disk!");
        }
    }
}

