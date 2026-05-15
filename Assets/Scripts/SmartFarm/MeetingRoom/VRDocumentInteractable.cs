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

        [Header("Paper Look")]
        [Tooltip("Once populated, ignore live-data updates so the document reads like a printed snapshot " +
                 "instead of a live screen. The initial values are still pulled from SmartFarmReportManager on Start.")]
        [SerializeField] private bool freezeContent = true;

        [Tooltip("Author shown above the signature line on the printed report.")]
        [SerializeField] private string reportAuthor = "Farm Operations";

        [Tooltip("Department / company name shown in the letterhead.")]
        [SerializeField] private string organizationName = "SMART FARM AGRO";

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
        private TMP_Text _dateText;
        private TMP_Text _refText;
        private TMP_Text _orgText;
        private TMP_Text _metricsText;
        private TMP_Text _authorText;
        private Image    _headerRule;
        private Image    _footerRule;
        private bool     _hasBeenPopulated;

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

            // The canvas was built with localRotation = Euler(90, 0, 0) so the page
            // lies flat on the table when the document is upright. To billboard it
            // toward the camera with text upright we want the canvas's WORLD
            // rotation to equal LookRotation(camera.forward, camera.up). So the
            // document's rotation must "undo" the canvas's local 90° tilt:
            //   doc.rotation = LookRotation(fwd, up) * Euler(-90, 0, 0)
            // Using +90 instead of -90 (the previous version) results in the page
            // being flipped 180° — exactly the "upside-down" symptom.
            Quaternion targetRot = Quaternion.LookRotation(fwd, up) * Quaternion.Euler(-90f, 0f, 0f);

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

            // ── Page paper (cream tint, slight inset margin frame) ───────────
            _pageBg = CreateImage(_pageRoot, "PageBackground", new Color(0.96f, 0.94f, 0.86f));
            FillParent(_pageBg.rectTransform, 0);

            // Subtle thin border to suggest a printed margin
            var border = CreateImage(_pageRoot, "Margin", new Color(0.78f, 0.72f, 0.55f, 0.55f));
            var borderRT = border.rectTransform;
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.offsetMin = new Vector2(20, 20);
            borderRT.offsetMax = new Vector2(-20, -20);
            var borderHole = CreateImage(border.rectTransform, "MarginHole", _pageBg.color);
            FillParent(borderHole.rectTransform, 2);

            // ── Letterhead (organisation + date + ref number) ────────────────
            _orgText = CreateText(_pageRoot, "Letterhead",
                organizationName,
                16, FontStyles.Bold | FontStyles.UpperCase,
                new Color(0.20f, 0.30f, 0.20f),
                TextAlignmentOptions.Left);
            var orgRT = _orgText.rectTransform;
            orgRT.anchorMin = new Vector2(0, 1);
            orgRT.anchorMax = new Vector2(0.55f, 1);
            orgRT.pivot     = new Vector2(0, 1);
            orgRT.anchoredPosition = new Vector2(32, -32);
            orgRT.sizeDelta = new Vector2(0, 24);

            _dateText = CreateText(_pageRoot, "DateText",
                "DATE: " + System.DateTime.Now.ToString("yyyy-MM-dd"),
                12, FontStyles.Normal,
                new Color(0.25f, 0.20f, 0.15f),
                TextAlignmentOptions.Right);
            var dateRT = _dateText.rectTransform;
            dateRT.anchorMin = new Vector2(0.55f, 1);
            dateRT.anchorMax = new Vector2(1, 1);
            dateRT.pivot     = new Vector2(1, 1);
            dateRT.anchoredPosition = new Vector2(-32, -32);
            dateRT.sizeDelta = new Vector2(0, 18);

            _refText = CreateText(_pageRoot, "RefText",
                "REF / —",
                12, FontStyles.Normal,
                new Color(0.25f, 0.20f, 0.15f),
                TextAlignmentOptions.Right);
            var refRT = _refText.rectTransform;
            refRT.anchorMin = new Vector2(0.55f, 1);
            refRT.anchorMax = new Vector2(1, 1);
            refRT.pivot     = new Vector2(1, 1);
            refRT.anchoredPosition = new Vector2(-32, -52);
            refRT.sizeDelta = new Vector2(0, 18);

            // Top horizontal rule (under the letterhead)
            _headerRule = CreateImage(_pageRoot, "HeaderRule", new Color(0.20f, 0.18f, 0.14f, 0.85f));
            var headerRuleRT = _headerRule.rectTransform;
            headerRuleRT.anchorMin = new Vector2(0, 1);
            headerRuleRT.anchorMax = new Vector2(1, 1);
            headerRuleRT.pivot     = new Vector2(0.5f, 1);
            headerRuleRT.anchoredPosition = new Vector2(0, -80);
            headerRuleRT.sizeDelta = new Vector2(-64, 2);

            // ── Title block ─────────────────────────────────────────────────
            _titleText = CreateText(_pageRoot, "Title",
                "REPORT",
                32, FontStyles.Bold | FontStyles.UpperCase,
                new Color(0.12f, 0.12f, 0.12f),
                TextAlignmentOptions.Center);
            var titleRT = _titleText.rectTransform;
            titleRT.anchorMin = new Vector2(0, 1);
            titleRT.anchorMax = new Vector2(1, 1);
            titleRT.pivot     = new Vector2(0.5f, 1);
            titleRT.anchoredPosition = new Vector2(0, -94);
            titleRT.sizeDelta = new Vector2(-64, 46);

            _subtitleText = CreateText(_pageRoot, "Subtitle",
                "summary",
                15, FontStyles.Italic,
                new Color(0.35f, 0.30f, 0.20f),
                TextAlignmentOptions.Center);
            var subRT = _subtitleText.rectTransform;
            subRT.anchorMin = new Vector2(0, 1);
            subRT.anchorMax = new Vector2(1, 1);
            subRT.pivot     = new Vector2(0.5f, 1);
            subRT.anchoredPosition = new Vector2(0, -140);
            subRT.sizeDelta = new Vector2(-64, 22);

            // Title underline rule
            var titleRule = CreateImage(_pageRoot, "TitleRule", new Color(0.20f, 0.18f, 0.14f, 0.60f));
            var titleRuleRT = titleRule.rectTransform;
            titleRuleRT.anchorMin = new Vector2(0, 1);
            titleRuleRT.anchorMax = new Vector2(1, 1);
            titleRuleRT.pivot     = new Vector2(0.5f, 1);
            titleRuleRT.anchoredPosition = new Vector2(0, -168);
            titleRuleRT.sizeDelta = new Vector2(-160, 1);

            // ── Body paragraph (justified prose) ─────────────────────────────
            _bodyText = CreateText(_pageRoot, "Body",
                "—",
                15, FontStyles.Normal,
                new Color(0.10f, 0.10f, 0.10f),
                TextAlignmentOptions.TopJustified);
            var bodyRT = _bodyText.rectTransform;
            bodyRT.anchorMin = new Vector2(0, 1);
            bodyRT.anchorMax = new Vector2(1, 1);
            bodyRT.pivot     = new Vector2(0.5f, 1);
            bodyRT.anchoredPosition = new Vector2(0, -188);
            bodyRT.sizeDelta = new Vector2(-72, 130);

            // ── Measurements section header + dotted-leader metric table ────
            var measLabel = CreateText(_pageRoot, "MeasurementsHeading",
                "MEASUREMENTS",
                12, FontStyles.Bold | FontStyles.UpperCase,
                new Color(0.25f, 0.20f, 0.10f),
                TextAlignmentOptions.Left);
            var measLabelRT = measLabel.rectTransform;
            measLabelRT.anchorMin = new Vector2(0, 1);
            measLabelRT.anchorMax = new Vector2(1, 1);
            measLabelRT.pivot     = new Vector2(0.5f, 1);
            measLabelRT.anchoredPosition = new Vector2(0, -324);
            measLabelRT.sizeDelta = new Vector2(-72, 16);

            _metricsText = CreateText(_pageRoot, "MetricsBody",
                "—",
                14, FontStyles.Normal,
                new Color(0.10f, 0.10f, 0.10f),
                TextAlignmentOptions.TopLeft);
            // Use a monospaced font feel via fixed-width digits + tabs to help dots align
            _metricsText.enableWordWrapping = false;
            _metricsText.overflowMode       = TextOverflowModes.Truncate;
            var metricsBodyRT = _metricsText.rectTransform;
            metricsBodyRT.anchorMin = new Vector2(0, 1);
            metricsBodyRT.anchorMax = new Vector2(1, 1);
            metricsBodyRT.pivot     = new Vector2(0.5f, 1);
            metricsBodyRT.anchoredPosition = new Vector2(0, -344);
            metricsBodyRT.sizeDelta = new Vector2(-72, 150);

            // ── Recommendations section ─────────────────────────────────────
            var recLabel = CreateText(_pageRoot, "RecommendationsHeading",
                "RECOMMENDATIONS",
                12, FontStyles.Bold | FontStyles.UpperCase,
                new Color(0.25f, 0.20f, 0.10f),
                TextAlignmentOptions.Left);
            var recLabelRT = recLabel.rectTransform;
            recLabelRT.anchorMin = new Vector2(0, 1);
            recLabelRT.anchorMax = new Vector2(1, 1);
            recLabelRT.pivot     = new Vector2(0.5f, 1);
            recLabelRT.anchoredPosition = new Vector2(0, -500);
            recLabelRT.sizeDelta = new Vector2(-72, 16);

            _recsText = CreateText(_pageRoot, "Recommendations",
                "—",
                14, FontStyles.Normal,
                new Color(0.10f, 0.10f, 0.10f),
                TextAlignmentOptions.TopLeft);
            var recsRT = _recsText.rectTransform;
            recsRT.anchorMin = new Vector2(0, 1);
            recsRT.anchorMax = new Vector2(1, 1);
            recsRT.pivot     = new Vector2(0.5f, 1);
            recsRT.anchoredPosition = new Vector2(0, -520);
            recsRT.sizeDelta = new Vector2(-72, 120);

            // ── Footer: signature line + author ─────────────────────────────
            _footerRule = CreateImage(_pageRoot, "FooterRule", new Color(0.20f, 0.18f, 0.14f, 0.55f));
            var footerRuleRT = _footerRule.rectTransform;
            footerRuleRT.anchorMin = new Vector2(0, 0);
            footerRuleRT.anchorMax = new Vector2(1, 0);
            footerRuleRT.pivot     = new Vector2(0.5f, 0);
            footerRuleRT.anchoredPosition = new Vector2(0, 78);
            footerRuleRT.sizeDelta = new Vector2(-64, 1);

            _authorText = CreateText(_pageRoot, "Author",
                "Signed:  ____________________________   " + reportAuthor,
                12, FontStyles.Italic,
                new Color(0.25f, 0.20f, 0.15f),
                TextAlignmentOptions.Left);
            var authorRT = _authorText.rectTransform;
            authorRT.anchorMin = new Vector2(0, 0);
            authorRT.anchorMax = new Vector2(1, 0);
            authorRT.pivot     = new Vector2(0.5f, 0);
            authorRT.anchoredPosition = new Vector2(0, 56);
            authorRT.sizeDelta = new Vector2(-64, 18);

            var pageNumber = CreateText(_pageRoot, "PageNumber",
                "Page 1 of 1",
                10, FontStyles.Italic,
                new Color(0.40f, 0.32f, 0.20f),
                TextAlignmentOptions.Center);
            var pageRT = pageNumber.rectTransform;
            pageRT.anchorMin = new Vector2(0, 0);
            pageRT.anchorMax = new Vector2(1, 0);
            pageRT.pivot     = new Vector2(0.5f, 0);
            pageRT.anchoredPosition = new Vector2(0, 32);
            pageRT.sizeDelta = new Vector2(-64, 16);

            _baseColor = _pageBg.color;
        }

        private void RefreshUI()
        {
            if (_pageBg == null) return;

            // Once populated, behave like a printed snapshot — live data ticks
            // no longer redraw the page so it stops looking like a screen.
            if (freezeContent && _hasBeenPopulated) return;

            if (report == null)
            {
                _titleText.text = "NO REPORT ASSIGNED";
                _subtitleText.text = "";
                _bodyText.text     = "Assign a SmartFarmReportData asset.";
                if (_metricsText != null) _metricsText.text = "";
                _recsText.text     = "";
                return;
            }

            // Keep the cream paper colour even if the asset specifies something
            // brighter — the goal is a printed look.
            _pageBg.color = report.pageColor;
            _baseColor    = report.pageColor;

            _titleText.text    = string.IsNullOrEmpty(report.title) ? "REPORT" : report.title.ToUpperInvariant();
            _subtitleText.text = report.subtitle;
            _bodyText.text     = FormatBody(report.body);
            _recsText.text     = FormatBody(report.recommendations);

            if (_orgText != null)
                _orgText.text = string.IsNullOrEmpty(organizationName) ? "SMART FARM" : organizationName;
            if (_dateText != null)
                _dateText.text = "DATE:  " + System.DateTime.Now.ToString("yyyy-MM-dd");
            if (_refText != null)
                _refText.text = "REF / " + ShortRef(report.reportId, report.reportType);
            if (_authorText != null)
                _authorText.text = "Signed:  ____________________________   "
                                   + (string.IsNullOrEmpty(reportAuthor) ? "Farm Operations" : reportAuthor);

            BuildMetricsText();

            _hasBeenPopulated = true;
        }

        private void BuildMetricsText()
        {
            if (_metricsText == null) return;
            if (report == null || report.metrics == null || report.metrics.Count == 0)
            {
                _metricsText.text = "<i>— no measurements recorded —</i>";
                return;
            }

            // Print classic "dotted-leader" rows: label ............... value.
            // Using TMP's mono-space tag to keep dot count aligned for any font.
            var sb = new System.Text.StringBuilder(256);
            for (int i = 0; i < report.metrics.Count; i++)
            {
                var m = report.metrics[i];
                if (string.IsNullOrWhiteSpace(m.label)) continue;

                string label   = m.label.Trim();
                string valueStr = $"{m.value:0.##}{m.unit}";

                // ~38 mono-space columns for the dotted row, value right-aligned.
                int columns = 38;
                int gap     = Mathf.Max(2, columns - label.Length - valueStr.Length);
                string dots = new string('.', gap);

                if (m.isCritical)
                {
                    sb.Append("<color=#9C2A20>");
                    sb.Append("<mspace=8.8>");
                    sb.Append(label);
                    sb.Append(' ');
                    sb.Append(dots);
                    sb.Append(' ');
                    sb.Append(valueStr);
                    sb.Append("</mspace>");
                    sb.Append("</color>");
                }
                else
                {
                    sb.Append("<mspace=8.8>");
                    sb.Append(label);
                    sb.Append(' ');
                    sb.Append(dots);
                    sb.Append(' ');
                    sb.Append(valueStr);
                    sb.Append("</mspace>");
                }

                if (i < report.metrics.Count - 1) sb.Append('\n');
            }
            _metricsText.text = sb.ToString();
        }

        private static string ShortRef(string reportId, SmartFarmReportType type)
        {
            string prefix = type switch
            {
                SmartFarmReportType.CropHealth      => "CH",
                SmartFarmReportType.Irrigation      => "IR",
                SmartFarmReportType.WeatherForecast => "WF",
                SmartFarmReportType.HarvestPlanning => "HP",
                SmartFarmReportType.SoilAnalysis    => "SA",
                SmartFarmReportType.WaterUsage      => "WU",
                _                                   => "RP"
            };
            string suffix = string.IsNullOrEmpty(reportId)
                ? System.Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant()
                : reportId.Replace("-", "").ToUpperInvariant();
            if (suffix.Length > 6) suffix = suffix.Substring(0, 6);
            return $"{prefix}-{System.DateTime.Now:yy}-{suffix}";
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
                    sb.Append("<b>•  ");
                    sb.Append(clean);
                    sb.Append("</b>");
                }
                else if (!string.IsNullOrEmpty(line))
                {
                    sb.Append("•  ");
                    sb.Append(line);
                }
                if (i < lines.Length - 1) sb.Append('\n');
            }
            return sb.ToString();
        }

        private void OnReportUpdated(SmartFarmReportData updated)
        {
            if (updated == null || updated != report) return;
            // Honour the printed-snapshot setting — once we've drawn the page,
            // ignore further live-data ticks.
            if (freezeContent && _hasBeenPopulated) return;
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

    }
}
