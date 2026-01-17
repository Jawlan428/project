# 🚀 QUICK START - Audit System Setup

## Step 1: Create AuditSystem GameObject (2 minutes)

1. **Open your Office scene** (or main scene)
2. **In Hierarchy panel:**
   - Right-click → Create Empty
   - Name it: `AuditSystem`
3. **In Inspector panel:**
   - Select `AuditSystem`
   - Click "Add Component"
   - Type: `AuditBootstrap`
   - Press Enter

✅ **Done!** The system will auto-start when you play the scene.

---

## Step 2: Integrate Player Name (1 line of code)

**File to edit:** `Assets/VRMPAssets/Scripts/Player/PlayerAppearanceMenu.cs`

**Find this method** (around line 40):
```csharp
public void SubmitNewPlayerName(string text)
{
    XRINetworkGameManager.LocalPlayerName.Value = text;
}
```

**Change it to:**
```csharp
public void SubmitNewPlayerName(string text)
{
    XRINetworkGameManager.LocalPlayerName.Value = text;
    
    // AUDIT INTEGRATION
    PlayerIdentity.Instance.SetPlayerName(text);
    AuditLogger.Instance.Log(AuditEventType.JOIN_MEETING);
}
```

✅ **Save the file** - Unity will auto-compile.

---

## Step 3: Test It!

1. **Press Play** in Unity
2. **Check BehaviorBoard** - You should see:
   - `SESSION_START | player=Unknown`
   - `JOIN_MEETING | player=YourName` (after entering name)
3. **Check Console** - Look for `[AUDIT]` messages
4. **Stop Play** - This triggers SESSION_END and saves JSON

---

## Step 4: Find Your Log Files

After stopping Play mode, logs are saved to:
```
C:\Users\Fahid Jamoly\AppData\LocalLow\<YourCompany>\<YourProject>\AuditLogs\
```

**To find exact path:**
- Check Unity Console for: `[AuditLogger] Created log directory: <path>`
- Or add this temporary debug code:
  ```csharp
  Debug.Log("Log path: " + Application.persistentDataPath + "/AuditLogs/");
  ```

---

## ✅ That's It!

The system is now working. You can add more event hooks later (see `AUDIT_SYSTEM_SETUP.md` for examples).

**Next Steps (Optional):**
- Add apple interaction logging
- Add zone transition logging
- Customize event types
