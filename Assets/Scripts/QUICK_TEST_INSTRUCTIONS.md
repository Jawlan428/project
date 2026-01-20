# Quick Test Instructions - Analytics Canvas Audit Log Display

## 🚀 Fastest Way to Test (30 seconds)

1. **Open Unity and your SampleScene**

2. **Create Test Generator:**
   - In Hierarchy, right-click → Create Empty
   - Name it: `TestEventGenerator`
   - Click "Add Component"
   - Search for: `AuditLogTestGenerator`
   - ✅ Make sure "Generate On Start" is checked

3. **Press Play ▶️**

4. **Look at your AUDIT LOG panel**
   - You should see test events appearing!
   - Events will have colors and show different types

---

## ✅ What You Should See

Your AUDIT LOG panel should display events like:

```
[14:30:45]  SESSION_START  TestPlayer
[14:30:46]  JOIN_MEETING  TestPlayer
[14:30:47]  ENTER_OFFICE  TestPlayer  → [Office]
[14:30:48]  APPLE_PICKED  TestPlayer  → Red Apple  [Orchard]
[14:30:49]  POLL_VOTE  TestPlayer  → Option A
...
```

---

## 🎯 Test Different Features

### Test 1: Generate More Events
While in Play mode:
1. Open Console (Ctrl+Shift+C)
2. Type in the command line (if available) or use buttons:
   ```csharp
   FindObjectOfType<AuditLogTestGenerator>().GenerateTestEvents();
   ```

### Test 2: View Historical Events
1. Play the scene once to generate events
2. Stop playing (events are saved to disk automatically)
3. Play again - old events should load from files!

### Test 3: Check Saved Files
Look for JSON files in:
- `YourProject/QuestRecordings/AuditLogs/`
- Or: `Desktop/My project_AuditLogs/`

Files are named: `audit_[sessionId]_[date-time].json`

---

## 🔧 Troubleshooting

### ❌ No Events Appear
**Fix:** Check Unity Console for errors
- Look for: `[AnalyticsCanvas]` messages
- Look for: `[AUDIT]` messages
- Should see: "Initialized" and "Loading events"

### ❌ "Content Panel not assigned!" Error
**Fix:** 
1. Select `AnalyticsCanvas` in Hierarchy
2. In Inspector, find `AnalyticsCanvasController` component
3. Check if "Content Panel" field is assigned
4. If not, drag the Content panel from your UI to that field

### ❌ Nothing Happens
**Fix:** Check AuditLogger exists:
1. Play the scene
2. Look in Hierarchy for "AuditLogger" GameObject
3. Should appear automatically (DontDestroyOnLoad)
4. If missing, check Console for initialization errors

---

## 📝 Console Messages You Should See

When everything works correctly:

```
[AUDIT] Initialized (persistent). Session ID: abc-123-...
[AUDIT] Log directory: C:\Users\...\Desktop\My project_AuditLogs
[AnalyticsCanvas] STARTING
[AnalyticsCanvas] Content Panel found: Content
[AnalyticsCanvas] Subscribed to AuditLogger.OnAuditEvent
[AnalyticsCanvas] Scanning audit log directory: ...
[AnalyticsCanvas] Found X audit log files
[AnalyticsCanvas] Loaded X events from [filename]
[AuditLogTestGenerator] Generating 10 test events...
[AUDIT] SESSION_START | player=TestPlayer
[AnalyticsCanvas] Event received: SESSION_START
[AnalyticsCanvas] Adding row #1: ...
```

---

## 🎮 Add a Button (Optional - 2 minutes)

Want to trigger events with a button click?

1. **In Hierarchy:**
   - Find your AnalyticsCanvas UI
   - Right-click → UI → Button - TextMeshPro
   - Position it near the AUDIT LOG panel
   - Rename to: "GenerateEventsButton"

2. **Create Button Handler:**
   - Create Empty GameObject: "ButtonHandler"
   - Add Component: `AuditLogUIButtons`

3. **Connect Button:**
   - Select the button
   - In Inspector, find "Button" component
   - Click "+" under "On Click ()"
   - Drag "ButtonHandler" to the object field
   - Choose: `AuditLogUIButtons.OnGenerateTestEvents`

4. **Test:**
   - Play the scene
   - Press the button (in VR or click in Game view)
   - Events should appear!

---

## 📊 Understanding the Display

### Color Legend:
- 🔵 **Blue** = Timestamp (HH:mm:ss)
- 🟡 **Gold** = Event Type (SESSION_START, POLL_VOTE, etc.)
- ⚪ **White** = Player Name
- ⚫ **Gray** = Target/Object ID
- 🟢 **Green** = Zone Name

### Settings (in Inspector):
- **Max Visible Rows**: How many events to show (default: 100)
- **Newest On Top**: Latest events at top (default: true)

---

## 🎉 Next Steps

Once you confirm it's working:

1. **Remove Test Generator** (or uncheck "Generate On Start")
2. **Integrate with your game**:
   - Add audit log calls in your existing scripts
   - See: `PollBoard.cs` for an example (lines 146-152)
3. **Customize colors** in `AnalyticsCanvasController.cs` (line 116)
4. **Add more event types** in `AuditEventType.cs` if needed

---

## 📚 Full Documentation

For detailed information, see:
- `ANALYTICS_CANVAS_SETUP.md` - Complete setup guide
- `AUDIT_SYSTEM_SETUP.md` - Original audit system documentation

---

**Quick Support:**
- Console shows `[AnalyticsCanvas]` messages ✅
- Console shows `[AUDIT]` messages ✅
- Events appear on the panel ✅
- JSON files created in directories ✅

**You're all set!** 🎉

