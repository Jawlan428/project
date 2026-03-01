using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace SmartFarm
{
    /// <summary>
    /// Connects Poll Vote UI elements to PollVoteManager.
    /// Assign buttons and text fields in Inspector.
    /// </summary>
    public class PollVoteUI : MonoBehaviour
    {
        [Header("Manager")]
        [SerializeField] private PollVoteManager pollManager;

        [Header("UI References")]
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private TMP_Text resultsText;
        [SerializeField] private Button voteAButton;
        [SerializeField] private Button voteBButton;
        [SerializeField] private Button openPollButton;
        [SerializeField] private Button closePollButton;

        [Header("Refresh")]
        [SerializeField] private float refreshInterval = 0.3f;

        private float _nextRefresh;

        private void Start()
        {
            EnsureUIReferences();
            EnsurePollManager();
            if (pollManager != null)
            {
                pollManager.OnVoteReceived.AddListener(OnVoteReceived);
                pollManager.OnPollResultApplied.AddListener(OnPollResultApplied);
            }
            // Both onClick and PollButtonForwarder (for XR – pointer clicks may not trigger onClick)
            WireButton(voteAButton, OnVoteA);
            WireButton(voteBButton, OnVoteB);
            WireButton(openPollButton, OnOpenPoll);
            WireButton(closePollButton, OnClosePoll);
            _nextRefresh = Time.time + refreshInterval;
        }

        private void WireButton(Button btn, System.Action action)
        {
            if (btn == null) return;
            btn.onClick.AddListener(() => action());
            var forwarder = btn.GetComponent<PollButtonForwarder>();
            if (forwarder == null) forwarder = btn.gameObject.AddComponent<PollButtonForwarder>();
            if (forwarder.onPointerClick == null) forwarder.onPointerClick = new UnityEngine.Events.UnityEvent();
            forwarder.onPointerClick.RemoveAllListeners();
            forwarder.onPointerClick.AddListener(() => action());
        }

        private void EnsureUIReferences()
        {
            if (questionText == null) questionText = transform.Find("QuestionText")?.GetComponent<TMP_Text>();
            if (resultsText == null) resultsText = transform.Find("ResultsText")?.GetComponent<TMP_Text>();
            if (voteAButton == null) voteAButton = transform.Find("Button_Yes")?.GetComponent<Button>();
            if (voteBButton == null) voteBButton = transform.Find("Button_No")?.GetComponent<Button>();
            if (openPollButton == null) openPollButton = transform.Find("Button_OpenPoll")?.GetComponent<Button>();
            if (closePollButton == null) closePollButton = transform.Find("Button_Close&Apply")?.GetComponent<Button>();
            var buttons = GetComponentsInChildren<Button>(true);
            if (voteAButton == null && buttons.Length >= 1) voteAButton = buttons[0];
            if (voteBButton == null && buttons.Length >= 2) voteBButton = buttons[1];
            if (openPollButton == null && buttons.Length >= 3) openPollButton = buttons[2];
            if (closePollButton == null && buttons.Length >= 4) closePollButton = buttons[3];
        }

        private void EnsurePollManager()
        {
            if (pollManager == null)
                pollManager = FindFirstObjectByType<PollVoteManager>();
            if (pollManager == null)
                Debug.LogWarning("[PollVoteUI] PollVoteManager not found! Buttons will not work.");
        }

        private void OnVoteA()
        {
            EnsurePollManager();
            if (pollManager == null) return;
            if (!pollManager.IsPollOpen) pollManager.OpenPoll();
            pollManager.VoteOptionA();
            RefreshResults();
        }

        private void OnVoteB()
        {
            EnsurePollManager();
            if (pollManager == null) return;
            if (!pollManager.IsPollOpen) pollManager.OpenPoll();
            pollManager.VoteOptionB();
            RefreshResults();
        }
        private void OnOpenPoll() { EnsurePollManager(); pollManager?.OpenPoll(); RefreshResults(); }
        private void OnClosePoll() { EnsurePollManager(); pollManager?.ClosePollAndApply(); RefreshResults(); }

        private void OnDestroy()
        {
            if (pollManager != null)
            {
                pollManager.OnVoteReceived.RemoveListener(OnVoteReceived);
                pollManager.OnPollResultApplied.RemoveListener(OnPollResultApplied);
            }
        }

        private void OnVoteReceived(string msg)
        {
            RefreshResults();
        }

        private void OnPollResultApplied(bool optionAWon)
        {
            RefreshResults();
        }

        private void Update()
        {
            if (Time.time < _nextRefresh) return;
            _nextRefresh = Time.time + refreshInterval;
            RefreshResults();
        }

        private void RefreshResults()
        {
            if (pollManager == null) return;

            if (questionText != null)
                questionText.text = pollManager.Question;

            if (resultsText != null)
            {
                int total = pollManager.TotalVotes;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"<b>{pollManager.OptionA}</b>: {pollManager.VotesA} ({pollManager.PercentA:F0}%)");
                sb.AppendLine($"<b>{pollManager.OptionB}</b>: {pollManager.VotesB} ({pollManager.PercentB:F0}%)");
                if (total > 0)
                {
                    var votersA = pollManager.GetVotersForOptionA();
                    var votersB = pollManager.GetVotersForOptionB();
                    if (votersA.Count > 0)
                        sb.AppendLine($"  <color=#888>Voters A: {string.Join(", ", votersA)}</color>");
                    if (votersB.Count > 0)
                        sb.AppendLine($"  <color=#888>Voters B: {string.Join(", ", votersB)}</color>");
                }
                resultsText.text = sb.ToString().TrimEnd();
            }
        }
    }
}
