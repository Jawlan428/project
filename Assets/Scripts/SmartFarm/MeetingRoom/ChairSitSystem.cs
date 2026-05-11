using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SmartFarm.MeetingRoom
{
    /// <summary>
    /// Per-chair sit interaction. Attach this to any chair prefab in the meeting room.
    /// <para>
    /// The component will:
    /// <list type="bullet">
    ///   <item>Spawn (or use) a sit anchor positioned above the seat.</item>
    ///   <item>Show a floating "Sit" prompt when the player is close.</item>
    ///   <item>Configure an <see cref="XRSimpleInteractable"/> so any XR interactor can trigger sit/stand.</item>
    ///   <item>Smoothly move the XR rig to / from the sit anchor.</item>
    ///   <item>Restore the player to their previous pose when standing up.</item>
    /// </list>
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class ChairSitSystem : MonoBehaviour
    {
        public static ChairSitSystem CurrentlySeated { get; private set; }

        [Header("Player Rig")]
        [Tooltip("Root of the XR rig (the one that moves around the world). Auto-found if left empty.")]
        [SerializeField] private Transform playerRig;

        [Tooltip("Optional: head camera reference. Used to calculate where to place the rig so the head is over the sit anchor.")]
        [SerializeField] private Transform playerHead;

        [Header("Sit Anchor")]
        [Tooltip("Local-space offset above the chair where the user's hips should rest.")]
        [SerializeField] private Vector3 sitLocalOffset = new Vector3(0f, 0.55f, 0.05f);

        [Tooltip("If the chair contains a child named \"SitAnchor\" it will be used instead of the offset.")]
        [SerializeField] private bool autoFindSitAnchorChild = true;

        [Header("Proximity")]
        [Tooltip("Distance (m) within which the sit prompt becomes visible.")]
        [SerializeField] [Range(0.3f, 3f)] private float promptDistance = 1.4f;

        [Header("Sit / Stand Motion")]
        [Tooltip("Seconds to blend the rig from current pose to the seated pose.")]
        [SerializeField] [Range(0.05f, 1.5f)] private float blendDuration = 0.4f;

        [Tooltip("If true, the rig faces the table while seated.")]
        [SerializeField] private bool faceTowardsTable = true;

        [Tooltip("Transform the seated player should face (typically the table centre). Defaults to nearest 'MeetingTable' tagged object.")]
        [SerializeField] private Transform faceTarget;

        [Header("Highlight")]
        [Tooltip("Material colour pulse applied to the chair when the player is near.")]
        [SerializeField] private Color highlightTint = new Color(1f, 0.95f, 0.55f, 1f);

        private Transform _sitAnchor;
        private XRSimpleInteractable _interactable;
        private Canvas _promptCanvas;
        private TMP_Text _promptText;
        private bool _isSeated;
        private Vector3 _standPosition;
        private Quaternion _standRotation;
        private Coroutine _blendCoroutine;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private Color[] _originalColors;
        private float _hoverAmount;

        private void Awake()
        {
            EnsureSitAnchor();
            EnsureInteractable();
            BuildPrompt();
            _renderers = GetComponentsInChildren<Renderer>(true);
            CacheOriginalColors();
            _mpb = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (_interactable != null)
            {
                _interactable.selectEntered.AddListener(OnSelected);
                _interactable.activated.AddListener(OnActivated);
            }
        }

        private void OnDisable()
        {
            if (_interactable != null)
            {
                _interactable.selectEntered.RemoveListener(OnSelected);
                _interactable.activated.RemoveListener(OnActivated);
            }
        }

        private void Start()
        {
            if (playerRig == null) playerRig = FindRig();
            if (playerHead == null && Camera.main != null) playerHead = Camera.main.transform;
            if (faceTarget == null) faceTarget = FindFaceTarget();
        }

        private void Update()
        {
            if (playerRig == null) return;

            // Show / hide prompt based on distance.
            float dist = Vector3.Distance(playerRig.position, transform.position);
            bool nearby = !_isSeated && dist < promptDistance;
            if (_promptCanvas != null && _promptCanvas.gameObject.activeSelf != nearby)
                _promptCanvas.gameObject.SetActive(nearby);

            // Smooth highlight.
            float target = nearby ? 1f : 0f;
            _hoverAmount = Mathf.Lerp(_hoverAmount, target, Time.deltaTime * 6f);
            ApplyHighlight(_hoverAmount);

            // Make the prompt face the player.
            if (_promptCanvas != null && nearby && playerHead != null)
            {
                Vector3 toHead = playerHead.position - _promptCanvas.transform.position;
                if (toHead.sqrMagnitude > 0.0001f)
                {
                    Quaternion look = Quaternion.LookRotation(-toHead.normalized, Vector3.up);
                    _promptCanvas.transform.rotation = look;
                }
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void ToggleSit()
        {
            if (_isSeated) StandUp();
            else SitDown();
        }

        public void SitDown()
        {
            if (_isSeated || playerRig == null || _sitAnchor == null) return;

            // Stand any other chair the player might already be seated on.
            if (CurrentlySeated != null && CurrentlySeated != this) CurrentlySeated.StandUp();

            _standPosition = playerRig.position;
            _standRotation = playerRig.rotation;

            Vector3 targetPos = _sitAnchor.position;
            // Compensate for the head being above the rig pivot, so the head ends up over the seat.
            if (playerHead != null)
            {
                Vector3 headOffset = playerHead.position - playerRig.position;
                headOffset.y = 0f;
                targetPos -= headOffset;
            }

            Quaternion targetRot = playerRig.rotation;
            if (faceTowardsTable && faceTarget != null)
            {
                Vector3 toTable = faceTarget.position - _sitAnchor.position;
                toTable.y = 0f;
                if (toTable.sqrMagnitude > 0.0001f)
                    targetRot = Quaternion.LookRotation(toTable.normalized, Vector3.up);
            }

            if (_blendCoroutine != null) StopCoroutine(_blendCoroutine);
            _blendCoroutine = StartCoroutine(BlendRig(targetPos, targetRot, true));

            _isSeated = true;
            CurrentlySeated = this;
        }

        public void StandUp()
        {
            if (!_isSeated || playerRig == null) return;
            if (_blendCoroutine != null) StopCoroutine(_blendCoroutine);
            _blendCoroutine = StartCoroutine(BlendRig(_standPosition, _standRotation, false));
            _isSeated = false;
            if (CurrentlySeated == this) CurrentlySeated = null;
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private IEnumerator BlendRig(Vector3 targetPos, Quaternion targetRot, bool seating)
        {
            if (_promptCanvas != null) _promptCanvas.gameObject.SetActive(false);
            Vector3 startPos = playerRig.position;
            Quaternion startRot = playerRig.rotation;
            float t = 0f;
            float dur = Mathf.Max(0.05f, blendDuration);
            while (t < dur)
            {
                t += Time.deltaTime;
                float n = Mathf.SmoothStep(0f, 1f, t / dur);
                playerRig.position = Vector3.Lerp(startPos, targetPos, n);
                playerRig.rotation = Quaternion.Slerp(startRot, targetRot, n);
                yield return null;
            }
            playerRig.position = targetPos;
            playerRig.rotation = targetRot;

            if (seating && _promptText != null)
                _promptText.text = "Press Trigger to Stand";
            else if (_promptText != null)
                _promptText.text = "Press to Sit";
        }

        private void EnsureSitAnchor()
        {
            if (autoFindSitAnchorChild)
            {
                var t = transform.Find("SitAnchor");
                if (t != null)
                {
                    _sitAnchor = t;
                    return;
                }
            }

            var go = new GameObject("SitAnchor");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = sitLocalOffset;
            _sitAnchor = go.transform;
        }

        private void EnsureInteractable()
        {
            _interactable = GetComponent<XRSimpleInteractable>();
            if (_interactable == null)
                _interactable = gameObject.AddComponent<XRSimpleInteractable>();

            if (GetComponent<Collider>() == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.4f, 0f);
                box.size = new Vector3(0.5f, 0.8f, 0.5f);
                box.isTrigger = true;
            }
        }

        private void BuildPrompt()
        {
            var canvasGO = new GameObject("SitPrompt", typeof(RectTransform));
            canvasGO.transform.SetParent(transform, false);
            canvasGO.transform.localPosition = (Vector3.up * 1.2f) + sitLocalOffset;
            _promptCanvas = canvasGO.AddComponent<Canvas>();
            _promptCanvas.renderMode = RenderMode.WorldSpace;
            canvasGO.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 60f;

            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(260, 90);
            rt.localScale = Vector3.one * 0.0015f;

            var bgGO = new GameObject("BG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bg = bgGO.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = false;
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(canvasGO.transform, false);
            _promptText = textGO.AddComponent<TextMeshProUGUI>();
            _promptText.text = "Press to Sit";
            _promptText.fontSize = 36f;
            _promptText.alignment = TextAlignmentOptions.Center;
            _promptText.color = Color.white;
            _promptText.fontStyle = FontStyles.Bold;
            var trt = textGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8, 4);
            trt.offsetMax = new Vector2(-8, -4);

            canvasGO.SetActive(false);
        }

        private Transform FindRig()
        {
            // Common rig names in XR samples / Oculus / XRI starter assets.
            string[] candidates = { "XR Origin (XR Rig)", "XR Origin", "XRRig", "OVRCameraRig", "XR Interaction Manager" };
            foreach (var n in candidates)
            {
                var go = GameObject.Find(n);
                if (go != null) return go.transform;
            }
            // Final fallback: camera's grand-parent (usually rig root).
            if (Camera.main != null && Camera.main.transform.parent != null)
            {
                var p = Camera.main.transform.parent;
                return p.parent != null ? p.parent : p;
            }
            return null;
        }

        private Transform FindFaceTarget()
        {
            var tagged = GameObject.FindGameObjectWithTag("MeetingTable");
            if (tagged != null) return tagged.transform;
            var mim = FindFirstObjectByType<MeetingInteractionManager>();
            return mim != null ? mim.transform : null;
        }

        private void CacheOriginalColors()
        {
            if (_renderers == null) return;
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null || r.sharedMaterial == null) continue;
                if (r.sharedMaterial.HasProperty(BaseColorId))
                    _originalColors[i] = r.sharedMaterial.GetColor(BaseColorId);
                else if (r.sharedMaterial.HasProperty(ColorId))
                    _originalColors[i] = r.sharedMaterial.GetColor(ColorId);
                else
                    _originalColors[i] = Color.white;
            }
        }

        private void ApplyHighlight(float amount)
        {
            if (_renderers == null || _originalColors == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                Color blended = Color.Lerp(_originalColors[i], highlightTint, amount * 0.45f);
                _mpb.Clear();
                r.GetPropertyBlock(_mpb);
                if (r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColorId))
                    _mpb.SetColor(BaseColorId, blended);
                else
                    _mpb.SetColor(ColorId, blended);
                r.SetPropertyBlock(_mpb);
            }
        }

        private void OnSelected(SelectEnterEventArgs args)
        {
            ToggleSit();
        }

        private void OnActivated(ActivateEventArgs args)
        {
            ToggleSit();
        }
    }
}
