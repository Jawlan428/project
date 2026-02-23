# Smart Crop Growth System – Setup Guide

## Overview

This system simulates plant growth over time with multiple stages, affected by water, sunlight, temperature, and fertilizer. It is designed for VR agriculture scenes and is Quest-friendly (tick-based updates, minimal allocations).

---

## Quick Start – Watermelon Plants

**Add to SampleScene:**  
**Tools > Plant Growth > Add Watermelon Plants to SampleScene**

Opens SampleScene, adds PlantGrowthManager + 3 watermelon plants, and saves. Press Play to see them grow.

**Or create a new scene:**  
**Tools > Plant Growth > FULL WATERMELON SETUP - New Scene**

---

## Other Setup Options

**Minimal test (capsule placeholders):**  
**Tools > Plant Growth > Create Minimal Test Scene (guaranteed to work)**

**Add to current scene (capsules):**  
**Tools > Plant Growth > COMPLETE SETUP - Do This First!**

This will:
- Create all prefabs and assets
- Add PlantGrowthManager + 3 plants to your scene
- Fix materials (no magenta)
- Set fast growth (5/10/15/20 sec per stage)
- Clear any corrupted save data

Then press **Play**. You should see 3 colored capsule plants that grow.

---

## Other Setup Options

### Option A: Add to your current scene
1. Open your scene in Unity
2. **Tools > Plant Growth > Add Full Setup to Current Scene**
3. Press **Play** – plants will grow automatically

### Option B: Create a new demo scene
1. **Tools > Plant Growth > Create New Demo Scene**
2. A new scene `PlantGrowthDemo` is created with Manager + 3 plants
3. Press **Play**

### Option C: Manual setup (if prefabs don’t exist yet)
1. **Tools > Plant Growth > Setup Wizard - Create All Assets**
2. Then use **Option A** or **Option B** above

---

## Manual Setup (Alternative)

1. **Run the Setup Wizard**
   - **Tools > Plant Growth > Setup Wizard - Create All Assets**
   - This creates:
     - Stage placeholder prefabs (colored capsules)
     - `DefaultPlantStage` ScriptableObject
     - `PlantInstance` prefab
     - `PlantGrowthManager` prefab

2. **Add Manager to Scene**
   - Drag `Assets/PlantGrowth/Prefabs/PlantGrowthManager.prefab` into your scene
   - Ensure it is active (one instance)

3. **Add Plants**
   - Drag `Assets/PlantGrowth/Prefabs/PlantInstance.prefab` into the scene 2–3 times
   - Position them where you want (e.g., on a farm floor)

4. **Play**
   - Plants will grow through stages over time
   - Use the API below to water/fertilize from VR interactions

---

## Manual Setup (if Wizard Fails)

### Step 1: Create Plant Stage Asset

1. Right-click in Project window: **Create > Plant Growth > Plant Stage Asset**
2. Name it `DefaultPlantStage` (or similar)
3. Configure:
   - **Stage Prefabs**: Assign 4 prefabs (Seed, Sprout, Young, Mature)
   - **Stage Durations**: e.g. `10, 30, 60, 120` seconds
   - **Ideal Ranges**: Water 50–80, Sunlight 60–90, Temp 18–30°C, Fertilizer 30–70
   - **Decay Rates**: Water 2/s, Fertilizer 0.5/s

### Step 2: Create Stage Prefabs

Create 4 simple 3D objects (or use primitives):

- **Stage 0 (Seed)**: Small brown cube/capsule
- **Stage 1 (Sprout)**: Small green cylinder
- **Stage 2 (Young)**: Medium green plant
- **Stage 3 (Mature)**: Full-size plant

Save each as a prefab in `Assets/PlantGrowth/Prefabs/Stages/`.

### Step 3: Create Plant Instance Prefab

1. Create empty GameObject: `PlantInstance`
2. Add child: `StageHolder` (empty transform)
3. Add `PlantController` component
4. Assign:
   - **Stage Asset**: Your PlantStageAsset
   - **Stage Holder**: The StageHolder transform
