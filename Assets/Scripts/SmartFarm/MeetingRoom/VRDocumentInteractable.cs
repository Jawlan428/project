using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SmartFarm.MeetingRoom
{
    /// <summary>
    /// A grabbable farming document in the meeting area.
    /// <para>
    /// At runtime this component:
    /// <list type="bullet">
    ///   <item>Configures the required Rigidbody, Collider and <see cref="XRGrabInteractable"/>.</item>
    ///   <item>Auto-builds a world-space TMP canvas with title, body, metric bars and recommendations.</item>
    ///   <item>Subscribes to <see cref="SmartFarmReportManager.OnReportUpdated"/> to refresh content live.</item>
    ///   <item>Snaps the document back to its rest pose when released, so the table stays tidy.</item>
    /// </list>
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VRDocumentInteractable : MonoBehaviour
    {
        [Header("Report")]
        [Tooltip("Report data driving the document UI.")]
        [SerializeField] private SmartFarmReportData report;

        [Header("Page Size")]
        [Tooltip("Width of the document page in metres. Sized for a small printed report that fits comfortably on a meeting table.")]
        [SerializeField] private float pageWidth = 0.14f;

        [Tooltip("Height of the document page in metres.")]
        [SerializeField] private float pageHeight = 0.20f;

        [Header("Physics")]
        [Tooltip("Mass of the document. Light objects feel more like paper.")]
        [SerializeField] private float documentMass = 0.08f;

        [Tooltip("Drag while in the air, helps the paper feel like paper.")]
        [SerializeField] private float linearDrag = 1.8f;

        [Tooltip("Angular drag while in the air.")]
        [SerializeField] private float angularDrag = 2.5f;

        [Header("Return To Table")]
        [Tooltip("If true, the document smoothly returns to its initial rest pose when released and stationary.")]
        [SerializeField] private bool returnToRestPose = true;

        [Tooltip("Time after release before the return-to-rest tween starts.")]
        [SerializeField] private float restDelay = 3f;

        [Tooltip("When the document is at rest on the table, freeze it as a kinematic body so it never falls off or rolls away.")]
        [SerializeField] private bool restAsKinematic = true;

        [Tooltip("On Start, raycast straight down from the document and snap it to the first surface hit (the table top).")]
        [SerializeField] private bool snapToSurfaceOnStart = true;

        [Tooltip("Maximum distance the start-up raycast looks downward for a surface.")]
        [SerializeField] private float snapRaycastDistance = 1.5f;

        [Tooltip("Layers the snap raycast considers as valid surfaces. Default = everything.")]
        [SerializeField] private LayerMask snapLayerMask = ~0;

        [Header("Inspect Mode (tap to read)")]
        [Tooltip("A short tap (select + release within this time) toggles inspect mode instead of a normal grab.")]
        [Range(0.05f, 0.6f)] [SerializeField] private float tapMaxDuration = 0.28f;

        [Tooltip("Maximum hand movement during a tap. Above this it's treated as a real grab.")]
        [Range(0.005f, 0.2f)] [SerializeField] private float tapMaxMovement = 0.04f;

        [Tooltip("Distance in metres in front of the camera the document floats during inspect mode.")]
        [Range(0.2f, 1f)] [SerializeField] private float inspectDistance = 0.42f;

        [Tooltip("Vertical offset from eye line during inspect mode (negative = below eye line).")]
        [Range(-0.4f, 0.4f)] [SerializeField] private float inspectVerticalOffset = -0.08f;

        [Tooltip("Page scale multiplier while in inspect mode — overrides the report's readingZoom.")]
        [Range(1f, 3f)] [SerializeField] private float inspectScale = 1.6f;

        [Tooltip("Lerp speed for inspect-mode follow.")]
        [Range(1f, 30f)] [SerializeField] private float inspectLerpSpeed = 10f;

        [Header("Highlight")]
        [Tooltip("Tint applied to the page when the document is hovered by a hand.")]
        [SerializeField] private Color hoverTint = new Color(1f, 0.95f, 0.65f, 1f);

        public SmartFarmReportData Report => report;
        public XRGrabInteractable Grab { get; private set; }
        public bool IsHeld => Grab != null && Grab.isSelected;

        private Rigidbody _rb;
        private Canvas _canvas;
        private RectTransform _pageRoot;
        private Image _pageBg;
        private Color _baseColor;
        private TMP_Text _titleText;
        private TMP_Text _subtitleText;
        private TMP_Text _bodyText;
        private TMP_Text _recsText;
        private RectTransform _metricsContainer;
        private readonly List<MetricBarUI> _metricBars = new List<MetricBarUI>();

        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private bool _restCaptured;
        private float _releasedAt = -1f;
        private Vector3 _basePageScale = Vector3.one;
        private BoxCollider _boxCollider;
        private bool _isInspecting;
        private bool _wasInspectingAtSelect;
        private float _selectStartTime;
        private Vector3 _selectStartInteractorPos;
        private Transform _cameraTransform;

        /// <summary>True while the document is floating in front of the user for reading.</summary>
        public bool IsInspecting => _isInspecting;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = documentMass;
            _rb.useGravity = !restAsKinematic;
#if UNITY_2023_2_OR_NEWER
            _rb.linearDamping = linearDrag;
            _rb.angularDamping = angularDrag;
#else
            _rb.drag = linearDrag;
            _rb.angularDrag = angularDrag;
#endif
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = restAsKinematic
                ? CollisionDetectionMode.Discrete
                : CollisionDetectionMode.ContinuousDynamic;
            _rb.isKinematic = restAsKinematic;

            EnsureCollider();
            EnsureGrabInteractable();
            BuildCanvas();
        }

        private void OnEnable()
        {
            if (Grab != null)
            {
                Grab.selectEntered.AddListener(OnGrabbed);
                Grab.selectExited.AddListener(OnReleased);
                Grab.hoverEntered.AddListener(OnHoverEnter);
                Grab.hoverExited.AddListener(OnHoverExit);
            }

            if (SmartFarmReportManager.Instance != null)
                SmartFarmReportManager.Instance.OnReportUpdated += OnReportUpdated;
        }

        private void OnDisable()
        {
            if (Grab != null)
            {
                Grab.selectEntered.RemoveListener(OnGrabbed);
                Grab.selectExited.RemoveListener(OnReleased);
                Grab.hoverEntered.RemoveListener(OnHoverEnter);
                Grab.hoverExited.RemoveListener(OnHoverExit);
            }

            if (SmartFarmReportManager.Instance != null)
                SmartFarmReportManager.Instance.OnReportUpdated -= OnReportUpdated;
        }

        private void Start()
        {
            if (Camera.main != null) _cameraTransform = Camera.main.transform;

            if (snapToSurfaceOnStart) SnapDownToSurface();

            if (!_restCaptured)
            {
                _restPosition = transform.position;
                _restRotation = transform.rotation;
                _restCaptured = true;
            }

            if (SmartFarmReportManager.Instance != null && report != null)
            {
                SmartFarmReportManager.Instance.Register(report);
                SmartFarmReportManager.Instance.Refresh(report);
            }

            RefreshUI();
        }

        /// <summary>
        /// Raycasts straight down from a point slightly above the document and snaps the
        /// transform to the first surface hit (typically the table top). The document's
        /// own colliders are ignored so it cannot hit itself.
        /// </summary>
        public bool SnapDownToSurface()
        {
            Vector3 origin = transform.position + Vector3.up * (snapRaycastDistance * 0.5f);
            var hits = Physics.RaycastAll(origin, Vector3.down, snapRaycastDistance, snapLayerMask, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return false;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.collider == null) continue;
                if (h.collider.transform == transform) continue;
                if (h.collider.transform.IsChildOf(transform)) continue;

                transform.position = h.point + Vector3.up * 0.003f;
                return true;
            }
            return false;
        }

        private void Update()
        {
            // Inspect mode owns the transform.
            if (_isInspecting)
            {
                UpdateInspectFollow();
                return;
            }

            if (IsHeld || _releasedAt < 0f) return;

            // After release, settle and then either kinematic-snap or tween back to rest.
#if UNITY_2023_2_OR_NEWER
            bool moving = _rb.linearVelocity.sqrMagnitude > 0.01f;
#else
            bool moving = _rb.velocity.sqrMagnitude > 0.01f;
#endif
            if (moving) return;

            // Once movement has stopped, lock as kinematic to keep it on the table.
            if (restAsKinematic && !_rb.isKinematic)
            {
                _rb.isKinematic = true;
                _rb.useGravity = false;
            }

            if (!returnToRestPose) return;
            if (Time.time - _releasedAt < restDelay) return;

            transform.position = Vector3.Lerp(transform.position, _restPosition, Time.deltaTime * 2f);
            transform.rotation = Quaternion.Slerp(transform.rotation, _restRotation, Time.deltaTime * 2f);
        }

        private void UpdateInspectFollow()
        {
            if (_cameraTransform == null && Camera.main != null) _cameraTransform = Camera.main.transform;
            if (_cameraTransform == null) return;

            Vector3 fwd = _cameraTransform.forward;
            Vector3 up = _cameraTransform.up;
            Vector3 targetPos = _cameraTransform.position + fwd * inspectDistance + up * inspectVerticalOffset;

            // The page's local +Y points "out of the page" because we built the canvas
            // rotated 90° on X. We want that to face the camera.
            Quaternion targetRot = Quaternion.LookRotation(fwd, up) * Quaternion.Euler(90f, 0f, 0f);

            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * inspectLerpSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * inspectLerpSpeed);

            // Smoothly grow the page to the inspect scale.
            if (_pageRoot != null)
            {
                Vector3 target = _basePageScale * inspectScale;
                _pageRoot.localScale = Vector3.Lerp(_pageRoot.localScale, target, Time.deltaTime * inspectLerpSpeed);
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Assign a different report at runtime (e.g. swapping documents).</summary>
        public void SetReport(SmartFarmReportData newReport)
        {
            report = newReport;
            if (SmartFarmReportManager.Instance != null && report != null)
            {
                SmartFarmReportManager.Instance.Register(report);
                SmartFarmReportManager.Instance.Refresh(report);
            }
            RefreshUI();
        }

        /// <summary>Apply a uniform multiplier to the world-space canvas (used by the reader system).</summary>
        public void SetReadingZoom(float zoom)
        {
            if (_pageRoot == null) return;
            float clamped = Mathf.Clamp(zoom, 0.8f, 2.5f);
            _pageRoot.localScale = _basePageScale * clamped;
        }

        /// <summary>
        /// Change the page size at runtime. Updates both the world-space canvas
        /// scale and the physics collider so existing documents in the scene can
        /// be resized without rebuilding.
        /// </summary>
        public void SetPageSize(float widthMeters, float heightMeters)
        {
            pageWidth = Mathf.Max(0.02f, widthMeters);
            pageHeight = Mathf.Max(0.02f, heightMeters);

            if (_boxCollider != null)
            {
                _boxCollider.size = new Vector3(pageWidth, 0.005f, pageHeight);
                _boxCollider.center = Vector3.zero;
            }

            if (_pageRoot != null)
            {
                _basePageScale = new Vector3(pageWidth / 512f, pageHeight / 720f, 1f);
                _pageRoot.localScale = _basePageScale;
            }
        }

        /// <summary>Captures the current pose as the new rest pose (called when placed in scene by the editor tool).</summary>
        public void CaptureRestPose()
        {
            _restPosition = transform.position;
            _restRotation = transform.rotation;
            _restCaptured = true;
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private void EnsureCollider()
        {
            _boxCollider = GetComponent<BoxCollider>();
            if (_boxCollider == null) _boxCollider = gameObject.AddComponent<BoxCollider>();
            _boxCollider.size = new Vector3(pageWidth, 0.005f, pageHeight);
            _boxCollider.center = Vector3.zero;
        }

        private void EnsureGrabInteractable()
        {
            Grab = GetComponent<XRGrabInteractable>();
            if (Grab == null) Grab = gameObject.AddComponent<XRGrabInteractable>();

            Grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            Grab.trackPosition = true;
            Grab.trackRotation = true;
            Grab.throwOnDetach = true;
            Grab.smoothPosition = true;
            Grab.smoothRotation = true;
        }

        private void BuildCanvas()
        {
            if (_canvas != null) return;

            var canvasGO = new GameObject("ReportCanvas");
            canvasGO.transform.SetParent(transform, false);
            canvasGO.transform.localPosition = new Vector3(0f, 0.003f, 0f);
            canvasGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            canvasGO.layer = gameObject.layer;

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            canvasGO.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 100f;

            _pageRoot = canvasGO.GetComponent<RectTransform>();
            _pageRoot.sizeDelta = new Vector2(512, 720);
            _basePageScale = new Vector3(pageWidth / 512f, pageHeight / 720f, 1f);
            _pageRoot.localScale = _basePageScale;

            _pageBg = CreateImage(_pageRoot, "PageBackground", Color.white);
            FillParent(_pageBg.rectTransform, 0);

            // Header bar
            var header = CreateImage(_pageRoot, "Header", new Color(0.15f, 0.45f, 0.25f));
            var headerRT = header.rectTransform;
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.anchoredPosition = Vector2.zero;
            headerRT.sizeDelta = new Vector2(0, 90);

            _titleText = CreateText(headerRT, "Title", "REPORT", 36, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);
            var titleRT = _titleText.rectTransform;
            titleRT.anchorMin = new Vector2(0, 0);
            titleRT.anchorMax = new Vector2(1, 1);
            titleRT.offsetMin = new Vector2(24, 30);
            titleRT.offsetMax = new Vector2(-24, -6);

            _subtitleText = CreateText(headerRT, "Subtitle", "summary", 18, FontStyles.Italic, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Left);
            var subRT = _subtitleText.rectTransform;
            subRT.anchorMin = new Vector2(0, 0);
            subRT.anchorMax = new Vector2(1, 0);
            subRT.pivot = new Vector2(0.5f, 0);
            subRT.offsetMin = new Vector2(24, 6);
            subRT.offsetMax = new Vector2(-24, 36);

            // Body
            _bodyText = CreateText(_pageRoot, "Body", "—", 20, FontStyles.Normal, new Color(0.1f, 0.12f, 0.1f), TextAlignmentOptions.TopLeft);
            var bodyRT = _bodyText.rectTransform;
            bodyRT.anchorMin = new Vector2(0, 1);
            bodyRT.anchorMax = new Vector2(1, 1);
            bodyRT.pivot = new Vector2(0.5f, 1);
            bodyRT.anchoredPosition = new Vector2(0, -100);
            bodyRT.sizeDelta = new Vector2(-48, 140);

            // Metrics container
            var metricsGO = new GameObject("Metrics", typeof(RectTransform), typeof(VerticalLayoutGroup));
            metricsGO.transform.SetParent(_pageRoot, false);
            _metricsContainer = metricsGO.GetComponent<RectTransform>();
            _metricsContainer.anchorMin = new Vector2(0, 1);
            _metricsContainer.anchorMax = new Vector2(1, 1);
            _metricsContainer.pivot = new Vector2(0.5f, 1);
            _metricsContainer.anchoredPosition = new Vector2(0, -260);
            _metricsContainer.sizeDelta = new Vector2(-48, 280);

            var vlg = metricsGO.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 10;
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            // Recommendations footer
            var footerBG = CreateImage(_pageRoot, "Footer", new Color(0.92f, 0.94f, 0.88f));
            var footerRT = footerBG.rectTransform;
            footerRT.anchorMin = new Vector2(0, 0);
            footerRT.anchorMax = new Vector2(1, 0);
            footerRT.pivot = new Vector2(0.5f, 0);
            footerRT.anchoredPosition = new Vector2(0, 0);
            footerRT.sizeDelta = new Vector2(0, 150);

            _recsText = CreateText(footerRT, "Recommendations", "—", 18, FontStyles.Normal, new Color(0.1f, 0.12f, 0.1f), TextAlignmentOptions.TopLeft);
            var recsRT = _recsText.rectTransform;
            recsRT.anchorMin = new Vector2(0, 0);
            recsRT.anchorMax = new Vector2(1, 1);
            recsRT.offsetMin = new Vector2(16, 12);
            recsRT.offsetMax = new Vector2(-16, -12);

            _baseColor = _pageBg.color;
        }

        private void RefreshUI()
        {
            if (_pageBg == null) return;
            if (report == null)
            {
                _titleText.text = "No Report Assigned";
                _subtitleText.text = "";
                _bodyText.text = "Assign a SmartFarmReportData asset.";
                _recsText.text = "";
                return;
            }

            _pageBg.color = report.pageColor;
            _baseColor = report.pageColor;

            _titleText.text = report.title;
            _subtitleText.text = report.subtitle;
            _bodyText.text = FormatBody(report.body);
            _recsText.text = "Recommendations:\n" + FormatBody(report.recommendations);

            // Header tint follows accent colour.
            var header = _titleText.transform.parent.GetComponent<Image>();
            if (header != null) header.color = report.accentColor;

            BuildMetricBars();
        }

        private static string FormatBody(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            var lines = raw.Split('\n');
            var sb = new System.Text.StringBuilder(raw.Length + 32);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimStart();
                if (line.StartsWith("!"))
                {
                    string clean = line.Substring(1).TrimStart();
                    sb.Append("<color=#B5302A><b>• ");
                    sb.Append(clean);
                    sb.Append("</b></color>");
                }
                else
                {
                    sb.Append(line);
                }
                if (i < lines.Length - 1) sb.Append('\n');
            }
            return sb.ToString();
        }

        private void BuildMetricBars()
        {
            int needed = report != null && report.metrics != null ? report.metrics.Count : 0;

            while (_metricBars.Count < needed)
            {
                _metricBars.Add(CreateMetricBar(_metricsContainer, _metricBars.Count));
            }

            for (int i = 0; i < _metricBars.Count; i++)
            {
                bool active = i < needed;
                _metricBars[i].Root.gameObject.SetActive(active);
                if (active) _metricBars[i].Apply(report.metrics[i]);
            }
        }

        private static MetricBarUI CreateMetricBar(RectTransform parent, int index)
        {
            var rowGO = new GameObject($"Metric_{index}", typeof(RectTransform), typeof(LayoutElement));
            rowGO.transform.SetParent(parent, false);
            var rowRT = rowGO.GetComponent<RectTransform>();
            rowRT.sizeDelta = new Vector2(0, 38);
            rowGO.GetComponent<LayoutElement>().preferredHeight = 38;

            var label = CreateText(rowRT, "Label", "Label", 18, FontStyles.Bold, new Color(0.1f, 0.12f, 0.1f), TextAlignmentOptions.Left);
            var lblRT = label.rectTransform;
            lblRT.anchorMin = new Vector2(0, 0);
            lblRT.anchorMax = new Vector2(0.45f, 1);
            lblRT.offsetMin = Vector2.zero;
            lblRT.offsetMax = Vector2.zero;

            var trackBG = CreateImage(rowRT, "Track", new Color(0.85f, 0.86f, 0.78f));
            var trackRT = trackBG.rectTransform;
            trackRT.anchorMin = new Vector2(0.46f, 0.18f);
            trackRT.anchorMax = new Vector2(0.88f, 0.82f);
            trackRT.offsetMin = Vector2.zero;
            trackRT.offsetMax = Vector2.zero;

            var fillImg = CreateImage(trackRT, "Fill", new Color(0.27f, 0.7f, 0.35f));
            var fillRT = fillImg.rectTransform;
            fillRT.anchorMin = new Vector2(0, 0);
            fillRT.anchorMax = new Vector2(0, 1);
            fillRT.pivot = new Vector2(0, 0.5f);
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            fillRT.sizeDelta = new Vector2(0, 0);

            var valueText = CreateText(rowRT, "Value", "0", 18, FontStyles.Bold, new Color(0.1f, 0.12f, 0.1f), TextAlignmentOptions.Right);
            var valRT = valueText.rectTransform;
            valRT.anchorMin = new Vector2(0.88f, 0);
            valRT.anchorMax = new Vector2(1f, 1f);
            valRT.offsetMin = Vector2.zero;
            valRT.offsetMax = Vector2.zero;

            return new MetricBarUI
            {
                Root = rowRT,
                Label = label,
                Fill = fillImg,
                Track = trackBG,
                Value = valueText
            };
        }

        private void OnReportUpdated(SmartFarmReportData updated)
        {
            if (updated == null || updated != report) return;
            RefreshUI();
        }

        // ── XR event handlers ────────────────────────────────────────────────

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            _releasedAt = -1f;

            // While held, the rigidbody must be dynamic so the XR grab can move it.
            if (_rb != null)
            {
                _rb.isKinematic = false;
                _rb.useGravity = false; // gravity off so paper doesn't drop the moment you let go.
            }

            _selectStartTime = Time.time;
            _selectStartInteractorPos = args.interactorObject.transform != null
                ? args.interactorObject.transform.position
                : transform.position;
            _wasInspectingAtSelect = _isInspecting;

            // If the user taps a document that's already being inspected, exit inspect mode
            // so they can grab and manipulate it freely.
            if (_isInspecting) ExitInspect();

            if (SmartFarmReportManager.Instance != null && report != null)
                SmartFarmReportManager.Instance.Refresh(report);
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            _releasedAt = Time.time;

            // Re-enable gravity briefly so a thrown document falls naturally.
            if (_rb != null) _rb.useGravity = !restAsKinematic;

            // Tap detection — if the user just briefly touched the document, treat it as
            // a "press to inspect" rather than a real grab.
            float duration = Time.time - _selectStartTime;
            Vector3 endPos = args.interactorObject.transform != null
                ? args.interactorObject.transform.position
                : transform.position;
            float moved = Vector3.Distance(_selectStartInteractorPos, endPos);

            bool wasTap = duration <= tapMaxDuration && moved <= tapMaxMovement;
            if (wasTap && !_wasInspectingAtSelect && !_isInspecting)
            {
                // Tap on a resting doc → fly it up to the face.
                EnterInspect();
            }
            // If they tapped while inspecting, we already exited in OnGrabbed — no action needed.
        }

        /// <summary>Toggle the document between rest pose and inspect-in-front-of-face mode.</summary>
        public void ToggleInspect()
        {
            if (_isInspecting) ExitInspect();
            else EnterInspect();
        }

        /// <summary>Float the document in front of the user's face for comfortable reading.</summary>
        public void EnterInspect()
        {
            _isInspecting = true;
            if (_rb != null)
            {
                _rb.isKinematic = true;
                _rb.useGravity = false;
#if UNITY_2023_2_OR_NEWER
                _rb.linearVelocity = Vector3.zero;
#else
                _rb.velocity = Vector3.zero;
#endif
                _rb.angularVelocity = Vector3.zero;
            }

            if (SmartFarmReportManager.Instance != null && report != null)
                SmartFarmReportManager.Instance.Refresh(report);
        }

        /// <summary>Return the document to its rest pose on the table.</summary>
        public void ExitInspect()
        {
            _isInspecting = false;
            if (_pageRoot != null) _pageRoot.localScale = _basePageScale;

            // Snap back to rest pose immediately (the rest pose was captured at Start).
            if (_restCaptured)
            {
                transform.position = _restPosition;
                transform.rotation = _restRotation;
            }

            if (_rb != null)
            {
                _rb.isKinematic = restAsKinematic;
                _rb.useGravity = !restAsKinematic;
            }
        }

        private void OnHoverEnter(HoverEnterEventArgs args)
        {
            if (_pageBg != null) _pageBg.color = hoverTint;
        }

        private void OnHoverExit(HoverExitEventArgs args)
        {
            if (_pageBg != null) _pageBg.color = _baseColor;
        }

        // ── UI helpers ────────────────────────────────────────────────────────

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static TMP_Text CreateText(Transform parent, string name, string text, float size, FontStyles style, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = align;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void FillParent(RectTransform rt, float padding)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        private class MetricBarUI
        {
            public RectTransform Root;
            public TMP_Text Label;
            public Image Fill;
            public Image Track;
            public TMP_Text Value;

            public void Apply(ReportMetric m)
            {
                Label.text = m.label;
                Fill.color = m.isCritical ? new Color(0.85f, 0.25f, 0.2f) : m.color;
                float pct = m.maxValue <= 0f ? 0f : Mathf.Clamp01(m.value / m.maxValue);
                Fill.rectTransform.anchorMax = new Vector2(pct, 1f);
                Value.text = $"{m.value:0.##}{m.unit}";
                Value.color = m.isCritical ? new Color(0.7f, 0.15f, 0.1f) : new Color(0.1f, 0.12f, 0.1f);
            }
        }
    }
}
