# Smart Collaborative VR Agriculture Platform – Setup Guide

> **Architecture overview:** See [PLATFORM_ARCHITECTURE.md](PLATFORM_ARCHITECTURE.md)

## Quick Start (One-Click)

**Tools > Farm > Farm Setup**

This creates everything in your current scene:
- FarmSimulationHub (with NetworkObject, FarmSimulationManager, FarmSimulationNetworkSync, PollVoteManager)
- FarmDashboard (world-space Canvas with metrics + Poll panel)
- PlantGrowthManager (if not present)
- 3 PlantInstance prefabs (if none exist)
- XR UI Input Module on EventSystem
- Adds scene to Build Settings

Then **Save Prefabs** (optional): **Tools > Farm > Save Prefabs**  
And **Register** (for multiplayer): **Tools > Farm > Register with NetworkManager**

---

## Overview

This system combines:
- **Smart Crop Growth Simulation** (tick-based, host-authoritative)
- **3D Farm Management Dashboard** (world-space UI)
- **Poll Vote System** (Option A / B, networked)
- **Event Logging** (connects to AuditLogger / 3D recording)
- **Host-Authoritative Multiplayer** (Netcode for GameObjects)

---

## 1. Folder Structure

```
Assets/
├── Scripts/
│   ├── SmartFarm/
│   │   ├── EventLogger.cs
│   │   ├── FarmSimulationState.cs
│   │   ├── FarmSimulationManager.cs
│   │   ├── FarmSimulationNetworkSync.cs
│   │   ├── FarmDashboardUI.cs
│   │   ├── PollVoteManager.cs
│   │   ├── NetworkSyncInterface.cs
│   │   └── SMART_FARM_SETUP_GUIDE.md
│   └── PlantGrowth/
│       ├── PlantController.cs
│       ├── PlantGrowthManager.cs
│       └── ...
├── SmartFarm/
│   ├── Prefabs/
│   │   ├── FarmSimulationHub.prefab      (Manager + NetworkSync)
│   │   ├── FarmDashboard.prefab         (World-space Canvas)
│   │   └── PollVotePanel.prefab         (Poll UI with buttons)
│   └── Scenes/
│       └── SmartFarmScene.unity
└── PlantGrowth/
    └── (existing plant prefabs)
```

---

## 2. Scripts Reference

| Script | Purpose |
|--------|---------|
| **FarmSimulationManager** | Host-only tick loop; global temp, irrigation, water usage, alerts, predicted yield |
| **PlantController** | Per-plant: growth stages (Seed→Sprout→Young→Mature→Dead), health, water, temp, fertilizer |
| **FarmDashboardUI** | Display only: soil, health, water, temp, yield, alerts |
| **PollVoteManager** | A/B vote system; one vote per participant; Option A wins = irrigation ON |
| **EventLogger** | Logs events → AuditLogger → 3D recording/version system |
| **FarmSimulationNetworkSync** | Implements INetworkSyncInterface; syncs state host → clients |
| **NetworkSyncInterface** | INetworkSyncInterface + LocalSyncInterface (fallback) |

---

## 3. Step-by-Step Unity Setup

### Step 1: Create the Farm Simulation Hub

1. Create an empty GameObject: **FarmSimulationHub**
2. Add **NetworkObject** component (required for Netcode)
3. Add **FarmSimulationManager** component
4. Add **FarmSimulationNetworkSync** component
5. Add **PlantGrowthManager** component (or reference existing one in scene)
6. Configure:
   - **FarmSimulationManager**: Tick Interval 0.5, assign PlantGrowthManager ref
   - **FarmSimulationNetworkSync**: Will auto-bind
7. **Important**: Add FarmSimulationHub to the **NetworkManager's Network Prefabs List** if it will be spawned. For scene objects, ensure the scene is in Build Settings and the object has NetworkObject.

### Step 2: Create World-Space Farm Dashboard Canvas

1. **Create Canvas**
   - GameObject → UI → Canvas
   - Name: **FarmDashboard**
   - Canvas: Render Mode = **World Space**
   - Rect Transform: Scale (0.01, 0.01, 0.01), Position where you want in 3D

2. **Add XR UI Support**
   - Add **Tracked Device Graphic Raycaster** to the Canvas
   - Ensure **EventSystem** exists in scene with **XR UI Input Module**
   - Add **Canvas Group** if you want fade/block raycasts

3. **Create Display Panel**
   - Create child Panel (Image with dark background)
   - Add 6 TextMeshPro - Text (UI) children:
     - Soil Moisture
     - Crop Health
     - Water Usage Today
     - Temperature
     - Predicted Yield
     - Active Alerts

4. **Add FarmDashboardUI**
   - Add **FarmDashboardUI** component to Canvas
   - Assign each TMP_Text to the corresponding slot
   - Assign FarmSimulationManager and FarmSimulationNetworkSync (or leave null to auto-find)

5. **XR Interaction**
   - Ensure **XR Ray Interactor** on your controllers can hit the Canvas
   - Use **Tracked Device Graphic Raycaster** on the Canvas
   - No extra setup needed if XR Interaction Toolkit is configured

### Step 3: Create Poll Vote Panel

