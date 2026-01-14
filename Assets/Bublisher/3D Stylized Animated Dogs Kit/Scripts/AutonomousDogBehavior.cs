using UnityEngine;

namespace DemoKitStylizedAnimatedDogs
{
    /// <summary>
    /// Simple autonomous dog behavior for VR environments.
    /// Makes the dog wander randomly within a defined area, with idle and sitting states.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class AutonomousDogBehavior : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Center point for the dog's wandering area")]
        public Transform wanderCenter;
        
        [Tooltip("Maximum distance from center the dog can wander")]
        [Range(2f, 20f)]
        public float wanderRadius = 10f;
        
        [Tooltip("Speed when walking")]
        [Range(0.5f, 3f)]
        public float walkSpeed = 1.5f;
        
        [Tooltip("Speed when running (optional)")]
        [Range(2f, 5f)]
        public float runSpeed = 3f;
        
        [Tooltip("How fast the dog rotates toward movement direction")]
        [Range(1f, 10f)]
        public float rotationSpeed = 5f;

        [Header("Behavior Settings")]
        [Tooltip("Minimum time to idle before moving again (seconds)")]
        [Range(1f, 10f)]
        public float minIdleTime = 2f;
        
        [Tooltip("Maximum time to idle before moving again (seconds)")]
        [Range(2f, 15f)]
        public float maxIdleTime = 5f;
        
        [Tooltip("Minimum time to walk before considering stopping (seconds)")]
        [Range(2f, 10f)]
        public float minWalkTime = 3f;
        
        [Tooltip("Maximum time to walk before stopping (seconds)")]
        [Range(5f, 20f)]
        public float maxWalkTime = 8f;
        
        [Tooltip("Chance to sit instead of idle (0-1)")]
        [Range(0f, 1f)]
        public float sitChance = 0.3f;
        
        [Tooltip("Chance to run instead of walk (0-1)")]
        [Range(0f, 1f)]
        public float runChance = 0.2f;

        [Header("Ground Settings")]
        [Tooltip("Lock Y position to this value (set to dog's current Y if unsure)")]
        public bool lockYPosition = true;
        
        [Tooltip("Y position to lock to (only used if lockYPosition is true)")]
        public float lockedYPosition = 0f;

        // Animation IDs from the animator controller
        private const int ANIM_IDLE = 0;           // Breathing/Idle
        private const int ANIM_WALKING = 2;        // Walking01
        private const int ANIM_RUNNING = 4;        // Running
        private const int ANIM_SITTING = 7;        // SittingStart

        // Components
        private Animator animator;
        private CharacterController characterController;

        // State management
        private enum DogState
        {
            Idle,
            Walking,
            Running,
            Sitting
        }

        private DogState currentState = DogState.Idle;
        private float stateTimer = 0f;
        private float stateDuration = 0f;

        // Movement
        private Vector3 targetPosition;
        private Vector3 startPosition;
        private bool hasReachedTarget = false;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            characterController = GetComponent<CharacterController>();
            
            // If no CharacterController, add one for smooth movement
            if (characterController == null)
            {
                characterController = gameObject.AddComponent<CharacterController>();
                characterController.height = 1f;
                characterController.radius = 0.3f;
                characterController.center = new Vector3(0, 0.5f, 0);
            }

            // Store initial position as wander center if not set
            if (wanderCenter == null)
            {
                GameObject centerObj = new GameObject("DogWanderCenter");
                centerObj.transform.position = transform.position;
                wanderCenter = centerObj.transform;
            }

            startPosition = transform.position;
            
