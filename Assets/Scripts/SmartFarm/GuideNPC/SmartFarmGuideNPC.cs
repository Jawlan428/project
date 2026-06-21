using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SmartFarm.GuideNPC
{
    /// <summary>
    /// A friendly "Smart Farm Guide" NPC built around the Gardner Avatar (or any
    /// humanoid avatar).
    ///
    /// Behaviour:
    ///   1. WELCOME  – when the player gets close, the guide turns to face them,
    ///                 plays a greeting/wave, shows a floating "Welcome to Smart
    ///                 Farm VR" label and (optionally) plays a voice line.
    ///   2. MENU     – when the player selects the guide with an XR ray / poke
    ///                 interactor, a floating world-space menu opens with the four
    ///                 destination buttons.
    ///   3. GUIDE    – pressing a button sends the guide walking (WALK animation
    ///                 only, never run) along the NavMesh to the chosen target.
    ///                 On arrival it stops, faces the player and optionally points.
    ///
    /// Quest-friendly, dependency-light and tolerant of a missing Animator /
    /// missing animator parameters (it simply skips animation calls it can't make).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class SmartFarmGuideNPC : MonoBehaviour
    {
        // ── Movement (walking only) ───────────────────────────────────────────
        [Header("Movement (walk only — never run)")]
        [Tooltip("Walking speed in metres/second. Recommended 1.2 – 1.8.")]
        [Range(0.5f, 2.2f)] [SerializeField] private float walkSpeed = 1.4f;

        [Tooltip("How quickly the agent turns (deg/s). Lower = smoother.")]
        [SerializeField] private float angularSpeed = 220f;

        [Tooltip("Acceleration (m/s²). Moderate keeps it from snapping to top speed.")]
        [SerializeField] private float acceleration = 6f;

        [Tooltip("How close to the target the guide stops.")]
        [SerializeField] private float stoppingDistance = 1.5f;

        // ── Animator ──────────────────────────────────────────────────────────
        [Header("Animator")]
        [Tooltip("Animator on the avatar. Auto-found in children if left empty.")]
        [SerializeField] private Animator animator;

        [Tooltip("Float parameter driven by movement speed (drives Idle↔Walk). Leave as 'Speed' to match the generated controller.")]
        [SerializeField] private string speedParameter = "Speed";

        [Tooltip("Trigger fired when greeting the player. Leave empty if the avatar has no greeting clip.")]
        [SerializeField] private string greetTrigger = "Greet";

        [Tooltip("Trigger fired when pointing at a destination. Leave empty if the avatar has no pointing clip.")]
        [SerializeField] private string pointTrigger = "Point";

        [Tooltip("Damping applied to the Speed parameter so Idle↔Walk blends smoothly.")]
        [SerializeField] private float speedDamping = 0.12f;

        // ── Welcome behaviour ─────────────────────────────────────────────────
        [Header("Welcome")]
        [Tooltip("Player root / camera. Auto-found from the main camera if empty.")]
        [SerializeField] private Transform player;

        [Tooltip("The player must come within this distance to trigger the welcome.")]
        [SerializeField] private float welcomeRadius = 3.5f;

        [Tooltip("Welcome won't re-trigger until the player leaves and re-enters this larger radius.")]
        [SerializeField] private float welcomeResetRadius = 6f;

        [Tooltip("Headline shown on the floating label.")]
        [SerializeField] private string welcomeTitle = "Welcome to Smart Farm VR";

        [Tooltip("Sub-line shown under the headline.")]
        [SerializeField] private string welcomeSubtitle = "Tap me to choose where to go.";

        [Tooltip("Optional voice line played on welcome.")]
        [SerializeField] private AudioClip welcomeVoice;

        // ── Destinations ──────────────────────────────────────────────────────
        [Header("Destinations")]
        [Tooltip("The four farm areas. If left empty, the guide auto-collects every GuideDestination in the scene on Start.")]
        [SerializeField] private List<GuideDestinationEntry> destinations = new List<GuideDestinationEntry>();

        [Header("Interaction & Menu")]
        [Tooltip("XR Simple Interactable used to open the menu. Auto-added if missing.")]
        [SerializeField] private XRSimpleInteractable interactable;

        [Tooltip("The floating button menu. Auto-found / auto-built if empty.")]
        [SerializeField] private GuideMenuUI menu;

        [Tooltip("Open the menu automatically right after welcoming the player.")]
        [SerializeField] private bool openMenuAfterWelcome = false;

        // ── Floating label ────────────────────────────────────────────────────
        [Header("Floating Label")]
        [Tooltip("Height above the guide's origin for the welcome/status label.")]
        [SerializeField] private float labelHeight = 2.1f;

        [Tooltip("Build a world-space status label automatically at runtime.")]
        [SerializeField] private bool buildLabel = true;

        // ── Audio ─────────────────────────────────────────────────────────────
        [Header("Audio")]
        [SerializeField] private AudioSource voiceSource;

        // ── State ─────────────────────────────────────────────────────────────
        public enum GuideState { Idle, Welcoming, Walking, Arrived }
        public GuideState State { get; private set; } = GuideState.Idle;

        public IReadOnlyList<GuideDestinationEntry> Destinations => destinations;

        private NavMeshAgent _agent;
        private GuideStatusLabel _label;
        private bool _hasWelcomed;
        private GuideDestinationEntry _currentTrip;
        private bool _arriving;
        private float _arriveFaceTimer;

        // Cached animator parameter availability so we never throw on missing params.
        private bool _hasSpeed, _hasGreet, _hasPoint;
        private int _speedHash, _greetHash, _pointHash;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            ConfigureAgent();

            if (animator == null) animator = GetComponentInChildren<Animator>();
            CacheAnimatorParameters();

            if (interactable == null) interactable = GetComponent<XRSimpleInteractable>();
            if (interactable == null) interactable = gameObject.AddComponent<XRSimpleInteractable>();

            if (voiceSource == null)
            {
                voiceSource = GetComponent<AudioSource>();
                if (voiceSource == null) voiceSource = gameObject.AddComponent<AudioSource>();
            }
            voiceSource.playOnAwake = false;
            voiceSource.spatialBlend = 1f; // 3D so it sounds like it comes from the guide.
        }

        private void OnEnable()
        {
            if (interactable != null)
                interactable.selectEntered.AddListener(OnInteractableSelected);
        }

        private void OnDisable()
        {
            if (interactable != null)
                interactable.selectEntered.RemoveListener(OnInteractableSelected);
        }

        private void Start()
        {
            if (player == null && Camera.main != null) player = Camera.main.transform;

            CollectDestinationsFromSceneIfEmpty();

            if (buildLabel)
            {
                _label = GuideStatusLabel.Create(transform, labelHeight);
                _label.Hide();
            }

            if (menu == null) menu = GetComponentInChildren<GuideMenuUI>(true);
            if (menu == null) menu = FindFirstObjectByType<GuideMenuUI>();
            if (menu != null)
            {
                menu.Initialize(this);
                menu.Hide();
            }

            SetSpeedParameter(0f, instant: true);
        }

        private void Update()
        {
            UpdateWelcomeDetection();
            UpdateLocomotionAnimation();
            UpdateArrival();

            if (_label != null) _label.FaceCamera(player);
        }

        // ── Agent / animator config ───────────────────────────────────────────

        private void ConfigureAgent()
        {
            _agent.speed = walkSpeed;            // walking speed only
            _agent.angularSpeed = angularSpeed;  // smooth turning
            _agent.acceleration = acceleration;  // moderate
            _agent.stoppingDistance = stoppingDistance;
            _agent.autoBraking = true;
            _agent.updateRotation = true;        // face the way it walks
            _agent.updatePosition = true;
        }

        private void CacheAnimatorParameters()
        {
            _hasSpeed = _hasGreet = _hasPoint = false;
            if (animator == null || animator.runtimeAnimatorController == null) return;

            foreach (var p in animator.parameters)
            {
                if (!string.IsNullOrEmpty(speedParameter) && p.name == speedParameter && p.type == AnimatorControllerParameterType.Float)
                {
                    _hasSpeed = true; _speedHash = p.nameHash;
                }
                else if (!string.IsNullOrEmpty(greetTrigger) && p.name == greetTrigger && p.type == AnimatorControllerParameterType.Trigger)
                {
                    _hasGreet = true; _greetHash = p.nameHash;
                }
                else if (!string.IsNullOrEmpty(pointTrigger) && p.name == pointTrigger && p.type == AnimatorControllerParameterType.Trigger)
                {
                    _hasPoint = true; _pointHash = p.nameHash;
                }
            }
        }

        // ── Welcome ───────────────────────────────────────────────────────────

        private void UpdateWelcomeDetection()
        {
            if (player == null) return;

            float dist = Vector3.Distance(FlatPosition(transform.position), FlatPosition(player.position));

            if (!_hasWelcomed && dist <= welcomeRadius && State == GuideState.Idle)
            {
                Welcome();
            }
            else if (_hasWelcomed && dist > welcomeResetRadius && State != GuideState.Walking)
            {
                // Player wandered off — allow the welcome to play again next time.
                _hasWelcomed = false;
                if (_label != null) _label.Hide();
            }

            // While idle / welcoming, gently face the player.
            if ((State == GuideState.Idle || State == GuideState.Welcoming || State == GuideState.Arrived))
                FacePlayer(smooth: true);
        }

        /// <summary>Run the welcome sequence: face the player, wave, show label, speak.</summary>
        public void Welcome()
        {
            _hasWelcomed = true;
            State = GuideState.Welcoming;

            FireTrigger(_hasGreet, _greetHash);

            if (_label != null) _label.Show(welcomeTitle, welcomeSubtitle);

            if (welcomeVoice != null && voiceSource != null)
                voiceSource.PlayOneShot(welcomeVoice);

            if (openMenuAfterWelcome && menu != null)
                menu.Show();

            CancelInvoke(nameof(EndWelcome));
            Invoke(nameof(EndWelcome), 2.5f);
        }

        private void EndWelcome()
        {
            if (State == GuideState.Welcoming) State = GuideState.Idle;
        }

        // ── Interaction → open menu ───────────────────────────────────────────

        private void OnInteractableSelected(SelectEnterEventArgs args)
        {
            ToggleMenu();
        }

        public void ToggleMenu()
        {
            if (menu == null) return;
            if (menu.IsVisible) menu.Hide();
            else
            {
                FacePlayer(smooth: false);
                menu.Show();
            }
        }

        // ── Guide movement ────────────────────────────────────────────────────

        /// <summary>Walk the guide to the destination registered for the given area.</summary>
        public void GoTo(GuideArea area)
        {
            var entry = FindDestination(area);
            if (entry == null || entry.target == null)
            {
                Debug.LogWarning($"[SmartFarmGuide] No destination transform assigned for {area}.");
                if (_label != null) _label.Show("Destination not set", area + " has no target.");
                return;
            }
            GoTo(entry);
        }

        /// <summary>Walk the guide to an explicit destination entry.</summary>
        public void GoTo(GuideDestinationEntry entry)
        {
            if (entry == null || entry.target == null) return;
            if (!EnsureOnNavMesh())
            {
                Debug.LogWarning("[SmartFarmGuide] Guide is not on a baked NavMesh — cannot walk. Bake the NavMesh and place the guide on it.");
                if (_label != null) _label.Show("Cannot move", "NavMesh not found under the guide.");
                return;
            }

            _currentTrip = entry;
            _arriving = false;

            if (menu != null) menu.Hide();

            _agent.isStopped = false;
            _agent.stoppingDistance = stoppingDistance;
            _agent.SetDestination(entry.target.position);
            State = GuideState.Walking;

            if (_label != null) _label.Show("Follow me", "Walking to " + entry.label + ".");
        }

        /// <summary>Walk to an arbitrary world transform (used by custom triggers).</summary>
        public void GoTo(Transform target)
        {
            if (target == null) return;
            GoTo(new GuideDestinationEntry(GuideArea.CropField, target.name, target));
        }

        public void Stop()
        {
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
            State = GuideState.Idle;
        }

        private void UpdateArrival()
        {
            if (State != GuideState.Walking || _agent == null || !_agent.isOnNavMesh) return;
            if (_agent.pathPending) return;

            if (_agent.remainingDistance <= _agent.stoppingDistance &&
                (!_agent.hasPath || _agent.velocity.sqrMagnitude < 0.02f))
            {
                Arrive();
            }
        }

        private void Arrive()
        {
            State = GuideState.Arrived;
            _agent.isStopped = true;

            // Face whatever the destination asked us to look at, else the player.
            var dest = _currentTrip != null ? _currentTrip.target : null;
            var marker = dest != null ? dest.GetComponent<GuideDestination>() : null;

            if (marker != null && marker.LookAtOnArrival != null)
            {
                FaceTowards(marker.LookAtOnArrival.position, smooth: false);
                FireTrigger(_hasPoint, _pointHash);
            }
            else
            {
                FacePlayer(smooth: false);
            }

            string area = _currentTrip != null ? _currentTrip.label : "the area";
            if (_label != null) _label.Show("We're here", "This is the " + area + ".");

            // After a moment, turn back to face the player and return to idle.
            _arriving = true;
            _arriveFaceTimer = 2.5f;
        }

        // ── Animation driving ─────────────────────────────────────────────────

        private void UpdateLocomotionAnimation()
        {
            if (animator == null) return;

            float speed = (_agent != null && _agent.isOnNavMesh) ? _agent.velocity.magnitude : 0f;

            // Normalise so the walk blend sits at ~1 while moving, 0 when idle.
            float normalized = walkSpeed > 0.01f ? Mathf.Clamp01(speed / walkSpeed) : 0f;
            SetSpeedParameter(normalized, instant: false);

            if (_arriving)
            {
                _arriveFaceTimer -= Time.deltaTime;
                FacePlayer(smooth: true);
                if (_arriveFaceTimer <= 0f)
                {
                    _arriving = false;
                    State = GuideState.Idle;
                    if (_label != null && State == GuideState.Idle) _label.Hide();
                }
            }
        }

        private void SetSpeedParameter(float value, bool instant)
        {
            if (animator == null || !_hasSpeed) return;
            if (instant || speedDamping <= 0f)
                animator.SetFloat(_speedHash, value);
            else
                animator.SetFloat(_speedHash, value, speedDamping, Time.deltaTime);
        }

        private void FireTrigger(bool exists, int hash)
        {
            if (animator != null && exists) animator.SetTrigger(hash);
        }

        // ── Facing helpers ────────────────────────────────────────────────────

        private void FacePlayer(bool smooth)
        {
            if (player == null) return;
            FaceTowards(player.position, smooth);
        }

        private void FaceTowards(Vector3 worldPoint, bool smooth)
        {
            Vector3 dir = FlatPosition(worldPoint) - FlatPosition(transform.position);
            if (dir.sqrMagnitude < 0.0001f) return;

            Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = smooth
                ? Quaternion.Slerp(transform.rotation, target, Time.deltaTime * 6f)
                : target;
        }

        private static Vector3 FlatPosition(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // ── Destinations helpers ──────────────────────────────────────────────

        private GuideDestinationEntry FindDestination(GuideArea area)
        {
            for (int i = 0; i < destinations.Count; i++)
                if (destinations[i] != null && destinations[i].area == area)
                    return destinations[i];
            return null;
        }

        private void CollectDestinationsFromSceneIfEmpty()
        {
            bool hasAnyTarget = false;
            for (int i = 0; i < destinations.Count; i++)
                if (destinations[i] != null && destinations[i].target != null) { hasAnyTarget = true; break; }

            if (hasAnyTarget) return;

            var markers = FindObjectsByType<GuideDestination>(FindObjectsSortMode.None);
            if (markers == null || markers.Length == 0) return;

            destinations.Clear();
            foreach (var m in markers)
                destinations.Add(new GuideDestinationEntry(m.Area, m.Label, m.transform));
        }

        private bool EnsureOnNavMesh()
        {
            if (_agent == null) return false;
            if (_agent.isOnNavMesh) return true;

            // Try to snap the agent onto the nearest NavMesh point.
            if (NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas))
            {
                _agent.Warp(hit.position);
                return _agent.isOnNavMesh;
            }
            return false;
        }

        // ── Editor gizmos ─────────────────────────────────────────────────────
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, welcomeRadius);
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, welcomeResetRadius);
        }
#endif
    }
}
