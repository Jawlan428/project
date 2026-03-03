using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Editor tool to create a properly configured Audit Log Canvas from scratch
/// </summary>
public class CreateAuditLogCanvas : EditorWindow
{
    [MenuItem("Tools/Analytics Canvas/Create New Audit Log Canvas")]
    public static void CreateCanvas()
    {
        Debug.Log("Creating new Audit Log Canvas...");
        
        // Create main canvas
        GameObject canvasGO = new GameObject("AuditLogCanvas");
        
        // Add Canvas component
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        
        // Add CanvasScaler
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100;
        
        // Add GraphicRaycaster
        canvasGO.AddComponent<GraphicRaycaster>();
        
        // Set RectTransform
        RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(800, 600);
        canvasRT.localScale = new Vector3(0.001f, 0.001f, 0.001f);
        canvasRT.localPosition = new Vector3(0, 1.5f, 2f);
        
        // Create background panel
        GameObject bgPanel = CreatePanel(canvasGO.transform, "Background", new Color(0.1f, 0.1f, 0.15f, 0.95f));
        RectTransform bgRT = bgPanel.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        
        // Create header
        GameObject header = CreatePanel(bgPanel.transform, "Header", new Color(0.15f, 0.2f, 0.3f, 1f));
        RectTransform headerRT = header.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0, 1);
        headerRT.anchorMax = new Vector2(1, 1);
        headerRT.pivot = new Vector2(0.5f, 1);
        headerRT.anchoredPosition = Vector2.zero;
        headerRT.sizeDelta = new Vector2(0, 50);
        
        // Add header text
        GameObject headerTextGO = new GameObject("HeaderText");
        headerTextGO.transform.SetParent(header.transform, false);
        RectTransform headerTextRT = headerTextGO.AddComponent<RectTransform>();
        headerTextRT.anchorMin = Vector2.zero;
        headerTextRT.anchorMax = Vector2.one;
        headerTextRT.offsetMin = new Vector2(20, 0);
        headerTextRT.offsetMax = new Vector2(-20, 0);
        
        TextMeshProUGUI headerText = headerTextGO.AddComponent<TextMeshProUGUI>();
        headerText.text = "AUDIT LOG";
        headerText.fontSize = 28;
        headerText.color = new Color(0.4f, 0.8f, 1f, 1f);
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.fontStyle = FontStyles.Bold;
        
        // Create scroll view area
        GameObject scrollArea = new GameObject("ScrollView");
        scrollArea.transform.SetParent(bgPanel.transform, false);
        
        RectTransform scrollAreaRT = scrollArea.AddComponent<RectTransform>();
        scrollAreaRT.anchorMin = new Vector2(0, 0);
        scrollAreaRT.anchorMax = new Vector2(1, 1);
        scrollAreaRT.offsetMin = new Vector2(10, 40);
        scrollAreaRT.offsetMax = new Vector2(-10, -60);
        
