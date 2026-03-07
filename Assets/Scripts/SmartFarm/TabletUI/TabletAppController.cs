using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

namespace SmartFarm
{
    public enum TabletPinMode
    {
        Wrist,
        Desk
    }

    /// <summary>
    /// Controls VR tablet shell: tabs, status, page switching, and pin mode.
    /// </summary>
    public class TabletAppController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private FarmDataManager dataManager;

        [Header("Header")]
        [SerializeField] private TMP_Text appTitleText;
        [SerializeField] private TMP_Text connectionStatusText;
        [SerializeField] private Image connectionStatusIcon;
        [SerializeField] private Color connectedColor = new Color(0.25f, 0.9f, 0.35f);
        [SerializeField] private Color disconnectedColor = new Color(0.9f, 0.35f, 0.35f);

        [Header("Tabs")]
        [SerializeField] private Button overviewTabButton;
        [SerializeField] private Button irrigationTabButton;
        [SerializeField] private Button alertsTabButton;
        [SerializeField] private Button pollsTabButton;
        [SerializeField] private Button historyTabButton;
        [SerializeField] private GameObject overviewPage;
        [SerializeField] private GameObject irrigationPage;
        [SerializeField] private GameObject alertsPage;
        [SerializeField] private GameObject pollsPage;
        [SerializeField] private GameObject historyPage;
        [SerializeField] private SimpleUIAnimationHelper animationHelper;

        [Header("Pin Mode")]
        [SerializeField] private Button pinToggleButton;
        [SerializeField] private Button wristModeButton;
        [SerializeField] private Button deskModeButton;
        [SerializeField] private TMP_Text pinButtonLabel;
        [SerializeField] private Transform leftWristAnchor;
        [SerializeField] private Transform deskAnchor;
        [SerializeField] private TabletPinMode pinMode = TabletPinMode.Desk;
        [SerializeField] private bool startPinned = false;
        [SerializeField] private float snapDuration = 0.25f;
        [SerializeField] private float wristFollowLerp = 12f;

        private bool _isPinned;
        private Coroutine _snapRoutine;
        private GameObject _activePage;

        private void Start()
        {
            if (dataManager == null) dataManager = FindFirstObjectByType<FarmDataManager>();
            if (appTitleText != null && string.IsNullOrWhiteSpace(appTitleText.text))
                appTitleText.text = "Smart Farm Tablet";

            WireButtons();
            SetPage(overviewPage);
            _isPinned = startPinned;
            RefreshPinButton();
            UpdateConnectionStatus();

            if (_isPinned)
                SnapToCurrentAnchor(true);
        }

        private void OnEnable()
        {
            if (dataManager != null)
                dataManager.OnDataChanged += OnDataChanged;
        }

        private void OnDisable()
        {
            if (dataManager != null)
                dataManager.OnDataChanged -= OnDataChanged;
        }

        private void LateUpdate()
        {
            if (!_isPinned || pinMode != TabletPinMode.Wrist || leftWristAnchor == null) return;
            transform.position = Vector3.Lerp(transform.position, leftWristAnchor.position, Time.deltaTime * wristFollowLerp);
            transform.rotation = Quaternion.Slerp(transform.rotation, leftWristAnchor.rotation, Time.deltaTime * wristFollowLerp);
        }

        private void WireButtons()
        {
            if (overviewTabButton   != null) overviewTabButton.onClick.AddListener(()   => SetPage(overviewPage));
            if (irrigationTabButton != null) irrigationTabButton.onClick.AddListener(() => SetPage(irrigationPage));
            if (alertsTabButton     != null) alertsTabButton.onClick.AddListener(()     => SetPage(alertsPage));
            if (pollsTabButton      != null) pollsTabButton.onClick.AddListener(()      => SetPage(pollsPage));
            if (historyTabButton    != null) historyTabButton.onClick.AddListener(()    => SetPage(historyPage));

            if (pinToggleButton != null) pinToggleButton.onClick.AddListener(TogglePin);
            if (wristModeButton != null) wristModeButton.onClick.AddListener(() => SetPinMode(TabletPinMode.Wrist));
            if (deskModeButton != null) deskModeButton.onClick.AddListener(() => SetPinMode(TabletPinMode.Desk));
        }

        private void OnDataChanged(FarmSimulationState _)
        {
            UpdateConnectionStatus();
        }

        private void UpdateConnectionStatus()
        {
            bool connected = NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;
            if (connectionStatusText != null)
                connectionStatusText.text = connected ? "Connected" : "Local";
            if (connectionStatusIcon != null)
                connectionStatusIcon.color = connected ? connectedColor : disconnectedColor;
        }

        public void SetPage(GameObject page)
        {
            if (page == null) return;
            if (_activePage == page) return;

            if (animationHelper != null)
                animationHelper.SwitchPage(_activePage, page);
            else
            {
                if (_activePage != null) _activePage.SetActive(false);
                page.SetActive(true);
            }
            _activePage = page;
        }

        public void TogglePin()
        {
            _isPinned = !_isPinned;
            RefreshPinButton();
            if (_isPinned) SnapToCurrentAnchor(false);
        }

        public void SetPinMode(TabletPinMode mode)
        {
            pinMode = mode;
            if (_isPinned) SnapToCurrentAnchor(false);
        }

        private void RefreshPinButton()
        {
            if (pinButtonLabel != null)
                pinButtonLabel.text = _isPinned ? "Unpin" : "Pin";
        }

        private void SnapToCurrentAnchor(bool instant)
        {
            Transform target = pinMode == TabletPinMode.Wrist ? leftWristAnchor : deskAnchor;
            if (target == null) return;

            if (_snapRoutine != null) StopCoroutine(_snapRoutine);
            if (instant)
            {
                transform.SetPositionAndRotation(target.position, target.rotation);
            }
            else
            {
                _snapRoutine = StartCoroutine(SmoothSnap(target, snapDuration));
            }
        }

        private IEnumerator SmoothSnap(Transform target, float duration)
        {
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            float t = 0f;
            while (t < duration && target != null)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(t / duration);
                transform.position = Vector3.Lerp(startPos, target.position, a);
                transform.rotation = Quaternion.Slerp(startRot, target.rotation, a);
                yield return null;
            }
            if (target != null)
                transform.SetPositionAndRotation(target.position, target.rotation);
        }
    }
}
