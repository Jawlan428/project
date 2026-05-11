using System.Collections.Generic;
using UnityEngine;

namespace SmartFarm.MeetingRoom
{
    /// <summary>
    /// Watches every <see cref="VRDocumentInteractable"/> in the scene and applies
    /// a smooth "reading zoom" effect when the user brings the document close to
    /// their face. Also auto-orients the page towards the camera so reading is
    /// always comfortable.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class DocumentReaderSystem : MonoBehaviour
    {
        [Header("Camera")]
        [Tooltip("VR head/camera. Defaults to Camera.main if left empty.")]
        [SerializeField] private Transform headTransform;

        [Header("Reading Trigger")]
        [Tooltip("Distance (in metres) below which a held document enters reading mode.")]
        [SerializeField] [Range(0.15f, 0.8f)] private float readingDistance = 0.45f;

        [Tooltip("Hysteresis margin so the zoom does not flicker near the threshold.")]
        [SerializeField] [Range(0.02f, 0.2f)] private float exitMargin = 0.08f;

        [Header("Smoothing")]
        [Tooltip("How fast the zoom blends in and out.")]
        [SerializeField] [Range(1f, 20f)] private float zoomLerpSpeed = 6f;

        [Tooltip("If true, the document gently turns to face the camera while being read.")]
        [SerializeField] private bool faceCameraWhileReading = true;

        [Tooltip("How fast the document rotates to face the camera.")]
        [SerializeField] [Range(1f, 20f)] private float faceLerpSpeed = 4f;

        private readonly Dictionary<VRDocumentInteractable, float> _currentZoom = new Dictionary<VRDocumentInteractable, float>();
        private readonly Dictionary<VRDocumentInteractable, bool> _isReading = new Dictionary<VRDocumentInteractable, bool>();
        private VRDocumentInteractable[] _docs;
        private float _refreshTimer;

        private void Awake()
        {
            if (headTransform == null && Camera.main != null) headTransform = Camera.main.transform;
        }

        private void OnEnable()
        {
            RefreshDocuments();
        }

        private void Update()
        {
            // Refresh the doc list periodically so new documents spawned at runtime get picked up.
            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer <= 0f)
            {
                RefreshDocuments();
                _refreshTimer = 2f;
            }

            if (headTransform == null && Camera.main != null) headTransform = Camera.main.transform;
            if (headTransform == null || _docs == null) return;

            for (int i = 0; i < _docs.Length; i++)
            {
                var doc = _docs[i];
                if (doc == null) continue;

                float targetZoom = 1f;
                bool reading = _isReading.TryGetValue(doc, out var prev) && prev;

                // Inspect mode owns its own scaling — skip the proximity-zoom there.
                if (doc.IsInspecting)
                {
                    _isReading[doc] = false;
                    _currentZoom[doc] = 1f;
                    continue;
                }

                if (doc.IsHeld)
                {
                    float dist = Vector3.Distance(headTransform.position, doc.transform.position);
                    float enter = readingDistance;
                    float exit = readingDistance + exitMargin;

                    if (!reading && dist < enter)
                    {
                        reading = true;
                    }
                    else if (reading && dist > exit)
                    {
                        reading = false;
                    }

                    float baseZoom = doc.Report != null ? doc.Report.readingZoom : 1.35f;
                    targetZoom = reading ? baseZoom : 1f;

                    if (reading && faceCameraWhileReading)
                    {
                        var toCam = headTransform.position - doc.transform.position;
                        if (toCam.sqrMagnitude > 0.0001f)
                        {
                            // The page's local "up" is the canvas normal (we built the canvas rotated 90° on X).
                            Quaternion look = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
                            Quaternion target = look * Quaternion.Euler(90f, 0f, 0f);
                            doc.transform.rotation = Quaternion.Slerp(doc.transform.rotation, target, Time.deltaTime * faceLerpSpeed);
                        }
                    }
                }
                else
                {
                    reading = false;
                }

                _isReading[doc] = reading;

                float current = _currentZoom.TryGetValue(doc, out var c) ? c : 1f;
                current = Mathf.Lerp(current, targetZoom, Time.deltaTime * zoomLerpSpeed);
                _currentZoom[doc] = current;
                doc.SetReadingZoom(current);
            }
        }

        /// <summary>Re-collect documents in the scene (call after spawning new docs).</summary>
        public void RefreshDocuments()
        {
            _docs = FindObjectsByType<VRDocumentInteractable>(FindObjectsSortMode.None);
        }
    }
}
