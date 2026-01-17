#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor script to automatically set up the Audit System in the current scene.
/// </summary>
public class AuditSystemSetup : EditorWindow
{
    [MenuItem("Tools/Audit System/Setup Audit System in Current Scene")]
    public static void SetupAuditSystem()
    {
        // Check if we're in a scene
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == "")
        {
            EditorUtility.DisplayDialog("Error", "Please open a scene first!", "OK");
            return;
        }

        // Check if AuditSystem already exists
        GameObject existingSystem = GameObject.Find("AuditSystem");
        if (existingSystem != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                "AuditSystem Already Exists",
                "An AuditSystem GameObject already exists in this scene. Do you want to remove it and create a new one?",
                "Yes, Replace",
                "Cancel"
            );

            if (replace)
            {
                DestroyImmediate(existingSystem);
            }
            else
            {
                Debug.Log("[AuditSystemSetup] Setup cancelled - AuditSystem already exists.");
                return;
            }
        }

        // Create the AuditSystem GameObject
        GameObject auditSystem = new GameObject("AuditSystem");
        
        // Add AuditBootstrap component
        auditSystem.AddComponent<AuditBootstrap>();

        // Mark scene as dirty so changes are saved
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        // Select the new GameObject
        Selection.activeGameObject = auditSystem;

        Debug.Log("[AuditSystemSetup] ✅ AuditSystem created successfully! Component: AuditBootstrap");
        EditorUtility.DisplayDialog(
            "Setup Complete!",
            "AuditSystem GameObject has been created with AuditBootstrap component.\n\n" +
            "The system will automatically start when you play the scene.",
            "OK"
        );
    }

    [MenuItem("Tools/Audit System/Check Audit System Status")]
    public static void CheckAuditSystemStatus()
    {
        GameObject auditSystem = GameObject.Find("AuditSystem");
        
        if (auditSystem == null)
        {
            EditorUtility.DisplayDialog(
                "Status",
                "❌ AuditSystem GameObject not found in current scene.\n\nUse 'Setup Audit System' to create it.",
                "OK"
            );
            return;
        }

        AuditBootstrap bootstrap = auditSystem.GetComponent<AuditBootstrap>();
        
        if (bootstrap == null)
        {
            EditorUtility.DisplayDialog(
                "Status",
                "⚠️ AuditSystem GameObject exists but AuditBootstrap component is missing.\n\nUse 'Setup Audit System' to fix it.",
                "OK"
            );
            Selection.activeGameObject = auditSystem;
            return;
        }

        EditorUtility.DisplayDialog(
            "Status",
            "✅ AuditSystem is properly set up!\n\n" +
            "GameObject: AuditSystem\n" +
            "Component: AuditBootstrap ✓",
            "OK"
        );
        Selection.activeGameObject = auditSystem;
    }

    [MenuItem("Tools/Audit System/Remove Audit System from Scene")]
    public static void RemoveAuditSystem()
    {
        GameObject auditSystem = GameObject.Find("AuditSystem");
        
        if (auditSystem == null)
        {
            EditorUtility.DisplayDialog("Info", "No AuditSystem found in current scene.", "OK");
            return;
        }

        bool confirm = EditorUtility.DisplayDialog(
            "Confirm Removal",
            "Are you sure you want to remove the AuditSystem GameObject from this scene?",
            "Yes, Remove",
            "Cancel"
        );

        if (confirm)
        {
            DestroyImmediate(auditSystem);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[AuditSystemSetup] AuditSystem removed from scene.");
            EditorUtility.DisplayDialog("Removed", "AuditSystem has been removed from the scene.", "OK");
        }
    }

    [MenuItem("Tools/Audit System/Ensure Persistent Audit")]
    public static void EnsurePersistentAudit()
    {
        // Check if we're in a scene
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == "")
        {
            EditorUtility.DisplayDialog("Info", 
                "Note: AuditAutoInstaller will automatically create AuditSystem at runtime.\n\n" +
                "This menu option is for testing. The system will work automatically when you press Play.",
                "OK");
            return;
        }

        // Check if AuditSystem already exists
        GameObject existingSystem = GameObject.Find("AuditSystem");
        if (existingSystem != null)
        {
            // Ensure it has AuditBootstrap
            if (existingSystem.GetComponent<AuditBootstrap>() == null)
            {
                existingSystem.AddComponent<AuditBootstrap>();
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                Debug.Log("[AuditSystemSetup] Added AuditBootstrap to existing AuditSystem.");
            }

            // Ensure it's marked as DontDestroyOnLoad
            EditorUtility.DisplayDialog("Status",
                "✅ AuditSystem found in scene.\n\n" +
                "Note: AuditAutoInstaller will ensure it persists at runtime even if removed from scenes.",
                "OK");
            Selection.activeGameObject = existingSystem;
            return;
        }

        // Create AuditSystem
        GameObject auditSystem = new GameObject("AuditSystem");
        auditSystem.AddComponent<AuditBootstrap>();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = auditSystem;

        EditorUtility.DisplayDialog("Created",
            "✅ AuditSystem created in current scene.\n\n" +
            "Note: AuditAutoInstaller will automatically ensure it exists at runtime even if not in scenes.",
            "OK");
        
        Debug.Log("[AuditSystemSetup] Created AuditSystem with AuditBootstrap component.");
    }
}
#endif
