# Audit Logging System - Setup Guide

## Overview
The audit logging system tracks meaningful VR interactions and persists them to JSON files. All events are displayed in real-time on the BehaviorBoard.

## Scripts Created
All scripts are located in `Assets/Scripts/`:
- `AuditEventType.cs` - Enumeration of event types
- `AuditEvent.cs` - Serializable event data structure
- `PlayerIdentity.cs` - Singleton for storing player name
- `AuditLogger.cs` - Main logging singleton
- `AuditBootstrap.cs` - Initialization component

---

## Setup Steps

### Step 1: Create the AuditSystem GameObject
1. In Unity, open the **Office** scene (or your main scene)
2. Create a new empty GameObject:
   - Right-click in Hierarchy → Create Empty
   - Name it: `AuditSystem`
3. Attach the `AuditBootstrap` component:
   - Select `AuditSystem`
   - In Inspector, click "Add Component"
   - Search for "AuditBootstrap" and add it

**That's it!** The system will auto-initialize on scene start.

---

## Integration Points

### A) Player Name Setup (Required)
When the player confirms their name in the Join UI, add this code:

```csharp
// AUDIT INTEGRATION
// In your existing "Join" UI script where player name is confirmed:
PlayerIdentity.Instance.SetPlayerName(inputName);
AuditLogger.Instance.Log(AuditEventType.JOIN_MEETING);
```

**Example location:** In `PlayerAppearanceMenu.cs` or wherever `SubmitNewPlayerName` is called:
```csharp
public void SubmitNewPlayerName(string text)
{
    XRINetworkGameManager.LocalPlayerName.Value = text;
    
    // AUDIT INTEGRATION
    PlayerIdentity.Instance.SetPlayerName(text);
    AuditLogger.Instance.Log(AuditEventType.JOIN_MEETING);
}
```

### B) Apple Interaction Examples

**Apple Picked:**
```csharp
// AUDIT INTEGRATION
AuditLogger.Instance.Log(
    AuditEventType.APPLE_PICKED, 
    targetId: appleName, 
    zoneName: "Orchard", 
    position: appleTransform.position
);
```

**Apple Dropped:**
```csharp
// AUDIT INTEGRATION
AuditLogger.Instance.Log(
    AuditEventType.APPLE_DROPPED, 
    targetId: appleName, 
    zoneName: "Orchard", 
    position: dropPosition
);
```

**Apple Added to Inventory:**
```csharp
// AUDIT INTEGRATION
AuditLogger.Instance.Log(
    AuditEventType.APPLE_ADDED_TO_INVENTORY, 
    targetId: "Inventory", 
    metaJson: "{\"appleName\":\"" + appleName + "\",\"slot\":\"" + slotIndex + "\"}"
);
```

**Apple Removed from Inventory:**
```csharp
// AUDIT INTEGRATION
AuditLogger.Instance.Log(
    AuditEventType.APPLE_REMOVED_FROM_INVENTORY, 
    targetId: "Inventory", 
    metaJson: "{\"appleName\":\"" + appleName + "\"}"
);
```

### C) Zone/Scene Transitions

**Enter Office:**
```csharp
// AUDIT INTEGRATION
Vector3 playerPos = playerTransform.position;
AuditLogger.Instance.Log(
    AuditEventType.ENTER_OFFICE, 
    zoneName: "Office", 
    position: playerPos
);
```

**Exit Office:**
```csharp
// AUDIT INTEGRATION
AuditLogger.Instance.Log(
    AuditEventType.EXIT_OFFICE, 
    zoneName: "Office"
);
```

**Leave Meeting:**
```csharp
// AUDIT INTEGRATION
AuditLogger.Instance.Log(AuditEventType.LEAVE_MEETING);
```

### D) Error Logging
```csharp
// AUDIT INTEGRATION
AuditLogger.Instance.Log(
    AuditEventType.ERROR, 
    targetId: "System", 
    metaJson: "{\"error\":\"" + errorMessage + "\",\"stackTrace\":\"" + stackTrace + "\"}"
);
```

