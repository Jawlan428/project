using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bridge component that subscribes to AuditLogger events and displays them on BehaviorBoard in real-time.
/// Attach this to the AuditSystem GameObject (same as AuditBootstrap) for persistence across scenes.
/// </summary>
public class AuditBoardBridge : MonoBehaviour
{
    private Queue<string> _queuedMessages = new Queue<string>();
    private bool _hasInitialized = false;

    void Awake()
    {
        // Ensure this persists across scenes
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        // Subscribe to audit events
        AuditLogger.OnAuditEvent += HandleAuditEvent;
        
        // Subscribe to scene loaded to check for BehaviorBoard
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        if (!_hasInitialized)
        {
            Debug.Log("[AUDIT] BoardBridge active");
            _hasInitialized = true;
        }
    }

    void OnDisable()
    {
        // Unsubscribe from events
        AuditLogger.OnAuditEvent -= HandleAuditEvent;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Update()
    {
        // Periodically check if BehaviorBoard became available
        if (_queuedMessages.Count > 0 && BehaviorBoard.Instance != null)
        {
            FlushQueuedMessages();
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Try to flush queued messages when a new scene loads
        if (_queuedMessages.Count > 0 && BehaviorBoard.Instance != null)
        {
            FlushQueuedMessages();
        }
    }

    /// <summary>
    /// Handles audit events by formatting and sending to BehaviorBoard.
    /// </summary>
    private void HandleAuditEvent(AuditEvent evt)
    {
        // Format event into readable line
        string line = FormatEventLine(evt);
        
        // Try to send to BehaviorBoard
        if (BehaviorBoard.Instance != null)
        {
            BehaviorBoard.Instance.AddLine(line);
        }
        else
        {
            // Queue for later if board not available
            _queuedMessages.Enqueue(line);
        }
    }

    /// <summary>
    /// Formats an AuditEvent into a short readable line for the board.
    /// Format: "EVENT_TYPE | player=name | target=id | zone=zone"
    /// </summary>
    private string FormatEventLine(AuditEvent evt)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append(evt.eventType);
        
        // Add player name
        if (!string.IsNullOrEmpty(evt.playerName))
        {
            sb.Append(" | player=").Append(evt.playerName);
        }
        
        // Add target if present
        if (!string.IsNullOrEmpty(evt.targetId))
        {
            sb.Append(" | target=").Append(evt.targetId);
        }
        
        // Add zone if present
        if (!string.IsNullOrEmpty(evt.zoneName))
        {
            sb.Append(" | zone=").Append(evt.zoneName);
        }
        
        // Add position if present (optional, for debugging)
        // Uncomment if you want position in the board display:
        // if (evt.position != null)
        // {
        //     sb.Append($" | pos=({evt.position.x:F1},{evt.position.y:F1},{evt.position.z:F1})");
        // }
        
        return sb.ToString();
    }

    /// <summary>
    /// Flushes all queued messages to BehaviorBoard when it becomes available.
    /// </summary>
    private void FlushQueuedMessages()
    {
        if (BehaviorBoard.Instance == null || _queuedMessages.Count == 0)
            return;

        int count = _queuedMessages.Count;
        while (_queuedMessages.Count > 0)
        {
            string msg = _queuedMessages.Dequeue();
            BehaviorBoard.Instance.AddLine(msg);
        }

        Debug.Log($"[AUDIT] BehaviorBoard detected, flushing {count} lines");
    }

    void OnDestroy()
    {
        // Cleanup
        AuditLogger.OnAuditEvent -= HandleAuditEvent;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
