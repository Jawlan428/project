using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

/// <summary>
/// Simple Analytics Canvas Controller - displays audit events in real-time
/// </summary>
public class AnalyticsCanvasController : MonoBehaviour
{
    [Header("Required References")]
    public ScrollRect eventScrollRect;
    public RectTransform contentPanel;
    public TextMeshProUGUI eventCountText;
    
    [Header("Optional References")]
    public GameObject eventRowPrefab;
    
    [Header("Settings")]
    public int maxVisibleRows = 100;
    public bool newestOnTop = true;
    
    // Internal state
    private List<GameObject> _rows = new List<GameObject>();
    private ConcurrentQueue<AuditEvent> _pendingEvents = new ConcurrentQueue<AuditEvent>();
    private int _totalEvents = 0;

    void Start()
    {
        Debug.Log("========================================");
        Debug.Log("[AnalyticsCanvas] STARTING");
        Debug.Log("========================================");

        if (contentPanel == null)
        {
            Debug.LogError("[AnalyticsCanvas] Content Panel not assigned!");
            return;
        }

        Debug.Log($"[AnalyticsCanvas] Content Panel found: {contentPanel.name}");
        
        // Clear any existing rows (they may be stacked incorrectly)
        ClearExistingRows();

        // Ensure Content Panel has a VerticalLayoutGroup
        EnsureLayoutComponents();

        // Subscribe to audit events
        AuditLogger.OnAuditEvent += OnEvent;
        Debug.Log("[AnalyticsCanvas] Subscribed to AuditLogger.OnAuditEvent");

        // Load existing events after a short delay
        StartCoroutine(LoadExistingEventsDelayed());
    }
    
    /// <summary>
    /// Clears any existing rows from the content panel
    /// </summary>
    void ClearExistingRows()
    {
        if (contentPanel == null) return;
        
        int childCount = contentPanel.childCount;
        if (childCount > 0)
        {
            Debug.Log($"[AnalyticsCanvas] Clearing {childCount} existing rows...");
            for (int i = childCount - 1; i >= 0; i--)
            {
                Destroy(contentPanel.GetChild(i).gameObject);
            }
            _rows.Clear();
            _totalEvents = 0;
            Debug.Log("[AnalyticsCanvas] Existing rows cleared!");
        }
    }
    
