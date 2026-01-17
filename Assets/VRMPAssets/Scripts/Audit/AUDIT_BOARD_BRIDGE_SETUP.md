# Audit Board Bridge - Setup Guide

## ✅ Implementation Complete

The real-time BehaviorBoard display is now implemented using an event-based architecture.

---

## 📋 What Was Changed

### 1. AuditLogger.cs (Minimal Patch)
- **Added:** `public static event Action<AuditEvent> OnAuditEvent;`
- **Added:** `OnAuditEvent?.Invoke(evt);` after adding event to list
- **Removed:** Old `TryWriteToBoard()` call (replaced by event system)
- **Location:** Lines ~15 and ~208

### 2. AuditBoardBridge.cs (New Script)
- **Location:** `Assets/VRMPAssets/Scripts/Audit/AuditBoardBridge.cs`
- **Function:** Subscribes to `AuditLogger.OnAuditEvent` and displays events on BehaviorBoard
- **Features:**
  - Formats events into readable lines
  - Queues messages if BehaviorBoard unavailable
  - Flushes queued messages when board becomes available
  - Lightweight and safe (no heavy allocations)

### 3. AuditAutoInstaller.cs (Updated)
- **Added:** Automatically attaches `AuditBoardBridge` to AuditSystem
- **Location:** Lines ~26-30 and ~42

---

## 🚀 Setup Instructions

### Automatic Setup (Recommended)
**No manual setup needed!** The `AuditAutoInstaller` automatically:
1. Creates AuditSystem GameObject
2. Attaches AuditBootstrap
3. Attaches AuditBoardBridge ⭐ **NEW**

### Manual Setup (If Needed)
If you need to manually attach the bridge:

1. **Find AuditSystem GameObject:**
   - In Hierarchy, search for "AuditSystem"
   - If it doesn't exist, it will be created automatically at runtime

2. **Add AuditBoardBridge Component:**
   - Select AuditSystem
   - In Inspector, click **Add Component**
   - Search for: `AuditBoardBridge`
   - Add it

**That's it!** The bridge will automatically:
- Subscribe to audit events
- Display them on BehaviorBoard in real-time
- Queue messages if board not available
- Flush when board becomes available

---

## 🧪 Test Checklist

### Test 1: Session Start
1. **Press Play** from any scene
2. **Check Console** - Should see:
   ```
   [AUDIT] Initialized (persistent) - Created AuditSystem automatically.
   [AUDIT] BoardBridge active
   ```
3. **When Office scene loads** - BehaviorBoard should show:
   ```
   SESSION_START | player=Unknown
   ```

### Test 2: Join Meeting
1. **Enter player name** (e.g., "FJ9") in Join UI
2. **Press Join/Connect**
3. **Check BehaviorBoard** - Should immediately show:
   ```
   JOIN_MEETING | player=FJ9
   ```

### Test 3: Apple Pickup
1. **Grab an apple**
2. **Check BehaviorBoard** - Should immediately show:
   ```
   APPLE_PICKED | player=FJ9 | target=Apple_12 | zone=Orchard
   ```

### Test 4: Apple Drop
1. **Release the apple**
2. **Check BehaviorBoard** - Should immediately show:
   ```
   APPLE_DROPPED | player=FJ9 | target=Apple_12 | zone=Orchard
   ```

### Test 5: Add to Inventory
1. **Place apple in inventory**
2. **Check BehaviorBoard** - Should immediately show:
   ```
   APPLE_ADDED_TO_INVENTORY | player=FJ9 | target=Inventory | zone=Office
   ```

### Test 6: Remove from Inventory
1. **Remove apple from inventory**
2. **Check BehaviorBoard** - Should immediately show:
   ```
   APPLE_REMOVED_FROM_INVENTORY | player=FJ9 | target=Inventory | zone=Office
   ```

### Test 7: Leave Meeting
1. **Disconnect/Leave room**
2. **Check BehaviorBoard** - Should immediately show:
   ```
   LEAVE_MEETING | player=FJ9
   ```

### Test 8: Message Queuing (If Office Not Loaded)
1. **Press Play** from a scene without BehaviorBoard
2. **Perform actions** (join, pick apple, etc.)
3. **Check Console** - Messages are queued (no errors)
4. **Load Office scene**
5. **Check Console** - Should see:
   ```
   [AUDIT] BehaviorBoard detected, flushing N lines
   ```
6. **Check BehaviorBoard** - All queued events should appear

---

## 🔍 Debug Messages

The system provides concise debug messages:

- `[AUDIT] BoardBridge active` - Bridge initialized
- `[AUDIT] BehaviorBoard detected, flushing N lines` - Queued messages flushed
- `[AUDIT] <event> | player=name | target=id | zone=zone` - Event logged (existing)

**No spam logs per frame** - Only important state changes are logged.

---

## 📊 Event Format

Events are formatted as:
```
EVENT_TYPE | player=name | target=id | zone=zone
```

Examples:
- `SESSION_START | player=Unknown`
- `JOIN_MEETING | player=FJ9`
- `APPLE_PICKED | player=FJ9 | target=Apple_12 | zone=Orchard`
- `APPLE_ADDED_TO_INVENTORY | player=FJ9 | target=Inventory | zone=Office`

---

## ✅ Verification

**Success Criteria:**
- ✅ BehaviorBoard shows events in real-time (same moment as JSON logging)
- ✅ Events appear in same order as JSON file
- ✅ All events that are saved to JSON also appear on board
- ✅ Messages queue if board not available, flush when available
- ✅ No duplicate SESSION_END entries
- ✅ Desktop JSON saving unchanged

---

## ⚠️ Troubleshooting

**Q: Events not appearing on BehaviorBoard**
- Check Console for `[AUDIT] BoardBridge active`
- Verify AuditBoardBridge component is attached to AuditSystem
- Check if BehaviorBoard.Instance exists in Office scene
- Look for queued messages: Check if "flushing N lines" appears when Office loads

**Q: Events appear but delayed**
- Check if messages are being queued (look for flush message)
- Verify BehaviorBoard.Instance is available when events fire
- Check Console for any errors

**Q: Duplicate events**
- Should not happen - each event is logged once
- Verify AuditBoardBridge is only attached once to AuditSystem

---

## 📝 Notes

- **Event-Based Architecture:** Clean separation between logging and display
- **Automatic Setup:** Bridge is auto-attached by AuditAutoInstaller
- **Persistent:** Bridge persists across scenes (DontDestroyOnLoad)
- **Lightweight:** No heavy allocations, efficient string building
- **Safe:** Null checks for BehaviorBoard, graceful queuing

---

## 🎉 Success!

The BehaviorBoard now displays all audit events in real-time, matching exactly what's saved to JSON! 🚀
