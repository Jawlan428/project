using UnityEngine;

/// <summary>
/// Simple spawner for chicken NPCs.
/// Spawns chickens at random positions within a defined area.
/// </summary>
public class ChickenSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("Chicken prefab to spawn (must have ChickenWanderNPC component)")]
    public GameObject chickenPrefab;
    
    [Tooltip("Number of chickens to spawn")]
    [Range(1, 20)]
    public int chickenCount = 3;
    
    [Tooltip("Center point for spawn area")]
    public Transform spawnCenter;
    
    [Tooltip("Radius around center to spawn chickens")]
    [Range(1f, 50f)]
    public float spawnRadius = 8f;
    
    [Tooltip("Minimum distance between spawned chickens")]
    [Range(0.5f, 5f)]
    public float minDistanceBetweenChickens = 2f;
    
    [Header("Spawn Position")]
    [Tooltip("Y offset from spawn center (ground level)")]
    [Range(-2f, 2f)]
    public float spawnYOffset = 0f;
    
    [Tooltip("Layer mask for ground detection when spawning")]
    public LayerMask groundLayerMask = 1; // Default layer
    
    [Header("Auto Setup")]
    [Tooltip("Automatically configure spawned chickens with this wander center")]
    public Transform autoWanderCenter;
    
    [Tooltip("Automatically set wander radius on spawned chickens")]
    [Range(1f, 50f)]
    public float autoWanderRadius = 10f;
    
    private void Start()
    {
        if (chickenPrefab == null)
        {
            Debug.LogError("ChickenSpawner: Chicken Prefab is not assigned!");
            return;
        }
        
        if (spawnCenter == null)
        {
            Debug.LogWarning("ChickenSpawner: Spawn Center not set! Using this transform position.");
            spawnCenter = transform;
        }
        
        SpawnChickens();
    }
    
    public void SpawnChickens()
    {
        int spawnedCount = 0;
        int attempts = 0;
        int maxAttempts = chickenCount * 20; // Prevent infinite loops
        
        while (spawnedCount < chickenCount && attempts < maxAttempts)
        {
            attempts++;
            
            // Try to find a valid spawn position
            Vector3 spawnPosition = GetRandomSpawnPosition();
            
            if (IsValidSpawnPosition(spawnPosition))
            {
                // Spawn chicken
                GameObject chicken = Instantiate(chickenPrefab, spawnPosition, Quaternion.identity, transform);
                spawnedCount++;
                
                // Auto-configure if enabled
                if (autoWanderCenter != null)
                {
                    ChickenWanderNPC wanderScript = chicken.GetComponent<ChickenWanderNPC>();
                    if (wanderScript != null)
                    {
                        wanderScript.centerTransform = autoWanderCenter;
                        wanderScript.wanderRadius = autoWanderRadius;
                    }
                }
                
                // Name the chicken
                chicken.name = $"Chicken_{spawnedCount}";
            }
        }
        
        if (spawnedCount < chickenCount)
        {
            Debug.LogWarning($"ChickenSpawner: Only spawned {spawnedCount} out of {chickenCount} chickens. " +
                           $"Try increasing spawn radius or decreasing min distance.");
        }
        else
        {
            Debug.Log($"ChickenSpawner: Successfully spawned {spawnedCount} chickens!");
        }
    }
    
    private Vector3 GetRandomSpawnPosition()
    {
        // Get random point in circle
        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
        Vector3 centerPos = spawnCenter.position;
        
        Vector3 spawnPos = new Vector3(
            centerPos.x + randomCircle.x,
            centerPos.y + spawnYOffset,
            centerPos.z + randomCircle.y
        );
        
        // Raycast down to find ground
        RaycastHit hit;
        Vector3 rayStart = spawnPos + Vector3.up * 2f;
        
        if (Physics.Raycast(rayStart, Vector3.down, out hit, 10f, groundLayerMask))
        {
            spawnPos.y = hit.point.y + spawnYOffset;
        }
        
        return spawnPos;
    }
    
    private bool IsValidSpawnPosition(Vector3 position)
    {
        // Check distance from all existing chickens
        foreach (Transform child in transform)
        {
            if (child.gameObject.activeInHierarchy)
            {
                float distance = Vector3.Distance(position, child.position);
                if (distance < minDistanceBetweenChickens)
                {
                    return false;
                }
            }
        }
        
        return true;
    }
    
    // Visualize spawn area in editor
    private void OnDrawGizmosSelected()
    {
        if (spawnCenter != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnCenter.position, spawnRadius);
            
            // Draw spawn center
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(spawnCenter.position, 0.3f);
        }
    }
}
