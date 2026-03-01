# Smart Collaborative VR Agriculture Platform – Architecture

## Overview

A modular, scalable, Quest-friendly VR agriculture platform combining:
- **Smart Crop Growth Simulation** (tick-based, host-authoritative)
- **3D Farm Management Dashboard** (world-space UI)
- **Poll Vote System** (Option A / B, networked)
- **3D Recording + Event Logging** (AuditLogger integration)
- **Host-Authoritative Multiplayer** (Netcode for GameObjects)

---

## 1. Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        HOST (Server) – Authoritative                          │
├─────────────────────────────────────────────────────────────────────────────┤
│  FarmSimulationManager          PlantGrowthManager         PollVoteManager   │
│  ├─ Tick loop (0.5–1s)          ├─ Tick loop               ├─ OpenPoll     │
│  ├─ Global temp, irrigation      ├─ SimulateTick(plant)      ├─ VoteServerRpc│
│  ├─ Water usage, alerts          └─ Apply environment         └─ ClosePoll    │
│  └─ Build FarmSimulationState    PlantController (per plant)   Apply result  │
│           │                              │                          │       │
│           └──────────────────────────────┼──────────────────────────┘       │
│                                         │                                    │
│  FarmSimulationNetworkSync ◄────────────┘                                    │
│  ├─ SetState(state)  [Host only]                                             │
│  └─ NetworkVariables → broadcast to clients                                  │
└─────────────────────────────────────────────────────────────────────────────┘
                                          │
                    NetworkVariables (Netcode for GameObjects)
                                          │
┌─────────────────────────────────────────────────────────────────────────────┐
│                        CLIENTS – Display Only                                 │
├─────────────────────────────────────────────────────────────────────────────┤
│  FarmSimulationNetworkSync         FarmDashboardUI         PollVoteUI         │
│  ├─ GetState()                     ├─ Display metrics      ├─ Vote buttons   │
│  └─ OnStateUpdated → UI            └─ Alerts (red)         └─ Results text   │
│                                                                              │
│  EventLogger.LogEvent() → AuditLogger → 3D Recording / Version System         │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Core Modules

| Module | Responsibility | Host | Client |
|--------|----------------|------|--------|
| **FarmSimulationManager** | Global temp, irrigation, water usage, alerts, predicted yield | ✓ Runs tick | ✗ Display only |
| **PlantController** | Per-plant: growth stages, health, water, temp, fertilizer | ✓ Simulated by PlantGrowthManager | ✗ Visuals only |
| **PlantGrowthManager** | Tick-based plant simulation, save/load | ✓ Runs tick | ✗ Init only |
| **FarmDashboardUI** | Display soil, health, water, temp, yield, alerts | ✓ | ✓ |
| **PollVoteManager** | A/B votes, one per participant, apply irrigation | ✓ Validates & applies | ✓ Sends RPCs |
| **EventLogger** | Log events → AuditLogger → 3D recording | ✓ | ✓ (via host RPCs) |
| **FarmSimulationNetworkSync** | Sync state host → clients | ✓ Writes | ✓ Reads |

---

## 3. Smart Crop Growth System

### Growth Stages
- **0** Seed → **1** Sprout → **2** Young → **3** Mature → **4** Dead (optional)

### Per-Plant Factors
- **Health** (0–100): Reduced by poor water, temp, sunlight, fertilizer
- **Soil moisture** (water level): Decays over time; irrigation adds water
- **Temperature**: Set globally by FarmSimulationManager
- **Fertilizer**: Decays; can be added via VR tools
- **Growth multiplier**: Based on ideal ranges (geometric mean)

### Tick-Based Simulation
- Interval: 0.5–1 second (configurable)
- No per-frame `Update()` on plants
- Water level decreases over time
- Health = 0 → plant dies (Dead stage)

### FarmSimulationManager Controls
- Global temperature
- Irrigation state (ON/OFF)
- Daily water usage
- Predicted yield (healthy mature plants)

---

## 4. 3D Farm Dashboard (World Space UI)

**Display only** – no simulation logic.

| Field | Source |
|-------|--------|
| Soil Moisture % | Average of all plants |
| Crop Health % | Average of all plants |
| Water Usage Today | FarmSimulationManager |
| Temperature | Global temperature |
| Predicted Yield | Count of healthy mature plants |
| Active Alerts | Computed from thresholds |

### XR Support
- XR Interaction Toolkit
- XR Ray Interactor
- Tracked Device Graphic Raycaster
- XR UI Input Module on EventSystem

---

## 5. Alert System

