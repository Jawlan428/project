using UnityEngine;

/// <summary>
/// Makes Chicken_001 wander freely within a defined area.
/// Uses CharacterController.Move (NOT root motion).
/// Automatically disables conflicting scripts.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CharacterController))]
public class ChickenWanderNPC : MonoBehaviour
{
    [Header("Wander Area")]
    [Tooltip("Center point for wandering. If null, uses initial position.")]
    public Transform centerTransform;
    
    [Tooltip("Maximum distance from center")]
    [Range(1f, 20f)]
    public float wanderRadius = 3f;
    
    [Header("Movement")]
    [Tooltip("Walking speed")]
    [Range(0.1f, 3f)]
    public float walkSpeed = 0.6f;
    
    [Tooltip("Rotation speed")]
    [Range(50f, 500f)]
    public float turnSpeed = 240f;
    
    [Tooltip("Distance to stop at destination")]
    [Range(0.1f, 1f)]
    public float stoppingDistance = 0.2f;
    
    [Header("Behavior")]
    [Tooltip("Minimum idle time (seconds)")]
    [Range(0.5f, 10f)]
    public float idleTimeMin = 1f;
    
    [Tooltip("Maximum idle time (seconds)")]
    [Range(1f, 10f)]
    public float idleTimeMax = 3f;
    
    [Header("Gravity")]
    [Tooltip("Gravity force")]
    public float gravity = -9.81f;
    
    // Components
    private Animator animator;
    private CharacterController characterController;
    
    // State
    private Vector3 currentDestination;
    private Vector3 wanderCenterPosition;
    private float idleTimer;
    private bool isMoving;
    private Vector3 velocity; // For gravity
    private Vector3 lastPosition; // For movement detection
    
    // Animation
    private const string SPEED_PARAM = "Speed";
    private const string STATE_PARAM = "State";
    
    private void Awake()
    {
        // Early debug log to confirm script is attached
        Debug.Log($"[ChickenWanderNPC] Awake() called on {gameObject.name}. Script is attached and GameObject is active.");
        
        // CRITICAL: Disable conflicting scripts automatically
        DisableConflictingScripts();
    }
    
    private void Start()
    {
        // Get components
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
        
        // Validate components
        if (animator == null)
        {
            Debug.LogError("[ChickenWanderNPC] Animator not found! Disabling.");
            enabled = false;
            return;
        }
        
        if (characterController == null)
        {
            Debug.LogError("[ChickenWanderNPC] CharacterController not found! Disabling.");
            enabled = false;
            return;
        }
        
        // CRITICAL: Disable root motion (we handle movement manually)
        animator.applyRootMotion = false;
        
        // Set wander center
        if (centerTransform != null)
        {
            wanderCenterPosition = centerTransform.position;
        }
        else
        {
            wanderCenterPosition = transform.position;
        }
        
        // Initialize state
        isMoving = false;
        idleTimer = Random.Range(idleTimeMin, idleTimeMax);
        velocity = Vector3.zero;
        lastPosition = transform.position;
        
        // Set idle animation
        SetAnimationIdle();
        
        Debug.Log($"[ChickenWanderNPC] Started on {gameObject.name}. Center: {wanderCenterPosition}, Radius: {wanderRadius}");
    }
    
    private void Update()
    {
        // Update gravity
        UpdateGravity();
        
        if (isMoving)
        {
            // Move towards destination
            MoveTowardsDestination();
            
            // Check if arrived
            if (HasReachedDestination())
            {
                isMoving = false;
                idleTimer = Random.Range(idleTimeMin, idleTimeMax);
                SetAnimationIdle();
                Debug.Log($"[ChickenWanderNPC] Arrived, idling for {idleTimer:F2} seconds...");
            }
        }
        else
        {
            // Idle: wait, then pick new target
            idleTimer -= Time.deltaTime;
            
            if (idleTimer <= 0f)
            {
                ChooseNewDestination();
                isMoving = true;
                SetAnimationWalk();
                Debug.Log($"[ChickenWanderNPC] New target: {currentDestination}");
            }
        }
        
        // Debug: Check if actually moving
        if (isMoving)
        {
            float moved = Vector3.Distance(transform.position, lastPosition);
            if (moved < 0.001f && Time.time > 2f)
            {
                Debug.LogWarning($"[ChickenWanderNPC] WARNING: Not moving! Position unchanged. Check CharacterController.");
            }
            lastPosition = transform.position;
        }
    }
    
    /// <summary>
    /// Automatically disables known conflicting scripts
    /// </summary>
    private void DisableConflictingScripts()
    {
        System.Collections.Generic.List<string> disabledScripts = new System.Collections.Generic.List<string>();
        
        // Disable CreatureMover (from asset pack)
        var creatureMover = GetComponent<ithappy.Animals_FREE.CreatureMover>();
        if (creatureMover != null && creatureMover.enabled)
        {
            creatureMover.enabled = false;
            disabledScripts.Add("CreatureMover");
        }
        
        // Disable MovePlayerInput
        var movePlayerInput = GetComponent<ithappy.Animals_FREE.MovePlayerInput>();
        if (movePlayerInput != null && movePlayerInput.enabled)
        {
            movePlayerInput.enabled = false;
            disabledScripts.Add("MovePlayerInput");
        }
        
        // Disable ThirdPersonCamera
        var thirdPersonCamera = GetComponent<ithappy.Animals_FREE.ThirdPersonCamera>();
        if (thirdPersonCamera != null && thirdPersonCamera.enabled)
        {
            thirdPersonCamera.enabled = false;
            disabledScripts.Add("ThirdPersonCamera");
        }
        
        // Disable PlayerCamera
        var playerCamera = GetComponent<ithappy.Animals_FREE.PlayerCamera>();
        if (playerCamera != null && playerCamera.enabled)
        {
            playerCamera.enabled = false;
            disabledScripts.Add("PlayerCamera");
        }
        
        // Warn if Rigidbody exists and is not kinematic
        var rigidbody = GetComponent<Rigidbody>();
        if (rigidbody != null && !rigidbody.isKinematic)
        {
            rigidbody.isKinematic = true;
            disabledScripts.Add("Rigidbody (set to kinematic)");
        }
        
        // Log what was disabled
        if (disabledScripts.Count > 0)
        {
            Debug.LogWarning($"[ChickenWanderNPC] Disabled conflicting scripts: {string.Join(", ", disabledScripts)}");
        }
    }
    