---

## Verification

### 1. Check BehaviorBoard
- Run the scene
- Look at the BehaviorBoard in the Office scene
- You should see audit events appearing in real-time:
  - `SESSION_START | player=Unknown`
  - `JOIN_MEETING | player=FJ9` (after setting name)
  - Other events as they occur

### 2. Check Console
- Open Unity Console (Window → General → Console)
- Look for `[AUDIT]` prefixed messages
- Should see: `[AuditLogger] Initialized. Session ID: <guid>`

### 3. Check JSON Files
The audit logs are saved to:
```
Application.persistentDataPath + "/AuditLogs/"
```

**To find the path:**
- In Unity Editor: Check Console for `[AuditLogger] Created log directory: <path>`
- On Windows: Usually `C:\Users\<YourUsername>\AppData\LocalLow\<CompanyName>\<ProjectName>\AuditLogs\`
- File format: `audit_<sessionId>_<yyyy-MM-dd_HH-mm-ss>.json`

**Example file location:**
```
C:\Users\Fahid Jamoly\AppData\LocalLow\DefaultCompany\YourProject\AuditLogs\audit_abc123_2024-01-15_14-30-45.json
```

### 4. Manual Flush (Optional)
If you need to flush events to disk before session end:
```csharp
AuditLogger.Instance.Flush();
```

---

## JSON File Format

Each session creates one JSON file with this structure:
```json
{
    "events": [
        {
            "timestamp": "2024-01-15T14:30:45.123Z",
            "sessionId": "abc123-def456-...",
            "playerName": "FJ9",
            "eventType": "SESSION_START",
            "targetId": null,
            "sceneName": "Office",
            "zoneName": null,
            "position": null,
            "metaJson": null
        },
        {
            "timestamp": "2024-01-15T14:30:50.456Z",
            "sessionId": "abc123-def456-...",
            "playerName": "FJ9",
            "eventType": "APPLE_PICKED",
            "targetId": "Apple_12",
            "sceneName": "Office",
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

## Extending the System

### Adding New Event Types
1. Open `AuditEventType.cs`
2. Add new enum value:
   ```csharp
   public enum AuditEventType
   {
       // ... existing types ...
       NEW_EVENT_TYPE
   }
   ```
3. Use it anywhere:
   ```csharp
   AuditLogger.Instance.Log(AuditEventType.NEW_EVENT_TYPE, targetId: "Target");
   ```

### Custom Metadata
Use the `metaJson` parameter for structured data:
```csharp
string meta = "{\"customField\":\"value\",\"number\":42}";
AuditLogger.Instance.Log(AuditEventType.CUSTOM_EVENT, metaJson: meta);
```

---

## Safety Features

✅ **Null-safe:** BehaviorBoard is checked before use (no errors if missing)  
✅ **Auto-initialization:** Singletons create themselves if needed  
✅ **DontDestroyOnLoad:** PlayerIdentity and AuditLogger persist across scenes  
✅ **Auto-flush:** Events are saved on ApplicationQuit and OnDestroy  
✅ **Directory creation:** Log directory is created automatically if missing  

---

## Troubleshooting

**Q: No events appear on BehaviorBoard**  
A: Check that BehaviorBoard.Instance exists in the scene. The system is safe if it doesn't exist (no errors), but events won't display.

**Q: JSON files not created**  
A: Check Console for errors. Verify `Application.persistentDataPath` is writable. Check the path in Console logs.

**Q: Player name always "Unknown"**  
A: Ensure you call `PlayerIdentity.Instance.SetPlayerName(name)` when the player confirms their name.

**Q: Duplicate SESSION_START events**  
A: Ensure only one GameObject has `AuditBootstrap` attached. Check for multiple instances in the scene.

---

## Notes

- All scripts use built-in Unity APIs only (no Asset Store dependencies)
- No existing gameplay, UI, networking, or XR setup is modified
- Integration requires only adding one line per hook point (marked with `// AUDIT INTEGRATION`)
- The system is fully extensible for future event types
