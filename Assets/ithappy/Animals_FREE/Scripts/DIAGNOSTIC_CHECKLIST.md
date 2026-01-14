================================================================================
CHICKEN SCRIPT NOT RUNNING - DIAGNOSTIC CHECKLIST
================================================================================

If you see NO "[ChickenWanderNPC] Started..." message in Console,
the script is NOT running. Follow this checklist:

================================================================================
CHECK 1: IS SCRIPT ATTACHED TO CHICKEN IN SCENE?
================================================================================

CRITICAL: Script must be on the CHICKEN IN THE SCENE, not just the prefab!

1. In Hierarchy, find "Chicken_001" (or whatever you named it)
   - It should be a SCENE object (black text), NOT prefab instance (blue text)

2. SELECT Chicken_001 in Hierarchy

3. Look in Inspector window (right side)

4. Scroll through components - do you see "Chicken Wander NPC" component?
   □ YES - Go to CHECK 2
   □ NO - Go to "HOW TO ATTACH SCRIPT" below

================================================================================
CHECK 2: IS COMPONENT ENABLED?
================================================================================

1. With Chicken_001 selected, find "Chicken Wander NPC" component in Inspector

2. Look at the TOP-LEFT of the component
   - There should be a CHECKBOX

3. Is checkbox CHECKED?
   □ YES - Go to CHECK 3
   □ NO - CHECK IT NOW, then test again

================================================================================
CHECK 3: IS GAMEOBJECT ACTIVE?
================================================================================

1. With Chicken_001 selected in Hierarchy

2. Look at the TOP-LEFT of Inspector (GameObject name)

3. Is there a CHECKBOX next to the name?
   - Is it CHECKED?
   □ YES - Go to CHECK 4
   □ NO - CHECK IT NOW

================================================================================
CHECK 4: ARE THERE COMPILATION ERRORS?
================================================================================

1. Open Console (Window → General → Console)

2. Look for RED error messages

3. Do you see errors mentioning "ChickenWanderNPC"?
   □ NO - Go to CHECK 5
   □ YES - Fix errors first (script won't run if it doesn't compile)

================================================================================
CHECK 5: IS CHICKEN IN SCENE OR JUST PREFAB?
================================================================================

CRITICAL DIFFERENCE:

PREFAB (Project window):
- This is the template
- Script added here affects all instances
- But script won't run unless instance is in scene

SCENE INSTANCE (Hierarchy):
- This is the actual chicken in your scene
- Script MUST be attached HERE for it to run
- Even if prefab has script, scene instance needs it too

HOW TO CHECK:

1. Look in Hierarchy window
2. Do you see "Chicken_001" listed?
   □ YES - Select it and verify script is attached (CHECK 1)
   □ NO - You need to DRAG the prefab into scene first!

================================================================================
HOW TO ATTACH SCRIPT TO SCENE INSTANCE
================================================================================

METHOD 1: Add to Scene Instance (Recommended)

1. In Hierarchy, SELECT Chicken_001 (the one in your scene)

2. In Inspector, click "Add Component"

3. Search: "Chicken Wander NPC"

4. Click to add

5. Configure settings (Center Transform, Radius, etc.)

6. Press Play and check Console

METHOD 2: Add to Prefab (Affects All Instances)

1. In Project window, DOUBLE-CLICK Chicken_001.prefab
   - This opens Prefab Mode (you'll see "Prefab" in top bar)

2. Select root GameObject

3. Add Component → "Chicken Wander NPC"

4. Configure settings

5. Click "<" arrow to exit Prefab Mode

6. If chicken already in scene, it should update automatically
   - If not, remove old instance and drag prefab in again

================================================================================
QUICK TEST: ADD SCRIPT AND IMMEDIATELY CHECK CONSOLE
================================================================================

1. Select Chicken_001 in Hierarchy

2. Add Component → "Chicken Wander NPC"

3. DON'T configure anything yet - just add it

4. Press Play

5. IMMEDIATELY check Console

6. You should see:
   "[ChickenWanderNPC] Started on Chicken_001..."

If you see this message, script IS running!
If you DON'T see it, go back through checklist.

================================================================================
COMMON MISTAKES
================================================================================

❌ MISTAKE 1: Script only on prefab, not scene instance
   ✅ FIX: Add script to chicken IN SCENE (Hierarchy)

❌ MISTAKE 2: Component disabled
   ✅ FIX: Check the checkbox at top of component

❌ MISTAKE 3: GameObject inactive
   ✅ FIX: Check checkbox next to GameObject name

❌ MISTAKE 4: Script has compilation errors
   ✅ FIX: Check Console for red errors, fix them

❌ MISTAKE 5: Wrong GameObject selected
   ✅ FIX: Make sure you select the ROOT Chicken_001, not a child

================================================================================
VERIFICATION: WHAT YOU SHOULD SEE IN CONSOLE
================================================================================

When script runs correctly, you'll see these messages IN ORDER:

1. "[ChickenWanderNPC] Started on Chicken_001. Center: ..., Radius: 3"
   - This appears IMMEDIATELY when Play starts

2. "[ChickenWanderNPC] Disabled conflicting scripts: ..." (if conflicts found)
   - This appears right after Start

3. "[ChickenWanderNPC] New target: (x, y, z)"
   - This appears after idle timer expires (1-3 seconds)

4. "[ChickenWanderNPC] Arrived, idling for X.XX seconds..."
   - This appears when chicken reaches destination

If you see message #1, script IS running!
If you DON'T see message #1, script is NOT running - go through checklist.

================================================================================
