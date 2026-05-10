using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm
{
    /// <summary>
    /// Displays farm alerts in a vertical scrollable list.
    /// Each row shows: severity badge · time · message preview.
    /// Tapping any row opens an in-page detail overlay with full info + Acknowledge.
    ///
    /// Runtime-safe: automatically adds VerticalLayoutGroup / ContentSizeFitter to
    /// listRoot and creates the detail panel if they are not wired in the Inspector.
    /// This means the fix works on any existing scene without needing a re-setup.
    /// </summary>
    public class AlertsUI : MonoBehaviour
    {
        [Header("List")]
        [SerializeField] private FarmDataManager  dataManager;
        [SerializeField] private TMP_Text         badgeCountText;
        [SerializeField] private GameObject       badgeRoot;
        [SerializeField] private Transform        listRoot;
        [SerializeField] private AlertListItemUI  itemPrefab;
        [SerializeField] private GameObject       emptyStateRoot;

        [Header("Detail Panel (auto-created at runtime if not assigned)")]
        [SerializeField] private GameObject detailPanel;
        [SerializeField] private Image      detailBackground;
        [SerializeField] private TMP_Text   detailSeverityText;
        [SerializeField] private TMP_Text   detailTimestampText;
        [SerializeField] private TMP_Text   detailMessageText;
        [SerializeField] private Button     detailAcknowledgeButton;
        [SerializeField] private Button     detailCloseButton;

        // ── Colours ───────────────────────────────────────────────────────────

        private static readonly Color InfoBg     = new Color(0.08f, 0.22f, 0.46f, 0.98f);
        private static readonly Color WarnBg     = new Color(0.46f, 0.28f, 0.04f, 0.98f);
        private static readonly Color CriticalBg = new Color(0.52f, 0.08f, 0.08f, 0.98f);

        // ── State ─────────────────────────────────────────────────────────────

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private FarmAlertItem             _detailItem;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            EnsureListLayout();
            EnsureDetailPanel();
        }

        private void OnEnable()
        {
            if (dataManager == null)
                dataManager = FindFirstObjectByType<FarmDataManager>();
            if (dataManager != null)
                dataManager.OnAlertsChanged += OnAlertsChanged;
        }

        private void OnDisable()
        {
            if (dataManager != null)
                dataManager.OnAlertsChanged -= OnAlertsChanged;
        }

        // ── Layout bootstrap ──────────────────────────────────────────────────

        /// <summary>
        /// Adds VerticalLayoutGroup + ContentSizeFitter to listRoot if they are
        /// missing. This is the fix for existing scenes where items pile up because
        /// the editor-built ListRoot had no layout component.
        /// </summary>
        private void EnsureListLayout()
        {
            if (listRoot == null) return;

            var vlg = listRoot.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
                vlg = listRoot.gameObject.AddComponent<VerticalLayoutGroup>();

            vlg.spacing              = 8;
            vlg.padding              = new RectOffset(6, 6, 6, 6);
            vlg.childAlignment       = TextAnchor.UpperCenter;
            vlg.childControlWidth    = true;
            vlg.childControlHeight   = true;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;

            var csf = listRoot.GetComponent<ContentSizeFitter>();
            if (csf == null)
                csf = listRoot.gameObject.AddComponent<ContentSizeFitter>();

            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            // Anchor to top so items stack downward
            var rt = listRoot as RectTransform ?? listRoot.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.pivot     = new Vector2(0.5f, 1f);
                rt.anchorMin = new Vector2(0.03f, 0f);
                rt.anchorMax = new Vector2(0.97f, 0.86f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
        }

        // ── Detail panel bootstrap ────────────────────────────────────────────

        private void EnsureDetailPanel()
        {
            if (detailPanel != null)
            {
                detailPanel.SetActive(false);
                WireDetailButtons();
                return;
            }

            // Create a full-page overlay detail panel programmatically
            var panel = new GameObject("AlertDetailPanel", typeof(RectTransform));
            panel.transform.SetParent(transform, false);

            var rt = (RectTransform)panel.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var bg = panel.AddComponent<Image>();
            bg.color = InfoBg;
            detailBackground = bg;

            // Header bar
            var header = MakePanel(panel.transform, "DetailHeader",
                new Vector2(0f, 0.84f), new Vector2(1f, 1f),
                new Color(0f, 0f, 0f, 0.25f));

            detailSeverityText = MakeText(header.transform, "Severity",
                "INFO", 22, TextAlignmentOptions.Left,
                new Vector2(0.04f, 0f), new Vector2(0.55f, 1f));

            detailTimestampText = MakeText(header.transform, "Timestamp",
                "00:00:00", 16, TextAlignmentOptions.Right,
                new Vector2(0.55f, 0.1f), new Vector2(0.97f, 0.9f));
            detailTimestampText.color = new Color(0.78f, 0.90f, 1f, 1f);

            // Full message
            detailMessageText = MakeText(panel.transform, "DetailMessage",
                "", 18, TextAlignmentOptions.TopLeft,
                new Vector2(0.05f, 0.26f), new Vector2(0.95f, 0.82f));
            detailMessageText.textWrappingMode = TextWrappingModes.Normal;

            // Buttons row
            var ackBtn = MakeButton(panel.transform, "DetailAcknowledge", "Acknowledge",
                new Vector2(0.05f, 0.06f), new Vector2(0.48f, 0.20f),
                new Color(0.10f, 0.65f, 0.22f, 1f));
            detailAcknowledgeButton = ackBtn.GetComponent<Button>();

            var closeBtn = MakeButton(panel.transform, "DetailClose", "Close",
                new Vector2(0.52f, 0.06f), new Vector2(0.95f, 0.20f),
                new Color(0.30f, 0.30f, 0.42f, 1f));
            detailCloseButton = closeBtn.GetComponent<Button>();

            detailPanel = panel;
            panel.SetActive(false);
            WireDetailButtons();
        }

        private void WireDetailButtons()
        {
            if (detailCloseButton != null)
            {
                detailCloseButton.onClick.RemoveAllListeners();
                detailCloseButton.onClick.AddListener(CloseDetail);
            }
            if (detailAcknowledgeButton != null)
            {
                detailAcknowledgeButton.onClick.RemoveAllListeners();
                detailAcknowledgeButton.onClick.AddListener(AcknowledgeFromDetail);
            }
        }

        // ── Alert list ────────────────────────────────────────────────────────

        private void OnAlertsChanged(IReadOnlyList<FarmAlertItem> alerts, int unreadCount)
        {
            if (badgeRoot      != null) badgeRoot.SetActive(unreadCount > 0);
            if (badgeCountText != null) badgeCountText.text = unreadCount.ToString();

            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Destroy(_spawned[i]);
            _spawned.Clear();

            bool empty = alerts == null || alerts.Count == 0;
            if (emptyStateRoot != null) emptyStateRoot.SetActive(empty);
            if (empty || itemPrefab == null || listRoot == null) return;

            for (int i = 0; i < alerts.Count; i++)
            {
                var alert = alerts[i];
                var go    = Instantiate(itemPrefab.gameObject, listRoot);
                go.SetActive(true);

                // LayoutElement gives VLG a fixed row height to work with
                var le = go.GetComponent<LayoutElement>();
                if (le == null) le = go.AddComponent<LayoutElement>();
                le.preferredHeight = 56;
                le.minHeight       = 46;

                var ui = go.GetComponent<AlertListItemUI>();
                if (ui != null)
                    ui.Bind(alert, ShowDetail);

                _spawned.Add(go);
            }
        }

        // ── Detail panel logic ────────────────────────────────────────────────

        private void ShowDetail(FarmAlertItem alert)
        {
            if (detailPanel == null) return;

            _detailItem = alert;

            Color bg = alert.severity switch
            {
                FarmAlertSeverity.Critical => CriticalBg,
                FarmAlertSeverity.Warning  => WarnBg,
                _                          => InfoBg
            };

            if (detailBackground    != null) detailBackground.color    = bg;
            if (detailSeverityText  != null) detailSeverityText.text   = alert.severity.ToString().ToUpperInvariant();
            if (detailTimestampText != null) detailTimestampText.text  = alert.timestampUtc.ToLocalTime().ToString("HH:mm:ss   dd MMM yyyy");
            if (detailMessageText   != null) detailMessageText.text    = alert.message;

            if (detailAcknowledgeButton != null)
                detailAcknowledgeButton.gameObject.SetActive(!alert.acknowledged);

            detailPanel.SetActive(true);
        }

        private void CloseDetail()
        {
            if (detailPanel != null) detailPanel.SetActive(false);
        }

        private void AcknowledgeFromDetail()
        {
            if (_detailItem != null)
                dataManager?.AcknowledgeAlert(_detailItem.id);
            CloseDetail();
        }

        // ── Runtime UI helpers ────────────────────────────────────────────────

        private static GameObject MakePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = color;
            return go;
        }

        private static TMP_Text MakeText(Transform parent, string name, string value,
            float size, TextAlignmentOptions align, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text          = value;
            t.fontSize      = size;
            t.alignment     = align;
            t.color         = Color.white;
            t.raycastTarget = false;
            return t;
        }

        private static GameObject MakeButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            go.AddComponent<Button>().targetGraphic = img;

            var tgo = new GameObject("Text", typeof(RectTransform));
            tgo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)tgo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var t = tgo.AddComponent<TextMeshProUGUI>();
            t.text          = label;
            t.fontSize      = 17;
            t.color         = Color.white;
            t.alignment     = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return go;
        }
    }
}
