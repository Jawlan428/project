# Farm Setup – Quick Guide

## One-Click Setup

**Tools > Farm > Farm Setup**

Creates the full Smart Collaborative VR Agriculture Platform:
- FarmSimulationHub (simulation, network sync, poll voting)
- FarmDashboard (world-space UI with metrics + poll panel)
- PlantGrowthManager + 3 plants
- XR UI support
- Adds scene to Build Settings

---

## Farm Menu (Tools > Farm)

| Option | Purpose |
|--------|---------|
| **Farm Setup** | One-click full setup |
| **Save Prefabs** | Save FarmSimulationHub and FarmDashboard to Assets/SmartFarm/Prefabs |
| **Register with NetworkManager** | Add hub to network prefabs (for multiplayer) |
| **Clear Save Data** | Delete plant save file (fixes freeze/corruption) |
| **Fix Plant Materials** | Fix magenta/pink plant colors |

---

## Plant Growth Menu (Tools > Plant Growth)

Same options as Farm menu, plus:
- **Farm Setup** – Same as above
- **Clear Save Data**
- **Fix Plant Materials**

---

## After Setup

1. Press **Play**
2. Dashboard shows: Soil Moisture, Crop Health, Water Usage, Temperature, Predicted Yield, Alerts
3. Use **Open Poll** → **Vote A** / **Vote B** → **Close & Apply** to control irrigation
