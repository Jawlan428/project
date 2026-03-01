# Farm Setup – Quick Start

## Run Farm Setup

1. Open Unity and your scene (e.g. SampleScene or your farm scene).
2. Go to **Tools > Farm > Farm Setup**.
3. Save the scene (Ctrl+S).
4. Press **Play**.

## What Gets Created

- **FarmSimulationHub** – Simulation, network sync, poll voting
- **FarmDashboard** – World-space UI at (0, 1.5, 2) with metrics + poll panel
- **PlantGrowthManager** – Plant simulation
- **3 Plants** – At (0,0,0), (1.5,0,0), (3,0,0)
- **EventSystem** – With XR UI Input Module (if missing)

## If You See Errors

- **"SmartFarm not found"** – Check Console for compile errors. Fix any red errors first.
- **Plants are magenta** – Run **Tools > Farm > Fix Plant Materials**.
- **Dashboard not visible** – Move the camera to (0, 1.5, 2) or select FarmDashboard and adjust position.

## Multiplayer

1. Run Farm Setup.
2. **Tools > Farm > Save Prefabs**.
3. **Tools > Farm > Register with NetworkManager**.
