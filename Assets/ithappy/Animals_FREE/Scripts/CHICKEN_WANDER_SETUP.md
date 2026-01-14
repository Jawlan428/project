# Chicken Wander Setup Guide
## Make Your Chicken Move Freely!

---

## 🎯 **GOAL**
Make `Chicken_001` move freely around your farm area as an ambient NPC.

---

## 📋 **STEP-BY-STEP SETUP**

### **Step 1: Disable Conflicting Scripts**

**CRITICAL:** The chicken prefab has `CreatureMover` which requires input and will conflict.

1. **Select `Chicken_001` prefab** in Project window:
   - Navigate to: `Assets/ithappy/Animals_FREE/Prefabs/Chicken_001.prefab`
   - **Double-click** to open in **Prefab Mode**

2. **Disable CreatureMover:**
   - In Inspector, find `CreatureMover` component
   - **Uncheck the checkbox** at top-left of component (disables it)
   - **OR** click **three dots (⋮)** → **Remove Component** (permanent)

3. **Disable MovePlayerInput (if present):**
   - Find `MovePlayerInput` component
   - **Uncheck** to disable it

4. **Exit Prefab Mode:**
   - Click **"<" arrow** in top-left to exit Prefab Mode
   - Changes are saved automatically

---

### **Step 2: Add ChickenWanderNPC Script**

1. **Open `Chicken_001` prefab again** (double-click in Project)

2. **Select root GameObject** (`Chicken_001`)

3. **Add Component:**
   - In Inspector, click **Add Component**
   - Search: `Chicken Wander NPC`
   - Click to add

4. **Exit Prefab Mode** (click "<" arrow)

---

### **Step 3: Create Wander Area Center**

1. **In Scene Hierarchy**, right-click → **Create Empty**
2. **Name it:** `ChickenAreaCenter`
3. **Position it** at the center of where you want chickens to wander
   - Example: If your farm pen is at (0, 0, 0), place it there
   - Or wherever you want the center of wandering area

---

### **Step 4: Configure ChickenWanderNPC**

**Still in Prefab Mode (or exit and re-enter):**

1. Select `Chicken_001` root GameObject
2. Find `Chicken Wander NPC` component in Inspector
3. **Set these values:**

| Field | Value | Description |
|-------|-------|-------------|
| **Center Transform** | `ChickenAreaCenter` | Drag the GameObject from Hierarchy (or leave null to use initial position) |
| **Wander Radius** | `3` | How far from center (adjust as needed) |
| **Walk Speed** | `0.6` | Movement speed (0.4 = slow, 1.0 = fast) |
| **Turn Speed** | `240` | Rotation speed |
| **Stopping Distance** | `0.2` | When to stop (default is fine) |
| **Idle Time Min** | `1` | Min idle seconds |
| **Idle Time Max** | `3` | Max idle seconds |
| **Gravity** | `-9.81` | Keep grounded (default is fine) |
| **Enable Debug Logs** | ✅ **CHECKED** | See what's happening in Console |

4. **Save:** Exit Prefab Mode (auto-saves)

---

### **Step 5: Place Chicken in Scene**

1. **Drag `Chicken_001` prefab** from Project window into Scene Hierarchy
2. **Position it** within the wander radius of `ChickenAreaCenter`
3. **Verify:**
   - `ChickenWanderNPC` component is enabled (checkbox checked)
   - `Center Transform` is assigned (or null to use initial position)

---

### **Step 6: Test It!**

1. **Press Play** (▶)
2. **Check Console** (Window → General → Console)
3. **You should see:**
   - `[ChickenWanderNPC] Initialized. Starting idle...`
   - `[ChickenWanderNPC] New target: ...`
   - `[ChickenWanderNPC] Reached destination...`

4. **Watch the chicken:**
   - Should start idle (standing still)
   - After 1-3 seconds, pick a random point
   - Walk towards it smoothly
   - Stop when reached
   - Idle for 1-3 seconds
   - Repeat!

---

## ✅ **VERIFICATION CHECKLIST**

Before testing, verify:

- [ ] `CreatureMover` is **disabled** or **removed**
- [ ] `MovePlayerInput` is **disabled** (if present)
- [ ] `ChickenWanderNPC` component is **added** and **enabled**
- [ ] `Center Transform` is assigned (or left null)
- [ ] `Animator` component exists with `Chicken.controller` assigned
- [ ] `CharacterController` component exists
- [ ] `Enable Debug Logs` is checked (for troubleshooting)

---

## 🐛 **TROUBLESHOOTING**

### **Chicken Still Doesn't Move**

**Check Console for errors:**
- Red errors = fix immediately
- Yellow warnings = check if they're blocking movement

**Common Issues:**

1. **CreatureMover Still Enabled:**
   - Console will show: `[ChickenWanderNPC] WARNING: CreatureMover is enabled!`
   - **Fix:** Disable `CreatureMover` component

