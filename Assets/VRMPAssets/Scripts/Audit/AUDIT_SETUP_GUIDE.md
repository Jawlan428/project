# Audit System - Complete Setup Guide

## ✅ Implementation Status

All audit scripts are implemented and integrated. This guide shows you how to set up the remaining components.

---

## 📋 Required Setup Steps

### Step 1: OfficeZoneTrigger Setup (For Enter/Exit Office Logging)

1. **In the Office scene**, create a trigger zone at the office entrance:
   - Create an empty GameObject: Right-click in Hierarchy → Create Empty
   - Name it: `OfficeZoneTrigger`
   - Position it at the office entrance/exit point

2. **Add Collider:**
   - Select `OfficeZoneTrigger`
   - In Inspector, click **Add Component** → **Box Collider** (or Sphere Collider)
   - Check **Is Trigger** checkbox
   - Adjust size to cover the entrance area (e.g., width: 3, height: 2.5, depth: 0.5)

3. **Add OfficeZoneTrigger Script:**
   - With `OfficeZoneTrigger` selected, click **Add Component**
   - Search for: `OfficeZoneTrigger`
   - Add it

4. **Configure Settings:**
   - **Zone Name:** "Office" (default)
   - **Player Tag:** "Player" (or leave empty to detect any object)
   - **Log Enter:** ✓ (checked)
   - **Log Exit:** ✓ (checked)

**Result:** When player walks through the trigger, ENTER_OFFICE and EXIT_OFFICE events will be logged.

---

### Step 2: Verify Existing Integrations

The following hooks are **already integrated** (no action needed):

✅ **Apple Pickup/Drop** - `AppleGrabHandler.cs`
- APPLE_PICKED logged when apple is grabbed
- APPLE_DROPPED logged when apple is released

✅ **Inventory** - `VRInventoryBox.cs`
- APPLE_ADDED_TO_INVENTORY logged when item placed in inventory
- APPLE_REMOVED_FROM_INVENTORY logged when item removed

✅ **Join Meeting** - `SessionManager.cs`
- JOIN_MEETING logged when connection succeeds
- Player name is set via `PlayerAppearanceMenu.SubmitNewPlayerName()`

✅ **Player Name** - `PlayerIdentity.cs`
- Automatically created and persists across scenes
- Name set when player enters name in UI

---

## 🧪 Test Checklist

### Test 1: Session Start
1. **Press Play** from any scene
2. **Check Console** - Should see:
   ```
   [AUDIT] Initialized (persistent). Session ID: <guid>
   [AUDIT] Log directory: <path>
   ```
3. **Check BehaviorBoard** (when Office loads) - Should see:
   ```
   SESSION_START | player=Unknown
   ```

### Test 2: Player Name & Join
1. **Enter player name** (e.g., "FJ9") in the Join UI
2. **Press Join/Connect**
3. **Check Console** - Should see:
   ```
   [PlayerIdentity] Player name set to: FJ9
   [AUDIT] JOIN_MEETING | player=FJ9
   [AUDIT] Forwarded to board: JOIN_MEETING | player=FJ9
   ```
4. **Check BehaviorBoard** - Should see:
   ```
   JOIN_MEETING | player=FJ9
   ```

### Test 3: Apple Interactions
1. **Pick up an apple** (grab it)
2. **Check BehaviorBoard** - Should see:
   ```
   APPLE_PICKED | player=FJ9 | target=Apple_12 | zone=Orchard
   ```
3. **Drop the apple** (release it)
4. **Check BehaviorBoard** - Should see:
   ```
   APPLE_DROPPED | player=FJ9 | target=Apple_12 | zone=Orchard
   ```

### Test 4: Inventory
1. **Place apple in inventory/basket**
2. **Check BehaviorBoard** - Should see:
   ```
   APPLE_ADDED_TO_INVENTORY | player=FJ9 | target=Inventory | zone=Office
   ```
3. **Remove apple from inventory**
4. **Check BehaviorBoard** - Should see:
   ```
   APPLE_REMOVED_FROM_INVENTORY | player=FJ9 | target=Inventory | zone=Office
   ```

### Test 5: Office Enter/Exit (After Step 1 Setup)
1. **Walk into office** (through trigger zone)
2. **Check BehaviorBoard** - Should see:
   ```
   ENTER_OFFICE | player=FJ9 | zone=Office
   ```
3. **Walk out of office** (through trigger zone)
4. **Check BehaviorBoard** - Should see:
   ```
   EXIT_OFFICE | player=FJ9 | zone=Office
   ```

