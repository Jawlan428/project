# 🔧 Manual Setup (If Menu Doesn't Appear)

## Quick Manual Setup (2 minutes)

### Step 1: Create the GameObject
1. In the **Hierarchy panel** (left side), right-click on an empty area
2. Click **Create Empty**
3. A new GameObject appears - rename it to: **AuditSystem**
   - Click on it, then press F2, or
   - Double-click the name and type: `AuditSystem`

### Step 2: Add the Component
1. Select the **AuditSystem** GameObject (click on it in Hierarchy)
2. Look at the **Inspector panel** (right side)
3. Click the **Add Component** button at the bottom
4. In the search box, type: `AuditBootstrap`
5. Click on **Audit Bootstrap** when it appears
6. The component is now attached!

### Step 3: Save the Scene
- Press **Ctrl+S** (or **Cmd+S** on Mac)
- OR go to **File → Save**

### Step 4: Test It!
1. Press **Play** (▶️)
2. Check **BehaviorBoard** - you should see audit events
3. Enter a player name - should see `JOIN_MEETING | player=YourName`
4. Press **Stop** (⏹️)

---

## ✅ That's It!

The AuditSystem is now set up manually. It will work exactly the same as the automatic setup.

---

## Why the Menu Might Not Show

The menu item might not appear if:
- Unity is still compiling scripts (wait a bit)
- Unity needs a refresh (try Assets → Refresh)
- Editor scripts need to be recompiled (restart Unity)

But manual setup works perfectly fine! 🎉