1. Create a child under FarmDashboard or a separate Canvas: **PollVotePanel**
2. Add:
   - Question text (TMP_Text): "Enable Irrigation?"
   - Two buttons: **Vote A**, **Vote B**
   - Results text (TMP_Text): shows counts and percentages
   - **Open Poll** button
   - **Close & Apply** button

3. Add **PollVoteUI** component to PollVotePanel:
   - Assign PollVoteManager (or leave null to auto-find)
   - Assign questionText, resultsText, voteAButton, voteBButton, openPollButton, closePollButton
   - PollVoteUI wires buttons automatically and refreshes results

4. **PollVoteManager** must be on a GameObject with **NetworkObject**
   - Add to FarmSimulationHub or create **PollVoteObject**
   - Add to Network Prefabs if spawned dynamically

### Step 4: Host Authoritative Logic

- **FarmSimulationManager**: Only runs `SimulateTick` when `NetworkManager.Singleton.IsServer` (or when not connected)
- **FarmSimulationNetworkSync**: Host writes via `SetState()`; clients read via `GetState()`
- **PollVoteManager**: ServerRpc for votes; host validates and applies irrigation

No client should run plant simulation. Clients only display synced state.

### Step 5: Connect Modules

| From              | To                    | Connection                          |
|-------------------|------------------------|-------------------------------------|
| FarmSimulationHub  | PlantGrowthManager     | Assign in Inspector                  |
| FarmSimulationHub  | FarmSimulationNetworkSync | Same GameObject                   |
| FarmDashboardUI    | FarmSimulationManager  | Auto-find or assign                  |
| FarmDashboardUI    | FarmSimulationNetworkSync | Auto-find or assign              |
| PollVoteManager    | FarmSimulationManager  | Uses `FarmSimulationManager.Instance.SetIrrigationEnabled()` |
| EventLogger        | AuditLogger            | Static calls; AuditLogger must exist |

---

## 4. Prefab Structure Recommendation

```
FarmSimulationHub (root)
├── FarmSimulationManager
├── FarmSimulationNetworkSync
├── NetworkObject
└── (optional) PlantGrowthManager reference

FarmDashboard (World Space Canvas)
├── FarmDashboardUI
├── Tracked Device Graphic Raycaster
├── Panel
│   ├── SoilMoistureText
│   ├── CropHealthText
│   ├── WaterUsageText
│   ├── TemperatureText
│   ├── PredictedYieldText
│   └── AlertsText
└── PollVotePanel (child)
    ├── QuestionText
    ├── VoteAButton
    ├── VoteBButton
    ├── ResultsText
    ├── OpenPollButton
    └── ClosePollButton
```

---

## 4. XR UI Configuration Checklist

- [ ] EventSystem has **XR UI Input Module**
- [ ] Canvas has **Tracked Device Graphic Raycaster**
- [ ] XR Ray Interactor (on controller) can hit UI layers
- [ ] Canvas layer is in **Raycast** mask of XR Ray Interactor

---

## 6. Network Configuration

- [ ] NetworkManager (VR Multiplayer) in scene
- [ ] FarmSimulationHub and PollVoteManager objects have **NetworkObject**
- [ ] If scene objects: add scene to Build Settings; Netcode will spawn scene NetworkObjects
- [ ] If prefabs: add to NetworkManager's Network Prefabs list

---

## 7. Alert Thresholds (Configurable in FarmSimulationManager)

| Alert                    | Default Threshold |
|--------------------------|-------------------|
| Low Water Level          | Soil Moisture < 30% |
| High Temperature Risk    | Temperature > 35°C |
| Crop Health Critical     | Crop Health < 40% |

Alerts display in **red** and disappear when conditions are safe.

---

## 7. Event Logging Integration

All important actions call `EventLogger.LogEvent()` which forwards to:
- **AuditLogger** (FARM_EVENT, FARM_IRRIGATION_CHANGED, etc.)
- **3D Recording / Version System** (via AuditLogger persistence)

Example events:
- "Vote Opened"
- "Jawlan voted Option A"
- "Irrigation Enabled"
- "Temperature changed to 32°C"
- "Plant #abc123 reached Mature stage"

---

## 9. Per-Plant Sync (Optional Future)

Currently, the **dashboard** (aggregated soil moisture, health, yield, etc.) is synced from host to clients. Individual **plant visuals** (stage, health) are not yet synced per-plant. On clients, plants may remain at initial state. To sync each plant, add NetworkVariables to PlantController or a separate PlantStateSync component.

---

## 10. Performance (Quest-Safe)

- Tick interval: 0.5–1 s (no per-frame Update for simulation)
- No allocations inside tick loops
- No per-plant Update()
- Supports 20–100 plants without FPS drop

---

## 11. Quick Test (Single-Player)

1. Add FarmSimulationHub to scene (with PlantGrowthManager + plants)
2. Add FarmDashboard Canvas with FarmDashboardUI
3. Press Play
4. Dashboard should show live data
5. Use FarmSimulationManager.SetIrrigationEnabled(true) from a test script to verify irrigation

---

## 12. Multiplayer Test

1. Build or use ParrelSync for two instances
2. One as Host, one as Client
3. Host: simulation runs; dashboard updates
4. Client: dashboard shows synced data (from FarmSimulationNetworkSync)
5. Both: can vote; host applies result
