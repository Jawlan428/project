using System.Collections.Generic;
using TMPro;
using UnityEngine;

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

    [Header("Player")]
    public string localPlayerId = "LocalPlayer";

    private readonly Dictionary<string, int> votesByPlayer = new Dictionary<string, int>();
    private int[] counts = new int[0];

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
        Vote(localPlayerId, optionIndex);
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

        if (votesByPlayer.TryGetValue(playerId, out int previous))
        {
            if (previous == optionIndex)
                return;
            counts[previous] = Mathf.Max(0, counts[previous] - 1);
        }

        votesByPlayer[playerId] = optionIndex;
        counts[optionIndex] += 1;

        Debug.Log($"[PollBoard] Vote registered: {playerId} -> {options[optionIndex]}");
        RefreshResults();
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
            sb.AppendLine($"{options[i]}: {counts[i]} ({pct:0.0}%)");
        }

        resultsText.text = sb.ToString().TrimEnd();
        Debug.Log("[PollBoard] Results updated.");
    }
}

