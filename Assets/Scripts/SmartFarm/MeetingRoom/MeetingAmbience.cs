using UnityEngine;

namespace SmartFarm.MeetingRoom
{
    /// <summary>
    /// Drives ambient audio + subtle visual life for the meeting area:
    /// <list type="bullet">
    ///   <item>Loops a low-volume office/farm ambience track.</item>
    ///   <item>Occasionally plays one of the supplied "discussion murmur" clips
    ///         at a random nearby seat to give the room a sense of habitation.</item>
    /// </list>
    /// All audio is optional — leave the clip references empty for a silent room.
    /// </summary>
    [DisallowMultipleComponent]
    public class MeetingAmbience : MonoBehaviour
    {
        [Header("Looping Ambience")]
        [Tooltip("Continuously playing background loop (room tone, distant farm sounds).")]
        [SerializeField] private AudioClip loopClip;

        [Range(0f, 1f)] [SerializeField] private float loopVolume = 0.15f;

        [Header("Discussion Murmur")]
        [Tooltip("Short clips of muted voices played randomly around the table.")]
        [SerializeField] private AudioClip[] murmurClips;

        [SerializeField] private float minDelay = 6f;
        [SerializeField] private float maxDelay = 16f;
        [Range(0f, 1f)] [SerializeField] private float murmurVolume = 0.35f;

        [Header("Emit Points")]
        [Tooltip("Locations where the murmur clips can play. Falls back to chairs registered with the meeting manager.")]
        [SerializeField] private Transform[] emitPoints;

        private AudioSource _loopSource;
        private AudioSource _murmurSource;
        private float _nextMurmurAt;

        private void Awake()
        {
            _loopSource = gameObject.AddComponent<AudioSource>();
            _loopSource.loop = true;
            _loopSource.playOnAwake = false;
            _loopSource.spatialBlend = 0f;
            _loopSource.volume = loopVolume;

            _murmurSource = gameObject.AddComponent<AudioSource>();
            _murmurSource.loop = false;
            _murmurSource.playOnAwake = false;
            _murmurSource.spatialBlend = 1f;
            _murmurSource.maxDistance = 6f;
            _murmurSource.rolloffMode = AudioRolloffMode.Linear;
            _murmurSource.volume = murmurVolume;
        }

        private void OnEnable()
        {
            if (loopClip != null)
            {
                _loopSource.clip = loopClip;
                _loopSource.Play();
            }
            ScheduleNextMurmur();
        }

        private void Update()
        {
            if (murmurClips == null || murmurClips.Length == 0) return;
            if (Time.time < _nextMurmurAt) return;
            PlayRandomMurmur();
            ScheduleNextMurmur();
        }

        private void ScheduleNextMurmur()
        {
            float delay = Random.Range(minDelay, maxDelay);
            _nextMurmurAt = Time.time + delay;
        }

        private void PlayRandomMurmur()
        {
            var clip = murmurClips[Random.Range(0, murmurClips.Length)];
            if (clip == null) return;

            Transform point = PickEmitPoint();
            if (point != null) _murmurSource.transform.position = point.position;

            _murmurSource.pitch = Random.Range(0.92f, 1.08f);
            _murmurSource.PlayOneShot(clip, murmurVolume);
        }

        private Transform PickEmitPoint()
        {
            if (emitPoints != null && emitPoints.Length > 0)
            {
                var t = emitPoints[Random.Range(0, emitPoints.Length)];
                if (t != null) return t;
            }
            if (MeetingInteractionManager.Instance != null)
            {
                var chairs = MeetingInteractionManager.Instance.Chairs;
                if (chairs != null && chairs.Count > 0)
                {
                    var c = chairs[Random.Range(0, chairs.Count)];
                    if (c != null) return c.transform;
                }
            }
            return transform;
        }
    }
}
