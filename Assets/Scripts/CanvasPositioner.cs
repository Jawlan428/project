using UnityEngine;

public class CanvasPositioner : MonoBehaviour
{
    [Header("Settings")]
    public float distanceFromCamera = 2f;
    public float heightOffset = 0f;
    public bool updateEveryFrame = false;
    
    private Camera mainCamera;
    private Canvas canvas;

    void Start()
    {
        canvas = GetComponent<Canvas>();
        
        // Make sure Canvas is in World Space
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            Debug.Log("✅ Canvas set to World Space");
        }
        
        // Find the main camera
        FindCamera();
        
        // Position the canvas
        if (mainCamera != null)
        {
            PositionCanvas();
            Debug.Log("✅ Canvas positioned in front of camera: " + mainCamera.name);
        }
        else
        {
            Debug.LogError("❌ No camera found! Canvas cannot be positioned.");
        }
    }

    void Update()
    {
        if (updateEveryFrame && mainCamera != null)
        {
            PositionCanvas();
        }
    }

    void FindCamera()
    {
        // Try to find the main camera
        mainCamera = Camera.main;
        
        if (mainCamera == null)
        {
            // Find any active camera
            mainCamera = FindObjectOfType<Camera>();
        }
        
        if (mainCamera != null)
        {
            Debug.Log("🎥 Found camera: " + mainCamera.name);
        }
    }

    void PositionCanvas()
    {
        if (mainCamera == null) return;
        
        // Position canvas in front of camera
        Vector3 position = mainCamera.transform.position + 
                          mainCamera.transform.forward * distanceFromCamera +
                          mainCamera.transform.up * heightOffset;
        
        transform.position = position;
        
        // Make canvas face the camera
        transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position);
        
        // Set scale
        transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
        
        // Set canvas size
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.sizeDelta = new Vector2(500, 300);
        }
    }

    // Call this from inspector or other scripts to reposition
    public void RepositionCanvas()
    {
        FindCamera();
        PositionCanvas();
    }
}

