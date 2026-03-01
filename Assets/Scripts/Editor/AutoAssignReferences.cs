using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// Quick tool to auto-assign all references for AnalyticsCanvasController
/// </summary>
public class AutoAssignReferences
{
    [MenuItem("Tools/Analytics Canvas/Auto-Assign References")]
    public static void AssignReferences()
    {
        AnalyticsCanvasController controller = Object.FindFirstObjectByType<AnalyticsCanvasController>();
        if (controller == null)
        {
            EditorUtility.DisplayDialog("Error", "AnalyticsCanvasController not found in scene!", "OK");
            return;
        }

        int assignedCount = 0;

        // Find EventScrollRect
        if (controller.eventScrollRect == null)
        {
            ScrollRect scrollRect = Object.FindFirstObjectByType<ScrollRect>();
            if (scrollRect != null)
            {
                controller.eventScrollRect = scrollRect;
                assignedCount++;
                Debug.Log("[AutoAssign] Assigned Event Scroll Rect");
            }
        }

        // Find ContentPanel (look for Content under Viewport)
        if (controller.contentPanel == null)
        {
            Transform content = FindChildRecursive(controller.transform, "Content");
            if (content != null && content.GetComponent<RectTransform>() != null)
            {
                controller.contentPanel = content.GetComponent<RectTransform>();
                assignedCount++;
                Debug.Log("[AutoAssign] Assigned Content Panel");
            }
        }

        // Find EventRowPrefab
        if (controller.eventRowPrefab == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/EventRow.prefab");
            if (prefab != null)
            {
                controller.eventRowPrefab = prefab;
                assignedCount++;
                Debug.Log("[AutoAssign] Assigned Event Row Prefab");
            }
        }

        // Find EventCountText
        if (controller.eventCountText == null)
        {
            TextMeshProUGUI[] texts = Object.FindObjectsOfType<TextMeshProUGUI>();
            foreach (var text in texts)
            {
                if (text.name.Contains("EventCount") || text.text.Contains("Events:"))
                {
                    controller.eventCountText = text;
                    assignedCount++;
                    Debug.Log("[AutoAssign] Assigned Event Count Text");
                    break;
                }
            }
        }

        EditorUtility.SetDirty(controller);
        
        if (assignedCount > 0)
        {
            EditorUtility.DisplayDialog("Success", 
                $"Assigned {assignedCount} reference(s)!\n\nCheck the Console for details.", 
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Info", 
                "No unassigned references found.\n\nYou may need to assign them manually in the Inspector.", 
                "OK");
        }
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;
            
            Transform found = FindChildRecursive(child, name);
            if (found != null)
                return found;
        }
        return null;
    }
}

