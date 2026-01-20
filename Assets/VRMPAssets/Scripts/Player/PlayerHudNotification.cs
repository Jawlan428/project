using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace XRMultiplayer
{
    /// <summary>
    /// This class controls the display of the Player HUD Notification aka the Toast.
    /// </summary>
    public class PlayerHudNotification : MonoBehaviour
    {
        /// <summary>
        /// The singleton instance of this class.
        /// </summary>
        public static PlayerHudNotification Instance;

        /// <summary>
        /// Safely shows a notification text. Use this static method for null-safe access.
        /// </summary>
        /// <param name="text">Text to display</param>
        /// <param name="displayTime">How long to display the notification</param>
        public static void Show(string text, float displayTime = 3.0f)
        {
            if (Instance == null)
            {
                Debug.LogWarning($"[PlayerHudNotification] Instance is null. Cannot show: {text}");
                return;
            }

            if (string.IsNullOrEmpty(text))
            {
                Debug.LogWarning("[PlayerHudNotification] Attempted to show empty or null text.");
                return;
            }

            Instance.ShowText(text, displayTime);
        }

        [Header("Display Options")]
        [SerializeField] bool m_LockPitch = true;
        [SerializeField] bool m_LockRoll = true;
        /// <summary>
        /// The speed at which the toast follows the camera.
        /// </summary>
        [SerializeField] float m_FollowSpeed = 5.0f;

        /// <summary>
        /// The amount of time to display the toast.
        /// </summary>
        [SerializeField] float m_DisplayTime = 3.0f;

        /// <summary>
        /// The speed at which the toast fades in and out.
        /// </summary>
        [SerializeField] float m_ShowHideSpeed = 5.0f;

        [Header("Display References")]
        /// <summary>
        /// Text component to display the toast.
        /// </summary>
        [SerializeField] TMP_Text m_Text;

        /// <summary>
        /// The layout group transform that contains the toast.
        /// </summary>
        [SerializeField] Transform m_LayoutGroupTransform;

        /// <summary>
        /// The canvas group that contains the toast.
        /// </summary>
        [SerializeField] CanvasGroup m_CanvasGroup;

        /// <summary>
        /// The canvas component for rendering order control.
        /// </summary>
        Canvas m_Canvas;

        /// <summary>
        /// The main camera.
        /// </summary>
        Camera m_Camera;

        /// <summary>
        /// The transform of this object.
        /// </summary>
        Transform m_Transform;

        /// <summary>
        /// Sorting order for the notification canvas. Higher values render on top.
        /// </summary>
        [SerializeField] int m_CanvasSortingOrder = 100;

        ///<inheritdoc/>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Utils.Log("Duplicate PlayerHudNotification found. Destroying duplicate instance.", 2);
                // If there's already an instance, destroy this duplicate
                Destroy(gameObject);
                return;
            }

            Instance = this;
            
            // Find and destroy any other PlayerHudNotification instances in the scene
            PlayerHudNotification[] allNotifications = FindObjectsByType<PlayerHudNotification>(FindObjectsSortMode.None);
            foreach (PlayerHudNotification notification in allNotifications)
            {
                if (notification != this)
                {
                    Debug.LogWarning($"[PlayerHudNotification] Found duplicate notification instance: {notification.gameObject.name}. Destroying it.");
                    Destroy(notification.gameObject);
                }
            }
        }

        /// <inheritdoc/>
        private void Start()
        {
            // Double-check for duplicates in Start (in case they were created after Awake)
            // Use a coroutine to check after a small delay to ensure all components are initialized
            StartCoroutine(CheckForDuplicatesDelayed());

            m_Camera = Camera.main;
            m_Transform = transform;

            if (m_Camera == null)
            {
                Debug.LogError("[PlayerHudNotification] Camera.main is null! Notification position updates will fail.");
            }

            // Get or create Canvas component for proper rendering order
            m_Canvas = GetComponent<Canvas>();
            if (m_Canvas == null)
            {
                m_Canvas = GetComponentInParent<Canvas>();
                if (m_Canvas == null)
                {
                    // Create a World Space Canvas if none exists
                    m_Canvas = gameObject.AddComponent<Canvas>();
                    m_Canvas.renderMode = RenderMode.WorldSpace;
                    m_Canvas.worldCamera = m_Camera;
                    Debug.Log("[PlayerHudNotification] Created World Space Canvas component.");
                }
            }

            // Set high sorting order to ensure notifications render on top
            if (m_Canvas != null)
            {
                m_Canvas.sortingOrder = m_CanvasSortingOrder;
                Debug.Log($"[PlayerHudNotification] Canvas sorting order set to {m_CanvasSortingOrder}");
            }

            if (m_CanvasGroup == null)
                m_CanvasGroup = GetComponentInChildren<CanvasGroup>();

            if (m_Text == null)
            {
                m_Text = GetComponentInChildren<TMP_Text>();
                if (m_Text == null)
                {
                    Debug.LogError("[PlayerHudNotification] TMP_Text component not found! Assign m_Text in the inspector.");
                }
            }

            if (m_CanvasGroup != null)
                m_CanvasGroup.alpha = 0.0f;

            if (m_LayoutGroupTransform != null)
                m_LayoutGroupTransform.gameObject.SetActive(false);
        }

        [ContextMenu("Show Text Test")]
        void ShowTextTest()
        {
            ShowText("Test Text", m_DisplayTime);
        }

        /// <summary>
        /// Shows the toast with the given text.
        /// </summary>
        public void ShowText(string textToShow, float displayTime = 3.0f)
        {
            // Validate text input
            if (string.IsNullOrEmpty(textToShow))
            {
                Debug.LogWarning("[PlayerHudNotification] ShowText called with empty or null text.");
                return;
            }

            // Validate required references
            if (m_Text == null)
            {
                Debug.LogError("[PlayerHudNotification] m_Text reference is null! Assign it in the inspector.");
                return;
            }

            if (m_LayoutGroupTransform == null)
            {
                Debug.LogError("[PlayerHudNotification] m_LayoutGroupTransform reference is null!");
                return;
            }

            if (m_CanvasGroup == null)
            {
                Debug.LogError("[PlayerHudNotification] m_CanvasGroup reference is null!");
                return;
            }

            m_DisplayTime = displayTime;
            m_Text.text = textToShow;
            
            // Ensure Canvas has high sorting order to render on top
            if (m_Canvas != null)
            {
                m_Canvas.sortingOrder = m_CanvasSortingOrder;
            }
            
            // Force update the text mesh to ensure it renders
            m_Text.ForceMeshUpdate();
            
            m_LayoutGroupTransform.gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(ShowRoutine());
            
            Debug.Log($"[PlayerHudNotification] Showing: {textToShow}");
        }

        /// <inheritdoc/>
        private void LateUpdate()
        {
            if (m_Camera == null)
            {
                m_Camera = Camera.main;
                if (m_Camera == null) return;
            }

            m_Transform.position = m_Camera.transform.position;

            Quaternion lookRot = Quaternion.LookRotation(m_Camera.transform.forward);

            Vector3 offset = lookRot.eulerAngles;

            if (m_LockPitch)
                offset.x = 0;
            if (m_LockRoll)
                offset.z = 0;

            lookRot = Quaternion.Euler(offset);

            m_Transform.rotation = Quaternion.Slerp(m_Transform.rotation, lookRot, Time.deltaTime * m_FollowSpeed);
        }

        /// <summary>
        /// Coroutine to show the toast.
        /// </summary>
        /// <returns></returns>
        IEnumerator ShowRoutine()
        {
            while (m_CanvasGroup.alpha < 1.0f)
            {
                m_CanvasGroup.alpha += Time.deltaTime * m_ShowHideSpeed;
                yield return null;
            }

            StartCoroutine(DisplayRoutine());
        }

        /// <summary>
        /// Coroutine to display the toast.
        /// </summary>
        /// <returns></returns>
        IEnumerator DisplayRoutine()
        {
            yield return new WaitForSeconds(m_DisplayTime);

            StartCoroutine(HideTime());
        }

        /// <summary>
        /// Coroutine to hide the toast.
        /// </summary>
        /// <returns></returns>
        IEnumerator HideTime()
        {
            while (m_CanvasGroup.alpha > 0.0f)
            {
                m_CanvasGroup.alpha -= Time.deltaTime * m_ShowHideSpeed;
                yield return null;
            }
            m_LayoutGroupTransform.gameObject.SetActive(false);
        }

        /// <summary>
        /// Coroutine to check for duplicate notifications after a delay.
        /// </summary>
        IEnumerator CheckForDuplicatesDelayed()
        {
            // Wait a frame to ensure all components are initialized
            yield return null;
            
            PlayerHudNotification[] allNotifications = FindObjectsByType<PlayerHudNotification>(FindObjectsSortMode.None);
            
            if (allNotifications.Length > 1)
            {
                Debug.LogWarning($"[PlayerHudNotification] Found {allNotifications.Length} notification instances. Checking for duplicates...");
                
                // Find the one that's actually working (has text and is set up properly)
                PlayerHudNotification workingNotification = null;
                List<PlayerHudNotification> toDestroy = new List<PlayerHudNotification>();
                
                foreach (PlayerHudNotification notification in allNotifications)
                {
                    TMP_Text text = notification.GetComponentInChildren<TMP_Text>();
                    
                    // Check if this notification has valid setup
                    bool hasValidSetup = notification.m_Text != null && notification.m_LayoutGroupTransform != null;
                    
                    // Check if text is placeholder or empty
                    bool isPlaceholder = text != null && (text.text == "A Short Placeholder Notification." || string.IsNullOrWhiteSpace(text.text));
                    
                    // Check if this is the instance (the one we want to keep)
                    bool isThisInstance = notification == this;
                    
                    if (hasValidSetup && !isPlaceholder)
                    {
                        // This is a working notification
                        if (workingNotification == null)
                        {
                            workingNotification = notification;
                            Debug.Log($"[PlayerHudNotification] Found working notification: {notification.gameObject.name}");
                        }
                        else if (!isThisInstance)
                        {
                            // Multiple working notifications, destroy duplicates
                            Debug.LogWarning($"[PlayerHudNotification] Multiple working notifications found. Destroying duplicate: {notification.gameObject.name}");
                            toDestroy.Add(notification);
                        }
                    }
                    else
                    {
                        // This is an empty/invalid notification (has placeholder text or no setup)
                        if (!isThisInstance)
                        {
                            Debug.LogWarning($"[PlayerHudNotification] Found empty/invalid notification: {notification.gameObject.name}. Text: '{text?.text ?? "null"}', HasSetup: {hasValidSetup}");
                            toDestroy.Add(notification);
                        }
                        else if (isPlaceholder || !hasValidSetup)
                        {
                            // This instance is the empty one, but we should keep it if it's the only one
                            // Wait to see if there's a working one
                        }
                    }
                }
                
                // Destroy all duplicates
                foreach (var notification in toDestroy)
                {
                    if (notification != null)
                    {
                        Debug.LogWarning($"[PlayerHudNotification] Destroying duplicate notification: {notification.gameObject.name}");
                        Destroy(notification.gameObject);
                    }
                }
                
                // If we found a working one and this isn't it, destroy this
                if (workingNotification != null && workingNotification != this)
                {
                    Debug.LogWarning($"[PlayerHudNotification] Another working notification exists. Destroying this duplicate: {gameObject.name}");
                    Destroy(gameObject);
                    yield break;
                }
            }
        }
    }
}
