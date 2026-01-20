using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// SIMPLE Audit Log Display - Uses a single Text component to show all events
/// This is the most reliable approach for displaying audit events
/// </summary>
public class SimpleAuditLogDisplay : MonoBehaviour
{
    [Header("Main Text Display")]
    [Tooltip("The TextMeshProUGUI component that will display all events")]
    public TextMeshProUGUI logText;
    
    [Header("Event Counter")]
    [Tooltip("Optional: Text to show event count")]
    public TextMeshProUGUI countText;
    
    [Header("Scroll View")]
    [Tooltip("Optional: ScrollRect for scrolling")]
    public ScrollRect scrollRect;
    
    [Header("Settings")]
    public int maxEvents = 100;
    public bool newestFirst = true;
    public float fontSize = 14f;
    
    private List<string> _eventLines = new List<string>();
    private int _totalCount = 0;
    private bool _needsUpdate = false;

    void Awake()
    {
        Debug.Log("========================================");
        Debug.Log("[SimpleAuditLog] AWAKE - Initializing...");
        Debug.Log("========================================");
    }

    void Start()
    {
        Debug.Log("[SimpleAuditLog] START - Setting up...");
        
        // Auto-find logText if not assigned
        if (logText == null)
        {
            logText = GetComponentInChildren<TextMeshProUGUI>();
            if (logText != null)
            {
                Debug.Log($"[SimpleAuditLog] Auto-found logText: {logText.name}");
            }
        }
        
        if (logText == null)
        {
            Debug.LogError("[SimpleAuditLog] ERROR: No TextMeshProUGUI found! Creating one...");
            CreateLogText();
        }
        
        // Configure the text component
        if (logText != null)
        {
            ConfigureLogText();
        }
        
        // Subscribe to audit events
        AuditLogger.OnAuditEvent += OnAuditEvent;
        Debug.Log("[SimpleAuditLog] Subscribed to AuditLogger.OnAuditEvent");
        
        // Load existing events
        StartCoroutine(LoadExistingEvents());
        
        // Add a test message immediately
        AddTestMessage();
    }
    
    void CreateLogText()
    {
        Debug.Log("[SimpleAuditLog] Creating log text component...");
        
        // Create a child GameObject for the text
        GameObject textObj = new GameObject("AuditLogText");
        textObj.transform.SetParent(transform, false);
        
        // Add RectTransform
        RectTransform rt = textObj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(10, 10);
        rt.offsetMax = new Vector2(-10, -10);
        rt.pivot = new Vector2(0, 1);
        
        // Add TextMeshProUGUI
        logText = textObj.AddComponent<TextMeshProUGUI>();
        Debug.Log("[SimpleAuditLog] Created log text component!");
    }
    
    void ConfigureLogText()
    {
        logText.fontSize = fontSize;
        logText.color = Color.white;
        logText.alignment = TextAlignmentOptions.TopLeft;
        logText.overflowMode = TextOverflowModes.Overflow;
        logText.enableWordWrapping = true;
        logText.richText = true;
        logText.raycastTarget = false;
        
        // Set initial text so we know it's working
        logText.text = "<color=#00FF00>AUDIT LOG INITIALIZED</color>\nWaiting for events...\n";
        Debug.Log("[SimpleAuditLog] Configured log text component!");
    }
    
    void AddTestMessage()
    {
        // Add a visible test message
        string testLine = "<color=#FFFF00>▶ AUDIT LOG READY - Events will appear below</color>";
        _eventLines.Add(testLine);
        _needsUpdate = true;
        Debug.Log("[SimpleAuditLog] Added test message");
    }

    void OnDestroy()
    {
        AuditLogger.OnAuditEvent -= OnAuditEvent;
    }
    
    IEnumerator LoadExistingEvents()
    {
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log("[SimpleAuditLog] Loading existing events...");
        
        if (AuditLogger.Instance != null)
        {
            List<AuditEvent> events = AuditLogger.Instance.GetRecentEvents();
            Debug.Log($"[SimpleAuditLog] Found {events.Count} existing events");
            
            foreach (var evt in events)
            {
                AddEventLine(evt);
                yield return null;
            }
            
            _needsUpdate = true;
        }
        else
        {
            Debug.LogWarning("[SimpleAuditLog] AuditLogger.Instance is null");
        }
    }

    void OnAuditEvent(AuditEvent evt)
    {
        Debug.Log($"[SimpleAuditLog] Received event: {evt.eventType}");
        AddEventLine(evt);
        _needsUpdate = true;
    }
    
    void AddEventLine(AuditEvent evt)
    {
        _totalCount++;
        
        // Format the line with colors
        string time = evt.GetFormattedTime();
        string type = evt.eventType ?? "UNKNOWN";
        string player = evt.playerName ?? "Unknown";
        string target = string.IsNullOrEmpty(evt.targetId) ? "" : $" → {evt.targetId}";
        string zone = string.IsNullOrEmpty(evt.zoneName) ? "" : $" [{evt.zoneName}]";
        
        // Color-coded line
        string line = $"<color=#7BC8FF>{time}</color> <color=#FFD700>{type}</color> <color=#FFFFFF>{player}</color><color=#AAAAAA>{target}</color><color=#88FF88>{zone}</color>";
        
        if (newestFirst)
        {
            _eventLines.Insert(0, line);
        }
        else
        {
            _eventLines.Add(line);
        }
        
        // Remove excess
        while (_eventLines.Count > maxEvents)
        {
            if (newestFirst)
                _eventLines.RemoveAt(_eventLines.Count - 1);
            else
                _eventLines.RemoveAt(0);
        }
    }
    
    void Update()
    {
        if (_needsUpdate && logText != null)
        {
            _needsUpdate = false;
            UpdateDisplay();
        }
    }
    
    void UpdateDisplay()
    {
        if (logText == null) return;
        
        // Build the full text
        StringBuilder sb = new StringBuilder();
        
        foreach (string line in _eventLines)
        {
            sb.AppendLine(line);
        }
        
        logText.text = sb.ToString();
        
        // Update counter
        if (countText != null)
        {
            countText.text = $"Events: {_totalCount}";
        }
        
        // Scroll to top or bottom
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = newestFirst ? 1f : 0f;
        }
        
        Debug.Log($"[SimpleAuditLog] Display updated - {_eventLines.Count} lines shown");
    }
    
    /// <summary>
    /// Call this to manually add a test event
    /// </summary>
    public void AddTestEvent()
    {
        string[] testTypes = { "SESSION_START", "JOIN_MEETING", "ENTER_OFFICE", "APPLE_PICKED", "POLL_VOTE" };
        string[] testPlayers = { "Alice", "Bob", "Charlie", "Diana" };
        string[] testTargets = { "Red Apple", "Option A", "Meeting Room", "" };
        
        string line = $"<color=#7BC8FF>{System.DateTime.Now:HH:mm:ss}</color> " +
                      $"<color=#FFD700>{testTypes[Random.Range(0, testTypes.Length)]}</color> " +
                      $"<color=#FFFFFF>{testPlayers[Random.Range(0, testPlayers.Length)]}</color>";
        
        if (Random.value > 0.5f)
        {
            line += $" <color=#AAAAAA>→ {testTargets[Random.Range(0, testTargets.Length)]}</color>";
        }
        
        _eventLines.Insert(0, line);
        _totalCount++;
        _needsUpdate = true;
        
        Debug.Log("[SimpleAuditLog] Added test event");
    }
}

