using System.Collections.Generic;
using UnityEngine;

namespace SmartFarm.MeetingRoom
{
    /// <summary>
    /// Top-level orchestrator for the VR Smart Farm meeting area.
    /// <para>
    /// This component does not duplicate the sub-system logic — it composes them.
    /// On Awake it locates / spawns:
    /// <list type="bullet">
    ///   <item><see cref="SmartFarmReportManager"/></item>
    ///   <item><see cref="DocumentReaderSystem"/></item>
    ///   <item><see cref="MeetingAmbience"/></item>
    /// </list>
    /// It also wires up every <see cref="ChairSitSystem"/> and <see cref="VRDocumentInteractable"/>
    /// found under <see cref="meetingRoot"/>, so a level designer can just drop one
    /// of these on a "Meeting" GameObject and have everything work.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class MeetingInteractionManager : MonoBehaviour
    {
        public static MeetingInteractionManager Instance { get; private set; }

        [Header("Scene Hierarchy")]
        [Tooltip("Root object that contains the table, chairs, documents and decorations. Defaults to this transform.")]
        [SerializeField] private Transform meetingRoot;

        [Header("Documents")]
        [Tooltip("Auto-spawned documents will use these report assets in order.")]
        [SerializeField] private List<SmartFarmReportData> defaultReports = new List<SmartFarmReportData>();

        [Tooltip("Existing documents in the scene (any VRDocumentInteractable under meetingRoot is also picked up automatically).")]
        [SerializeField] private List<VRDocumentInteractable> documents = new List<VRDocumentInteractable>();

        [Header("Chairs")]
        [Tooltip("Existing chairs in the scene. Any ChairSitSystem under meetingRoot is also picked up automatically.")]
        [SerializeField] private List<ChairSitSystem> chairs = new List<ChairSitSystem>();

        [Header("Sub-systems")]
        [SerializeField] private SmartFarmReportManager reportManager;
        [SerializeField] private DocumentReaderSystem readerSystem;
        [SerializeField] private MeetingAmbience ambience;

        public SmartFarmReportManager ReportManager => reportManager;
        public DocumentReaderSystem ReaderSystem => readerSystem;
        public IReadOnlyList<VRDocumentInteractable> Documents => documents;
        public IReadOnlyList<ChairSitSystem> Chairs => chairs;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (meetingRoot == null) meetingRoot = transform;

            EnsureSubSystems();
            CollectChildren();
        }

        private void Start()
        {
            // Register all documents' reports with the manager.
            if (reportManager != null)
            {
                for (int i = 0; i < documents.Count; i++)
                {
                    var doc = documents[i];
                    if (doc != null && doc.Report != null)
                        reportManager.Register(doc.Report);
                }
                reportManager.RefreshAll();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Re-scans the meeting root for chairs and documents. Call after spawning new items at runtime.</summary>
        public void Rescan()
        {
            CollectChildren();
            if (readerSystem != null) readerSystem.RefreshDocuments();
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private void EnsureSubSystems()
        {
            if (reportManager == null)
            {
                reportManager = FindFirstObjectByType<SmartFarmReportManager>();
                if (reportManager == null)
                {
                    var go = new GameObject("SmartFarmReportManager");
                    go.transform.SetParent(transform, false);
                    reportManager = go.AddComponent<SmartFarmReportManager>();
                }
            }

            if (readerSystem == null)
            {
                readerSystem = FindFirstObjectByType<DocumentReaderSystem>();
                if (readerSystem == null)
                {
                    var go = new GameObject("DocumentReaderSystem");
                    go.transform.SetParent(transform, false);
                    readerSystem = go.AddComponent<DocumentReaderSystem>();
                }
            }

            if (ambience == null)
            {
                ambience = GetComponentInChildren<MeetingAmbience>();
            }
        }

        private void CollectChildren()
        {
            documents.Clear();
            documents.AddRange(meetingRoot.GetComponentsInChildren<VRDocumentInteractable>(true));

            chairs.Clear();
            chairs.AddRange(meetingRoot.GetComponentsInChildren<ChairSitSystem>(true));
        }
    }
}
