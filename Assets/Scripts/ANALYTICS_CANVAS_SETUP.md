# Analytics Canvas - Audit Log Display Setup Guide

## Overview
This guide shows you how to display audit log events on the Analytics Canvas UI in your SampleScene. The system will show:
- **Real-time events** as they occur in the scene
- **Historical events** loaded from audit log JSON files
- **Formatted display** with timestamps, event types, players, and details

---

## What's Already Set Up ✅

Your SampleScene already has:
1. **AnalyticsCanvas** GameObject with `AnalyticsCanvasController` script
2. **AuditLogger** system that records events
3. **UI Panel** with "AUDIT LOG" header (visible in your screenshot)

---

## Quick Start - Test the System

### Method 1: Use Test Generator Script (Easiest)

1. **Add Test Generator to Scene:**
   - In Unity, select any GameObject (or create a new one called "TestEventGenerator")
   - Add Component → `AuditLogTestGenerator`
   - Configure settings:
     - ✅ Generate On Start: Checked
     - Number Of Events: 10
     - Delay Between Events: 0.5

2. **Run the Scene:**
   - Press Play
   - Watch the AUDIT LOG panel - it should display test events!

### Method 2: Add a Test Button to the UI

1. **Create a Button:**
   - In Hierarchy, find your AnalyticsCanvas
   - Right-click → UI → Button - TextMeshPro
   - Position it near your AUDIT LOG panel

2. **Add Button Handler:**
   - Create an empty GameObject: "AuditLogButtons"
   - Add Component → `AuditLogUIButtons`
   - Select your button
   - In Button component → OnClick()
   - Drag the "AuditLogButtons" GameObject to the slot
   - Select function: `AuditLogUIButtons.OnGenerateTestEvents`

3. **Run and Test:**
   - Press Play
   - Click the button in VR or in the Game view
   - Events should appear on the AUDIT LOG panel!

---

## How It Works

### Real-Time Events
The `AnalyticsCanvasController` subscribes to `AuditLogger.OnAuditEvent` and displays events as they happen.

### Loading from Files
On startup, the controller searches for audit log JSON files in these locations (in order):
1. `YourProject/QuestRecordings/AuditLogs/` (relative to project)
2. `Desktop/YourProjectName_AuditLogs/` (where logs are saved)
3. `Application.persistentDataPath/AuditLogs/` (fallback location)

All events from found JSON files are loaded and displayed first, then real-time events are shown.

---

## Event Display Format

Events are displayed with color-coded information:
```
[HH:mm:ss]  EVENT_TYPE  PlayerName  → Target  [Zone]
```

Example:
```
[14:30:45]  POLL_VOTE  Alice  → Option A  [Meeting Room]
[14:30:50]  APPLE_PICKED  Bob  → Red Apple  [Orchard]
[14:31:00]  JOIN_MEETING  Charlie
```

Colors:
- 🔵 **Blue** - Timestamp
- 🟡 **Gold** - Event Type
- ⚪ **White** - Player Name
- ⚫ **Gray** - Target
- 🟢 **Green** - Zone

---

## Generating Real Events in Your Scene

### Example: Log a Poll Vote
```csharp
AuditLogger.Instance.Log(
    AuditEventType.POLL_VOTE,
    targetId: "Option A",
    metaJson: "{\"question\":\"Your poll question?\"}"
);
```

### Example: Log Player Entering Zone
```csharp
AuditLogger.Instance.Log(
    AuditEventType.ENTER_OFFICE,
    zoneName: "Office",
    position: playerTransform.position
);
```

### Example: Log Apple Interaction
```csharp
AuditLogger.Instance.Log(
    AuditEventType.APPLE_PICKED,
    targetId: "Red Apple",
    zoneName: "Orchard",
    position: appleTransform.position
);
```

---

## Customization

### Adjust Display Settings

Select the **AnalyticsCanvas** GameObject and configure `AnalyticsCanvasController`:

- **Max Visible Rows**: Number of events to display (default: 100)
- **Newest On Top**: Show newest events at the top (default: true)
- **Event Scroll Rect**: Reference to the scroll view
- **Content Panel**: Reference to the content area where events are displayed

### Change Colors

