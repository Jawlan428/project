# Weather System Setup Guide

This guide explains how to set up the Weather Control Panel so that pressing **Sunny**, **Rainy**, or **Storm** changes the actual world visuals (light, sky, fog, rain, lightning, audio) and farm simulation values.

---

## 1. Scene Objects to Create

### A. Directional Light (Sun)

If you don't have one:

1. **GameObject → Light → Directional Light**
2. Name it `Directional Light`
3. Position: above and angled (e.g. rotation 50, -30, 0)
4. Assign this to **WeatherManager → Directional Light**

### B. Rain Particle System

1. **GameObject → Effects → Particle System**
2. Name it `RainParticles`
3. Configure:
   - **Shape**: Box (e.g. 20 x 1 x 20) above the farm
   - **Start Lifetime**: 1–2
   - **Start Speed**: 15–25 (downward)
   - **Start Size**: 0.05–0.1
   - **Gravity Modifier**: 1
   - **Simulation Space**: World
   - **Emission → Rate over Time**: 800 (WeatherManager will override for Storm)
4. Assign the **ParticleSystem** GameObject to **WeatherManager → Rain Particle System**
5. Start with the GameObject **disabled** (WeatherManager enables it for Rainy/Storm)

### C. Skybox Materials (Optional)

Create or import 3 skybox materials:

- **Sunny**: Clear blue sky (e.g. Procedural Sky or custom)
- **Rainy**: Cloudy/gray sky
- **Storm**: Dark stormy sky

Assign in **WeatherManager**:
- Sunny Skybox
- Rainy Skybox  
- Storm Skybox

If left empty, the current scene skybox is used for all weather types.

### D. Audio Sources

1. Create 3 empty GameObjects as children of your WeatherManager object (or FarmSimulationHub)
2. Add **AudioSource** to each:
   - `SunnyAmbient` – optional birds/ambient
   - `RainAmbient` – rain loop (enable **Loop**)
   - `StormAmbient` – wind + thunder loop (enable **Loop**)
3. Assign **AudioClip** for each (import or use Unity’s free audio)
4. Assign in **WeatherManager**:
   - Sunny Ambient Source
   - Rainy Ambient Source
   - Storm Ambient Source

### E. Lightning Effect (Optional)

1. **Option A**: Add **LightningEffect** to the same GameObject as the Directional Light, or  
2. **Option B**: Create a child GameObject with a Light + LightningEffect
3. Assign the **LightningEffect** component to **WeatherManager → Lightning Effect**
4. LightningEffect will flash the light during Storm

---

## 2. WeatherManager Inspector Setup

On **FarmSimulationHub** (or wherever WeatherManager lives):

### References

| Field | Assign |
|-------|--------|
| Simulation Manager | FarmSimulationManager (same hub) |
| Plant Growth Manager | PlantGrowthManager |
| Directional Light | Your Directional Light |
| Rain Particle System | RainParticles GameObject |

### Skybox

**Full setup creates 3 procedural skybox materials automatically** in `Assets/SmartFarm/WeatherSkyboxes/`:
- `Skybox_Sunny.mat` – clear blue sky
- `Skybox_Rainy.mat` – cloudy gray sky
- `Skybox_Storm.mat` – dark stormy sky

They are assigned to WeatherManager automatically. If you prefer custom skyboxes, assign your own materials and they will override the defaults.

### Fog (optional)

Default values are in the script. Adjust if needed:

- Sunny: low density ~0.001
- Rainy: medium ~0.008
- Storm: higher ~0.02

### Rain Particles

| Field | Default |
|-------|---------|
| Rainy Emission Rate | 800 |
| Storm Emission Rate | 2000 |

### Lightning

| Field | Assign |
|-------|--------|
| Lightning Effect | LightningEffect component |

### Audio

| Field | Assign |
|-------|--------|
| Sunny Ambient Source | SunnyAmbient AudioSource |
| Rainy Ambient Source | RainAmbient AudioSource |
| Storm Ambient Source | StormAmbient AudioSource |

---

## 3. WeatherUIController Wiring

On your **Weather Control Panel** GameObject:

### References

| Field | Assign |
|-------|--------|
| Weather Manager | WeatherManager (on FarmSimulationHub) |
| Current Weather Text | TMP_Text showing "Current: Sunny" |
| Description Text | TMP_Text for the description |
| Sunny Button | Button |
| Rainy Button | Button |
| Storm Button | Button |

### Button OnClick Events

**WeatherUIController** wires buttons in code when references are assigned. No manual OnClick setup needed.

If buttons are not assigned, the script tries to find them by name (Sunny, Rainy, Storm).

---

## 4. Quick Setup Checklist

- [ ] Directional Light exists and is assigned to WeatherManager
- [ ] Rain Particle System created and assigned (start disabled)
- [ ] WeatherManager has Simulation Manager + Plant Growth Manager
- [ ] WeatherUIController has Weather Manager + all 3 buttons + text fields
- [ ] (Optional) Skybox materials assigned
- [ ] (Optional) Audio sources assigned
- [ ] (Optional) LightningEffect on Directional Light and assigned

---

## 5. Behavior Summary

| Weather | Light | Sky | Fog | Rain | Sound | Farm Effects |
|---------|-------|-----|-----|------|-------|--------------|
| **Sunny** | Bright, warm | Clear | Minimal | Off | Optional ambient | Temp ↑, growth ↑, moisture ↓ |
| **Rainy** | Softer, dimmer | Cloudy | Medium | On | Rain loop | Moisture ↑, temp ↓, health ↑ |
| **Storm** | Dark | Stormy | Heavy | Heavy rain | Storm loop | Moisture ↑↑, health ↓, random damage |

---

## 6. Troubleshooting

**Buttons don’t change visuals**
- Ensure WeatherManager is on the scene (e.g. FarmSimulationHub)
- Check WeatherUIController → Weather Manager is assigned
- Verify Directional Light is assigned

**Rain doesn’t show**
- Rain Particle System GameObject must be assigned
- Ensure it’s a child of an active object

**No lightning**
- LightningEffect must be assigned
- LightningEffect needs a Light reference (uses same object’s Light if not set)

**Farm values don’t change**
- FarmSimulationManager and PlantGrowthManager must be assigned
- Ensure you’re in Host/Single-player (simulation runs on host only)
