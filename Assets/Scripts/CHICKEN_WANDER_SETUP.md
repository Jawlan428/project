# Chicken Wander NPC Setup Guide
## Step-by-Step Instructions for Unity VR (Meta Quest 3)

---

## 📋 **Overview**

This guide will help you set up 2-4 autonomous chickens that wander naturally in your farm scene. The system is optimized for Quest 3 VR performance with minimal CPU usage.

**Scripts Created:**
- `ChickenWanderNPC.cs` - Handles individual chicken wandering behavior
- `ChickenSpawner.cs` - Spawns multiple chickens automatically

---

## 🎯 **Step 1: Prepare the Chicken Prefab**

### 1.1 Locate the Chicken Prefab
1. In Unity Project window, navigate to:
   ```
   Assets/ithappy/Animals_FREE/Prefabs/Chicken_001.prefab
   ```

### 1.2 Open the Prefab for Editing
1. **Double-click** `Chicken_001.prefab` in the Project window
2. This opens the prefab in **Prefab Mode** (you'll see "Prefab" in the top bar)

### 1.3 Remove Existing Movement Scripts (Important!)
The prefab may have existing movement scripts that conflict with our system:

1. In the **Inspector**, select the root GameObject (`Chicken_001`)
2. Look for these components and **remove them**:
   - `CreatureMover` (if present)
   - `MovePlayerInput` (if present)
   - Any other movement/input scripts

**To remove:** Click the **three dots (⋮)** next to the component name → **Remove Component**

### 1.4 Verify Required Components
Ensure the prefab has:
- ✅ **Animator** component (should already exist)
- ✅ **CharacterController** component (should already exist)
- ✅ Animator Controller assigned: `Chicken.controller`

**If CharacterController is missing:**
- Click **Add Component** → Search "Character Controller" → Add it
- Set **Radius**: `0.26`
- Set **Height**: `0.63`
- Set **Center**: `(0, 0.31, 0)`

### 1.5 Add the ChickenWanderNPC Script
1. Still in **Prefab Mode**, select the root GameObject
2. Click **Add Component** → Search "Chicken Wander NPC" → Add it
3. **DO NOT** configure settings yet (we'll do that after spawning)

### 1.6 Save and Exit Prefab Mode
1. Click the **"<" arrow** in the top-left to exit Prefab Mode
2. Or click **"Open Prefab"** button again to toggle off

---

## 🏗️ **Step 2: Create the Wander Area Center**

### 2.1 Create Empty GameObject
1. In your **Scene Hierarchy**, right-click → **Create Empty**
2. Name it: `ChickenWanderAreaCenter`
3. Position it where you want the center of the chicken wandering area (e.g., middle of your farm)

**Example Position:** If your farm is at (0, 0, 0), place it there.

---

## 🐔 **Step 3: Set Up the Spawner**

### 3.1 Create Spawner GameObject
1. In **Hierarchy**, right-click → **Create Empty**
2. Name it: `ChickenSpawner`
3. Position it at the same location as `ChickenWanderAreaCenter` (or nearby)

### 3.2 Add ChickenSpawner Script
1. Select `ChickenSpawner` in Hierarchy
2. In **Inspector**, click **Add Component** → Search "Chicken Spawner" → Add it

### 3.3 Configure Spawner Settings
In the **Inspector**, set these values:

| Field | Value | Description |
|-------|-------|-------------|
| **Chicken Prefab** | `Chicken_001` | Drag the prefab from Project window |
| **Chicken Count** | `3` | Number of chickens to spawn (2-4 recommended) |
| **Spawn Center** | `ChickenWanderAreaCenter` | Drag the center GameObject here |
| **Spawn Radius** | `8` | Area where chickens spawn |
| **Min Distance Between Chickens** | `2` | Prevents overlapping |
| **Spawn Y Offset** | `0` | Adjust if chickens spawn too high/low |
| **Auto Wander Center** | `ChickenWanderAreaCenter` | Auto-configures spawned chickens |
| **Auto Wander Radius** | `10` | How far chickens can wander |

---

## ⚙️ **Step 4: Configure Individual Chicken Settings (Optional)**

If you want to customize each chicken's behavior after spawning:

1. **Select a spawned chicken** in Hierarchy (e.g., `Chicken_1`)
2. In **Inspector**, find the **Chicken Wander NPC** component
3. Adjust these settings:

### Movement Settings
- **Wander Radius**: `10` (how far from center they can go)
- **Walk Speed**: `1` (slow, natural pace)
- **Run Speed**: `3` (only if UseRun is enabled)
- **Turn Speed**: `200` (rotation speed)

### Behavior Settings
- **Idle Time Min**: `2` seconds
- **Idle Time Max**: `5` seconds
- **Walk Time Min**: `3` seconds
- **Walk Time Max**: `8` seconds
- **Use Run**: `false` (disable running for ambient NPCs)
- **Run Chance**: `0.1` (10% chance if enabled)

### Grounding Settings
- **Ground Layer Mask**: `Default` (or your ground layer)
- **Ground Check Distance**: `0.5`
- **Ground Offset**: `0.1`

---

## ▶️ **Step 5: Test the System**

### 5.1 Enter Play Mode
1. Click the **Play** button (▶) in Unity Editor
2. Chickens should spawn automatically at Start

### 5.2 Verify Behavior
Watch for:
- ✅ Chickens spawn at random positions
- ✅ Chickens walk around the area
- ✅ Chickens stop and play idle animation
- ✅ Chickens stay on the ground (no floating)
- ✅ Chickens rotate smoothly when changing direction

### 5.3 Check Performance
- Open **Window** → **Analysis** → **Profiler**
- Monitor CPU usage (should be minimal)
- Each chicken should use < 0.1ms per frame

---

## 🔧 **Troubleshooting**

### Problem: Chickens Don't Move
**Solution:**
1. Check that `ChickenWanderNPC` script is enabled (checkbox in Inspector)
2. Verify `Wander Center` is assigned
3. Check Console for errors (red messages)
4. Ensure Animator Controller is assigned (`Chicken.controller`)

### Problem: Animations Don't Play
**Solution:**
1. Verify Animator Controller: `Assets/ithappy/Animals_FREE/Animations/Animation_Controllers/Chicken.controller`
2. Check Animator parameters:
   - Should have `State` (float) parameter
   - Should have `Vert` (float) parameter
3. In Animator window, verify the BlendTree is set up correctly
4. Check that animation clips are assigned:
   - `Chicken_001_idle.anim`
   - `Chicken_003_walk.anim`
   - `Chicken_002_run.anim`

### Problem: Chickens Float or Drift Upward
**Solution:**
1. Increase **Ground Check Distance** to `1.0`
2. Adjust **Ground Offset** to `0.2`
3. Ensure your ground has a **Collider** component
4. Set **Ground Layer Mask** to match your ground layer

### Problem: Chickens Walk Through Each Other
**Solution:**
1. Ensure each chicken has a **CharacterController** component
2. CharacterController handles collision automatically
3. If still clipping, increase **Min Distance Between Chickens** in spawner

### Problem: Chickens Wander Too Far
**Solution:**
1. Reduce **Wander Radius** in `ChickenWanderNPC` component
2. Or reduce **Auto Wander Radius** in spawner

### Problem: Chickens Move Too Fast/Slow
**Solution:**
1. Adjust **Walk Speed** (0.5-2.0 recommended)
2. For slower movement, use `0.5-0.8`
3. For faster movement, use `1.5-2.0`

### Problem: Performance Issues on Quest 3
**Solution:**
1. Reduce **Chicken Count** to 2-3
2. Increase **Idle Time Min/Max** (chickens idle more = less CPU)
3. Disable **Use Run** (running uses more CPU)
4. Check Profiler for bottlenecks

---

## 📊 **Animation Parameter Mapping**

The system uses these Animator parameters:

| Parameter | Type | Values | Description |
|-----------|------|--------|-------------|
| `State` | Float | 0 = Idle<br>0.5 = Walk<br>1 = Run | Controls animation blend |
| `Vert` | Float | 0 | Vertical movement (always 0 for ground-based) |

**How it works:**
- `State = 0`: Idle animation plays
- `State = 0.5`: Walk animation plays
- `State = 1`: Run animation plays (if enabled)

---

## 🎮 **Advanced Customization**

### Custom Wander Patterns
To create custom wander areas (rectangular bounds instead of circular):

1. Modify `ChickenWanderNPC.cs`
2. Replace `ChooseNewDestination()` method
3. Use `Bounds` instead of radius-based system

### Multiple Spawn Areas
1. Create multiple `ChickenSpawner` GameObjects
2. Each with different `Spawn Center` and `Wander Radius`
3. Set different `Chicken Count` per area

### Different Behaviors Per Chicken
1. After spawning, select individual chickens
2. Customize their `ChickenWanderNPC` settings
3. Some can be slower, some faster, some with running enabled

---

## ✅ **Final Checklist**

Before building for Quest 3:

- [ ] All chickens have `ChickenWanderNPC` component
- [ ] All chickens have `CharacterController` component
- [ ] All chickens have `Animator` with `Chicken.controller`
- [ ] `Wander Center` is assigned on each chicken
- [ ] No conflicting movement scripts on chickens
- [ ] Ground has colliders
- [ ] Performance tested in Play Mode
- [ ] No errors in Console

---

## 📝 **Notes**

- **No NavMesh Required**: This system uses simple wandering, no NavMesh needed
- **VR Safe**: Scripts are optimized for VR performance
- **No UI/Inventory**: Pure ambient NPCs, no interaction needed
- **Quest 3 Optimized**: Minimal per-frame calculations

---

## 🆘 **Need Help?**

If you encounter issues:
1. Check Unity Console for errors (red messages)
2. Verify all references are assigned (no "None" fields)
3. Ensure prefab is properly set up
4. Test with a single chicken first, then add more

---

**Happy Farming! 🐔🌾**