            // Lock Y position if enabled
            if (lockYPosition)
            {
                lockedYPosition = transform.position.y;
            }
        }

        private void Start()
        {
            // Start in idle state
            SetState(DogState.Idle);
        }

        private void Update()
        {
            // Update state timer
            stateTimer += Time.deltaTime;

            // Lock Y position if enabled
            if (lockYPosition)
            {
                Vector3 pos = transform.position;
                pos.y = lockedYPosition;
                transform.position = pos;
            }

            // Handle current state
            switch (currentState)
            {
                case DogState.Idle:
                    HandleIdle();
                    break;
                case DogState.Walking:
                    HandleWalking();
                    break;
                case DogState.Running:
                    HandleRunning();
                    break;
                case DogState.Sitting:
                    HandleSitting();
                    break;
            }
        }

        private void HandleIdle()
        {
            // Check if it's time to move
            if (stateTimer >= stateDuration)
            {
                // Decide next action: walk or run
                bool shouldRun = Random.value < runChance;
                SetState(shouldRun ? DogState.Running : DogState.Walking);
            }
        }

        private void HandleWalking()
        {
            // Move toward target
            MoveTowardTarget(walkSpeed);

            // Check if reached target or time to stop
            if (hasReachedTarget || stateTimer >= stateDuration)
            {
                // Decide next action: idle or sit
                bool shouldSit = Random.value < sitChance;
                SetState(shouldSit ? DogState.Sitting : DogState.Idle);
            }
        }

        private void HandleRunning()
        {
            // Move toward target faster
            MoveTowardTarget(runSpeed);

            // Check if reached target or time to stop
            if (hasReachedTarget || stateTimer >= stateDuration)
            {
                // After running, usually go to idle
                SetState(DogState.Idle);
            }
        }

        private void HandleSitting()
        {
            // Just sit and wait
            if (stateTimer >= stateDuration)
            {
                // After sitting, go to idle
                SetState(DogState.Idle);
            }
        }

        private void MoveTowardTarget(float speed)
        {
            // Calculate direction to target
            Vector3 direction = (targetPosition - transform.position);
            direction.y = 0; // Keep movement horizontal

            float distanceToTarget = direction.magnitude;

            // Check if we've reached the target
            if (distanceToTarget < 0.5f)
            {
                hasReachedTarget = true;
                return;
            }

            // Normalize direction
            direction.Normalize();

            // Rotate toward movement direction
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            // Move using CharacterController
            Vector3 movement = direction * speed * Time.deltaTime;
            characterController.Move(movement);
        }

        private void SetState(DogState newState)
        {
            // Exit current state
            currentState = newState;
            stateTimer = 0f;
            hasReachedTarget = false;

            // Set animation based on state
            switch (newState)
            {
                case DogState.Idle:
                    animator.SetInteger("AnimationID", ANIM_IDLE);
                    stateDuration = Random.Range(minIdleTime, maxIdleTime);
                    break;

                case DogState.Walking:
                    animator.SetInteger("AnimationID", ANIM_WALKING);
                    stateDuration = Random.Range(minWalkTime, maxWalkTime);
                    PickRandomTarget();
                    break;

                case DogState.Running:
                    animator.SetInteger("AnimationID", ANIM_RUNNING);
                    stateDuration = Random.Range(minWalkTime * 0.7f, maxWalkTime * 0.7f); // Shorter duration for running
                    PickRandomTarget();
                    break;

                case DogState.Sitting:
                    animator.SetInteger("AnimationID", ANIM_SITTING);
                    stateDuration = Random.Range(minIdleTime * 1.5f, maxIdleTime * 2f); // Sit longer
                    break;
            }
        }

        private void PickRandomTarget()
        {
            // Pick a random point within the wander radius
            Vector2 randomCircle = Random.insideUnitCircle * wanderRadius;
            Vector3 centerPos = wanderCenter != null ? wanderCenter.position : startPosition;
            
            targetPosition = centerPos + new Vector3(randomCircle.x, 0, randomCircle.y);
            
            // Ensure Y is locked
            if (lockYPosition)
            {
                targetPosition.y = lockedYPosition;
            }
            else
            {
                targetPosition.y = transform.position.y;
            }
        }

        // Visualize wander area in editor
        private void OnDrawGizmosSelected()
        {
            if (wanderCenter != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(wanderCenter.position, wanderRadius);
            }
            else if (Application.isPlaying)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(startPosition, wanderRadius);
            }

            // Draw target position
            if (Application.isPlaying && currentState != DogState.Idle && currentState != DogState.Sitting)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(targetPosition, 0.3f);
                Gizmos.DrawLine(transform.position, targetPosition);
            }
        }
    }
}
