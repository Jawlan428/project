using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Script to handle apple detachment from tree when grabbed.
/// Destroys the FixedJoint component when the apple is grabbed via XR Interaction Toolkit.
/// </summary>
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
[RequireComponent(typeof(FixedJoint))]
public class AppleGrabHandler : MonoBehaviour
{
    [Header("Joint Settings")]
    [Tooltip("Should the joint be destroyed when grabbed?")]
    public bool destroyJointOnGrab = true;
    
    [Tooltip("Delay before destroying the joint (in seconds). Can create a more realistic pull effect.")]
    public float jointBreakDelay = 0f;
    
    [Header("Audio/Effects (Optional)")]
    [Tooltip("Play sound when apple is detached from tree")]
    public AudioSource detachSound;
    
    [Tooltip("Particle effect to spawn when apple detaches")]
    public GameObject detachEffect;
    
    private FixedJoint fixedJoint;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    private bool hasBeenGrabbed = false;
    
    void Start()
    {
        // Get required components
        fixedJoint = GetComponent<FixedJoint>();
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        
        // Validate components
        if (fixedJoint == null)
        {
            Debug.LogWarning($"AppleGrabHandler: No FixedJoint found on {gameObject.name}. Joint destruction will not work.");
        }
        
        if (grabInteractable == null)
        {
            Debug.LogWarning($"AppleGrabHandler: No XRGrabInteractable found on {gameObject.name}.");
            return;
        }
        
        // Subscribe to grab events
        grabInteractable.selectEntered.AddListener(OnGrabbed);
    }
    
    void OnGrabbed(SelectEnterEventArgs args)
    {
        // Only break joint once (prevents issues if re-grabbed)
        if (hasBeenGrabbed || !destroyJointOnGrab || fixedJoint == null)
            return;
        
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
    
    void OnDestroy()
    {
        // Clean up event listeners
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        }
    }
}

