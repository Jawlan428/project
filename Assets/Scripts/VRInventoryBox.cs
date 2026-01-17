using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(BoxCollider))]
public class VRInventoryBox : MonoBehaviour
{
    [Header("Slot Configuration")]
    [Tooltip("Number of slots in the X direction (width)")]
    [Range(1, 10)]
    public int slotsX = 2;
    
    [Tooltip("Number of slots in the Y direction (height)")]
    [Range(1, 10)]
    public int slotsY = 2;
    
    [Tooltip("Number of slots in the Z direction (depth)")]
    [Range(1, 10)]
    public int slotsZ = 2;
    
    [Header("Slot Spacing")]
    [Tooltip("Distance between slot centers in local space")]
    public Vector3 slotSpacing = new Vector3(0.3f, 0.3f, 0.3f);
    
    [Tooltip("Offset from box center to first slot (local space)")]
    public Vector3 slotOffset = Vector3.zero;
    
    [Tooltip("Padding from bottom of box to first row of slots")]
    public float bottomPadding = 0.02f;
    
    [Header("Socket Settings")]
    [Tooltip("Socket interactor radius for item detection")]
    [Range(0.01f, 0.5f)]
    public float socketRadius = 0.1f;
    
    [Tooltip("Should items snap to socket position?")]
    public bool snapToPosition = true;
    
    [Tooltip("Should items snap to socket rotation?")]
    public bool snapToRotation = true;
    
    [Header("Visual Debug (Optional)")]
    [Tooltip("Show gizmos for slot positions in editor")]
    public bool showGizmos = true;
    
    [Tooltip("Color for slot gizmos")]
    public Color gizmoColor = new Color(0f, 1f, 0f, 0.5f);
    
    private BoxCollider boxCollider;
    private Transform slotsParent;
    private XRSocketInteractor[] socketInteractors;
    private int totalSlots;
    private XRGrabInteractable[] occupiedSlots;
    