        // Add ScrollRect
        ScrollRect scrollRect = scrollArea.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        
        // Add Mask
        Image scrollBg = scrollArea.AddComponent<Image>();
        scrollBg.color = new Color(0.05f, 0.05f, 0.1f, 1f);
        Mask mask = scrollArea.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        
        // Create Viewport
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollArea.transform, false);
        
        RectTransform viewportRT = viewport.AddComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = Vector2.zero;
        viewportRT.offsetMax = Vector2.zero;
        viewportRT.pivot = new Vector2(0, 1);
        
        // Create Content
        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        
        RectTransform contentRT = content.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0, 1);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(0, 0);
        
        // Add ContentSizeFitter
        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        
        // Create the main log text
        GameObject logTextGO = new GameObject("LogText");
        logTextGO.transform.SetParent(content.transform, false);
        
        RectTransform logTextRT = logTextGO.AddComponent<RectTransform>();
        logTextRT.anchorMin = new Vector2(0, 1);
        logTextRT.anchorMax = new Vector2(1, 1);
        logTextRT.pivot = new Vector2(0, 1);
        logTextRT.anchoredPosition = Vector2.zero;
        logTextRT.sizeDelta = new Vector2(0, 0);
        
        TextMeshProUGUI logText = logTextGO.AddComponent<TextMeshProUGUI>();
        logText.text = "<color=#00FF00>AUDIT LOG READY</color>\n<color=#FFFF00>[*] Events will appear here...</color>\n";
        logText.fontSize = 14;
        logText.color = Color.white;
        logText.alignment = TextAlignmentOptions.TopLeft;
        logText.overflowMode = TextOverflowModes.Overflow;
        logText.textWrappingMode = TextWrappingModes.Normal;
        logText.richText = true;
        logText.raycastTarget = false;
        
        // Add LayoutElement to logText
        LayoutElement logLE = logTextGO.AddComponent<LayoutElement>();
        logLE.flexibleWidth = 1;
        logLE.minHeight = 50;
        
        // Set scroll rect references
        scrollRect.content = contentRT;
        scrollRect.viewport = viewportRT;
        
        // Create footer with event count
        GameObject footer = CreatePanel(bgPanel.transform, "Footer", new Color(0.1f, 0.12f, 0.18f, 1f));
        RectTransform footerRT = footer.GetComponent<RectTransform>();
        footerRT.anchorMin = new Vector2(0, 0);
        footerRT.anchorMax = new Vector2(1, 0);
        footerRT.pivot = new Vector2(0.5f, 0);
        footerRT.anchoredPosition = Vector2.zero;
        footerRT.sizeDelta = new Vector2(0, 30);
        
        // Add event count text
        GameObject countTextGO = new GameObject("EventCountText");
        countTextGO.transform.SetParent(footer.transform, false);
        RectTransform countTextRT = countTextGO.AddComponent<RectTransform>();
        countTextRT.anchorMin = Vector2.zero;
        countTextRT.anchorMax = Vector2.one;
        countTextRT.offsetMin = new Vector2(10, 0);
        countTextRT.offsetMax = new Vector2(-10, 0);
        
        TextMeshProUGUI countText = countTextGO.AddComponent<TextMeshProUGUI>();
        countText.text = "Events: 0";
        countText.fontSize = 16;
        countText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
        countText.alignment = TextAlignmentOptions.Left;
        
        // Add SimpleAuditLogDisplay component
        SimpleAuditLogDisplay display = canvasGO.AddComponent<SimpleAuditLogDisplay>();
        display.logText = logText;
        display.countText = countText;
        display.scrollRect = scrollRect;
        display.maxEvents = 100;
        display.newestFirst = true;
        display.fontSize = 14;
        
        // Select the new canvas
        Selection.activeGameObject = canvasGO;
        
        Debug.Log("✅ Audit Log Canvas created successfully!");
        Debug.Log("Position it in your scene and press Play to see events.");
        
        EditorUtility.DisplayDialog("Success!", 
            "Audit Log Canvas created!\n\n" +
            "1. Position it in your scene where you want it\n" +
            "2. Press Play to see audit events\n\n" +
            "The canvas is set to World Space mode at:\n" +
            "Position: (0, 1.5, 2)\n" +
            "Scale: (0.001, 0.001, 0.001)", 
            "OK");
    }
    
    static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        
        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        
        Image img = panel.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = true;
        
        return panel;
    }
    
    [MenuItem("Tools/Analytics Canvas/Replace Old Canvas With New")]
    public static void ReplaceOldCanvas()
    {
        // Find old AnalyticsCanvas
        GameObject oldCanvas = GameObject.Find("AnalyticsCanvas");
        Vector3 oldPosition = Vector3.zero;
        Vector3 oldRotation = Vector3.zero;
        Vector3 oldScale = new Vector3(0.001f, 0.001f, 0.001f);
        
        if (oldCanvas != null)
        {
            // Save position
            oldPosition = oldCanvas.transform.position;
            oldRotation = oldCanvas.transform.eulerAngles;
            oldScale = oldCanvas.transform.localScale;
            
            // Delete old canvas
            DestroyImmediate(oldCanvas);
            Debug.Log("Removed old AnalyticsCanvas");
        }
        
        // Create new canvas
        CreateCanvas();
        
        // Apply old position if we had one
        GameObject newCanvas = GameObject.Find("AuditLogCanvas");
        if (newCanvas != null && oldCanvas != null)
        {
            newCanvas.transform.position = oldPosition;
            newCanvas.transform.eulerAngles = oldRotation;
            newCanvas.transform.localScale = oldScale;
            Debug.Log("Applied old canvas position to new canvas");
        }
    }
}

