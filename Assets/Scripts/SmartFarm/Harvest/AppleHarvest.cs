using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SmartFarm.Harvest
{
    /// <summary>The ripeness state of an apple.</summary>
    public enum AppleHarvestState
    {
        ReadyToHarvest,
        NotReadyYet
    }

    /// <summary>
    /// Drives the harvest experience for a single apple:
    ///
    ///  • Holds a ripeness <see cref="AppleHarvestState"/> (Ready / Not Ready).
    ///  • Shows a floating world-space status label when the player gets close
    ///    (billboarded so it always faces the player).
    ///  • Adds an optional coloured glow (green = ready, red = not ready).
    ///  • Gates grabbing: only ripe apples can be picked. Picking an unripe apple
    ///    is rejected and the apple stays on the tree.
    ///
    /// Works alongside the existing <c>AppleGrabHandler</c> (which performs the
    /// physical detach / snap-back prevention) and the audit logging.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class AppleHarvest : MonoBehaviour
    {
        [Header("Harvest Status")]
        [Tooltip("Whether this apple is ripe enough to be harvested.")]
        [SerializeField] private AppleHarvestState state = AppleHarvestState.ReadyToHarvest;

        [Header("Proximity Label")]
        [Tooltip("Distance (metres) at which the floating status label appears.")]
        [SerializeField] private float showDistance = 2f;
        [Tooltip("Local height of the label above the apple's pivot (metres).")]
        [SerializeField] private float labelHeight = 0.22f;

        [Header("Visual Feedback (optional)")]
        [Tooltip("Add a soft coloured glow around the apple. Off by default - the floating label shows the status.")]
        [SerializeField] private bool useGlow = false;
        [SerializeField] private Color readyGlowColor = new Color(0.30f, 1f, 0.45f);
        [SerializeField] private Color notReadyGlowColor = new Color(1f, 0.30f, 0.25f);
        [SerializeField] private float glowRange = 0.55f;
        [SerializeField] private float glowIntensity = 2.2f;

        [Header("Audio (optional)")]
        [Tooltip("Played when a ripe apple is successfully harvested.")]
        [SerializeField] private AudioClip harvestSound;
        [Tooltip("Played when the player tries to pick an unripe apple.")]
        [SerializeField] private AudioClip warningSound;

        [Header("Grab Gating")]
        [Tooltip("If enabled, unripe apples cannot be picked and stay on the tree.")]
        [SerializeField] private bool blockUnripeGrab = true;

        // ── public API ───────────────────────────────────────────────────────
        public AppleHarvestState State => state;
        public bool IsReady => state == AppleHarvestState.ReadyToHarvest;
        public bool Harvested { get; private set; }

        /// <summary>Set the ripeness at runtime or from the editor setup tool.</summary>
        public void SetState(AppleHarvestState newState)
        {
            state = newState;
            ApplyVisualState();
        }

        // ── internals ────────────────────────────────────────────────────────
        private XRGrabInteractable _grab;
        private AppleHarvestLabel _label;
        private Light _glowLight;
        private AudioSource _audio;
        private Transform _cam;
        private bool _labelVisible;

        private void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
        }

        private void OnEnable()
        {
            if (_grab != null) _grab.selectEntered.AddListener(OnSelectEntered);
        }

        private void OnDisable()
        {
            if (_grab != null) _grab.selectEntered.RemoveListener(OnSelectEntered);
        }

        private void Start()
        {
            _audio = GetComponent<AudioSource>();
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 1f; // 3D positional audio for VR.
                _audio.minDistance = 0.5f;
                _audio.maxDistance = 12f;
            }

            _label = AppleHarvestLabel.Create(transform, labelHeight);
            _label.Hide();

            if (useGlow) CreateGlow();
            ApplyVisualState();
        }

        private void Update()
        {
            var cam = ResolvePlayerCamera();
            if (cam == null || _label == null) return;

            bool shouldShow = !Harvested && Vector3.Distance(transform.position, cam.position) <= showDistance;
            if (shouldShow != _labelVisible)
            {
                _labelVisible = shouldShow;
                if (shouldShow) _label.Show();
                else _label.Hide();
            }
        }

        /// <summary>
        /// Finds the player's camera robustly. In VR the head camera is not always
        /// tagged "MainCamera", so we fall back to any active camera in the scene.
        /// </summary>
        private Transform ResolvePlayerCamera()
        {
            if (_cam != null && _cam.gameObject.activeInHierarchy) return _cam;

            if (Camera.main != null) { _cam = Camera.main.transform; return _cam; }

            var any = Camera.allCamerasCount > 0 ? Camera.allCameras[0]
                                                 : FindFirstObjectByType<Camera>();
            _cam = any != null ? any.transform : null;
            return _cam;
        }

        // ── grab gating ──────────────────────────────────────────────────────
        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (Harvested) return;

            if (IsReady)
                Harvest();
            else if (blockUnripeGrab)
                RejectGrab(args);
        }

        private void Harvest()
        {
            Harvested = true;

            if (harvestSound != null && _audio != null)
                _audio.PlayOneShot(harvestSound);

            if (_label != null) _label.Hide();
            _labelVisible = false;
            if (_glowLight != null) _glowLight.enabled = false;
            // The physical detach / drop is handled by AppleGrabHandler.
        }

        private void RejectGrab(SelectEnterEventArgs args)
        {
            if (warningSound != null && _audio != null)
                _audio.PlayOneShot(warningSound);

            // Defer the cancel by one frame – cancelling selection from inside the
            // select callback is not safe. The apple stays kinematic on the tree in
            // the meantime, so it never actually leaves.
            StartCoroutine(ForceReleaseNextFrame());
        }

        private IEnumerator ForceReleaseNextFrame()
        {
            yield return null;

            if (_grab != null && _grab.isSelected && _grab.interactionManager != null)
                _grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)_grab);
        }

        // ── visuals ──────────────────────────────────────────────────────────
        private void CreateGlow()
        {
            var go = new GameObject("HarvestGlow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            _glowLight = go.AddComponent<Light>();
            _glowLight.type = LightType.Point;
            _glowLight.range = glowRange;
            _glowLight.intensity = glowIntensity;
            _glowLight.shadows = LightShadows.None;
            _glowLight.renderMode = LightRenderMode.ForceVertex;
        }

        private void ApplyVisualState()
        {
            if (_label != null) _label.SetStatus(IsReady);
            if (_glowLight != null)
                _glowLight.color = IsReady ? readyGlowColor : notReadyGlowColor;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Keep glow colour in sync while tweaking in the inspector at edit time.
            if (Application.isPlaying && _glowLight != null)
                _glowLight.color = IsReady ? readyGlowColor : notReadyGlowColor;
        }
#endif
    }
}
