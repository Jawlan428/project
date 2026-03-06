using UnityEngine;
using UnityEditor;

/// <summary>
/// Farm menu - Tools > Farm. Single source for all Farm menu items.
/// </summary>
public static class FarmMenu
{
    [MenuItem("Tools/Farm/Farm Setup")]
    [MenuItem("Tools/Plant Growth/Farm Setup")]
    public static void FarmSetup()
    {
        InvokeSmartFarm("FullSetupWithTablet");
    }

    [MenuItem("Tools/Farm/Full Platform Setup (Tablet)")]
    public static void FullPlatformSetupTablet()
    {
        InvokeSmartFarm("FullSetupWithTablet");
    }

    [MenuItem("Tools/Farm/Apply Tablet Theme (Auto)")]
    public static void ApplyTabletThemeAuto()
    {
        InvokeSmartFarm("ApplyTabletThemeAuto");
    }

    [MenuItem("Tools/Farm/Apply Tab Icons")]
    public static void ApplyTabIcons()
    {
        var applier = UnityEngine.Object.FindFirstObjectByType<SmartFarm.TabletThemeAutoApplier>();
        if (applier == null)
        {
            UnityEngine.Debug.LogWarning("[Farm] TabletThemeAutoApplier not found. Run Full Platform Setup first.");
            return;
        }
        applier.ApplyTabIconsOnly();
        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEngine.Debug.Log("[Farm] Tab icons applied. Assign sprites in DefaultTabletTheme asset first.");
    }

    [MenuItem("Tools/Farm/Create Farm Dashboard")]
    public static void CreateFarmDashboard()
    {
        InvokeSmartFarm("CreateFarmDashboardOnly");
    }

    [MenuItem("Tools/Farm/Create Weather Control Panel")]
    public static void CreateWeatherPanel()
    {
        InvokeSmartFarm("CreateWeatherPanelOnly");
    }

    [MenuItem("Tools/Farm/Create Full Weather Setup (Rain/Lightning/Audio)")]
    public static void CreateFullWeatherSetup()
    {
        InvokeSmartFarm("CreateFullWeatherSetupOnly");
    }

    [MenuItem("Tools/Farm/Enable XR UI on Controllers")]
    public static void EnableXRUIControllers()
    {
        InvokeSmartFarm("EnableXRUIControllers");
    }

    [MenuItem("Tools/Farm/Save Prefabs")]
    public static void SavePrefabs()
    {
        InvokeSmartFarm("SavePrefabs");
    }

    [MenuItem("Tools/Farm/Register with NetworkManager")]
    public static void RegisterWithNetworkManager()
    {
        InvokeSmartFarm("RegisterWithNetworkManager");
    }

    [MenuItem("Tools/Farm/Clear Save Data")]
    public static void ClearSaveData()
    {
        PlantGrowth.PlantSaveLoadService.DeleteSave();
        Debug.Log("[Farm] Save data cleared.");
    }

    [MenuItem("Tools/Farm/Fix Plant Materials")]
    public static void FixPlantMaterials()
    {
        PlantGrowth.Editor.PlantGrowthSetupWizard.FixPlantMaterials();
    }

    [MenuItem("Tools/Farm/Setup Wild Harvest Crops")]
    public static void SetupWildHarvestCrops()
    {
        PlantGrowth.Editor.PlantGrowthSetupWizard.SetupWildHarvestCrops();
    }

    [MenuItem("Tools/Farm/Rebuild Irrigation Page (3 buttons)")]
    public static void RebuildIrrigationPage()
    {
        InvokeSmartFarm("RebuildIrrigationPage");
    }

    [MenuItem("Tools/Farm/Update Mature Stage to Bunch (Wheat + Corn)")]
    public static void UpdateMatureStageToBunch()
    {
        InvokeCropSetup("UpdateMatureStageToBunch");
    }

    private static void InvokeSmartFarm(string methodName)
    {
        InvokeType("SmartFarm.Editor.SmartFarmSetupEditor", methodName);
    }

    private static void InvokeCropSetup(string methodName)
    {
        InvokeType("SmartFarm.Editor.CropGrowthSetupEditor", methodName);
    }

    private static void InvokeType(string typeName, string methodName)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = asm.GetType(typeName);
            if (type != null)
            {
                var method = type.GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (method != null)
                {
                    method.Invoke(null, null);
                    return;
                }
            }
        }
        Debug.LogError($"[Farm] {typeName}.{methodName} not found. Check Console for compile errors.");
    }
}
