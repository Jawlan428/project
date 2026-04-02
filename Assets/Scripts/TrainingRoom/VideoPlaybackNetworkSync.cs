using Unity.Netcode;
using UnityEngine;

namespace TrainingRoom
{
    /// <summary>
    /// Synchronises Training Room video playback across all connected players
    /// using Unity Netcode for GameObjects.
    ///
    /// Architecture:
    ///   - The Host (server) is the authority for video state.
    ///   - Clients send RPC requests to the host; host applies them and broadcasts state.
    ///   - Late-joining clients receive the current state immediately via NetworkVariables.
    ///
    /// Attach this to the same GameObject as (or a child of) the NetworkObject for
    /// the training room. The TrainingRoomManager must be assigned in the inspector.
    ///
    /// Usage from TrainingRoomTabletPage:
    ///   if (networkSync.IsNetworkReady) networkSync.RequestPlay(index);
    ///   else trainingRoomManager.PlayVideoAtIndex(index);
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class VideoPlaybackNetworkSync : NetworkBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [SerializeField] private TrainingRoomManager trainingRoomManager;

        [Tooltip("How often (seconds) the host broadcasts the current seek position to keep clients in sync")]
        [SerializeField] private float seekSyncInterval = 5f;

        // ── Network state (authority = host) ─────────────────────────────────

        private readonly NetworkVariable<int>   _videoIndex   = new(-1,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool>  _isPlaying    = new(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _seekPosition = new(0f,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // ── Internal ──────────────────────────────────────────────────────────

        private float _seekSyncTimer;

        /// <summary>True when the NetworkObject is spawned and the network is running.</summary>
        public bool IsNetworkReady => IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        // ── Netcode lifecycle ─────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _videoIndex.OnValueChanged   += OnVideoIndexChanged;
            _isPlaying.OnValueChanged    += OnIsPlayingChanged;
            _seekPosition.OnValueChanged += OnSeekPositionChanged;

            // Late-joining client: apply whatever state the host already has
            if (!IsServer && _videoIndex.Value >= 0)
            {
                trainingRoomManager?.PlayVideoAtIndex(_videoIndex.Value);
                if (!_isPlaying.Value)
                    trainingRoomManager?.Pause();
                trainingRoomManager?.SeekToNormalized(_seekPosition.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            _videoIndex.OnValueChanged   -= OnVideoIndexChanged;
            _isPlaying.OnValueChanged    -= OnIsPlayingChanged;
            _seekPosition.OnValueChanged -= OnSeekPositionChanged;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsServer || !_isPlaying.Value || trainingRoomManager == null) return;

            _seekSyncTimer += Time.deltaTime;
            if (_seekSyncTimer >= seekSyncInterval)
            {
                _seekSyncTimer = 0f;
                _seekPosition.Value = trainingRoomManager.GetNormalizedProgress();
            }
        }

        // ── Public request API (called by clients / local player) ─────────────

        /// <summary>Any player requests playback of the given library index.</summary>
        public void RequestPlay(int index)
        {
            if (IsServer) ApplyPlay(index);
            else RequestPlayServerRpc(index);
        }

        /// <summary>Any player requests pause.</summary>
        public void RequestPause()
        {
            if (IsServer) ApplyPause();
            else RequestPauseServerRpc();
        }

        /// <summary>Any player requests resume.</summary>
        public void RequestResume()
        {
            if (IsServer) ApplyResume();
            else RequestResumeServerRpc();
        }

        /// <summary>Any player requests stop.</summary>
        public void RequestStop()
        {
            if (IsServer) ApplyStop();
            else RequestStopServerRpc();
        }

        // ── ServerRPCs ────────────────────────────────────────────────────────

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestPlayServerRpc(int index) => ApplyPlay(index);

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestPauseServerRpc() => ApplyPause();

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestResumeServerRpc() => ApplyResume();

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestStopServerRpc() => ApplyStop();

        // ── Server-side state application ─────────────────────────────────────

        private void ApplyPlay(int index)
        {
            if (trainingRoomManager == null) return;
            _videoIndex.Value   = index;
            _isPlaying.Value    = true;
            _seekPosition.Value = 0f;
            _seekSyncTimer      = 0f;
            trainingRoomManager.PlayVideoAtIndex(index);
        }

        private void ApplyPause()
        {
            if (!_isPlaying.Value) return;
            _isPlaying.Value    = false;
            _seekPosition.Value = trainingRoomManager?.GetNormalizedProgress() ?? 0f;
            trainingRoomManager?.Pause();
        }

        private void ApplyResume()
        {
            if (_isPlaying.Value) return;
            _isPlaying.Value = true;
            trainingRoomManager?.Resume();
        }

        private void ApplyStop()
        {
            _isPlaying.Value    = false;
            _seekPosition.Value = 0f;
            trainingRoomManager?.Stop();
        }

        // ── NetworkVariable change handlers (client-side) ─────────────────────

        private void OnVideoIndexChanged(int previous, int current)
        {
            if (IsServer) return; // host already applied locally
            if (current < 0) return;
            trainingRoomManager?.PlayVideoAtIndex(current);
        }

        private void OnIsPlayingChanged(bool previous, bool current)
        {
            if (IsServer) return;
            if (current)
                trainingRoomManager?.Resume();
            else
                trainingRoomManager?.Pause();
        }

        private void OnSeekPositionChanged(float previous, float current)
        {
            if (IsServer) return;
            // Only apply if seek difference is significant (> 3 seconds equivalent)
            float diff = Mathf.Abs(current - (trainingRoomManager?.GetNormalizedProgress() ?? 0f));
            if (diff > 0.05f)
                trainingRoomManager?.SeekToNormalized(current);
        }
    }
}