    void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
        totalSlots = slotsX * slotsY * slotsZ;
        occupiedSlots = new XRGrabInteractable[totalSlots];
        CreateSlotsParent();
        CreateSocketInteractors();
    }
    
    private void CreateSlotsParent()
    {
        Transform existingParent = transform.Find("InventorySlots");
        if (existingParent != null)
        {
            slotsParent = existingParent;
            return;
        }
        
        GameObject parentObj = new GameObject("InventorySlots");
        parentObj.transform.SetParent(transform);
        parentObj.transform.localPosition = Vector3.zero;
        parentObj.transform.localRotation = Quaternion.identity;
        parentObj.transform.localScale = Vector3.one;
        slotsParent = parentObj.transform;
    }
    
    private void CreateSocketInteractors()
    {
        if (socketInteractors != null && socketInteractors.Length > 0)
        {
            foreach (var socket in socketInteractors)
            {
                if (socket != null)
                    DestroyImmediate(socket.gameObject);
            }
        }
        
        socketInteractors = new XRSocketInteractor[totalSlots];
        
        // Calculate bottom Y position using BoxCollider bounds (world space) converted to local space
        Vector3 worldBottom = new Vector3(0f, boxCollider.bounds.min.y, 0f);
        Vector3 localBottom = transform.InverseTransformPoint(worldBottom);
        float bottomY = localBottom.y + bottomPadding;
        
        int slotIndex = 0;
        
        for (int x = 0; x < slotsX; x++)
        {
            for (int y = 0; y < slotsY; y++)
            {
                for (int z = 0; z < slotsZ; z++)
                {
                    // Calculate local position starting from bottom
                    Vector3 localPos = new Vector3(
                        (x - (slotsX - 1) * 0.5f) * slotSpacing.x + slotOffset.x,
                        bottomY + (y * slotSpacing.y) + slotOffset.y,
                        (z - (slotsZ - 1) * 0.5f) * slotSpacing.z + slotOffset.z
                    );
                    
                    GameObject socketObj = new GameObject($"Slot_{x}_{y}_{z}");
                    socketObj.transform.SetParent(slotsParent);
                    socketObj.transform.localPosition = localPos;
                    socketObj.transform.localRotation = Quaternion.identity;
                    socketObj.transform.localScale = Vector3.one;
                    
                    SphereCollider sphereCollider = socketObj.AddComponent<SphereCollider>();
                    sphereCollider.radius = socketRadius;
                    sphereCollider.isTrigger = true;
                    
                    XRSocketInteractor socketInteractor = socketObj.AddComponent<XRSocketInteractor>();
                    ConfigureSocketInteractor(socketInteractor, slotIndex);
                    
                    socketInteractors[slotIndex] = socketInteractor;
                    slotIndex++;
                }
            }
        }
        
        Debug.Log($"[VRInventoryBox] Created {totalSlots} inventory slots on {gameObject.name}");
    }
    
    private void ConfigureSocketInteractor(XRSocketInteractor socket, int slotIndex)
    {
        socket.interactionLayers = InteractionLayerMask.GetMask("Default");
        socket.socketActive = true;
        socket.selectEntered.AddListener((args) => OnItemPlaced(args, slotIndex));
        socket.selectExited.AddListener((args) => OnItemRemoved(args, slotIndex));
        
        XRInteractionManager manager = FindFirstObjectByType<XRInteractionManager>();
        if (manager != null)
        {
            socket.interactionManager = manager;
        }
    }
    
    private void OnItemPlaced(SelectEnterEventArgs args, int slotIndex)
    {
        XRGrabInteractable item = args.interactableObject as XRGrabInteractable;
        if (item == null)
        {
            Debug.LogWarning($"[VRInventoryBox] Non-grabbable item placed in slot {slotIndex}");
            return;
        }
        
        occupiedSlots[slotIndex] = item;
        ConfigureItemForStorage(item);
        Debug.Log($"[VRInventoryBox] Item '{item.name}' placed in slot {slotIndex}");
        
        // AUDIT INTEGRATION
        if (AuditLogger.Instance != null)
        {
            int itemCount = GetOccupiedSlotCount();
            string metaJson = $"{{\"slot\":{slotIndex},\"count\":{itemCount}}}";
            AuditLogger.Instance.Log(
                AuditEventType.APPLE_ADDED_TO_INVENTORY,
                targetId: "Inventory",
                zoneName: "Office",
                position: item.transform.position,
                metaJson: metaJson
            );
        }
    }
    
    private void OnItemRemoved(SelectExitEventArgs args, int slotIndex)
    {
        XRGrabInteractable item = args.interactableObject as XRGrabInteractable;
        if (item == null)
            return;
        
        occupiedSlots[slotIndex] = null;
        ConfigureItemForGrabbing(item);
        Debug.Log($"[VRInventoryBox] Item '{item.name}' removed from slot {slotIndex}");
        
        // AUDIT INTEGRATION
        if (AuditLogger.Instance != null)
        {
            int itemCount = GetOccupiedSlotCount();
            string metaJson = $"{{\"slot\":{slotIndex},\"count\":{itemCount}}}";
            AuditLogger.Instance.Log(
                AuditEventType.APPLE_REMOVED_FROM_INVENTORY,
                targetId: "Inventory",
                zoneName: "Office",
                position: item.transform.position,
                metaJson: metaJson
            );
        }
    }
    
    private void ConfigureItemForStorage(XRGrabInteractable item)
    {
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }
        item.enabled = true;
    }
    
    private void ConfigureItemForGrabbing(XRGrabInteractable item)
    {
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
        }
    }
    
    public int GetOccupiedSlotCount()
    {
        int count = 0;
        foreach (var item in occupiedSlots)
        {
            if (item != null)
                count++;
        }
        return count;
    }
    
    public int GetAvailableSlotCount()
    {
        return totalSlots - GetOccupiedSlotCount();
    }
    
    public bool IsSlotOccupied(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= totalSlots)
            return false;
        
        return occupiedSlots[slotIndex] != null;
    }
    
    public XRGrabInteractable GetItemInSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= totalSlots)
            return null;
        
        return occupiedSlots[slotIndex];
    }
    
    void OnDrawGizmosSelected()
    {
        if (!showGizmos)
            return;
        
        Gizmos.color = gizmoColor;
        
        // Calculate bottom Y position using BoxCollider bounds (world space) converted to local space
        Vector3 worldBottom = new Vector3(0f, boxCollider.bounds.min.y, 0f);
        Vector3 localBottom = transform.InverseTransformPoint(worldBottom);
        float bottomY = localBottom.y + bottomPadding;
        
        for (int x = 0; x < slotsX; x++)
        {
            for (int y = 0; y < slotsY; y++)
            {
                for (int z = 0; z < slotsZ; z++)
                {
                    Vector3 localPos = new Vector3(
                        (x - (slotsX - 1) * 0.5f) * slotSpacing.x + slotOffset.x,
                        bottomY + (y * slotSpacing.y) + slotOffset.y,
                        (z - (slotsZ - 1) * 0.5f) * slotSpacing.z + slotOffset.z
                    );
                    
                    Vector3 worldPos = transform.TransformPoint(localPos);
                    Gizmos.DrawWireSphere(worldPos, socketRadius);
                }
            }
        }
    }
    
    [ContextMenu("Regenerate Slots")]
    public void RegenerateSlots()
    {
        totalSlots = slotsX * slotsY * slotsZ;
        occupiedSlots = new XRGrabInteractable[totalSlots];
        CreateSocketInteractors();
    }
}