| Alert | Threshold |
|-------|-----------|
| Low Water Level | Soil Moisture < 30% |
| High Temperature Risk | Temperature > 35°C |
| Crop Health Critical | Crop Health < 40% |

Alerts display in **red** and disappear when conditions are safe.

---

## 6. Poll Vote System (Option A / B)

- **Question**: "Enable Irrigation?" (configurable)
- **Option A**: Yes (Enable) → Irrigation ON
- **Option B**: No (Keep Off) → Irrigation remains OFF

### Rules
- One vote per participant
- Show voter names (host only; clients see counts)
- Show total votes and percentage per option
- Results synchronized via NetworkVariables

---

## 7. Host Authoritative Design

- **Only Host** updates farm simulation values
- **Clients** send action requests (e.g., VoteServerRpc)
- **Host** validates and updates
- **Host** broadcasts state via FarmSimulationNetworkSync

No client simulates farm logic independently.

---

## 8. Recording + Event Logging

Every important action calls `EventLogger.LogEvent()`:

| Event | Example |
|-------|---------|
| Vote Opened | `"Vote Opened"` |
| Vote | `"Jawlan voted Yes (Enable)"` |
| Irrigation | `"Irrigation Enabled"` |
| Temperature | `"Temperature changed to 32°C"` |
| Plant stage | `"Plant #abc123 reached Mature stage"` |

EventLogger forwards to **AuditLogger** (FARM_EVENT, FARM_IRRIGATION_CHANGED, etc.) for 3D recording/version management.

---

## 9. Performance (Quest-Safe)

- Tick-based updates (0.5–1 s)
- No allocations inside tick loops
- No per-plant `Update()`
- Efficient data structures
- Supports 20–100 plants without FPS drop

---

## 10. Folder Structure

```
Assets/
├── Scripts/
│   ├── SmartFarm/
│   │   ├── FarmSimulationManager.cs      # Host-only simulation
│   │   ├── FarmSimulationState.cs        # State struct
│   │   ├── FarmSimulationNetworkSync.cs  # Network sync (implements INetworkSyncInterface)
│   │   ├── FarmDashboardUI.cs            # Display only
│   │   ├── FarmDashboardDrag.cs          # Drag to move dashboard
│   │   ├── PollVoteManager.cs            # A/B vote system
│   │   ├── PollVoteUI.cs                 # Poll UI wiring
│   │   ├── EventLogger.cs                # Event → AuditLogger
│   │   ├── NetworkSyncInterface.cs       # INetworkSyncInterface + LocalSyncInterface
│   │   ├── PLATFORM_ARCHITECTURE.md      # This file
│   │   └── SMART_FARM_SETUP_GUIDE.md     # Setup instructions
│   └── PlantGrowth/
│       ├── PlantController.cs             # Per-plant logic
│       ├── PlantGrowthManager.cs         # Tick-based plant simulation
│       ├── PlantStageAsset.cs             # Stage config
│       └── ...
├── SmartFarm/
│   └── Prefabs/
│       ├── FarmSimulationHub.prefab
│       └── FarmDashboard.prefab
└── PlantGrowth/
    ├── Prefabs/
    │   └── PlantInstance.prefab
    └── Data/
        └── DefaultPlantStage.asset
```

---

## 11. Prefab Structure

```
FarmSimulationHub (root, NetworkObject)
├── FarmSimulationManager
├── FarmSimulationNetworkSync
└── PollVoteManager

FarmDashboard (World Space Canvas)
├── FarmDashboardUI
├── FarmDashboardDrag (on header)
├── GraphicRaycaster
├── TrackedDeviceGraphicRaycaster
├── Panel
│   ├── Header (draggable)
│   ├── SoilMoistureText
│   ├── CropHealthText
│   ├── WaterUsageText
│   ├── TemperatureText
│   ├── PredictedYieldText
│   ├── AlertsText
│   └── PollVotePanel
│       ├── QuestionText
│       ├── VoteAButton / VoteBButton
│       ├── ResultsText
│       ├── OpenPollButton
│       └── ClosePollButton
```

---

## 12. Data Flow

1. **Host**: FarmSimulationManager runs tick → computes state → FarmSimulationNetworkSync.SetState()
2. **Network**: NetworkVariables broadcast to clients
3. **Clients**: FarmSimulationNetworkSync.OnStateUpdated → FarmDashboardUI.ApplyState()
4. **Vote**: Client clicks Vote A/B → VoteServerRpc → Host validates → updates votes → ClosePollAndApply → FarmSimulationManager.SetIrrigationEnabled()