### Test 6: Desktop File Saving
1. **Perform several actions** (join, pick apple, etc.)
2. **Stop Play**
3. **Check Console** - Should see:
   ```
   [AUDIT] Saved audit file to: C:\Users\<YourName>\Desktop\<ProjectName>_AuditLogs\audit_<sessionId>_<timestamp>.json
   [AUDIT] Flushed X events to: <path>
   ```
4. **Open Windows File Explorer**
5. **Navigate to Desktop**
6. **Open folder:** `<ProjectName>_AuditLogs`
7. **Open the JSON file** - Should contain:
   ```json
   {
       "events": [
           {
               "timestamp": "2024-01-15T14:30:45.123Z",
               "sessionId": "<guid>",
               "playerName": "FJ9",
               "eventType": "SESSION_START",
               ...
           },
           {
               "eventType": "JOIN_MEETING",
               "playerName": "FJ9",
               ...
           },
           ...
       ]
   }
   ```

---

## 📁 File Locations

### Audit Scripts (Already Created):
- `Assets/VRMPAssets/Scripts/Audit/AuditEventType.cs`
- `Assets/VRMPAssets/Scripts/Audit/AuditEvent.cs`
- `Assets/VRMPAssets/Scripts/Audit/AuditLogger.cs`
- `Assets/VRMPAssets/Scripts/Audit/PlayerIdentity.cs`
- `Assets/VRMPAssets/Scripts/Audit/AuditBootstrap.cs`
- `Assets/VRMPAssets/Scripts/Audit/AuditAutoInstaller.cs`
- `Assets/VRMPAssets/Scripts/Audit/OfficeZoneTrigger.cs` ⭐ **NEW**

### Integrated Scripts (Hooks Added):
- `Assets/Scripts/AppleGrabHandler.cs` - Apple pickup/drop hooks
- `Assets/Scripts/VRInventoryBox.cs` - Inventory hooks
- `Assets/VRMPAssets/Scripts/Network/NetworkManagers/SessionManager.cs` - Join meeting hook
- `Assets/VRMPAssets/Scripts/Player/PlayerAppearanceMenu.cs` - Player name setting

---

## 🔍 Debug Messages

The system provides clear debug messages:

- `[AUDIT] Initialized (persistent)` - System started
- `[AUDIT] Forwarded to board: <message>` - Message sent to BehaviorBoard
- `[AUDIT] Board unavailable, queued: <message>` - Message queued (board not loaded yet)
- `[AUDIT] BehaviorBoard detected, flushing X queued messages` - Queued messages flushed
- `[AUDIT] Saved audit file to: <path>` - File saved successfully

---

## ⚠️ Troubleshooting

**Q: No events appear on BehaviorBoard**
- Check Console for `[AUDIT]` messages
- Verify BehaviorBoard.Instance exists in Office scene
- Check if messages are queued: Look for "Board unavailable, queued" messages
- When Office loads, queued messages should flush automatically

**Q: Player name always "Unknown"**
- Verify `PlayerIdentity.Instance.SetPlayerName(name)` is called when name is entered
- Check `PlayerAppearanceMenu.SubmitNewPlayerName()` is being called
- Verify JOIN_MEETING is logged when connection succeeds (not just when name is entered)

**Q: JSON file not on Desktop**
- Check Console for fallback message
- File may be in: `Application.persistentDataPath/AuditLogs/`
- Verify Desktop folder has write permissions

**Q: OfficeZoneTrigger not working**
- Verify Collider is set to **Is Trigger**
- Check **Player Tag** matches your player GameObject tag
- Verify trigger zone covers the entrance area
- Check Console for any errors

---

## ✅ Success Criteria

All tests pass = Audit system is fully working! 🎉

**Quick Verification:**
- ✅ SESSION_START appears on board
- ✅ JOIN_MEETING appears with correct player name
- ✅ APPLE_PICKED/DROPPED appear when interacting with apples
- ✅ APPLE_ADDED_TO_INVENTORY appears when placing in inventory
- ✅ ENTER_OFFICE/EXIT_OFFICE appear when walking through trigger
- ✅ JSON file saved to Desktop with all events

---

## 📝 Notes

- **Auto-Installation:** AuditSystem is created automatically at runtime (no manual setup needed)
- **Message Queuing:** Messages queue if BehaviorBoard not available, flush when board loads
- **Desktop Saving:** Primary location is Desktop, falls back to persistentDataPath if unavailable
- **Player Name:** Set when name is entered, used for all subsequent events
- **Non-Invasive:** All hooks are marked with `// AUDIT INTEGRATION` comments

---

**Setup Complete!** Just add the OfficeZoneTrigger (Step 1) and you're done! 🚀
