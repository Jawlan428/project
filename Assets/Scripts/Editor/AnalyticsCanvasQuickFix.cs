using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Quick fix tool to diagnose and fix Analytics Canvas display issues
/// </summary>
public class AnalyticsCanvasQuickFix
{
    [MenuItem("Tools/Analytics Canvas/Quick Fix - Show Events")]
    public static void QuickFix()
    {
        AnalyticsCanvasController[] controllers = Object.FindObjectsOfType<AnalyticsCanvasController>();
        if (controllers.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "AnalyticsCanvasController not found!", "OK");
            return;
        }

        if (controllers.Length > 1)
        {
            EditorUtility.DisplayDialog("Warning", 
                $"Found {controllers.Length} AnalyticsCanvasController components!\n\n" +
                "Only one should exist. Please delete the duplicate(s).\n\n" +
                "I will fix the first one only.", 
                "OK");
        }

        AnalyticsCanvasController controller = controllers[0];
        int fixes = 0;

        // CRITICAL FIX: Check if Content Panel is pointing to Viewport instead of Content
        if (controller.contentPanel != null && controller.contentPanel.name == "Viewport")
        {
            Debug.LogError("[QuickFix] Content Panel is pointing to Viewport instead of Content!");
            
            // Try to find Content child
            Transform contentChild = controller.contentPanel.transform.Find("Content");
            if (contentChild != null)
            {
                controller.contentPanel = contentChild.GetComponent<RectTransform>();
                fixes++;
                Debug.Log("[QuickFix] Fixed Content Panel reference!");
            }
        }

        // Check and enable Content Panel
        if (controller.contentPanel != null)
        {
            if (!controller.contentPanel.gameObject.activeSelf)
            {
                controller.contentPanel.gameObject.SetActive(true);
                fixes++;
                Debug.Log("[QuickFix] Enabled Content Panel");
            }
            
            // Check if Content has VerticalLayoutGroup
            VerticalLayoutGroup vlg = controller.contentPanel.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
            {
                vlg = controller.contentPanel.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 2f;
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                fixes++;
                Debug.Log("[QuickFix] Added VerticalLayoutGroup to Content");
            }
        }

        // Check and enable ScrollRect
        if (controller.eventScrollRect != null)
        {
            if (!controller.eventScrollRect.gameObject.activeSelf)
            {
                controller.eventScrollRect.gameObject.SetActive(true);
                fixes++;
                Debug.Log("[QuickFix] Enabled ScrollRect");
            }
        }

        // Check if canvas is active
        Canvas canvas = controller.GetComponentInParent<Canvas>();
        if (canvas != null && !canvas.gameObject.activeSelf)
        {
            canvas.gameObject.SetActive(true);
            fixes++;
            Debug.Log("[QuickFix] Enabled Canvas");
        }

        // Force refresh
        if (Application.isPlaying)
        {
            // Log a test event
            if (AuditLogger.Instance != null)
            {
                AuditLogger.Instance.Log(AuditEventType.JOIN_MEETING);
                Debug.Log("[QuickFix] Logged test event");
                fixes++;
            }
        }

        EditorUtility.SetDirty(controller);
        
        string message = fixes > 0 
            ? $"Applied {fixes} fix(es)!\n\nCheck Console for details."
            : "No issues found.\n\nIf events still don't show:\n1. Check filters are set to 'All'\n2. Verify EventRow prefab is assigned\n3. Check Console for errors";
            
        EditorUtility.DisplayDialog("Quick Fix Complete", message, "OK");
    }

    [MenuItem("Tools/Analytics Canvas/Test - Log Event Now")]
    public static void LogTestEvent()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Error", "Must be in Play Mode!", "OK");
            return;
        }

        if (AuditLogger.Instance == null)
        {
            EditorUtility.DisplayDialog("Error", "AuditLogger.Instance is null!", "OK");
            return;
        }

        AuditLogger.Instance.Log(AuditEventType.JOIN_MEETING);
        AuditLogger.Instance.Log(AuditEventType.ENTER_OFFICE, zoneName: "TestZone");
        AuditLogger.Instance.Log(AuditEventType.APPLE_PICKED, targetId: "TestApple");
        
        Debug.Log("[QuickFix] Logged 3 test events - they should appear in the canvas!");
        EditorUtility.DisplayDialog("Success", "Logged 3 test events!\n\nCheck the Analytics Canvas.", "OK");
    }

    [MenuItem("Tools/Analytics Canvas/Diagnose")]
    public static void Diagnose()
    {
        Debug.Log("=== Analytics Canvas Diagnosis ===");
        
        AnalyticsCanvasController controller = Object.FindFirstObjectByType<AnalyticsCanvasController>();
        if (controller == null)
        {
            Debug.LogError("[Diagnosis] AnalyticsCanvasController not found!");
            return;
        }
        
        Debug.Log($"[Diagnosis] Controller found: {controller.name}");
        Debug.Log($"[Diagnosis]   Enabled: {controller.enabled}");
        Debug.Log($"[Diagnosis]   EventScrollRect: {(controller.eventScrollRect != null ? "OK" : "NULL")}");
        Debug.Log($"[Diagnosis]   ContentPanel: {(controller.contentPanel != null ? "OK" : "NULL")}");
        Debug.Log($"[Diagnosis]   EventRowPrefab: {(controller.eventRowPrefab != null ? "OK" : "NULL")}");
        
        if (controller.contentPanel != null)
        {
            Debug.Log($"[Diagnosis]   ContentPanel active: {controller.contentPanel.gameObject.activeSelf}");
            Debug.Log($"[Diagnosis]   ContentPanel child count: {controller.contentPanel.childCount}");
        }
        
        if (Application.isPlaying)
        {
            if (AuditLogger.Instance != null)
            {
                var events = AuditLogger.Instance.GetRecentEvents();
                Debug.Log($"[Diagnosis]   Recent events in buffer: {events.Count}");
            }
        }
        
        Debug.Log("=== End Diagnosis ===");
    }
}

