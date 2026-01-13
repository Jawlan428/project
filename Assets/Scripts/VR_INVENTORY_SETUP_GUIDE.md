# VR Inventory Box Setup Guide
## Step-by-Step Instructions for SM_WoodBox_5

---

## 📁 **1. Script File Location**

### Current Location:
```
Assets/
  └── Scripts/
      └── VRInventoryBox.cs ✅
```

**✅ Verification:** The script should already be in `Assets/Scripts/` folder. If it's not there:
- Navigate to `Assets/Scripts/` in the Project window
- Look for `VRInventoryBox.cs`
- If missing, Unity may need to refresh (right-click Project window → Refresh)

---

## 🎯 **2. Adding the Script Component to SM_WoodBox_5**

### Method 1: Using the Inspector (Recommended for Beginners)

1. **Open your scene** containing `SM_WoodBox_5`
   - Look in the Hierarchy window (usually on the left side)

2. **Select SM_WoodBox_5**
   - Click on `SM_WoodBox_5` in the Hierarchy window
   - The Inspector window (usually on the right) will show its components

3. **Add Component**
   - At the bottom of the Inspector, click the **"Add Component"** button
   - Type "VR Inventory Box" in the search box
   - Click on **"VR Inventory Box"** from the dropdown list
   - ✅ The component is now added!

### Method 2: Drag and Drop

1. **Find the script** in Project window:
   - Navigate to `Assets/Scripts/`
   - Find `VRInventoryBox.cs`

2. **Drag to GameObject**:
   - Drag `VRInventoryBox.cs` from Project window
   - Drop it onto `SM_WoodBox_5` in the Hierarchy window
   - ✅ Component added!

### Method 3: If SM_WoodBox_5 is a Prefab Instance

**⚠️ Important:** If `SM_WoodBox_5` is a prefab instance (blue text in Hierarchy):

1. **Option A - Modify Instance Only:**
   - Select `SM_WoodBox_5` in Hierarchy
   - Add component normally (it will show "Override" in blue)
   - Changes only affect this scene instance

2. **Option B - Modify Prefab (affects all instances):**
   - Click the **"Open Prefab"** button (top of Inspector)
   - Add component to the prefab
   - Click **"Open Prefab"** again to exit prefab mode
   - All instances will have the component

---

## 🔧 **3. Required Components on SM_WoodBox_5**

### ✅ **Required Component:**

#### **BoxCollider** (Automatically Added)
- The script has `[RequireComponent(typeof(BoxCollider))]`
- Unity will **automatically add** a BoxCollider if missing
- If you see a warning, Unity is adding it for you

### ✅ **Recommended Components:**

#### **BoxCollider Settings:**
- **Is Trigger:** Leave as **unchecked** (false) - the box needs physical collision
- **Size:** Should match your box mesh size
- **Center:** Adjust if needed to match box center

### ❌ **NOT Required (but good to know):**

- **Rigidbody:** NOT needed on the box itself
- **XRGrabInteractable:** NOT needed on the box (only on items you want to store)
- **Mesh Collider:** BoxCollider is preferred for performance

---

## ✅ **4. Verifying the Script is Active and Working**

### **Step 1: Check Inspector Window**

After adding the component, you should see:

```
┌─────────────────────────────────────┐
│ VR Inventory Box (Script)           │
├─────────────────────────────────────┤
│ ✓ (Enabled checkbox)                │
│                                     │
│ Slot Configuration                  │
│   Slots X: [2]                      │
│   Slots Y: [2]                      │
│   Slots Z: [2]                      │
│                                     │
│ Slot Spacing                        │
│   X: [0.3]  Y: [0.3]  Z: [0.3]      │
│   Slot Offset: (0, 0, 0)            │
│                                     │
│ Socket Settings                     │
│   Socket Radius: [0.1]              │
│   ✓ Snap To Position                │
│   ✓ Snap To Rotation                │
│                                     │
│ Visual Debug (Optional)              │
│   ✓ Show Gizmos                     │
│   Gizmo Color: [Green]              │
└─────────────────────────────────────┘
```

### **Step 2: Check Console for Initialization**

1. **Enter Play Mode** (press Play button)
2. **Check Console** (Window → General → Console)
3. **Look for this message:**
   ```
   [VRInventoryBox] Created 8 inventory slots on SM_WoodBox_5
   ```
   - ✅ If you see this, the script is working!

### **Step 3: Visual Verification in Scene View**

1. **Select SM_WoodBox_5** in Hierarchy
2. **Switch to Scene View** (not Game view)
3. **Look for green wireframe spheres** inside the box
   - These represent inventory slots
   - You should see 8 spheres (2x2x2 grid by default)
   - If you don't see them:
     - Make sure "Show Gizmos" is checked in Inspector
     - Make sure SM_WoodBox_5 is selected

### **Step 4: Check Hierarchy for Child Objects**

After entering Play Mode, check the Hierarchy:

```
SM_WoodBox_5
  └── InventorySlots (created automatically)
      ├── Slot_0_0_0
      ├── Slot_0_0_1
      ├── Slot_0_1_0
      ├── Slot_0_1_1
      ├── Slot_1_0_0
      ├── Slot_1_0_1
      ├── Slot_1_1_0
      └── Slot_1_1_1
```

