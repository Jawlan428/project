using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm
{
    public class PollPageUI : MonoBehaviour
    {
        [SerializeField] private PollVoteManager pollManager;
        [SerializeField] private FarmDataManager dataManager;
        [SerializeField] private SimpleUIAnimationHelper animationHelper;

        [Header("Main")]
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private TMP_Text resultsText;
        [SerializeField] private TMP_Text votersAText;
        [SerializeField] private TMP_Text votersBText;
        [SerializeField] private Button openPollButton;

        [Header("Modal")]
        [SerializeField] private GameObject pollModalRoot;
        [SerializeField] private TMP_Text modalQuestionText;
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private TMP_Text voteSubmittedText;
        [SerializeField] private Button optionAButton;
        [SerializeField] private Button optionBButton;
        [SerializeField] private Button closeModalButton;
        [SerializeField] private float pollDurationSeconds = 15f;

        private Coroutine _countdownRoutine;
        private bool _voteSubmitted;

        private void Start()
        {
            if (dataManager == null) dataManager = FindFirstObjectByType<FarmDataManager>();
            if (pollManager == null)
                pollManager = dataManager != null ? dataManager.PollVoteManager : FindFirstObjectByType<PollVoteManager>();

            if (openPollButton != null) openPollButton.onClick.AddListener(OpenPollModal);
            if (optionAButton != null) optionAButton.onClick.AddListener(() => SubmitVote(true));
            if (optionBButton != null) optionBButton.onClick.AddListener(() => SubmitVote(false));
            if (closeModalButton != null) closeModalButton.onClick.AddListener(CloseModalAndApply);

            if (pollModalRoot != null) pollModalRoot.SetActive(false);
            RefreshResults();
        }

        private void OnEnable()
        {
            if (pollManager != null)
            {
                pollManager.OnVoteReceived.AddListener(OnPollChanged);
                pollManager.OnPollResultApplied.AddListener(OnPollResultApplied);
            }
        }

        private void OnDisable()
        {
            if (pollManager != null)
            {
                pollManager.OnVoteReceived.RemoveListener(OnPollChanged);
                pollManager.OnPollResultApplied.RemoveListener(OnPollResultApplied);
            }
            if (_countdownRoutine != null) StopCoroutine(_countdownRoutine);
        }

        private void OpenPollModal()
        {
            if (pollManager == null) return;
            pollManager.OpenPoll();
            _voteSubmitted = false;
            if (voteSubmittedText != null) voteSubmittedText.gameObject.SetActive(false);
            if (modalQuestionText != null) modalQuestionText.text = pollManager.Question;

            if (animationHelper != null) animationHelper.SetModalVisible(pollModalRoot, true);
            else if (pollModalRoot != null) pollModalRoot.SetActive(true);

            if (_countdownRoutine != null) StopCoroutine(_countdownRoutine);
            _countdownRoutine = StartCoroutine(PollCountdownRoutine());
            RefreshResults();
        }

        private IEnumerator PollCountdownRoutine()
        {
            float remaining = pollDurationSeconds;
            while (remaining > 0f)
            {
                if (countdownText != null) countdownText.text = $"Time: {remaining:F0}s";
                remaining -= 1f;
                yield return new WaitForSeconds(1f);
            }
            if (countdownText != null) countdownText.text = "Time: 0s";
            CloseModalAndApply();
        }

        private void SubmitVote(bool optionA)
        {
            if (_voteSubmitted || pollManager == null) return;
            if (optionA) pollManager.VoteOptionA();
            else pollManager.VoteOptionB();
            _voteSubmitted = true;
            if (voteSubmittedText != null)
            {
                voteSubmittedText.gameObject.SetActive(true);
                voteSubmittedText.text = "Vote submitted";
            }
            RefreshResults();
        }

        private void CloseModalAndApply()
        {
            if (pollManager != null) pollManager.ClosePollAndApply();
            if (_countdownRoutine != null) StopCoroutine(_countdownRoutine);
            _countdownRoutine = null;
            if (animationHelper != null) animationHelper.SetModalVisible(pollModalRoot, false);
            else if (pollModalRoot != null) pollModalRoot.SetActive(false);
            RefreshResults();
        }

        private void OnPollChanged(string _) => RefreshResults();
        private void OnPollResultApplied(bool _) => RefreshResults();

        private void RefreshResults()
        {
            if (pollManager == null) return;

            if (questionText != null) questionText.text = pollManager.Question;
            if (resultsText != null)
            {
                resultsText.text =
                    $"{pollManager.OptionA}: {pollManager.VotesA} ({pollManager.PercentA:F0}%)\n" +
                    $"{pollManager.OptionB}: {pollManager.VotesB} ({pollManager.PercentB:F0}%)";
            }

            if (votersAText != null)
            {
                var votersA = pollManager.GetVotersForOptionA();
                votersAText.text = votersA.Count > 0 ? $"Voters A: {string.Join(", ", votersA)}" : "Voters A: -";
            }
            if (votersBText != null)
            {
                var votersB = pollManager.GetVotersForOptionB();
                votersBText.text = votersB.Count > 0 ? $"Voters B: {string.Join(", ", votersB)}" : "Voters B: -";
            }
        }
    }
}
