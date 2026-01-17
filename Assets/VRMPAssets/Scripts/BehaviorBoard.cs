using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class BehaviorBoard : MonoBehaviour
{
    public static BehaviorBoard Instance;

    [Header("UI")]
    public TMP_Text eventText;

    [Header("Settings")]
    public int maxLines = 30;
    public bool autoExpireLines = true;
    public float lineLifetimeSeconds = 5f;

    private struct LineEntry
    {
        public string Text;
        public float Time;
    }

    private readonly Queue<LineEntry> lines = new Queue<LineEntry>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[BehaviorBoard] Duplicate instance found. Disabling this one.");
            enabled = false;
            return;
        }

        Instance = this;
        if (eventText == null)
            eventText = GetComponentInChildren<TMP_Text>(true);

        if (eventText == null)
            Debug.LogError("[BehaviorBoard] TMP_Text reference missing. Assign eventText in the Inspector.");
        else
            Debug.Log("[BehaviorBoard] Ready. Text target: " + eventText.name);

        AddLine("Board ready.");
    }

    void Update()
    {
        if (!autoExpireLines || lines.Count == 0) return;

        bool removed = false;
        float now = Time.unscaledTime;

        while (lines.Count > 0 && now - lines.Peek().Time >= lineLifetimeSeconds)
        {
            lines.Dequeue();
            removed = true;
        }

        if (removed)
            RefreshText();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void AddLine(string msg)
    {
        if (eventText == null)
        {
            Debug.LogWarning("[BehaviorBoard] Cannot add line; eventText is null. Message: " + msg);
            return;
        }

        string line = $"[{System.DateTime.Now:HH:mm:ss}] {msg}";
        lines.Enqueue(new LineEntry { Text = line, Time = Time.unscaledTime });

        while (lines.Count > maxLines)
            lines.Dequeue();

        RefreshText();
        Debug.Log("[BehaviorBoard] Updated text with: " + msg);
    }

    void RefreshText()
    {
        eventText.text = string.Join("\n", lines.Select(l => l.Text));
    }
}