✅ If you see "InventorySlots" with child slot objects, it's working!

### **Step 5: Test with an Item**

1. **Create a test item:**
   - Create a Cube (GameObject → 3D Object → Cube)
   - Add `XRGrabInteractable` component
   - Add `Rigidbody` component
   - Position it near the box

2. **In VR/Play Mode:**
   - Grab the cube
   - Move it near the box
   - Release it near a slot
   - ✅ It should snap into the slot!

---

## ⚠️ **5. Common Mistakes to Avoid**

### **Mistake #1: Adding Script to Wrong Object**
❌ **Wrong:** Adding script to a child object or parent
✅ **Correct:** Add script directly to `SM_WoodBox_5` root object

### **Mistake #2: Missing BoxCollider**
❌ **Wrong:** Removing BoxCollider or making it a trigger
✅ **Correct:** Keep BoxCollider, ensure it's NOT a trigger

### **Mistake #3: Wrong Slot Offset**
❌ **Wrong:** Slots appearing outside the box
✅ **Correct:** Adjust "Slot Offset" in Inspector to position slots inside the box
   - Use negative Y values to move slots down inside the box
   - Example: `(0, -0.2, 0)` might position slots lower

### **Mistake #4: Slot Radius Too Small**
❌ **Wrong:** Items not snapping into slots
✅ **Correct:** Increase "Socket Radius" if items aren't snapping (try 0.15 or 0.2)

### **Mistake #5: Items Missing Required Components**
❌ **Wrong:** Trying to store items without XRGrabInteractable
✅ **Correct:** Items must have:
   - `XRGrabInteractable` component
   - `Rigidbody` component
   - A Collider

### **Mistake #6: XR Interaction Manager Missing**
❌ **Wrong:** Sockets not working, no interaction
✅ **Correct:** Ensure your scene has an `XR Interaction Manager`
   - Usually part of XR Origin setup
   - The script will find it automatically, but verify it exists

### **Mistake #7: Modifying Prefab When You Shouldn't**
❌ **Wrong:** Accidentally modifying prefab when you only want scene changes
✅ **Correct:** 
   - If text is **black** in Hierarchy = scene object (safe to modify)
   - If text is **blue** in Hierarchy = prefab instance (be careful!)
   - Use "Override" for scene-only changes

### **Mistake #8: Not Entering Play Mode**
❌ **Wrong:** Expecting slots to appear in Edit Mode
✅ **Correct:** Slots are created at runtime (in Play Mode or when built)
   - Use Scene View gizmos to preview slot positions in Edit Mode

### **Mistake #9: Slots Created Outside Box**
❌ **Wrong:** Slots appearing above or beside the box
✅ **Correct:** 
   - Adjust "Slot Offset" to move slots inside
   - Check box's local coordinate system
   - Use negative Y values to move slots down into the box

### **Mistake #10: Forgetting to Configure Items**
❌ **Wrong:** Items can't be stored
✅ **Correct:** Items need:
   - `XRGrabInteractable` component
   - `Rigidbody` component (not kinematic initially)
   - Proper collider setup

---

## 🎮 **Quick Setup Checklist**

- [ ] Script is in `Assets/Scripts/VRInventoryBox.cs`
- [ ] `SM_WoodBox_5` is selected in Hierarchy
- [ ] `VR Inventory Box` component added in Inspector
- [ ] Component shows as enabled (checkbox checked)
- [ ] BoxCollider exists on SM_WoodBox_5 (auto-added if missing)
- [ ] Enter Play Mode
- [ ] Check Console for "Created X inventory slots" message
- [ ] Check Hierarchy for "InventorySlots" child object
- [ ] Check Scene View for green gizmo spheres (when selected)
- [ ] Test with an item that has XRGrabInteractable + Rigidbody

---

## 🆘 **Troubleshooting**

### **Problem: Script doesn't appear in Add Component menu**
**Solution:** 
- Make sure script is in `Assets/Scripts/` folder
- Check for compilation errors (red text in Console)
- Right-click Project window → Refresh

### **Problem: No slots created in Play Mode**
**Solution:**
- Check Console for errors
- Verify script is enabled (checkbox checked)
- Make sure BoxCollider exists

### **Problem: Slots appear outside the box**
**Solution:**
- Adjust "Slot Offset" Y value (try negative values like -0.2)
- Adjust "Slot Spacing" to fit your box size
- Check box's local coordinate system

### **Problem: Items don't snap into slots**
**Solution:**
- Increase "Socket Radius" (try 0.15 or 0.2)
- Verify items have XRGrabInteractable + Rigidbody
- Check that XR Interaction Manager exists in scene

---

## 📝 **Next Steps After Setup**

1. **Adjust Slot Configuration:**
   - Change `Slots X/Y/Z` for more/fewer slots
   - Adjust `Slot Spacing` based on item sizes
   - Fine-tune `Slot Offset` to position slots correctly

2. **Test in VR:**
   - Build and deploy to Quest 3
   - Grab items and place them in the box
   - Verify items snap and can be removed

3. **Customize Items:**
   - Add XRGrabInteractable to items you want to store
   - Ensure items have Rigidbody components
   - Test item placement

---

**Need Help?** Check the Console window for error messages and debug logs from the script.
