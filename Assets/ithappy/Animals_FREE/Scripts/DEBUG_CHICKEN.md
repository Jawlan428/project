================================================================================
HOW TO CHECK IF CHICKEN SCRIPT IS RUNNING
================================================================================

The XRMultiplayer errors are NOT related to the chicken script.
They're cleanup errors that won't prevent the chicken from moving.

================================================================================
STEP 1: FILTER CONSOLE TO SEE CHICKEN MESSAGES
================================================================================

1. Open Console window (Window → General → Console)

2. In Console, look for the search/filter box (usually top-right)

3. Type: "ChickenWanderNPC"

4. This will show ONLY messages from the chicken script

================================================================================
STEP 2: WHAT YOU SHOULD SEE
================================================================================

If script is running correctly, you'll see:

✓ [ChickenWanderNPC] Started on Chicken_001. Center: ..., Radius: 3
✓ [ChickenWanderNPC] Disabled conflicting scripts: CreatureMover (if found)
✓ [ChickenWanderNPC] New target: (x, y, z)
✓ [ChickenWanderNPC] Arrived, idling for X.XX seconds...

If you see these messages, the script IS running!

================================================================================
STEP 3: IF NO CHICKEN MESSAGES APPEAR
================================================================================

This means the script isn't running. Check:

1. Is ChickenWanderNPC component attached?
   - Select Chicken_001 in scene
   - Check Inspector for "Chicken Wander NPC" component

2. Is component enabled?
   - Checkbox at top of component should be CHECKED

3. Is script compiled?
   - Check Console for compilation errors
   - Look for red errors mentioning "ChickenWanderNPC"

4. Is chicken in scene?
   - Chicken_001 must be in Hierarchy (not just prefab)

================================================================================
STEP 4: TEST IN ISOLATION (OPTIONAL)
================================================================================

If XRMultiplayer errors are too distracting:

1. Create a NEW scene (File → New Scene)

2. Add a ground plane:
   - GameObject → 3D Object → Plane
   - Scale it up (e.g., 10x10)

3. Add chicken:
   - Drag Chicken_001 prefab into scene
   - Position above ground

4. Press Play

5. Check Console filtered for "ChickenWanderNPC"

This tests chicken WITHOUT VR multiplayer interference.

================================================================================
QUICK CHECKLIST
================================================================================

□ Console filtered to "ChickenWanderNPC"
□ See "[ChickenWanderNPC] Started..." message
□ See "[ChickenWanderNPC] New target..." messages
□ Chicken GameObject selected in scene
□ ChickenWanderNPC component visible in Inspector
□ Component checkbox is CHECKED
□ Ground has Collider component

================================================================================
