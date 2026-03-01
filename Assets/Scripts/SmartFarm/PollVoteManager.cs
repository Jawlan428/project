using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using XRMultiplayer;

namespace SmartFarm
{
    /// <summary>
    /// A/B Poll Vote System. One vote per participant.
    /// Shows voter names, totals, percentages. Results synchronized across network.
    /// When Option A wins: Irrigation ON. Option B: Irrigation remains OFF.
    /// Host-authoritative: host validates votes and applies result.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PollVoteManager : NetworkBehaviour
    {
        [Header("Poll Question")]
        [SerializeField] private string question = "Enable Irrigation?";

        [Header("Options (Fixed A/B)")]
        [SerializeField] private string optionA = "Yes (Enable)";
        [SerializeField] private string optionB = "No (Keep Off)";

        [Header("Events")]
        public UnityEvent<string> OnVoteReceived;
        public UnityEvent<bool> OnPollResultApplied; // true = Option A won

        // Host-only: vote storage. Synced via RPC/custom sync.
        private readonly Dictionary<ulong, int> _votesByClientId = new Dictionary<ulong, int>();
        // Single-player fallback when not connected
        private int _localVotesA, _localVotesB;
        private bool _localPollOpen;
        private bool _hasLocalAppliedResult;
        private bool _localAppliedIrrigationEnabled;

        private NetworkVariable<bool> _pollOpen = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<int> _votesA = new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<int> _votesB = new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<bool> _lastResultA = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private bool UseLocalMode
        {
            get
            {
                var nm = NetworkManager.Singleton;
                // Local mode unless networking is fully active and this object is spawned.
                return nm == null || !nm.IsListening || !nm.IsConnectedClient || !IsSpawned;
            }
        }

        private bool PreferLocalDisplayState => !IsServer && (_localPollOpen || _localVotesA > 0 || _localVotesB > 0);

        public string Question => question;
        public string OptionA => optionA;
        public string OptionB => optionB;
        public bool IsPollOpen => (UseLocalMode || PreferLocalDisplayState) ? _localPollOpen : _pollOpen.Value;
        public int VotesA => (UseLocalMode || PreferLocalDisplayState) ? _localVotesA : _votesA.Value;
        public int VotesB => (UseLocalMode || PreferLocalDisplayState) ? _localVotesB : _votesB.Value;
        public int TotalVotes => VotesA + VotesB;

        public float PercentA => TotalVotes > 0 ? 100f * VotesA / TotalVotes : 0;
        public float PercentB => TotalVotes > 0 ? 100f * VotesB / TotalVotes : 0;
        public bool HasLocalAppliedResult => _hasLocalAppliedResult;
        public bool LocalAppliedIrrigationEnabled => _localAppliedIrrigationEnabled;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
        }

        /// <summary>
        /// Open a new poll. Host only.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void OpenPollServerRpc()
        {
            if (!IsServer) return;
            _hasLocalAppliedResult = false;
            _votesByClientId.Clear();
            _votesA.Value = 0;
            _votesB.Value = 0;
            _pollOpen.Value = true;
            EventLogger.LogEvent("Vote Opened");
        }

        /// <summary>
        /// Client requests to vote. 0 = Option A, 1 = Option B.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void VoteServerRpc(int optionIndex, RpcParams rpcParams = default)
        {
            if (!IsServer || !_pollOpen.Value) return;
            if (optionIndex != 0 && optionIndex != 1) return;

            ulong clientId = rpcParams.Receive.SenderClientId;
            if (_votesByClientId.TryGetValue(clientId, out int prev))
            {
                if (prev == 0) _votesA.Value--;
                else _votesB.Value--;
            }

            _votesByClientId[clientId] = optionIndex;
            if (optionIndex == 0) _votesA.Value++;
            else _votesB.Value++;

            string voterName = GetPlayerName(clientId);
            string option = optionIndex == 0 ? optionA : optionB;
            EventLogger.LogVoteEvent(voterName, option, question);

            VoteResultClientRpc(optionIndex, voterName);
        }

        [ClientRpc]
        private void VoteResultClientRpc(int optionIndex, string voterName)
        {
            string option = optionIndex == 0 ? optionA : optionB;
            OnVoteReceived?.Invoke($"{voterName} voted {option}");
        }

        /// <summary>
        /// Close poll and apply result. Host only. Option A wins = irrigation ON.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ClosePollAndApplyServerRpc()
        {
            if (!IsServer) return;
            _pollOpen.Value = false;

            bool optionAWins = _votesA.Value >= _votesB.Value;
            _lastResultA.Value = optionAWins;

            if (FarmSimulationManager.Instance != null)
                FarmSimulationManager.Instance.SetIrrigationEnabled(optionAWins);

            EventLogger.LogEvent(optionAWins ? "Poll Result: Option A - Irrigation Enabled" : "Poll Result: Option B - Irrigation Off");

            ClosePollClientRpc(optionAWins);
        }