Edit the `AddEventRow()` method in `AnalyticsCanvasController.cs` (around line 116):

```csharp
string rowText = $"<color=#7BC8FF>{timeStr}</color>  <color=#FFD700>{typeStr}</color>  <color=#FFFFFF>{playerStr}</color>";
```

Change the hex color codes to your preference.

---

## Troubleshooting

### ❌ No Events Showing

**Check Console for logs:**
- Look for `[AnalyticsCanvas]` messages
- Should see: "Loading X existing events from memory..."

**Verify references:**
- Select AnalyticsCanvas GameObject
- Check that all references are assigned in `AnalyticsCanvasController`
- Event Scroll Rect should point to your scroll view
- Content Panel should point to the content area

### ❌ "Content Panel not assigned!" Error

The ContentPanel reference is missing:
1. In Hierarchy, expand AnalyticsCanvas → find the Content panel
2. Select AnalyticsCanvas
3. Drag the Content panel to the "Content Panel" field in Inspector

### ❌ Events Not Loading from Files

**Check if audit log files exist:**
- Look in `YourProject/QuestRecordings/AuditLogs/`
- Files are named: `audit_[sessionId]_[timestamp].json`

**Check Console for file loading messages:**
- Should see: "Scanning audit log directory: ..."
- Should see: "Found X audit log files"

**To create audit log files:**
1. Run your scene
2. Generate some events (using test generator or real gameplay)
3. Stop playing (this triggers auto-flush)
4. Check the directories mentioned above

### ❌ Player Name Shows as "Unknown"

Before logging events, set the player name:
```csharp
PlayerIdentity.Instance.SetPlayerName("YourPlayerName");
```

---

## Files in This System

### Core Scripts (in VRMPAssets/Scripts/Audit/)
- `AuditLogger.cs` - Main logging system
- `AuditEvent.cs` - Event data structure
- `AuditEventType.cs` - Event type enumeration
- `PlayerIdentity.cs` - Player name management
- `AuditBootstrap.cs` - System initialization

### Analytics Display (in Assets/Scripts/)
- `AnalyticsCanvasController.cs` - Main UI controller (✅ ENHANCED)
- `AuditEventRow.cs` - Individual event row component
- `AuditLogTestGenerator.cs` - Test event generator (NEW)
- `AuditLogUIButtons.cs` - UI button handlers (NEW)

### Other Related Scripts
- `PollBoard.cs` - Integrates with audit system for poll votes
- `BehaviorBoard.cs` - Simple event board (separate from Analytics Canvas)

---

## JSON File Format

Audit logs are saved as JSON files:

```json
{
  "events": [
    {
      "timestamp": "2026-01-20T14:30:45.123Z",
      "sessionId": "abc-123-def-456",
      "playerName": "Alice",
      "eventType": "POLL_VOTE",
      "targetId": "Option A",
      "sceneName": "SampleScene",
      "zoneName": null,
      "position": null,
      "metaJson": "{\"question\":\"Your question?\",\"chosenOption\":\"Option A\"}"
    },
    {
      "timestamp": "2026-01-20T14:30:50.456Z",
      "sessionId": "abc-123-def-456",
      "playerName": "Bob",
      "eventType": "APPLE_PICKED",
      "targetId": "Red Apple",
      "sceneName": "SampleScene",
      "zoneName": "Orchard",
      "position": {
        "x": 1.5,
        "y": 0.0,
        "z": 2.3
      },
      "metaJson": null
    }
  ]
}
```

---

## Next Steps

1. ✅ **Test the system** with the test generator
2. ✅ **Verify events display** on the Analytics Canvas
3. ✅ **Integrate with your gameplay** by adding audit log calls where needed
4. ✅ **Customize the display** colors and format to match your UI style

---

## Support

If you encounter issues:
1. Check Unity Console for `[AnalyticsCanvas]` and `[AUDIT]` messages
2. Verify all GameObject references are assigned in the Inspector
3. Ensure AuditLogger is initialized (check for "AuditLogger" GameObject in Hierarchy during play)
4. Check that audit log files exist in the expected directories

---

**Last Updated:** January 20, 2026
**System Version:** Enhanced Analytics Canvas v2.0

