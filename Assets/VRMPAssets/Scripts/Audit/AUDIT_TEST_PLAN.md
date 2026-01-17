# Audit System Test Plan

## ✅ Implementation Complete

All required features have been implemented:

1. ✅ **AuditAutoInstaller.cs** - Auto-creates AuditSystem before scene load
2. ✅ **Updated AuditLogger.cs** - Queues messages, flushes when BehaviorBoard available, saves to Desktop
3. ✅ **Updated AuditBootstrap.cs** - Ensures SESSION_START logs only once
4. ✅ **Updated Editor Menu** - Added "Ensure Persistent Audit" option

---

## 🧪 Step-by-Step Test Plan

### Test 1: Verify Auto-Installation
1. **Remove AuditSystem from all scenes** (if it exists)
2. **Press Play** from any entry scene (MainMenu/Loader/SampleScene)
3. **Check Console** - Should see:
   ```
   [AUDIT] Initialized (persistent) - Created AuditSystem automatically.
   [AUDIT] Initialized (persistent). Session ID: <guid>
   [AUDIT] Log directory: <path>
   ```
4. **Check Hierarchy** - Should see "AuditSystem" GameObject (even if not in scene)

**Expected Result:** ✅ AuditSystem is created automatically, no manual setup needed.

---

### Test 2: Verify Session Start Logging
1. **Press Play** from entry scene
2. **Check Console** - Should see:
   ```
   [AUDIT] SESSION_START | player=Unknown
   ```
3. **Check BehaviorBoard** (when Office scene loads):
   - Should see: `SESSION_START | player=Unknown`
   - OR if board loads later, messages should flush automatically

**Expected Result:** ✅ SESSION_START appears on board, even if board loads after audit starts.

---

### Test 3: Verify Message Queuing (BehaviorBoard loads later)
1. **Press Play** from entry scene (without BehaviorBoard)
2. **Wait for SESSION_START to log** (check Console)
3. **Load Office scene** (which has BehaviorBoard)
4. **Check Console** - Should see:
   ```
   [AUDIT] BehaviorBoard detected, flushing X queued messages
   ```
5. **Check BehaviorBoard** - Should show all queued messages

**Expected Result:** ✅ Messages queued when board unavailable, flushed when board appears.

---

### Test 4: Verify Player Name Logging
1. **Press Play**
2. **Enter player name** (e.g., "FJ9") in the Join UI
3. **Check Console** - Should see:
   ```
   [AUDIT] JOIN_MEETING | player=FJ9
   ```
4. **Check BehaviorBoard** - Should see:
   ```
   JOIN_MEETING | player=FJ9
   ```

**Expected Result:** ✅ Player name is correctly logged and displayed.

---

### Test 5: Verify Desktop File Saving (Windows)
1. **Press Play** and perform some actions
2. **Stop Play**
3. **Check Console** - Should see:
   ```
   [AUDIT] Saved audit file to: C:\Users\<YourName>\Desktop\<ProjectName>_AuditLogs\audit_<sessionId>_<timestamp>.json
   ```
4. **Open Windows File Explorer**
5. **Navigate to Desktop**
6. **Look for folder:** `<ProjectName>_AuditLogs`
7. **Open the JSON file** - Should contain all audit events

**Expected Result:** ✅ JSON file saved to Desktop folder (Windows) or fallback path.

---

### Test 6: Verify Fallback Path (Non-Windows or Access Denied)
1. **On non-Windows or if Desktop access fails:**
2. **Check Console** - Should see:
   ```
   [AUDIT] Desktop path not available (...), falling back to persistentDataPath.
   [AUDIT] Log directory: <Application.persistentDataPath>/AuditLogs
   ```
3. **File should save to:** `Application.persistentDataPath/AuditLogs/`

**Expected Result:** ✅ Gracefully falls back to persistentDataPath if Desktop unavailable.

---

### Test 7: Verify No Duplicate SESSION_START
1. **Press Play**
2. **Check Console** - Should see SESSION_START **once**
3. **Load different scenes** - Should NOT see duplicate SESSION_START
4. **Check JSON file** - Should have only ONE SESSION_START event

**Expected Result:** ✅ SESSION_START logs only once per application run.

---

### Test 8: Verify Session End and Flush
1. **Press Play** and perform actions
2. **Stop Play** (or quit application)
3. **Check Console** - Should see:
   ```
   [AUDIT] SESSION_END | player=<name>
   [AUDIT] Saved audit file to: <path>
   [AUDIT] Flushed X events to: <path>
   ```
4. **Verify JSON file exists** with all events including SESSION_END

**Expected Result:** ✅ Session end logged and file flushed on quit.

---

## 📋 Quick Verification Checklist

- [ ] Console shows "[AUDIT] Initialized (persistent)" on Play
- [ ] SESSION_START appears on BehaviorBoard (or queues if board not ready)
- [ ] JOIN_MEETING appears with correct player name
- [ ] JSON file saved to Desktop (Windows) or fallback path
- [ ] No duplicate SESSION_START events
- [ ] Messages queue when BehaviorBoard unavailable, flush when available
- [ ] Session end logged and file flushed on quit

---

## 🐛 Troubleshooting

**Q: No "[AUDIT] Initialized" message**
- Check Console for script errors
- Verify AuditAutoInstaller.cs is in Assets/VRMPAssets/Scripts/Audit/

**Q: Messages not appearing on BehaviorBoard**
- Check if BehaviorBoard.Instance exists in scene
- Check Console for "[AUDIT] BehaviorBoard detected, flushing X queued messages"
- Verify BehaviorBoard is in the same assembly (VRMPAssets)

**Q: File not saving to Desktop**
- Check Console for fallback message
- Verify Desktop path is accessible
- Check file permissions

**Q: Duplicate SESSION_START**
- Should not happen - check AuditBootstrap.cs and AuditLogger.cs logic
- Verify only one AuditSystem exists

---

## ✅ Success Criteria

All tests pass = Audit system is working correctly! 🎉
