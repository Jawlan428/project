using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Diagnostic script to verify the Analytics Canvas setup and display test messages
/// Attach this to the AnalyticsCanvas GameObject temporarily to test
/// </summary>
public class AnalyticsCanvasDiagnostic : MonoBehaviour
{
    void Start()
    {
        Debug.Log("========================================");
        Debug.Log("[DIAGNOSTIC] Analytics Canvas Diagnostic Starting...");
        Debug.Log("========================================");
        
        // Check this GameObject
        Debug.Log($"[DIAGNOSTIC] This GameObject: {gameObject.name}");
        Debug.Log($"[DIAGNOSTIC] GameObject Active: {gameObject.activeInHierarchy}");
        
        // Check for Canvas component
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            Debug.Log($"[DIAGNOSTIC] ✅ Canvas found - Render Mode: {canvas.renderMode}");
        }
        else
        {
            Debug.LogError("[DIAGNOSTIC] ❌ Canvas component missing!");
        }
        
        // Check for AnalyticsCanvasController
        AnalyticsCanvasController controller = GetComponent<AnalyticsCanvasController>();
        if (controller != null)
        {
            Debug.Log("[DIAGNOSTIC] ✅ AnalyticsCanvasController found");
            
            if (controller.contentPanel != null)
            {
                Debug.Log($"[DIAGNOSTIC] ✅ Content Panel assigned: {controller.contentPanel.name}");
                
                // Try to add a test row directly
                CreateTestRow(controller.contentPanel);
            }
            else
            {
                Debug.LogError("[DIAGNOSTIC] ❌ Content Panel not assigned!");
            }
        }
        else
        {
            Debug.LogWarning("[DIAGNOSTIC] ⚠️ AnalyticsCanvasController not found - may have compilation error");
        }
        
        // Check for AuditLogger
        if (AuditLogger.Instance != null)
        {
            Debug.Log("[DIAGNOSTIC] ✅ AuditLogger found");
            int eventCount = AuditLogger.Instance.GetRecentEvents().Count;
            Debug.Log($"[DIAGNOSTIC] AuditLogger has {eventCount} events");
        }
        else
        {
            Debug.LogWarning("[DIAGNOSTIC] ⚠️ AuditLogger not found yet (may initialize later)");
        }
        
        Debug.Log("========================================");
        Debug.Log("[DIAGNOSTIC] Diagnostic Complete - Check messages above");
        Debug.Log("========================================");
    }
    
    /// <summary>
    /// Creates a test row to verify the UI is working
    /// </summary>
    void CreateTestRow(RectTransform contentPanel)
    {
        Debug.Log("[DIAGNOSTIC] Creating test row...");
        
        try
        {
            // Create test row GameObject
            GameObject testRow = new GameObject("DIAGNOSTIC_TEST_ROW");
            testRow.transform.SetParent(contentPanel, false);
            
            // Add RectTransform
            RectTransform rowRect = testRow.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0, 1);
            rowRect.anchorMax = new Vector2(1, 1);
            rowRect.pivot = new Vector2(0, 1);
            rowRect.sizeDelta = new Vector2(0, 30);
            
            // Add background
            Image bg = testRow.AddComponent<Image>();
            bg.color = Color.yellow;
            
            // Add LayoutElement
            LayoutElement le = testRow.AddComponent<LayoutElement>();
            le.minHeight = 30;
            le.preferredHeight = 30;
            
            // Create text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(testRow.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 0);
            textRect.offsetMax = new Vector2(-10, 0);
            
            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = "✅ DIAGNOSTIC TEST ROW - If you see this, the UI is working!";
            text.fontSize = 18;
            text.color = Color.black;
            text.alignment = TextAlignmentOptions.Left;
            text.verticalAlignment = VerticalAlignmentOptions.Middle;
            
            Debug.Log("[DIAGNOSTIC] ✅ Test row created successfully!");
            Debug.Log("[DIAGNOSTIC] If you can see a YELLOW row with black text on the canvas, the UI is working!");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DIAGNOSTIC] ❌ Failed to create test row: {ex.Message}");
        }
    }
}

