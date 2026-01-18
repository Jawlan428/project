using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using XRMultiplayer;

public class PollBoard : MonoBehaviour
{
    [Header("Question")]
    public TMP_Text questionText;
    [TextArea] public string question = "Which option do you choose?";

    [Header("Options")]
    public string[] options = { "Option A", "Option B", "Option C", "Option D" };
    public bool allowChangeVote = false;

    [Header("Results")]
    public TMP_Text resultsText;
    public string resultsHeader = "Results";
    [Tooltip("Show the names of players who voted for each option")]
    public bool showVoterNames = true;

    [Header("Player")]
    [Tooltip("Fallback name if XRINetworkGameManager name is not available")]
    public string fallbackPlayerId = "LocalPlayer";

    private readonly Dictionary<string, int> votesByPlayer = new Dictionary<string, int>();
    private int[] counts = new int[0];

    /// <summary>
    /// Gets the current player's name from XRINetworkGameManager (Unity Creator name), 
    /// PlayerIdentity, or falls back to fallbackPlayerId
    /// </summary>
    private string LocalPlayerName
    {
        get
        {
            // First try XRINetworkGameManager (the Unity Creator name like "jo")
            string networkName = XRINetworkGameManager.LocalPlayerName?.Value;
            if (!string.IsNullOrEmpty(networkName) && networkName != "Player")
            {
                return networkName;
            }

            // Then try PlayerIdentity
            if (PlayerIdentity.Instance != null && !string.IsNullOrEmpty(PlayerIdentity.Instance.PlayerName) 
                && PlayerIdentity.Instance.PlayerName != "Unknown")
            {
                return PlayerIdentity.Instance.PlayerName;
            }

            return fallbackPlayerId;
        }
    }

    void Awake()
    {
        InitializeCounts();
        RefreshQuestion();
        RefreshResults();
        Debug.Log("[PollBoard] Initialized.");
    }

    void OnValidate()
    {
        if (options == null || options.Length == 0)
            options = new[] { "Option A", "Option B" };
    }

    public void Vote(int optionIndex)
    {
        Vote(LocalPlayerName, optionIndex);
    }

    public void Vote(string playerId, int optionIndex)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            Debug.LogWarning("[PollBoard] Vote ignored: playerId is empty.");
            return;
        }

        if (optionIndex < 0 || optionIndex >= options.Length)
        {
            Debug.LogWarning("[PollBoard] Vote ignored: option index out of range.");
            return;
        }

        if (!allowChangeVote && votesByPlayer.ContainsKey(playerId))
        {
            Debug.Log("[PollBoard] Vote ignored: player already voted.");
            return;
        }

        bool isChangingVote = false;
        string previousOption = null;

        if (votesByPlayer.TryGetValue(playerId, out int previous))
        {
            if (previous == optionIndex)
                return;
            counts[previous] = Mathf.Max(0, counts[previous] - 1);
            isChangingVote = true;
            previousOption = options[previous];
        }

        votesByPlayer[playerId] = optionIndex;
        counts[optionIndex] += 1;

        Debug.Log($"[PollBoard] Vote registered: {playerId} -> {options[optionIndex]}");
        
        // Log vote to AuditLogger
        LogVoteToAudit(playerId, options[optionIndex], isChangingVote, previousOption);
        
        RefreshResults();
    }

    /// <summary>
    /// Logs a poll vote to the AuditLogger system
    /// </summary>
    private void LogVoteToAudit(string voterName, string chosenOption, bool isChangingVote, string previousOption)
    {
        // Ensure PlayerIdentity has the correct name for audit logging
        if (PlayerIdentity.Instance != null)
        {
            string currentIdentityName = PlayerIdentity.Instance.PlayerName;
            if (currentIdentityName == "Unknown" || string.IsNullOrEmpty(currentIdentityName))
            {
                // Sync PlayerIdentity with the voter name
                PlayerIdentity.Instance.SetPlayerName(voterName);
            }
        }

        // Build metadata JSON
        string metaJson;
        if (isChangingVote)
        {
            metaJson = $"{{\"question\":\"{EscapeJson(question)}\",\"chosenOption\":\"{EscapeJson(chosenOption)}\",\"previousOption\":\"{EscapeJson(previousOption)}\",\"changedVote\":true}}";
        }
        else
        {
            metaJson = $"{{\"question\":\"{EscapeJson(question)}\",\"chosenOption\":\"{EscapeJson(chosenOption)}\"}}";
        }

        // Log to AuditLogger
        AuditLogger.Instance.Log(
            AuditEventType.POLL_VOTE,
            targetId: chosenOption,
            zoneName: null,
            position: transform.position,
            metaJson: metaJson
        );

        Debug.Log($"[PollBoard] Audit logged: {voterName} voted for {chosenOption}");
    }

    /// <summary>
    /// Escapes special characters for JSON string
    /// </summary>
    private string EscapeJson(string str)
    {
        if (string.IsNullOrEmpty(str)) return "";
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    public void ResetVotes()
    {
        votesByPlayer.Clear();
        InitializeCounts();
        Debug.Log("[PollBoard] Votes reset.");
        RefreshResults();
    }

    public void RefreshQuestion()
    {
        if (questionText == null)
        {
            Debug.LogWarning("[PollBoard] questionText not assigned.");
            return;
        }

        questionText.text = question;
    }

    void InitializeCounts()
    {
        counts = new int[options.Length];
    }

    void RefreshResults()
    {
        if (resultsText == null)
        {
            Debug.LogWarning("[PollBoard] resultsText not assigned.");
            return;
        }

        int total = 0;
        for (int i = 0; i < counts.Length; i++)
            total += counts[i];

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(resultsHeader))
            sb.AppendLine(resultsHeader);

        for (int i = 0; i < options.Length; i++)
        {
            float pct = total > 0 ? (counts[i] * 100f / total) : 0f;
            sb.AppendLine($"<b>{options[i]}</b>: {counts[i]} ({pct:0.0}%)");

            // Show voter names if enabled
            if (showVoterNames && counts[i] > 0)
            {
                var votersForOption = GetVotersForOption(i);
                if (votersForOption.Count > 0)
                {
                    sb.AppendLine($"  <color=#888888>Voters: {string.Join(", ", votersForOption)}</color>");
                }
            }
        }

        resultsText.text = sb.ToString().TrimEnd();
        Debug.Log("[PollBoard] Results updated.");
    }

    /// <summary>
    /// Gets a list of player names who voted for a specific option
    /// </summary>
    /// <param name="optionIndex">The option index to check</param>
    /// <returns>List of player names who voted for this option</returns>
    public List<string> GetVotersForOption(int optionIndex)
    {
        return votesByPlayer
            .Where(kvp => kvp.Value == optionIndex)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>
    /// Gets all current votes as a dictionary (player name -> option index)
    /// </summary>
    public Dictionary<string, int> GetAllVotes()
    {
        return new Dictionary<string, int>(votesByPlayer);
    }

    /// <summary>
    /// Gets the vote count for a specific option
    /// </summary>
    public int GetVoteCount(int optionIndex)
    {
        if (optionIndex < 0 || optionIndex >= counts.Length)
            return 0;
        return counts[optionIndex];
    }

    /// <summary>
    /// Gets the total number of votes cast
    /// </summary>
    public int GetTotalVotes()
    {
        int total = 0;
        for (int i = 0; i < counts.Length; i++)
            total += counts[i];
        return total;
    }
}

