using TMPro;
using UnityEngine;

public class RoomInfoNameBinder : MonoBehaviour
{
    [Header("References")]
    public TMP_InputField nameInput;
    public TMP_Text displayNameText;
    public PlayerBehaviorLogger logger;

    [Header("Settings")]
    public bool applyOnStart = true;
    public bool applyOnEndEdit = true;
    public bool autoFindReferences = true;
    public bool autoRefreshName = true;
    public float refreshInterval = 1f;
    public bool debugLogs = false;

    float nextRefreshTime;

    void Start()
    {
        if (logger == null)
            logger = FindFirstObjectByType<PlayerBehaviorLogger>();

        if (autoFindReferences)
            AutoFindReferences();

        if (applyOnStart)
            ApplyName();

        if (applyOnEndEdit && nameInput != null)
            nameInput.onEndEdit.AddListener(_ => ApplyName());

        nextRefreshTime = Time.unscaledTime + refreshInterval;
    }

    void Update()
    {
        if (!autoRefreshName) return;

        if (Time.unscaledTime >= nextRefreshTime)
        {
            ApplyName();
            nextRefreshTime = Time.unscaledTime + refreshInterval;
        }
    }

    void AutoFindReferences()
    {
        if (nameInput == null)
        {
            TMP_InputField[] inputs = FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None);
            foreach (var input in inputs)
            {
                if (!string.IsNullOrWhiteSpace(input.text))
                {
                    nameInput = input;
                    break;
                }
            }
        }

        if (displayNameText == null)
        {
            TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
            foreach (var tmp in texts)
            {
                string t = tmp.text != null ? tmp.text.Trim() : "";
                if (string.IsNullOrEmpty(t)) continue;
                if (t.Contains("(You)") || t.Contains(" You") || t.EndsWith("You"))
                {
                    displayNameText = tmp;
                    break;
                }
            }
        }
    }

    public void ApplyName()
    {
        if (logger == null) return;

        string chosen = null;
        if (nameInput != null && !string.IsNullOrWhiteSpace(nameInput.text))
            chosen = nameInput.text.Trim();
        else if (displayNameText != null && !string.IsNullOrWhiteSpace(displayNameText.text))
            chosen = displayNameText.text.Trim();

        if (!string.IsNullOrWhiteSpace(chosen))
            logger.SetPlayerName(chosen);
        else if (debugLogs)
            Debug.LogWarning("[RoomInfoNameBinder] No name found to apply.");
    }
}

