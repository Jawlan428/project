# Chicken Wander Setup - Quick Guide

## 🎯 Goal
Make `Chicken_001` prefab wander naturally around your farm area as an ambient NPC.

---

## 📝 Step-by-Step Setup

### Step 1: Disable/Remove CreatureMover Script

**Option A: Disable (Recommended - keeps script for reference)**
1. Select `Chicken_001` prefab in Project window
2. Double-click to open in **Prefab Mode**
3. In Inspector, find `CreatureMover` component
4. **Uncheck the checkbox** at the top-left of the component (disables it)

**Option B: Remove (Permanent deletion)**
1. In Prefab Mode, select root GameObject
2. Find `CreatureMover` component in Inspector
3. Click **three dots (⋮)** → **Remove Component**
4. Click **"<" arrow** to exit Prefab Mode

---

### Step 2: Add ChickenWanderNPC Script

1. Still in **Prefab Mode** (or exit and re-enter)
2. Select root GameObject (`Chicken_001`)
3. In Inspector, click **Add Component**
4. Search: `Chicken Wander NPC`
5. Click to add it

---

### Step 3: Create Wander Area Center

1. In your **Scene Hierarchy**, right-click → **Create Empty**
2. Name it: `ChickenAreaCenter`
3. Position it at the **center of your farm/pen area**
   - Example: If your farm pen is at (0, 0, 0), place it there
   - Or place it wherever you want chickens to wander around

---

### Step 4: Configure ChickenWanderNPC

**Still in Prefab Mode:**

1. Select `Chicken_001` root GameObject
2. Find `Chicken Wander NPC` component in Inspector
3. Set these values:

| Field | Value | Notes |
|-------|-------|-------|
| **Center Transform** | `ChickenAreaCenter` | Drag the GameObject from Hierarchy |
| **Wander Radius** | `3` | How far from center (default: 3) |
| **Walk Speed** | `0.6` | Movement speed (default: 0.6) |
| **Turn Speed** | `240` | Rotation speed (default: 240) |
| **Stopping Distance** | `0.2` | When to stop (default: 0.2) |
| **Idle Time Min** | `1` | Min idle seconds (default: 1) |
| **Idle Time Max** | `3` | Max idle seconds (default: 3) |
| **Gravity** | `-9.81` | Keep grounded (default: -9.81) |

4. **Save Prefab**: Click **"<" arrow** to exit Prefab Mode (auto-saves)

---

### Step 5: Place Chicken in Scene (if not already)

1. Drag `Chicken_001` prefab from Project window into Scene Hierarchy
2. Position it within the wander radius of `ChickenAreaCenter`
3. The chicken will start wandering automatically when you press Play

---

## ✅ Verification Checklist

Before testing:
- [ ] `CreatureMover` is disabled or removed
- [ ] `ChickenWanderNPC` component is added
- [ ] `Center Transform` is assigned (or left null to use initial position)
- [ ] `Animator` component exists and has `Chicken.controller` assigned
- [ ] `CharacterController` component exists

---

## 🎬 Test It

1. Press **Play** button (▶)
2. Chicken should:
   - Start idle (standing still)
   - After 1-3 seconds, pick a random point
   - Walk towards it smoothly
   - Stop when reached
   - Idle for 1-3 seconds
   - Repeat!

---

## 🔍 Animator Parameters

The script tries these Animator parameters **in order**:

1. **`State`** (Float) - Sets to `0` for Idle, `0.5` for Walk
2. **`Speed`** (Float) - Sets to `0` for Idle, `walkSpeed` for Walk
3. **`IsMoving`** (Bool) - Sets to `false` for Idle, `true` for Walk

**If none of these exist**, it falls back to:
- `Chicken_001_idle` animation clip for idle
- `Chicken_003_walk` animation clip for walk

### How to Check Your Animator Parameters:

1. Select `Chicken_001` prefab
2. In Inspector, find **Animator** component
3. Click the **Controller** field (should show `Chicken.controller`)
4. Double-click it to open **Animator** window
5. Look at **Parameters** tab (top-left of Animator window)
6. You'll see what parameters exist (e.g., `State`, `Vert`, etc.)

**Your controller likely has:**
- `State` (Float) - This is what the script will use! ✅

---

## 🐛 Troubleshooting

### Chicken Doesn't Move
- ✅ Check `ChickenWanderNPC` component is **enabled** (checkbox checked)
- ✅ Verify `Center Transform` is assigned OR leave it null
- ✅ Check Console for errors (Window → General → Console)

### Chicken Floats or Falls Through Ground
- ✅ Ensure your ground has a **Collider** component
- ✅ Increase `Gravity` value (try `-20`)

### Animations Don't Play
- ✅ Check Animator Controller is assigned (`Chicken.controller`)
- ✅ Verify animation clips exist in the controller
- ✅ Check Console for animation warnings

### Chicken Wanders Too Far
- ✅ Reduce `Wander Radius` value (try `2` or `1.5`)

### Chicken Moves Too Fast/Slow
- ✅ Adjust `Walk Speed` (try `0.4` for slower, `1.0` for faster)

---

## 📊 Default Values Reference

```csharp
wanderRadius = 3f
walkSpeed = 0.6f
turnSpeed = 240f
stoppingDistance = 0.2f
idleTimeMin = 1f
idleTimeMax = 3f
gravity = -9.81f
```

---

## 🎮 How It Works

**Simple State Machine:**
1. **Idle** → Wait for random time (1-3 seconds)
2. **Choose Destination** → Pick random point within radius
3. **Walk** → Move towards destination
4. **Stop** → When within `stoppingDistance` (0.2 units)
5. **Repeat** → Go back to Idle

**Movement:**
- Uses `CharacterController.Move()` for smooth movement
- Applies gravity to stay grounded
- Rotates smoothly towards movement direction

**Animation:**
- Automatically detects Animator parameters
- Falls back to direct animation clips if needed
- Switches between Idle/Walk based on movement state

---

**That's it! Your chicken should now wander naturally! 🐔**