    /// <summary>
    /// Updates gravity velocity
    /// </summary>
    private void UpdateGravity()
    {
        if (characterController.isGrounded)
        {
            velocity.y = -0.5f; // Small downward force to stay grounded
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
            velocity.y = Mathf.Max(velocity.y, -20f); // Clamp max fall speed
        }
    }
    
    /// <summary>
    /// Chooses a random destination within wander radius
    /// </summary>
    private void ChooseNewDestination()
    {
        // Random point in circle
        Vector2 randomCircle = Random.insideUnitCircle * wanderRadius;
        
        // Calculate destination (use current Y)
        currentDestination = new Vector3(
            wanderCenterPosition.x + randomCircle.x,
            transform.position.y,
            wanderCenterPosition.z + randomCircle.y
        );
        
        // Clamp to radius
        Vector3 horizontalDest = new Vector3(currentDestination.x, wanderCenterPosition.y, currentDestination.z);
        float distFromCenter = Vector3.Distance(horizontalDest, wanderCenterPosition);
        
        if (distFromCenter > wanderRadius)
        {
            Vector3 dir = (horizontalDest - wanderCenterPosition).normalized;
            currentDestination = wanderCenterPosition + dir * wanderRadius;
            currentDestination.y = transform.position.y;
        }
    }
    
    /// <summary>
    /// Moves chicken towards destination using CharacterController.Move
    /// CRITICAL: Combines horizontal movement + gravity in ONE Move() call
    /// </summary>
    private void MoveTowardsDestination()
    {
        // Calculate direction (horizontal only)
        Vector3 direction = currentDestination - transform.position;
        direction.y = 0f;
        
        if (direction.magnitude < 0.01f)
        {
            return;
        }
        
        direction.Normalize();
        
        // Calculate horizontal movement
        Vector3 horizontalMove = direction * walkSpeed * Time.deltaTime;
        
        // Combine with gravity
        Vector3 totalMove = horizontalMove + (velocity * Time.deltaTime);
        
        // CRITICAL: Move in one call (combines movement + gravity)
        characterController.Move(totalMove);
        
        // Rotate towards direction
        if (direction != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                turnSpeed * Time.deltaTime
            );
        }
    }
    
    /// <summary>
    /// Checks if destination reached
    /// </summary>
    private bool HasReachedDestination()
    {
        Vector3 horizontalPos = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 horizontalDest = new Vector3(currentDestination.x, 0f, currentDestination.z);
        return Vector3.Distance(horizontalPos, horizontalDest) <= stoppingDistance;
    }
    
    /// <summary>
    /// Sets animation to idle
    /// </summary>
    private void SetAnimationIdle()
    {
        if (animator == null) return;
        
        // Try Speed parameter first
        if (HasAnimatorParameter(SPEED_PARAM))
        {
            animator.SetFloat(SPEED_PARAM, 0f);
            return;
        }
        
        // Try State parameter
        if (HasAnimatorParameter(STATE_PARAM))
        {
            animator.SetFloat(STATE_PARAM, 0f); // 0 = Idle
            return;
        }
        
        // Fallback: Try state names
        TryPlayState("Idle");
        TryPlayState("Base Layer.Idle");
        TryPlayClip("Chicken_001_idle");
    }
    
    /// <summary>
    /// Sets animation to walk
    /// </summary>
    private void SetAnimationWalk()
    {
        if (animator == null) return;
        
        // Try Speed parameter first
        if (HasAnimatorParameter(SPEED_PARAM))
        {
            animator.SetFloat(SPEED_PARAM, walkSpeed);
            return;
        }
        
        // Try State parameter
        if (HasAnimatorParameter(STATE_PARAM))
        {
            animator.SetFloat(STATE_PARAM, 0.5f); // 0.5 = Walk
            return;
        }
        
        // Fallback: Try state names
        TryPlayState("Walk");
        TryPlayState("Base Layer.Walk");
        TryPlayClip("Chicken_003_walk");
    }
    
    /// <summary>
    /// Checks if animator has parameter
    /// </summary>
    private bool HasAnimatorParameter(string paramName)
    {
        if (animator == null || animator.parameters == null) return false;
        
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == paramName)
            {
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Tries to play animation state by name
    /// </summary>
    private void TryPlayState(string stateName)
    {
        if (animator == null) return;
        
        try
        {
            int hash = Animator.StringToHash(stateName);
            if (animator.HasState(0, hash))
            {
                animator.Play(hash, 0);
            }
        }
        catch { }
    }
    
    /// <summary>
    /// Tries to play animation clip by name
    /// </summary>
    private void TryPlayClip(string clipName)
    {
        if (animator == null) return;
        
        try
        {
            animator.CrossFade(clipName, 0.2f);
        }
        catch { }
    }
    
    /// <summary>
    /// Draw gizmos in editor
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Vector3 center = centerTransform != null ? centerTransform.position : transform.position;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, wanderRadius);
        
        if (Application.isPlaying && isMoving)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentDestination);
            Gizmos.DrawWireSphere(currentDestination, stoppingDistance);
        }
    }
}
