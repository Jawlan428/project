using UnityEngine;
using TMPro;

public class PlayerBehaviorLogger : MonoBehaviour
{
    public string playerName = "Player";
    public bool useAvatarName = true;
    public Transform avatarRoot;
    public TMP_Text avatarNameText;
    public bool autoFindNameText = true;
    public float nameRefreshInterval = 1f;
    public float moveThreshold = 0.1f;
    public float moveLogInterval = 5f;
    public bool logHeartbeat = false;
    public float heartbeatInterval = 5f;
    [Header("Debug Hotkeys (Editor/PC)")]
    public bool enableDebugHotkeys = false;
    public KeyCode grabKey = KeyCode.G;
    public KeyCode talkKey = KeyCode.T;
    public KeyCode buttonKey = KeyCode.B;

    Vector3 lastPos;
    float nextMoveLogTime;
    float nextHeartbeatTime;
    float nextNameRefreshTime;
    float lastGrabLogTime;
    float lastTalkLogTime;
    float lastButtonLogTime;
    bool hasLoggedNameSet;
    readonly System.Collections.Generic.List<string> pendingLines = new System.Collections.Generic.List<string>();

    void Start()
    {
        lastPos = transform.position;
        if (useAvatarName)
            RefreshAvatarName();
        TryAddLine($"{playerName} joined.");
        nextHeartbeatTime = Time.unscaledTime + heartbeatInterval;
        nextNameRefreshTime = Time.unscaledTime + nameRefreshInterval;
    }

    void Update()
    {
        FlushPending();

        if (useAvatarName && Time.unscaledTime >= nextNameRefreshTime)
        {
            RefreshAvatarName();
            nextNameRefreshTime = Time.unscaledTime + nameRefreshInterval;
        }

        // Example behavior: movement (log every N seconds if moved enough)
        if (Time.unscaledTime >= nextMoveLogTime)
        {
            float dist = Vector3.Distance(transform.position, lastPos);
            if (dist > moveThreshold)
            {
                TryAddLine($"{playerName} moved ({dist:0.0}m).");
                lastPos = transform.position;
            }
            nextMoveLogTime = Time.unscaledTime + moveLogInterval;
        }

        if (logHeartbeat && Time.unscaledTime >= nextHeartbeatTime)
        {
            TryAddLine($"{playerName} active.");
            nextHeartbeatTime = Time.unscaledTime + heartbeatInterval;
        }

        if (enableDebugHotkeys)
        {
            if (Input.GetKeyDown(grabKey))
                LogGrab("SampleObject");
            if (Input.GetKeyDown(talkKey))
                LogTalk("Hello!");
            if (Input.GetKeyDown(buttonKey))
                LogButton("SampleButton");
        }
    }

    public void LogGrab(string objectName)
    {
        if (Time.unscaledTime - lastGrabLogTime < 5f) return;
        lastGrabLogTime = Time.unscaledTime;
        TryAddLine($"{playerName} grabbed {objectName}.");
    }

    public void LogButton(string buttonName)
    {
        if (Time.unscaledTime - lastButtonLogTime < 5f) return;
        lastButtonLogTime = Time.unscaledTime;
        TryAddLine($"{playerName} pressed {buttonName}.");
    }

    public void LogTalk(string message)
    {
        if (Time.unscaledTime - lastTalkLogTime < 5f) return;
        lastTalkLogTime = Time.unscaledTime;
        TryAddLine($"{playerName} said: {message}");
    }

    public void LogMove(float distanceMeters)
    {
        TryAddLine($"{playerName} moved ({distanceMeters:0.0}m).");
    }

    public void SetPlayerName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;

        if (string.Equals(playerName, newName.Trim(), System.StringComparison.Ordinal))
            return;

        playerName = newName.Trim();
        useAvatarName = false;
        autoFindNameText = false;
        if (!hasLoggedNameSet)
        {
            TryAddLine($"{playerName} set name.");
            hasLoggedNameSet = true;
        }
    }

    public void LogGrab(GameObject grabbedObject)
    {
        string name = grabbedObject != null ? grabbedObject.name : "Unknown";
        TryAddLine($"{playerName} grabbed {name}.");
    }

    void TryAddLine(string msg)
    {
        if (BehaviorBoard.Instance == null)
        {
            pendingLines.Add(msg);
            Debug.LogWarning("[PlayerBehaviorLogger] Board missing. Queued: " + msg);
            return;
        }

        BehaviorBoard.Instance.AddLine(msg);
        Debug.Log("[PlayerBehaviorLogger] Sent: " + msg);
    }

    void FlushPending()
    {
        if (pendingLines.Count == 0 || BehaviorBoard.Instance == null) return;

        for (int i = 0; i < pendingLines.Count; i++)
            BehaviorBoard.Instance.AddLine(pendingLines[i]);

        pendingLines.Clear();
    }

    string ResolveAvatarName()
    {
        if (avatarNameText != null && !string.IsNullOrWhiteSpace(avatarNameText.text))
            return avatarNameText.text.Trim();

        if (autoFindNameText)
        {
            TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
            foreach (var tmp in texts)
            {
                string t = tmp.text != null ? tmp.text.Trim() : "";
                if (string.IsNullOrEmpty(t)) continue;
                if (t.Contains("(You)") || t.Contains(" You") || t.EndsWith("You"))
                    return t;
            }
        }

        Transform target = avatarRoot != null ? avatarRoot : transform;
        return string.IsNullOrEmpty(target.name) ? "Player" : target.name;
    }

    void RefreshAvatarName()
    {
        string resolved = ResolveAvatarName();
        if (!string.IsNullOrWhiteSpace(resolved))
            playerName = resolved;
    }
}