        [ClientRpc]
        private void ClosePollClientRpc(bool optionAWon)
        {
            _hasLocalAppliedResult = false;
            OnPollResultApplied?.Invoke(optionAWon);
        }

        /// <summary>
        /// Public API: Open poll (can be called from UI by any player; host processes).
        /// </summary>
        public void OpenPoll()
        {
            if (UseLocalMode)
                OpenPollLocal();
            else if (!IsServer)
            {
                // Immediate local feedback for non-host users.
                OpenPollLocal();
                OpenPollServerRpc();
            }
            else
                OpenPollServerRpc();
        }

        /// <summary>
        /// Public API: Vote for option 0 (A) or 1 (B).
        /// </summary>
        public void Vote(int optionIndex)
        {
            if (UseLocalMode)
                VoteLocal(optionIndex);
            else if (!IsServer)
            {
                // Immediate local feedback for non-host users.
                VoteLocal(optionIndex);
                VoteServerRpc(optionIndex);
            }
            else
                VoteServerRpc(optionIndex);
        }

        /// <summary>
        /// Public API: Vote Option A.
        /// </summary>
        public void VoteOptionA()
        {
            Vote(0);
        }

        /// <summary>
        /// Public API: Vote Option B.
        /// </summary>
        public void VoteOptionB()
        {
            Vote(1);
        }

        /// <summary>
        /// Public API: Close poll and apply result.
        /// </summary>
        public void ClosePollAndApply()
        {
            if (UseLocalMode)
                ClosePollLocal();
            else if (!IsServer)
            {
                // Immediate local feedback for non-host users.
                ClosePollLocal();
                ClosePollAndApplyServerRpc();
            }
            else
                ClosePollAndApplyServerRpc();
        }

        private void OpenPollLocal()
        {
            _localVotesA = 0;
            _localVotesB = 0;
            _localPollOpen = true;
            _hasLocalAppliedResult = false;
            EventLogger.LogEvent("Vote Opened");
            OnVoteReceived?.Invoke("Poll opened");
        }

        private void VoteLocal(int optionIndex)
        {
            if (!_localPollOpen || (optionIndex != 0 && optionIndex != 1)) return;
            // One vote per participant: replace previous
            if (_localVotesA > 0 || _localVotesB > 0)
            {
                if (_localVotesA > 0) _localVotesA--;
                else _localVotesB--;
            }
            if (optionIndex == 0) _localVotesA++;
            else _localVotesB++;
            string option = optionIndex == 0 ? optionA : optionB;
            EventLogger.LogVoteEvent("You", option, question);
            OnVoteReceived?.Invoke($"You voted {option}");
        }

        private void ClosePollLocal()
        {
            _localPollOpen = false;
            bool optionAWins = _localVotesA >= _localVotesB;
            _hasLocalAppliedResult = true;
            _localAppliedIrrigationEnabled = optionAWins;
            if (FarmSimulationManager.Instance != null)
                FarmSimulationManager.Instance.SetIrrigationEnabled(optionAWins);
            EventLogger.LogEvent(optionAWins ? "Irrigation Enabled" : "Irrigation Off");
            OnPollResultApplied?.Invoke(optionAWins);
        }

        /// <summary>
        /// Get list of voter names for Option A.
        /// </summary>
        public List<string> GetVotersForOptionA()
        {
            return GetVotersForOption(0);
        }

        /// <summary>
        /// Get list of voter names for Option B.
        /// </summary>
        public List<string> GetVotersForOptionB()
        {
            return GetVotersForOption(1);
        }

        private List<string> GetVotersForOption(int option)
        {
            var list = new List<string>();
            if (UseLocalMode)
            {
                if (option == 0 && _localVotesA > 0) list.Add("You");
                if (option == 1 && _localVotesB > 0) list.Add("You");
                return list;
            }
            if (!IsServer) return list;
            foreach (var kv in _votesByClientId)
            {
                if (kv.Value == option)
                    list.Add(GetPlayerName(kv.Key));
            }
            return list;
        }

        private string GetPlayerName(ulong clientId)
        {
            if (XRINetworkGameManager.Instance != null &&
                XRINetworkGameManager.Instance.TryGetPlayerByID(clientId, out var player))
            {
                string name = player.playerName;
                if (!string.IsNullOrEmpty(name) && name != "Player") return name;
            }
            return $"Player_{clientId}";
        }
    }
}
