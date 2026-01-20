using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using System.IO;

public class AnalyticsCanvasSetupTool : EditorWindow
{
    [MenuItem("Tools/Analytics Canvas Setup Tool")]
    public static void ShowWindow()
    {
        GetWindow<AnalyticsCanvasSetupTool>("Analytics Canvas Setup");
    }

    void OnGUI()
    {
        GUILayout.Label("Analytics Canvas Setup", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("DELETE Old Canvas", GUILayout.Height(30)))
        {
            DeleteOld();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("CREATE New Analytics Canvas", GUILayout.Height(50)))
        {
            CreateCanvas();
        }
    }

    void DeleteOld()
    {
        // Delete from scene
        GameObject old = GameObject.Find("AnalyticsCanvas");
        if (old != null) DestroyImmediate(old);
        
        old = GameObject.Find("analyticsCanvas");
        if (old != null) DestroyImmediate(old);

        // Delete prefab
        if (File.Exists("Assets/Prefabs/EventRow.prefab"))
        {
            AssetDatabase.DeleteAsset("Assets/Prefabs/EventRow.prefab");
        }

        AssetDatabase.Refresh();
        Debug.Log("Deleted old canvas and prefab");
    }

    void CreateCanvas()
    {
        DeleteOld();

        // === CREATE MAIN CANVAS ===
        GameObject canvasGO = new GameObject("AnalyticsCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 50;
        
        canvasGO.AddComponent<GraphicRaycaster>();

        // Position and size
        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(500, 400);
        canvasRect.localScale = new Vector3(0.001f, 0.001f, 0.001f);
        canvasRect.position = new Vector3(0, 1.5f, 2f);

        // === BACKGROUND ===
        GameObject bg = new GameObject("BG");
        bg.transform.SetParent(canvasGO.transform, false);
        RectTransform bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.05f, 0.08f, 0.12f, 0.98f);

        // === TITLE ===
        GameObject title = new GameObject("Title");
        title.transform.SetParent(canvasGO.transform, false);
        RectTransform titleRect = title.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 32);
        titleRect.anchoredPosition = Vector2.zero;
        Image titleBg = title.AddComponent<Image>();
        titleBg.color = new Color(0.1f, 0.15f, 0.2f, 1f);
        
        GameObject titleTextGO = new GameObject("TitleText");
        titleTextGO.transform.SetParent(title.transform, false);
        RectTransform ttRect = titleTextGO.AddComponent<RectTransform>();
        ttRect.anchorMin = Vector2.zero;
        ttRect.anchorMax = Vector2.one;
        ttRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI titleText = titleTextGO.AddComponent<TextMeshProUGUI>();
        titleText.text = "AUDIT LOG";
        titleText.fontSize = 22;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(0.4f, 0.8f, 1f);
        titleText.alignment = TextAlignmentOptions.Center;

        // === SCROLL VIEW ===
        GameObject scrollView = new GameObject("ScrollView");
        scrollView.transform.SetParent(canvasGO.transform, false);
        RectTransform scrollRect = scrollView.AddComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(6, 28);
        scrollRect.offsetMax = new Vector2(-6, -34);
        Image scrollBg = scrollView.AddComponent<Image>();
        scrollBg.color = new Color(0.02f, 0.04f, 0.06f, 1f);
        ScrollRect scroll = scrollView.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20;

        // Viewport
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollView.transform, false);
        RectTransform vpRect = viewport.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.sizeDelta = Vector2.zero;
        vpRect.anchoredPosition = Vector2.zero;
        viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        scroll.viewport = vpRect;

        // Content
        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);
        contentRect.anchoredPosition = Vector2.zero;
        
        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4;
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        
        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        scroll.content = contentRect;

        // === STATUS BAR ===
        GameObject status = new GameObject("StatusBar");
        status.transform.SetParent(canvasGO.transform, false);
        RectTransform statusRect = status.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0, 0);
        statusRect.anchorMax = new Vector2(1, 0);
        statusRect.pivot = new Vector2(0.5f, 0);
        statusRect.sizeDelta = new Vector2(0, 30);
        statusRect.anchoredPosition = Vector2.zero;
        Image statusBg = status.AddComponent<Image>();
        statusBg.color = new Color(0.1f, 0.12f, 0.15f, 1f);

        GameObject statusTextGO = new GameObject("Text");
        statusTextGO.transform.SetParent(status.transform, false);
        RectTransform stRect = statusTextGO.AddComponent<RectTransform>();
        stRect.anchorMin = Vector2.zero;
        stRect.anchorMax = Vector2.one;
        stRect.sizeDelta = Vector2.zero;
        stRect.offsetMin = new Vector2(15, 0);
        TextMeshProUGUI statusText = statusTextGO.AddComponent<TextMeshProUGUI>();
        statusText.text = "Events: 0";
        statusText.fontSize = 18;
        statusText.color = Color.white;
        statusText.alignment = TextAlignmentOptions.Left;

        // === ADD CONTROLLER TO CANVAS ===
        AnalyticsCanvasController controller = canvasGO.AddComponent<AnalyticsCanvasController>();
        controller.eventScrollRect = scroll;
        controller.contentPanel = contentRect;
        controller.eventCountText = statusText;
        controller.eventRowPrefab = null; // Not needed - we create rows directly now

        // Mark dirty and save
        EditorUtility.SetDirty(canvasGO);
        
        Selection.activeGameObject = canvasGO;
        EditorGUIUtility.PingObject(canvasGO);

        Debug.Log("=== ANALYTICS CANVAS CREATED ===");
        Debug.Log("Position: (0, 1.5, 2)");
        Debug.Log("Size: 50cm x 40cm");
        Debug.Log("Enter Play Mode to see events!");

        EditorUtility.DisplayDialog("Success!", 
            "Analytics Canvas created!\n\n" +
            "Enter Play Mode to see test events.\n\n" +
            "Move the canvas in Scene view to position it.", 
            "OK");
    }
}
