using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor utility to fix common issues with the Analytics Canvas setup
/// </summary>
public class AnalyticsCanvasFix : EditorWindow
{
    [MenuItem("Tools/Analytics Canvas/Fix Content Panel Layout")]
    public static void FixContentPanelLayout()
    {
        // Find the AnalyticsCanvas
        AnalyticsCanvasController controller = FindFirstObjectByType<AnalyticsCanvasController>();
        
        if (controller == null)
        {
            EditorUtility.DisplayDialog("Not Found", 
                "AnalyticsCanvasController not found in scene. Please open the SampleScene and ensure the AnalyticsCanvas GameObject exists.", 
                "OK");
            return;
        }
        
        if (controller.contentPanel == null)
        {
            EditorUtility.DisplayDialog("Missing Reference", 
                "Content Panel reference is not assigned on AnalyticsCanvasController. Please assign it in the Inspector.", 
                "OK");
            return;
        }
        
        RectTransform contentPanel = controller.contentPanel;
        bool madeChanges = false;
        
        // Add VerticalLayoutGroup if missing
        VerticalLayoutGroup vlg = contentPanel.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
        {
            vlg = contentPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            Debug.Log("[AnalyticsCanvasFix] Added VerticalLayoutGroup to Content Panel");
            madeChanges = true;
        }
        
        // Configure VerticalLayoutGroup
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 2f;
        vlg.padding = new RectOffset(5, 5, 5, 5);
        Debug.Log("[AnalyticsCanvasFix] Configured VerticalLayoutGroup");
        madeChanges = true;
        
        // Add ContentSizeFitter if missing
        ContentSizeFitter csf = contentPanel.GetComponent<ContentSizeFitter>();
        if (csf == null)
        {
            csf = contentPanel.gameObject.AddComponent<ContentSizeFitter>();
            Debug.Log("[AnalyticsCanvasFix] Added ContentSizeFitter to Content Panel");
            madeChanges = true;
        }
        
        // Configure ContentSizeFitter
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        Debug.Log("[AnalyticsCanvasFix] Configured ContentSizeFitter");
        
        // Mark scene as dirty
        if (madeChanges)
        {
            EditorUtility.SetDirty(contentPanel.gameObject);
            EditorUtility.DisplayDialog("Fixed!", 
                "Content Panel layout components have been added and configured!\n\n" +
                "Changes made:\n" +
                "✅ Added/configured VerticalLayoutGroup\n" +
                "✅ Added/configured ContentSizeFitter\n\n" +
                "Now press Play to see the events!", 
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Already Fixed", 
                "Content Panel already has all required components configured.", 
                "OK");
        }
    }
    
    [MenuItem("Tools/Analytics Canvas/Clear All Event Rows")]
    public static void ClearAllEventRows()
    {
        AnalyticsCanvasController controller = FindFirstObjectByType<AnalyticsCanvasController>();
        
        if (controller == null || controller.contentPanel == null)
        {
            EditorUtility.DisplayDialog("Not Found", 
                "AnalyticsCanvasController or Content Panel not found in scene.", 
                "OK");
            return;
        }
        
        if (!EditorUtility.DisplayDialog("Clear All Rows?", 
            "This will delete all event rows from the Content Panel. Continue?", 
            "Yes", "Cancel"))
        {
            return;
        }
        
        RectTransform contentPanel = controller.contentPanel;
        int childCount = contentPanel.childCount;
        
        for (int i = childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(contentPanel.GetChild(i).gameObject);
        }
        
        Debug.Log($"[AnalyticsCanvasFix] Cleared {childCount} event rows from Content Panel");
        EditorUtility.DisplayDialog("Cleared", 
            $"Cleared {childCount} event rows from Content Panel.", 
            "OK");
    }
    
    [MenuItem("Tools/Analytics Canvas/Verify Setup")]
    public static void VerifySetup()
    {
        AnalyticsCanvasController controller = FindFirstObjectByType<AnalyticsCanvasController>();
        
        System.Text.StringBuilder report = new System.Text.StringBuilder();
        report.AppendLine("Analytics Canvas Setup Report:");
        report.AppendLine("=====================================\n");
        
        // Check controller
        if (controller == null)
        {
            report.AppendLine("❌ AnalyticsCanvasController not found in scene!");
            EditorUtility.DisplayDialog("Setup Incomplete", report.ToString(), "OK");
            return;
        }
        report.AppendLine("✅ AnalyticsCanvasController found");
        
        // Check content panel
        if (controller.contentPanel == null)
        {
            report.AppendLine("❌ Content Panel reference not assigned!");
        }
        else
        {
            report.AppendLine("✅ Content Panel assigned: " + controller.contentPanel.name);
            
            // Check layout components
            VerticalLayoutGroup vlg = controller.contentPanel.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
            {
                report.AppendLine("❌ VerticalLayoutGroup missing on Content Panel");
            }
            else
            {
                report.AppendLine("✅ VerticalLayoutGroup present");
            }
            
            ContentSizeFitter csf = controller.contentPanel.GetComponent<ContentSizeFitter>();
            if (csf == null)
            {
                report.AppendLine("❌ ContentSizeFitter missing on Content Panel");
            }
            else
            {
                report.AppendLine("✅ ContentSizeFitter present");
            }
        }
        
        // Check scroll rect
        if (controller.eventScrollRect == null)
        {
            report.AppendLine("⚠️ Event Scroll Rect not assigned (optional)");
        }
        else
        {
            report.AppendLine("✅ Event Scroll Rect assigned");
        }
        
        // Check event count text
        if (controller.eventCountText == null)
        {
            report.AppendLine("⚠️ Event Count Text not assigned (optional)");
        }
        else
        {
            report.AppendLine("✅ Event Count Text assigned");
        }
        
        // Check AuditLogger
        if (FindFirstObjectByType<AuditBootstrap>() == null)
        {
            report.AppendLine("⚠️ AuditBootstrap not found (will auto-create at runtime)");
        }
        else
        {
            report.AppendLine("✅ AuditBootstrap found in scene");
        }
        
        report.AppendLine("\n=====================================");
        report.AppendLine("\nRecommendation:");
        
        bool hasIssues = report.ToString().Contains("❌");
        
        if (hasIssues)
        {
            report.AppendLine("⚠️ There are issues to fix.");
            report.AppendLine("\nRun: Tools > Analytics Canvas > Fix Content Panel Layout");
        }
        else
        {
            report.AppendLine("✅ Setup looks good! Press Play to test.");
        }
        
        Debug.Log(report.ToString());
        EditorUtility.DisplayDialog("Setup Verification", report.ToString(), "OK");
    }
}

