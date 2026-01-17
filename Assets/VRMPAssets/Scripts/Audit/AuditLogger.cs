using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton audit logger that records events, displays them on BehaviorBoard,
/// and persists them to JSON files on disk.
/// </summary>
public class AuditLogger : MonoBehaviour
{
    private static AuditLogger _instance;
    private string _sessionId;
    private List<AuditEvent> _events = new List<AuditEvent>();
    private string _logDirectory;
    private Queue<string> _queuedBoardMessages = new Queue<string>();
    private bool _hasLoggedSessionStart = false;

    // AUDIT INTEGRATION - Event for real-time board display
    public static event Action<AuditEvent> OnAuditEvent;

    public static AuditLogger Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("AuditLogger");
                _instance = go.AddComponent<AuditLogger>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[AuditLogger] Duplicate instance found. Destroying this one.");
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Generate session ID
        _sessionId = System.Guid.NewGuid().ToString();

        // Setup log directory (Desktop preferred, fallback to persistentDataPath)
        _logDirectory = ResolveLogDirectory();

        // Subscribe to scene loaded to check for BehaviorBoard
        SceneManager.sceneLoaded += OnSceneLoaded;

        Debug.Log($"[AUDIT] Initialized (persistent). Session ID: {_sessionId}");
        Debug.Log($"[AUDIT] Log directory: {_logDirectory}");
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // AUDIT INTEGRATION - Board display is now handled by AuditBoardBridge via events
        // No need to flush here - bridge handles it
    }

    void Update()
    {
        // AUDIT INTEGRATION - Board display is now handled by AuditBoardBridge via events
        // No need to check here - bridge handles it
    }

    /// <summary>
    /// Resolves the log directory path. Prefers Desktop, falls back to persistentDataPath.
    /// </summary>
    private string ResolveLogDirectory()
    {
        string desktopPath = null;
        string projectName = Application.productName;

        try
        {
            // Try to get Desktop path
            desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            
            if (!string.IsNullOrEmpty(desktopPath) && Directory.Exists(desktopPath))
            {
                string desktopLogPath = Path.Combine(desktopPath, $"{projectName}_AuditLogs");
                
                // Try to create directory
                try
                {
                    if (!Directory.Exists(desktopLogPath))
                    {
                        Directory.CreateDirectory(desktopLogPath);
                    }
                    
                    // Test write access
                    string testFile = Path.Combine(desktopLogPath, ".test");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                    
                    return desktopLogPath;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AUDIT] Cannot write to Desktop folder ({ex.Message}), falling back to persistentDataPath.");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AUDIT] Desktop path not available ({ex.Message}), falling back to persistentDataPath.");
        }

        // Fallback to persistentDataPath
        string fallbackPath = Path.Combine(Application.persistentDataPath, "AuditLogs");
        if (!Directory.Exists(fallbackPath))
        {
            Directory.CreateDirectory(fallbackPath);
        }
        
        return fallbackPath;
    }

    /// <summary>
    /// Attempts to flush queued messages to BehaviorBoard if it's available.
    /// </summary>
    private void TryFlushQueuedMessages()
    {
        if (BehaviorBoard.Instance == null || _queuedBoardMessages.Count == 0)
            return;

        int flushedCount = _queuedBoardMessages.Count;
        while (_queuedBoardMessages.Count > 0)
        {
            string msg = _queuedBoardMessages.Dequeue();
            BehaviorBoard.Instance.AddLine(msg);
        }

        Debug.Log($"[AUDIT] BehaviorBoard detected, flushing {flushedCount} queued messages");
    }

    /// <summary>
    /// Attempts to write a message to BehaviorBoard, queues it if board is not available.
    /// </summary>
    private void TryWriteToBoard(string message)
    {
        if (BehaviorBoard.Instance != null)
        {
            BehaviorBoard.Instance.AddLine(message);
            Debug.Log($"[AUDIT] Forwarded to board: {message}");
        }
        else
        {
            // Queue message for later
            _queuedBoardMessages.Enqueue(message);
            Debug.Log($"[AUDIT] Board unavailable, queued: {message}");
        }
    }

    /// <summary>
    /// Logs an audit event. Automatically fetches player name from PlayerIdentity.
    /// </summary>
    /// <param name="type">The type of event</param>
    /// <param name="targetId">Optional target identifier</param>
    /// <param name="zoneName">Optional zone name</param>
    /// <param name="position">Optional position</param>
    /// <param name="metaJson">Optional JSON metadata string</param>
    public void Log(AuditEventType type, string targetId = null, string zoneName = null, Vector3? position = null, string metaJson = null)
    {
        // Prevent duplicate SESSION_START logs
        if (type == AuditEventType.SESSION_START && _hasLoggedSessionStart)
        {
            return;
        }
        if (type == AuditEventType.SESSION_START)
        {
            _hasLoggedSessionStart = true;
        }

        // Get player name from PlayerIdentity
        string playerName = "Unknown";
        if (PlayerIdentity.Instance != null)
        {
            playerName = PlayerIdentity.Instance.PlayerName;
        }

        // Create audit event
        AuditEvent evt = new AuditEvent
        {
            sessionId = _sessionId,
            playerName = playerName,
            eventType = type.ToString(),
            targetId = targetId,
            sceneName = SceneManager.GetActiveScene().name,
            zoneName = zoneName,
            metaJson = metaJson
        };

        if (position.HasValue)
        {
            evt.position = new SerializableVector3(position.Value);
        }

        // Add to list
        _events.Add(evt);

        // AUDIT INTEGRATION - Broadcast event for real-time board display
        OnAuditEvent?.Invoke(evt);

        // Debug log
        string debugMsg = $"[AUDIT] {type} | player={playerName}";
        if (!string.IsNullOrEmpty(targetId))
            debugMsg += $" | target={targetId}";
        if (!string.IsNullOrEmpty(zoneName))
            debugMsg += $" | zone={zoneName}";
        Debug.Log(debugMsg);
    }

    /// <summary>
    /// Flushes all events to disk as a JSON file.
    /// </summary>
    public void Flush()
    {
        if (_events.Count == 0)
        {
            Debug.Log("[AUDIT] No events to flush.");
            return;
        }

        try
        {
            // Ensure directory exists
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            // Generate filename with timestamp
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string filename = $"audit_{_sessionId}_{timestamp}.json";
            string filepath = Path.Combine(_logDirectory, filename);

            // Serialize to JSON
            string json = JsonUtility.ToJson(new AuditEventList { events = _events }, true);

            // Write to file
            File.WriteAllText(filepath, json);

            Debug.Log($"[AUDIT] Saved audit file to: {filepath}");
            Debug.Log($"[AUDIT] Flushed {_events.Count} events to: {filepath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AUDIT] Failed to flush events: {ex.Message}");
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

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        
        if (_instance == this)
        {
            _instance = null;
        }
    }

    void OnApplicationQuit()
    {
        Flush();
    }
}
