# 📋 Step-by-Step Setup Guide

## ✅ Step 1: Open Unity Editor
- Make sure Unity Editor is open
- Wait for scripts to compile (check bottom-right corner for "Compiling..." to finish)

---

## ✅ Step 2: Open Your Scene
- In Unity, open the **Office scene** (or whichever scene you want audit logging in)
- File → Open Scene → Select your scene
- OR double-click the scene file in the Project window

---

## ✅ Step 3: Click the Setup Menu
1. Look at the **top menu bar** in Unity (File, Edit, Assets, GameObject, Component, Window, Help, **Tools**)
2. Click **Tools**
3. Click **Audit System**
4. Click **Setup Audit System in Current Scene**

---

## ✅ Step 4: Confirm Setup
- A dialog box will appear saying "Setup Complete!"
- Click **OK**
- You should see a new GameObject called **"AuditSystem"** in your Hierarchy panel

---

## ✅ Step 5: Verify It's There
- Look at the **Hierarchy panel** (usually on the left side)
- You should see **"AuditSystem"** in the list
- Click on it
- In the **Inspector panel** (usually on the right), you should see **"Audit Bootstrap (Script)"** component

---

## ✅ Step 6: Save Your Scene
- Press **Ctrl+S** (or Cmd+S on Mac)
- OR go to **File → Save** (or **File → Save Scene**)
- This saves the AuditSystem GameObject to your scene

---

## ✅ Step 7: Test It!
1. Press the **Play button** (▶️) at the top of Unity
2. Look at the **BehaviorBoard** in your scene (the world-space UI board)
3. You should see: `SESSION_START | player=Unknown`
4. Enter a player name in your game
5. You should see: `JOIN_MEETING | player=YourName`
6. Press **Stop** (⏹️) to stop playing

---

## ✅ Step 8: Check the Logs (Optional)
After stopping Play mode:
1. Open Unity **Console** window (Window → General → Console)
2. Look for a message like: `[AuditLogger] Created log directory: C:\Users\...`
3. Copy that path
4. Open Windows File Explorer
5. Paste the path in the address bar
6. You should see a JSON file like: `audit_abc123_2024-01-15_14-30-45.json`

---

## 🎉 Done!

Your audit system is now set up and working!

---

## ❓ Troubleshooting

**Q: I don't see "Tools → Audit System" in the menu**
- Wait a moment for Unity to compile scripts
- Try closing and reopening Unity
- Check if there are any script errors in the Console

**Q: The dialog says "AuditSystem already exists"**
- Click "Yes, Replace" to create a new one
- OR click "Cancel" if you want to keep the existing one

**Q: I don't see events on BehaviorBoard**
- Make sure BehaviorBoard GameObject exists in your scene
- Check Console for any errors
- Make sure you entered a player name

**Q: Where are the JSON files?**
- Check Unity Console for the log directory path
- Usually: `C:\Users\YourName\AppData\LocalLow\<CompanyName>\<ProjectName>\AuditLogs\`

---

## 📝 Quick Checklist

- [ ] Unity Editor is open
- [ ] Scene is open
- [ ] Clicked "Tools → Audit System → Setup Audit System in Current Scene"
- [ ] Saw "Setup Complete!" dialog
- [ ] AuditSystem appears in Hierarchy
- [ ] Saved the scene (Ctrl+S)
- [ ] Pressed Play and saw events on BehaviorBoard
- [ ] Stopped Play and checked for JSON file

---

That's it! You're all set! 🚀
