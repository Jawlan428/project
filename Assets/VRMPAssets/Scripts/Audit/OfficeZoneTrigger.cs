using UnityEngine;

/// <summary>
/// Trigger zone for detecting when player enters/exits the Office.
/// Attach this to a GameObject with a Collider set to "Is Trigger".
/// </summary>
[RequireComponent(typeof(Collider))]
public class OfficeZoneTrigger : MonoBehaviour
{
    [Header("Zone Settings")]
    [Tooltip("Name of the zone (e.g., 'Office', 'Orchard')")]
    public string zoneName = "Office";

    [Tooltip("Tag of the player object (leave empty to detect any object with 'Player' tag)")]
    public string playerTag = "Player";

    [Tooltip("Should we log when player enters?")]
    public bool logEnter = true;

    [Tooltip("Should we log when player exits?")]
    public bool logExit = true;

    private Collider triggerCollider;

    void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
        else
        {
            Debug.LogError($"[OfficeZoneTrigger] No Collider found on {gameObject.name}. Adding BoxCollider.");
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            triggerCollider = boxCollider;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!logEnter) return;

        // Check if it's the player
        if (string.IsNullOrEmpty(playerTag) || other.CompareTag(playerTag))
        {
            // Try to find XR Origin or main camera (player head)
            Transform playerTransform = GetPlayerTransform(other.gameObject);
            Vector3 playerPos = playerTransform != null ? playerTransform.position : other.transform.position;

            // AUDIT INTEGRATION
            if (AuditLogger.Instance != null)
            {
                AuditLogger.Instance.Log(
                    AuditEventType.ENTER_OFFICE,
                    zoneName: zoneName,
                    position: playerPos
                );
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!logExit) return;

        // Check if it's the player
        if (string.IsNullOrEmpty(playerTag) || other.CompareTag(playerTag))
        {
            // Try to find XR Origin or main camera (player head)
            Transform playerTransform = GetPlayerTransform(other.gameObject);
            Vector3 playerPos = playerTransform != null ? playerTransform.position : other.transform.position;

            // AUDIT INTEGRATION
            if (AuditLogger.Instance != null)
            {
                AuditLogger.Instance.Log(
                    AuditEventType.EXIT_OFFICE,
                    zoneName: zoneName,
                    position: playerPos
                );
            }
        }
    }

    /// <summary>
    /// Attempts to find the player's head/transform from the colliding object.
    /// </summary>
    private Transform GetPlayerTransform(GameObject obj)
    {
        // Check if it's the XR Origin or has a Camera (head)
        Camera cam = obj.GetComponentInChildren<Camera>();
        if (cam != null)
            return cam.transform;

        // Check parent for XR Origin
        Transform parent = obj.transform.parent;
        if (parent != null)
        {
            cam = parent.GetComponentInChildren<Camera>();
            if (cam != null)
                return cam.transform;
        }

        // Fallback to the object's transform
        return obj.transform;
    }

    void OnDrawGizmos()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            if (triggerCollider is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
            }
            else if (triggerCollider is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
            }
        }
    }
}
