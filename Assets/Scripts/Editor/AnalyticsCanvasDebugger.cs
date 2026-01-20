using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor tool to debug Analytics Canvas setup
/// </summary>
public class AnalyticsCanvasDebugger : EditorWindow
{
    [MenuItem("Tools/Analytics Canvas Debugger")]
    public static void ShowWindow()
    {
        GetWindow<AnalyticsCanvasDebugger>("Analytics Canvas Debugger");
    }

    void OnGUI()
    {
        GUILayout.Label("Analytics Canvas Debug Info", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("Check Setup"))
        {
            CheckSetup();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Log Test Event"))
        {
            if (Application.isPlaying)
            {
                if (AuditLogger.Instance != null)
                {
                    AuditLogger.Instance.Log(AuditEventType.JOIN_MEETING);
                    Debug.Log("[Debugger] Logged test event: JOIN_MEETING");
                }
                else
                {
                    Debug.LogError("[Debugger] AuditLogger.Instance is null!");
                }
            }
            else
            {
                Debug.LogWarning("[Debugger] Must be in Play Mode to log events!");
            }
        }

        GUILayout.Space(10);

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Play Mode Active", MessageType.Info);
            
            if (AuditLogger.Instance != null)
            {
                EditorGUILayout.LabelField("AuditLogger", "Found");
                var events = AuditLogger.Instance.GetRecentEvents();
                EditorGUILayout.LabelField("Events in buffer", events.Count.ToString());
            }
            else
            {
                EditorGUILayout.LabelField("AuditLogger", "NOT FOUND");
            }

            AnalyticsCanvasController controller = Object.FindObjectOfType<AnalyticsCanvasController>();
            if (controller != null)
            {
                EditorGUILayout.LabelField("AnalyticsCanvasController", "Found");
                EditorGUILayout.LabelField("Enabled", controller.enabled.ToString());
                EditorGUILayout.LabelField("Event ScrollRect", controller.eventScrollRect != null ? "Assigned" : "NULL");
                EditorGUILayout.LabelField("Content Panel", controller.contentPanel != null ? "Assigned" : "NULL");
                EditorGUILayout.LabelField("Event Row Prefab", controller.eventRowPrefab != null ? "Assigned" : "NULL");
            }
            else
            {
                EditorGUILayout.LabelField("AnalyticsCanvasController", "NOT FOUND");
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Enter Play Mode to see debug info", MessageType.Warning);
        }
    }

    private void CheckSetup()
    {
        Debug.Log("=== Analytics Canvas Setup Check ===");

        // Check Canvas
        GameObject canvas = GameObject.Find("analyticsCanvas");
        if (canvas == null)
        {
            Debug.LogError("[Debugger] analyticsCanvas GameObject not found in scene!");
        }
        else
        {
            Debug.Log($"[Debugger] analyticsCanvas found: {canvas.name}");
        }

        // Check Controller
        AnalyticsCanvasController controller = Object.FindObjectOfType<AnalyticsCanvasController>();
        if (controller == null)
        {
            Debug.LogError("[Debugger] AnalyticsCanvasController not found in scene!");
        }
        else
        {
            Debug.Log($"[Debugger] AnalyticsCanvasController found: {controller.name}");
            Debug.Log($"[Debugger]   Enabled: {controller.enabled}");
            Debug.Log($"[Debugger]   EventScrollRect: {(controller.eventScrollRect != null ? "ASSIGNED" : "NULL")}");
            Debug.Log($"[Debugger]   ContentPanel: {(controller.contentPanel != null ? "ASSIGNED" : "NULL")}");
            Debug.Log($"[Debugger]   EventRowPrefab: {(controller.eventRowPrefab != null ? "ASSIGNED" : "NULL")}");

            // Check UI structure
            if (controller.contentPanel != null)
            {
                int childCount = controller.contentPanel.childCount;
                Debug.Log($"[Debugger]   ContentPanel child count: {childCount}");
            }
        }

        // Check Prefab
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/EventRow.prefab");
        if (prefab == null)
        {
            Debug.LogError("[Debugger] EventRow prefab not found at Assets/Prefabs/EventRow.prefab!");
        }
        else
        {
            Debug.Log($"[Debugger] EventRow prefab found");
        }

        Debug.Log("=== End Setup Check ===");
    }
}