2. **CharacterController Issues:**
   - Check `CharacterController` component exists
   - Verify it's not disabled
   - Check `Min Move Distance` is small (0.001)

3. **Script Not Running:**
   - Check `ChickenWanderNPC` component is **enabled** (checkbox checked)
   - Check Console for initialization messages
   - If no messages, script might not be attached

4. **Chicken Stuck:**
   - Console will show: `[ChickenWanderNPC] WARNING: Chicken appears stuck!`
   - **Possible causes:**
     - Ground has no collider
     - CharacterController settings wrong
     - Another script interfering

5. **No Debug Messages:**
   - Check `Enable Debug Logs` is checked
   - Check Console filter (should show Info/Warnings/Errors)

---

## 🎬 **ANIMATION SETUP**

The script tries these methods **in order**:

1. **Animator Parameters:**
   - `State` (Float) → Sets to `0` for Idle, `0.5` for Walk
   - `Speed` (Float) → Sets to `0` for Idle, `walkSpeed` for Walk
   - `IsMoving` (Bool) → Sets to `false` for Idle, `true` for Walk

2. **Animation States:**
   - `Idle` state name
   - `Walk` state name

3. **Animation Clips:**
   - `Chicken_001_idle` clip
   - `Chicken_003_walk` clip

### **How to Check Your Animator:**

1. Select `Chicken_001` prefab
2. In Inspector, find **Animator** component
3. Click the **Controller** field (shows `Chicken.controller`)
4. **Double-click** to open **Animator** window
5. **Check Parameters tab** (top-left):
   - You'll see: `State` (Float) ✅
   - You'll see: `Vert` (Float)
6. **Check States:**
   - Look for states named `Idle`, `Walk`, `Run`
   - Or check what animation clips are assigned

**Your controller likely has `State` parameter - the script will use that!**

---

## 📊 **DEFAULT VALUES**

```csharp
wanderRadius = 3f          // 3 units from center
walkSpeed = 0.6f          // Moderate walking speed
turnSpeed = 240f          // Smooth rotation
stoppingDistance = 0.2f   // Stop when 0.2 units away
idleTimeMin = 1f          // Idle 1-3 seconds
idleTimeMax = 3f
gravity = -9.81f          // Standard gravity
```

---

## 🔍 **DEBUG LOGS**

With `Enable Debug Logs` checked, you'll see:

- `[ChickenWanderNPC] Initialized...` - Script started
- `[ChickenWanderNPC] New target: ...` - Picked new destination
- `[ChickenWanderNPC] Reached destination...` - Stopped at target
- `[ChickenWanderNPC] Animation: ...` - Animation changes
- `[ChickenWanderNPC] WARNING: ...` - Conflicts or issues

**If you see NO messages:**
- Script might not be attached
- Script might be disabled
- Check Console filter settings

---

## 🎮 **HOW IT WORKS**

**Simple State Machine:**
1. **Idle** → Wait for random time (1-3 seconds)
2. **Choose Destination** → Pick random point within radius
3. **Walk** → Move towards destination
4. **Stop** → When within `stoppingDistance` (0.2 units)
5. **Repeat** → Go back to Idle

**Movement:**
- Uses `CharacterController.Move()` for smooth movement
- **Combines horizontal movement + gravity in ONE call** (critical!)
- Rotates smoothly towards movement direction
- Applies gravity to stay grounded

**Animation:**
- Automatically detects Animator parameters
- Falls back to state names or clips if needed
- Switches between Idle/Walk based on movement state

---

## ⚠️ **IMPORTANT NOTES**

1. **CharacterController vs Rigidbody:**
   - Script uses `CharacterController.Move()`
   - If you have a `Rigidbody`, set it to **IsKinematic = true**
   - Or remove Rigidbody (CharacterController handles physics)

2. **Ground Colliders:**
   - Your ground MUST have a Collider component
   - Without colliders, chicken will fall through

3. **Prefab vs Scene Instance:**
   - If you modify prefab, all instances update
   - If you modify scene instance, only that instance changes
   - **Recommended:** Configure in prefab, then place in scene

4. **Multiple Chickens:**
   - Each chicken needs its own `ChickenWanderNPC` component
   - They can share the same `ChickenAreaCenter` or use different ones
   - Each will wander independently

---

## 🆘 **STILL NOT WORKING?**

1. **Check Console** - Look for red errors or yellow warnings
2. **Verify Script Location:**
   - Should be: `Assets/ithappy/Animals_FREE/Scripts/ChickenWanderNPC.cs`
3. **Test with Debug Logs:**
   - Enable `Enable Debug Logs`
   - Check Console for messages
   - If no messages, script isn't running
4. **Verify Components:**
   - `Animator` exists and enabled
   - `CharacterController` exists and enabled
   - `ChickenWanderNPC` exists and enabled
5. **Check Ground:**
   - Ground has Collider component
   - Ground is on correct layer

---

**That's it! Your chicken should now wander freely! 🐔**

If you see debug messages but chicken doesn't move, check CharacterController settings and ground colliders.