5. (Optional) Add `SphereCollider` for VR interaction
6. Save as prefab

### Step 4: Create Manager

1. Create empty GameObject: `PlantGrowthManager`
2. Add `PlantGrowthManager` component
3. Configure:
   - **Tick Interval**: 0.5 s (default)
   - **Global Sunlight**: 75
   - **Global Temperature**: 24
   - **Auto Save**: enabled, interval 30 s
4. Save as prefab

### Step 5: Scene Setup

1. Add `PlantGrowthManager` prefab to scene
2. Add 2–3 `PlantInstance` prefabs
3. Position plants (e.g., 1–2 m apart)

---

## Example Scene Layout

```
Scene
├── XR Origin (or VR rig)
├── PlantGrowthManager
├── Plant_1 (PlantInstance prefab) at (0, 0, 0)
├── Plant_2 (PlantInstance prefab) at (1.5, 0, 0)
└── Plant_3 (PlantInstance prefab) at (3, 0, 0)
```

---

## VR Interaction API

From any script (e.g., watering can, fertilizer tool):

```csharp
using PlantGrowth;

// Get reference to plant (e.g., from raycast hit)
PlantController plant = hit.collider.GetComponent<PlantController>();
if (plant != null && !plant.IsDead)
{
    plant.Water(30f);           // Add 30 water
    plant.AddFertilizer(20f);   // Add 20 fertilizer
}
```

**Environment (from manager or environment script):**

```csharp
PlantGrowthManager.Instance.SetGlobalSunlight(80f);
PlantGrowthManager.Instance.SetGlobalTemperature(25f);
```

**Per-plant environment override:**

```csharp
plant.SetSunlight(70f);
plant.SetTemperature(22f);
```

---

## Debug UI (Optional)

1. Create Canvas (World Space)
2. Add `PlantDebugUI` component
3. Assign a `PlantController` to **Target Plant**
4. Optionally assign Text and Slider references for health/water display

---

## Save / Load

- **Path**: `Application.persistentDataPath/plant_growth_save.json`
- **Auto-save**: Every 30 seconds (configurable)
- **Manual save**: `PlantGrowthManager.Instance.SaveNow();`
- **Delete save**: `PlantSaveLoadService.DeleteSave();`

Plants are matched by position when loading. Keep plant positions stable (within ~2 m) for correct restore.

---

## Performance Notes (Quest-Friendly)

- No per-plant `Update()` – all updates via manager tick
- Tick interval: 0.5 s default (configurable)
- Stage switching: instantiate once per stage change (acceptable)
- Minimal allocations in tick loop

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| **Plants appear magenta/pink** | Run **Tools > Plant Growth > Fix Plant Materials (fix magenta)** |
| **Want watermelon plants** | Run **Tools > Plant Growth > Use Watermelon Plant** – grows from sprout to watermelon! |
| **Want other real plants** | Run **Tools > Plant Growth > Use Real Plants (Pandazole Pack)** (requires Pandazole Farm Ranch Pack) |
| **Plants don't grow / stay small** | Run **Tools > Plant Growth > Fix Plant Growth (faster growth, visible progress)**. Also ensure PlantGrowthManager is in the scene and water plants (water decays over time). |
| Plants don't grow | Ensure PlantGrowthManager is in scene and active |
| Plants don't load | Check save path; ensure plants were saved before |
| Stage visuals wrong | Verify stage prefabs are assigned in PlantStageAsset |
| Health drops fast | Increase water, check temperature/sunlight ranges |

---

## File Structure

```
Assets/
├── PlantGrowth/
│   ├── Data/
│   │   └── DefaultPlantStage.asset
│   ├── Prefabs/
│   │   ├── PlantInstance.prefab
│   │   ├── PlantGrowthManager.prefab
│   │   └── Stages/
│   │       ├── Stage0_Seed.prefab
│   │       ├── Stage1_Sprout.prefab
│   │       ├── Stage2_Young.prefab
│   │       └── Stage3_Mature.prefab
│   └── (scripts in Assets/Scripts/PlantGrowth/)
```
