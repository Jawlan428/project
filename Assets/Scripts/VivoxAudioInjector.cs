using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Vivox;

/// <summary>
/// Routes Vivox participant audio through Unity AudioSources so it can be captured
/// by GameAudioCapture via the AudioListener.
/// 
/// Note: This requires Vivox to be configured to output through Unity's audio system.
/// If participants' voices aren't being captured, ensure your local microphone recording
/// is enabled to at least capture the host's voice.
/// </summary>
public class VivoxAudioInjector : MonoBehaviour
{
    [Header("Audio Settings")]
    [Tooltip("Volume multiplier for participant audio")]
    [Range(0f, 2f)]
    public float participantVolume = 1f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    /// <summary>
    /// Tracks active participants
    /// </summary>
    private Dictionary<string, ParticipantData> activeParticipants = new Dictionary<string, ParticipantData>();

    private bool isSubscribed = false;

    private class ParticipantData
    {
        public VivoxParticipant participant;
        public string participantId;
    }

    void Start()
    {
        SubscribeToVivoxEvents();
    }

    void OnEnable()
    {
        SubscribeToVivoxEvents();
    }

    void OnDisable()
    {
        UnsubscribeFromVivoxEvents();
    }

    void OnDestroy()
    {
        UnsubscribeFromVivoxEvents();
        activeParticipants.Clear();
    }

    void SubscribeToVivoxEvents()
    {
        if (isSubscribed) return;
        if (VivoxService.Instance == null) return;

        try
        {
            VivoxService.Instance.ParticipantAddedToChannel += OnParticipantAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel += OnParticipantRemoved;
            isSubscribed = true;
            LogDebug("Subscribed to Vivox participant events");

            // Handle already connected participants
            SetupExistingParticipants();
        }
        catch (System.Exception e)
        {
            LogDebug($"Failed to subscribe to Vivox events: {e.Message}");
        }
    }

    void UnsubscribeFromVivoxEvents()
    {
        if (!isSubscribed) return;
        if (VivoxService.Instance == null) return;

        try
        {
            VivoxService.Instance.ParticipantAddedToChannel -= OnParticipantAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemoved;
            isSubscribed = false;
            LogDebug("Unsubscribed from Vivox participant events");
        }
        catch (System.Exception e)
        {
            LogDebug($"Failed to unsubscribe from Vivox events: {e.Message}");
        }
    }

    void SetupExistingParticipants()
    {
        if (VivoxService.Instance == null || !VivoxService.Instance.IsLoggedIn) return;

        try
        {
            foreach (var channel in VivoxService.Instance.ActiveChannels)
            {
                foreach (var participant in channel.Value)
                {
                    if (!participant.IsSelf)
                    {
                        AddParticipant(participant);
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            LogDebug($"Error setting up existing participants: {e.Message}");
        }
    }

    void OnParticipantAdded(VivoxParticipant participant)
    {
        if (participant.IsSelf)
        {
            LogDebug("Local participant joined - skipping (own voice captured via microphone)");
            return;
        }

        AddParticipant(participant);
    }

    void OnParticipantRemoved(VivoxParticipant participant)
    {
        RemoveParticipant(participant.PlayerId);
    }

    void AddParticipant(VivoxParticipant participant)
    {
        string participantId = participant.PlayerId;

        if (activeParticipants.ContainsKey(participantId))
        {
            LogDebug($"Participant already tracked: {participantId}");
            return;
        }

        LogDebug($"Tracking participant: {participantId}");

        // Try to set up audio tap if available in this Vivox version
        TrySetupParticipantAudio(participant);

        var data = new ParticipantData
        {
            participant = participant,
            participantId = participantId
        };

        activeParticipants[participantId] = data;
        LogDebug($"Participant added: {participantId} (Total: {activeParticipants.Count})");
    }

    void TrySetupParticipantAudio(VivoxParticipant participant)
    {
        // Try to access ParticipantTapAudioSource if available
        // This property routes participant audio through a Unity AudioSource
        try
        {
            var audioSourceProperty = participant.GetType().GetProperty("ParticipantTapAudioSource");
            if (audioSourceProperty != null)
            {
                var audioSource = audioSourceProperty.GetValue(participant) as AudioSource;
                if (audioSource != null)
                {
                    audioSource.volume = participantVolume;
                    audioSource.spatialBlend = 0f; // 2D audio for recording
                    LogDebug($"Configured ParticipantTapAudioSource for {participant.PlayerId}");
                }
            }
        }
        catch (System.Exception e)
        {
            // ParticipantTapAudioSource not available in this version
            LogDebug($"ParticipantTapAudioSource not available: {e.Message}");
        }
    }

    void RemoveParticipant(string participantId)
    {
        if (!activeParticipants.ContainsKey(participantId))
        {
            return;
        }

        LogDebug($"Removing participant: {participantId}");
        activeParticipants.Remove(participantId);
    }

    /// <summary>
    /// Get the number of active participants being tracked
    /// </summary>
    public int GetActiveParticipantCount()
    {
        return activeParticipants.Count;
    }

    /// <summary>
    /// Check if Vivox audio injection is supported in this version
    /// </summary>
    public bool IsAudioInjectionSupported()
    {
        // Check if ParticipantTapAudioSource property exists
        try
        {
            var participantType = typeof(VivoxParticipant);
            return participantType.GetProperty("ParticipantTapAudioSource") != null;
        }
        catch
        {
            return false;
        }
    }

    void LogDebug(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"<color=#00FF88>[VivoxAudioInjector]</color> {message}");
        }
    }
}
