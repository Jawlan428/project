using UnityEngine;
using TMPro;

/// <summary>
/// Component for a single audit event row in the analytics canvas.
/// Displays: time, eventType, actor, object, summary
/// </summary>
public class AuditEventRow : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Text displaying the timestamp")]
    public TextMeshProUGUI timeText;
    
    [Tooltip("Text displaying the event type")]
    public TextMeshProUGUI eventTypeText;
    
    [Tooltip("Text displaying the actor/player name")]
    public TextMeshProUGUI actorText;
    
    [Tooltip("Text displaying the target/object")]
    public TextMeshProUGUI objectText;
    
    [Tooltip("Text displaying the summary")]
    public TextMeshProUGUI summaryText;

    private AuditEvent _currentEvent;

    void Awake()
    {
        // Auto-find text components if not assigned
        if (timeText == null)
            timeText = transform.Find("TimeText")?.GetComponent<TextMeshProUGUI>();
        
        if (eventTypeText == null)
            eventTypeText = transform.Find("EventTypeText")?.GetComponent<TextMeshProUGUI>();
        
        if (actorText == null)
            actorText = transform.Find("ActorText")?.GetComponent<TextMeshProUGUI>();
        
        if (objectText == null)
            objectText = transform.Find("ObjectText")?.GetComponent<TextMeshProUGUI>();
        
        if (summaryText == null)
            summaryText = transform.Find("SummaryText")?.GetComponent<TextMeshProUGUI>();
    }

    /// <summary>
    /// Sets the event data for this row
    /// </summary>
    public void SetEvent(AuditEvent evt)
    {
        _currentEvent = evt;
        UpdateDisplay();
    }

    /// <summary>
    /// Updates the UI display with current event data
    /// </summary>
    private void UpdateDisplay()
    {
        if (_currentEvent == null) return;

        // Time
        if (timeText != null)
        {
            timeText.text = _currentEvent.GetFormattedTime();
            EnsureTextSettings(timeText);
        }

        // Event Type
        if (eventTypeText != null)
        {
            eventTypeText.text = _currentEvent.eventType ?? "UNKNOWN";
            EnsureTextSettings(eventTypeText);
        }

        // Actor
        if (actorText != null)
        {
            actorText.text = _currentEvent.playerName ?? "Unknown";
            EnsureTextSettings(actorText);
        }

        // Object/Target
        if (objectText != null)
        {
            objectText.text = _currentEvent.targetId ?? "-";
            EnsureTextSettings(objectText);
        }

        // Summary
        if (summaryText != null)
        {
            summaryText.text = _currentEvent.GetSummary();
            EnsureTextSettings(summaryText);
        }
    }

    /// <summary>
    /// Ensures text settings prevent clipping and movement issues in VR
    /// </summary>
    private void EnsureTextSettings(TextMeshProUGUI text)
    {
        if (text == null) return;

        // Prevent overflow
        text.overflowMode = TextOverflowModes.Truncate;
        text.enableWordWrapping = false;
    }

    /// <summary>
    /// Gets the current event
    /// </summary>
    public AuditEvent GetEvent()
    {
        return _currentEvent;
    }
}