    /// <summary>
    /// Ensures the content panel has the required layout components
    /// </summary>
    void EnsureLayoutComponents()
    {
        if (contentPanel == null) return;
        
        bool needsRebuild = false;
        
        // Check for VerticalLayoutGroup
        VerticalLayoutGroup vlg = contentPanel.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
        {
            Debug.Log("[AnalyticsCanvas] Adding VerticalLayoutGroup to Content Panel...");
            vlg = contentPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            needsRebuild = true;
        }
        
        // Always configure to ensure correct settings
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 2f;
        vlg.padding = new RectOffset(5, 5, 5, 5);
        Debug.Log("[AnalyticsCanvas] VerticalLayoutGroup configured!");
        
        // Check for ContentSizeFitter
        ContentSizeFitter csf = contentPanel.GetComponent<ContentSizeFitter>();
        if (csf == null)
        {
            Debug.Log("[AnalyticsCanvas] Adding ContentSizeFitter to Content Panel...");
            csf = contentPanel.gameObject.AddComponent<ContentSizeFitter>();
            needsRebuild = true;
        }
        
        // Always configure to ensure correct settings
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        Debug.Log("[AnalyticsCanvas] ContentSizeFitter configured!");
        
        // Force layout rebuild if we added components
        if (needsRebuild || contentPanel.childCount > 0)
        {
            Debug.Log("[AnalyticsCanvas] Forcing layout rebuild...");
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentPanel);
            Canvas.ForceUpdateCanvases();
            Debug.Log("[AnalyticsCanvas] Layout rebuild complete!");
        }
    }

    void OnDestroy()
    {
        AuditLogger.OnAuditEvent -= OnEvent;
    }

    IEnumerator LoadExistingEventsDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        
        // First, load events from audit log files
        yield return StartCoroutine(LoadEventsFromAuditFiles());
        
        // Then, load existing events from AuditLogger's in-memory buffer
        if (AuditLogger.Instance != null)
        {
            List<AuditEvent> existingEvents = AuditLogger.Instance.GetRecentEvents();
            Debug.Log($"[AnalyticsCanvas] Loading {existingEvents.Count} existing events from memory...");
            
            foreach (var evt in existingEvents)
            {
                AddEventRow(evt);
                yield return null; // Spread over frames
            }
            
            Debug.Log($"[AnalyticsCanvas] Loaded {existingEvents.Count} events from memory!");
        }
        else
        {
            Debug.LogWarning("[AnalyticsCanvas] AuditLogger.Instance is NULL - cannot load existing events");
        }
    }
    
    IEnumerator LoadEventsFromAuditFiles()
    {
        // Try multiple possible audit log directories
        List<string> possiblePaths = new List<string>();
        
        // Path 1: QuestRecordings/AuditLogs (relative to project)
        string projectPath = System.IO.Path.Combine(Application.dataPath, "..", "QuestRecordings", "AuditLogs");
        possiblePaths.Add(projectPath);
        
        // Path 2: Desktop folder (where AuditLogger saves to)
        try
        {
            string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
            string desktopLogPath = System.IO.Path.Combine(desktopPath, $"{Application.productName}_AuditLogs");
            possiblePaths.Add(desktopLogPath);
        }
        catch { }
        
        // Path 3: PersistentDataPath/AuditLogs (fallback)
        string persistentPath = System.IO.Path.Combine(Application.persistentDataPath, "AuditLogs");
        possiblePaths.Add(persistentPath);
        
        List<AuditEvent> allFileEvents = new List<AuditEvent>();
        
        foreach (string logDir in possiblePaths)
        {
            if (!System.IO.Directory.Exists(logDir))
            {
                Debug.Log($"[AnalyticsCanvas] Audit log directory not found: {logDir}");
                continue;
            }
            
            Debug.Log($"[AnalyticsCanvas] Scanning audit log directory: {logDir}");
            
            string[] jsonFiles = System.IO.Directory.GetFiles(logDir, "audit_*.json");
            Debug.Log($"[AnalyticsCanvas] Found {jsonFiles.Length} audit log files");
            
            foreach (string filePath in jsonFiles)
            {
                try
                {
                    string jsonContent = System.IO.File.ReadAllText(filePath);
                    AuditEventList eventList = JsonUtility.FromJson<AuditEventList>(jsonContent);
                    
                    if (eventList != null && eventList.events != null)
                    {
                        Debug.Log($"[AnalyticsCanvas] Loaded {eventList.events.Count} events from {System.IO.Path.GetFileName(filePath)}");
                        allFileEvents.AddRange(eventList.events);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[AnalyticsCanvas] Failed to load audit file {filePath}: {ex.Message}");
                }
                
                yield return null; // Spread file loading over frames
            }
            
            // If we found files in this directory, don't check other directories
            if (jsonFiles.Length > 0)
                break;
        }
        
        // Sort events by timestamp (oldest first)
        allFileEvents.Sort((a, b) => string.Compare(a.timestamp, b.timestamp));
        
        Debug.Log($"[AnalyticsCanvas] Total events loaded from files: {allFileEvents.Count}");
        
        // Display all loaded events
        foreach (var evt in allFileEvents)
        {
            AddEventRow(evt);
            yield return null; // Spread over frames
        }
    }
    
    /// <summary>
    /// Wrapper class for JSON serialization of event list.
    /// </summary>
    [System.Serializable]
    private class AuditEventList
    {
        public List<AuditEvent> events;
    }

    void OnEvent(AuditEvent evt)
    {
        Debug.Log($"[AnalyticsCanvas] Event received: {evt.eventType}");
        _pendingEvents.Enqueue(evt);
    }

    void Update()
    {
        // Process any pending events
        while (_pendingEvents.TryDequeue(out AuditEvent evt))
        {
            AddEventRow(evt);
        }
    }

    void AddEventRow(AuditEvent evt)
    {
        if (contentPanel == null)
        {
            Debug.LogError("[AnalyticsCanvas] Cannot add row - contentPanel is NULL!");
            return;
        }

        _totalEvents++;
        
        // Format the display text
        string timeStr = evt.GetFormattedTime();
        string typeStr = evt.eventType ?? "UNKNOWN";
        string playerStr = evt.playerName ?? "Unknown";
        string targetStr = string.IsNullOrEmpty(evt.targetId) ? "-" : evt.targetId;
        string zoneStr = string.IsNullOrEmpty(evt.zoneName) ? "" : evt.zoneName;
        
        // Build single line text
        string rowText = $"<color=#7BC8FF>{timeStr}</color>  <color=#FFD700>{typeStr}</color>  <color=#FFFFFF>{playerStr}</color>";
        if (!string.IsNullOrEmpty(targetStr) && targetStr != "-")
            rowText += $"  <color=#AAAAAA>→ {targetStr}</color>";
        if (!string.IsNullOrEmpty(zoneStr))
            rowText += $"  <color=#88FF88>[{zoneStr}]</color>";

        Debug.Log($"[AnalyticsCanvas] Adding row #{_totalEvents}: {rowText}");

        // Create row GameObject
        GameObject rowGO = new GameObject($"Row_{_totalEvents}");
        rowGO.transform.SetParent(contentPanel, false);
        
        // Add RectTransform with fixed height
        RectTransform rowRect = rowGO.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0, 1);
        rowRect.anchorMax = new Vector2(1, 1);
        rowRect.pivot = new Vector2(0, 1);
        rowRect.sizeDelta = new Vector2(0, 24);
        
        // Add LayoutElement to enforce height
        LayoutElement rowLE = rowGO.AddComponent<LayoutElement>();
        rowLE.minHeight = 24;
        rowLE.preferredHeight = 24;
        rowLE.flexibleWidth = 1;
        
        // Row background
        Image rowBg = rowGO.AddComponent<Image>();
        rowBg.color = (_totalEvents % 2 == 0) 
            ? new Color(0.12f, 0.14f, 0.18f, 1f) 
            : new Color(0.08f, 0.1f, 0.14f, 1f);

        // Create single TextMeshPro for the whole row
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(rowGO.transform, false);
        
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8, 0);
        textRect.offsetMax = new Vector2(-8, 0);
        
        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = rowText;
        tmp.fontSize = 16; // Increased from 14 for better visibility
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle; // Center vertically in row
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.enableWordWrapping = false;
        tmp.richText = true;
        tmp.raycastTarget = false; // Improve performance

        // Add to list
        if (newestOnTop)
        {
            _rows.Insert(0, rowGO);
            rowGO.transform.SetAsFirstSibling();
        }
        else
        {
            _rows.Add(rowGO);
            rowGO.transform.SetAsLastSibling();
        }

        // Remove old rows if over limit
        while (_rows.Count > maxVisibleRows)
        {
            int idx = newestOnTop ? _rows.Count - 1 : 0;
            Destroy(_rows[idx]);
            _rows.RemoveAt(idx);
        }

        // Update counter
        if (eventCountText != null)
        {
            eventCountText.text = $"Events: {_totalEvents}";
        }

        // Force layout rebuild
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentPanel);
        Canvas.ForceUpdateCanvases();

        // Autoscroll
        if (eventScrollRect != null)
        {
            eventScrollRect.verticalNormalizedPosition = newestOnTop ? 1f : 0f;
        }
    }
}
