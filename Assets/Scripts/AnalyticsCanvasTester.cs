using UnityEngine;

/// <summary>
/// Test script to manually trigger audit events for testing the Analytics Canvas.
/// Attach this to any GameObject and call the methods to test.
/// </summary>
public class AnalyticsCanvasTester : MonoBehaviour
{
    [ContextMenu("Test: Log JOIN_MEETING")]
    public void TestJoinMeeting()
    {
        if (AuditLogger.Instance != null)
        {
            AuditLogger.Instance.Log(AuditEventType.JOIN_MEETING);
            Debug.Log("[AnalyticsCanvasTester] Logged JOIN_MEETING event");
        }
        else
        {
            Debug.LogError("[AnalyticsCanvasTester] AuditLogger.Instance is null!");
        }
    }

    [ContextMenu("Test: Log APPLE_PICKED")]
    public void TestApplePicked()
    {
        if (AuditLogger.Instance != null)
        {
            AuditLogger.Instance.Log(AuditEventType.APPLE_PICKED, targetId: "Apple_Test_01");
            Debug.Log("[AnalyticsCanvasTester] Logged APPLE_PICKED event");
        }
        else
        {
            Debug.LogError("[AnalyticsCanvasTester] AuditLogger.Instance is null!");
        }
    }

    [ContextMenu("Test: Log ENTER_OFFICE")]
    public void TestEnterOffice()
    {
        if (AuditLogger.Instance != null)
        {
            AuditLogger.Instance.Log(AuditEventType.ENTER_OFFICE, zoneName: "TestOffice", position: transform.position);
            Debug.Log("[AnalyticsCanvasTester] Logged ENTER_OFFICE event");
        }
        else
        {
            Debug.LogError("[AnalyticsCanvasTester] AuditLogger.Instance is null!");
        }
    }

    [ContextMenu("Test: Log Multiple Events")]
    public void TestMultipleEvents()
    {
        if (AuditLogger.Instance == null)
        {
            Debug.LogError("[AnalyticsCanvasTester] AuditLogger.Instance is null!");
            return;
        }

        AuditLogger.Instance.Log(AuditEventType.JOIN_MEETING);
        AuditLogger.Instance.Log(AuditEventType.ENTER_OFFICE, zoneName: "Office");
        AuditLogger.Instance.Log(AuditEventType.APPLE_PICKED, targetId: "Apple_01");
        AuditLogger.Instance.Log(AuditEventType.APPLE_DROPPED, targetId: "Apple_01");
        AuditLogger.Instance.Log(AuditEventType.POLL_VOTE, targetId: "Option_A");
        
        Debug.Log("[AnalyticsCanvasTester] Logged 5 test events");
    }

    [ContextMenu("Test: Check AnalyticsCanvasController")]
    public void CheckAnalyticsCanvasController()
    {
        AnalyticsCanvasController controller = FindFirstObjectByType<AnalyticsCanvasController>();
        if (controller == null)
        {
            Debug.LogError("[AnalyticsCanvasTester] AnalyticsCanvasController not found in scene!");
        }
        else
        {
            Debug.Log($"[AnalyticsCanvasTester] AnalyticsCanvasController found: {controller.name}");
            Debug.Log($"[AnalyticsCanvasTester] Enabled: {controller.enabled}");
            Debug.Log($"[AnalyticsCanvasTester] EventScrollRect: {(controller.eventScrollRect != null ? "Assigned" : "NULL")}");
            Debug.Log($"[AnalyticsCanvasTester] ContentPanel: {(controller.contentPanel != null ? "Assigned" : "NULL")}");
            Debug.Log($"[AnalyticsCanvasTester] EventRowPrefab: {(controller.eventRowPrefab != null ? "Assigned" : "NULL")}");
        }

        if (AuditLogger.Instance == null)
        {
            Debug.LogError("[AnalyticsCanvasTester] AuditLogger.Instance is null!");
        }
        else
        {
            Debug.Log($"[AnalyticsCanvasTester] AuditLogger found: {AuditLogger.Instance.name}");
            var recentEvents = AuditLogger.Instance.GetRecentEvents();
            Debug.Log($"[AnalyticsCanvasTester] Recent events in buffer: {recentEvents.Count}");
        }
    }

    void Update()
    {
        // Press 'T' key to test
        if (Input.GetKeyDown(KeyCode.T))
        {
            TestMultipleEvents();
        }
    }
}

