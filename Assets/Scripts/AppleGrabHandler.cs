using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Script to handle apple detachment from tree when grabbed.
/// Destroys the FixedJoint component when the apple is grabbed via XR Interaction Toolkit.
/// Note: FixedJoint is optional - it will be destroyed after first grab, so it's not required to persist.
/// </summary>
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class AppleGrabHandler : MonoBehaviour
{
    [Header("Joint Settings")]
    [Tooltip("Should the joint be destroyed when grabbed?")]
    public bool destroyJointOnGrab = true;
    
    [Tooltip("Delay before destroying the joint (in seconds). Can create a more realistic pull effect.")]
    public float jointBreakDelay = 0f;
    
    [Header("Physics Settings")]
    [Tooltip("Ensure the apple stays where you release it")]
    public bool keepPositionOnRelease = true;
    
    [Header("Audio/Effects (Optional)")]
    [Tooltip("Play sound when apple is detached from tree")]
    public AudioSource detachSound;
    
    [Tooltip("Particle effect to spawn when apple detaches")]
    public GameObject detachEffect;

    [Header("Behavior Logging (Optional)")]
    public PlayerBehaviorLogger behaviorLogger;
    
    private FixedJoint fixedJoint;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private SmartFarm.Harvest.AppleHarvest harvestStatus;
    private bool hasBeenGrabbed = false;
    private Vector3 originalPosition;
    private bool isOriginalPositionSet = false;
    private Vector3 lastValidPosition;
    private bool isBeingGrabbed = false;
    
    void Start()
    {
        // Get required components
        fixedJoint = GetComponent<FixedJoint>();
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        harvestStatus = GetComponent<SmartFarm.Harvest.AppleHarvest>();
        if (behaviorLogger == null)
            behaviorLogger = FindFirstObjectByType<PlayerBehaviorLogger>();
        
        // Validate components
        // FixedJoint is optional - it will be destroyed after first grab if present
        if (fixedJoint == null)
        {
            Debug.Log($"AppleGrabHandler: No FixedJoint found on {gameObject.name}. Apple is already detached or doesn't need joint breaking.");
        }
        
        if (grabInteractable == null)
        {
            Debug.LogWarning($"AppleGrabHandler: No XRGrabInteractable found on {gameObject.name}.");
            return;
        }
        
        if (rb == null)
        {
            Debug.LogWarning($"AppleGrabHandler: No Rigidbody found on {gameObject.name}.");
            return;
        }
        
        // Store original position
        if (!isOriginalPositionSet)
        {
            originalPosition = transform.position;
            isOriginalPositionSet = true;
        }
        
        // Configure XRGrabInteractable settings to ensure proper physics behavior
        ConfigureGrabInteractable();
        
        // Subscribe to grab/release events
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
        
        // Initialize last valid position
        lastValidPosition = transform.position;
    }
    
    void LateUpdate()
    {
        // If the object has been grabbed at least once and joint is broken, prevent snap-back
        if (hasBeenGrabbed && fixedJoint == null && !isBeingGrabbed)
        {
            // Check if object is trying to snap back to original position
            float distanceToOriginal = Vector3.Distance(transform.position, originalPosition);
            float distanceToLastValid = Vector3.Distance(transform.position, lastValidPosition);
            
            // If object is very close to original position but far from last valid, it's snapping back
            if (distanceToOriginal < 0.1f && distanceToLastValid > 0.5f)
            {
                Debug.LogWarning($"{gameObject.name} detected snap-back attempt! Preventing...");
                
                // Force correct settings
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.constraints = RigidbodyConstraints.None;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                
                grabInteractable.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.VelocityTracking;
                
                // Restore to last valid position
                if (lastValidPosition != Vector3.zero)
                {
                    transform.position = lastValidPosition;
                }
            }
            else
            {
                // Update last valid position if object has moved significantly
                if (distanceToLastValid > 0.1f)
                {
                    lastValidPosition = transform.position;
                }
            }
        }
    }
    
    void ConfigureGrabInteractable()
    {
        // CRITICAL: Set movement type to VelocityTracking to prevent snap-back
        // Kinematic or Instantaneous will cause the object to return to original position
        grabInteractable.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.VelocityTracking;
        
        // Ensure track position and rotation are enabled
        grabInteractable.trackPosition = true;
        grabInteractable.trackRotation = true;
        
        // CRITICAL: Ensure interaction layers allow all interactors (both hands)
        // Make sure interaction layer mask is not restrictive
        try
        {
            // Check if there's an interaction layer mask that might be blocking interactors
            // The default should allow all layers
            // We'll log the current settings for debugging
            var interactableLayers = grabInteractable.interactionLayers;
            Debug.Log($"{gameObject.name} Interaction Layers: {interactableLayers.value}");
        }
        catch
        {
            // If property doesn't exist in this version, continue
        }
        
        // CRITICAL: Allow re-grabbing - ensure interactable is not disabled after first grab
        // The interactable should remain enabled and interactable after release
        
        // Remove attach transform if it exists - this can cause snap-back behavior
        if (grabInteractable.attachTransform != null && grabInteractable.attachTransform == transform)
        {
            // If attach transform is set to self, we should clear it or create a child
            // For now, we'll leave it but ensure it doesn't cause issues
        }
        
        // Ensure the rigidbody is not kinematic initially (unless joint is holding it)
        if (fixedJoint == null || !fixedJoint.gameObject.activeInHierarchy)
        {
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.None;
        }
        
        Debug.Log($"{gameObject.name}: Configured - Movement Type: {grabInteractable.movementType}, Track Position: {grabInteractable.trackPosition}, Track Rotation: {grabInteractable.trackRotation}");
    }
    
    void OnGrabbed(SelectEnterEventArgs args)
    {
        // HARVEST GATING: unripe apples must stay on the tree. AppleHarvest will
        // force-release the interactor; here we simply refuse to break the joint
        // or alter physics so the apple never detaches.
        if (harvestStatus != null && !harvestStatus.IsReady)
        {
            Debug.Log($"{gameObject.name} grab rejected - apple is not ready for harvesting.");
            return;
        }

        isBeingGrabbed = true;
        
        // Log which interactor is grabbing (for debugging)
        Debug.Log($"{gameObject.name} grabbed by: {args.interactorObject.transform.name}");
        if (behaviorLogger != null)
            behaviorLogger.LogGrab(gameObject);
        
        // AUDIT INTEGRATION
        if (AuditLogger.Instance != null)
        {
            AuditLogger.Instance.Log(
                AuditEventType.APPLE_PICKED,
                targetId: gameObject.name,
                zoneName: "Orchard",
                position: transform.position
            );
        }
        
        // CRITICAL: Ensure movement type is VelocityTracking when grabbed
        // This prevents the object from snapping back to original position
        grabInteractable.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.VelocityTracking;
        
        // Ensure Rigidbody settings are correct
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.None;
        }
        
        // Update last valid position while grabbing
        lastValidPosition = transform.position;
        
        // Only break joint once on first grab (prevents issues if re-grabbed after joint is already broken)
        // Allow re-grabbing after joint is broken - this is why we check fixedJoint == null
        if (hasBeenGrabbed || !destroyJointOnGrab || fixedJoint == null)
        {
            // Joint already broken or shouldn't be broken - allow normal grab behavior
            return;
        }
        
        // Mark as grabbed and break joint only on first grab
        hasBeenGrabbed = true;
        
        // Delay destruction if specified
        if (jointBreakDelay > 0f)
        {
            Invoke(nameof(BreakJoint), jointBreakDelay);
        }
        else
        {
            BreakJoint();
        }
    }
    
    void BreakJoint()
    {
        if (fixedJoint != null)
        {
            // Ensure Rigidbody is not kinematic when breaking joint
            if (rb != null)
            {
                rb.isKinematic = false;
                // Remove any position constraints
                rb.constraints = RigidbodyConstraints.None;
            }
            
            // Play sound effect if available
            if (detachSound != null && detachSound.clip != null)
            {
                detachSound.Play();
            }
            
            // Spawn particle effect if available
            if (detachEffect != null)
            {
                GameObject effect = Instantiate(detachEffect, transform.position, Quaternion.identity);
                // Auto-destroy effect after 5 seconds if it doesn't have its own cleanup
                Destroy(effect, 5f);
            }
            
            // Destroy the joint component
            Destroy(fixedJoint);
            fixedJoint = null;
            
            Debug.Log($"{gameObject.name} detached from tree!");
        }
    }
    
    void OnReleased(SelectExitEventArgs args)
    {
        // Only set isBeingGrabbed to false if no other interactors are grabbing
        // Check if object is still selected by another interactor
        if (!grabInteractable.isSelected)
        {
            isBeingGrabbed = false;
        }
        
        Debug.Log($"{gameObject.name} released by: {args.interactorObject.transform.name}. Still selected: {grabInteractable.isSelected}");
        
        // AUDIT INTEGRATION
        if (AuditLogger.Instance != null && !grabInteractable.isSelected)
        {
            AuditLogger.Instance.Log(
                AuditEventType.APPLE_DROPPED,
                targetId: gameObject.name,
                zoneName: "Orchard",
                position: transform.position
            );
        }
        
        if (rb == null)
            return;
        
        // CRITICAL: Store the current position before any potential snap-back
        Vector3 releasePosition = transform.position;
        lastValidPosition = releasePosition; // Update last valid position
        
        // Force movement type to VelocityTracking (prevents snap-back)
        grabInteractable.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.VelocityTracking;
        
        // Ensure the rigidbody is NOT kinematic - this is crucial!
        rb.isKinematic = false;
        
        // Remove ALL constraints that might cause it to return
        rb.constraints = RigidbodyConstraints.None;
        
        // Only zero velocity if completely released (not being held by other hand)
        if (!grabInteractable.isSelected)
        {
            // Zero out any velocity that might be pulling it back
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            
            // Use a coroutine to continuously enforce position if needed
            if (keepPositionOnRelease)
            {
                StartCoroutine(EnforcePositionAfterRelease(releasePosition));
            }
        }
        
        Debug.Log($"{gameObject.name} released at position: {releasePosition}. Movement Type: {grabInteractable.movementType}, IsKinematic: {rb.isKinematic}");
    }
    
    System.Collections.IEnumerator EnforcePositionAfterRelease(Vector3 targetPosition)
    {
        float elapsed = 0f;
        float enforceDuration = 0.5f; // Enforce for half a second
        
        while (elapsed < enforceDuration)
        {
            elapsed += Time.deltaTime;
            
            // Continuously ensure settings are correct
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.constraints = RigidbodyConstraints.None;
            }
            
            grabInteractable.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.VelocityTracking;
            
            yield return null;
        }
        
        Debug.Log($"{gameObject.name} position enforcement complete. Final position: {transform.position}");
    }
    
    void OnDestroy()
    {
        // Clean up event listeners
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }
}

