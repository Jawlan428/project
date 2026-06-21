# Smart Farm Guide NPC

Turns the **Gardner Avatar** (or any humanoid avatar) into a friendly VR guide that
welcomes the player and walks them to four farm areas using a NavMeshAgent and a
**walk animation only** (never run).

Built for **Unity 6000.2**, **XR Interaction Toolkit 3.2.2**, Meta Quest friendly.

---

## Files

| File | Purpose |
|------|---------|
| `SmartFarmGuideNPC.cs` | Core controller: welcome, interaction, NavMesh walking, animation, facing. |
| `GuideMenuUI.cs` | Floating world-space VR menu (4 buttons), built in code, XR ray/poke ready. |
| `GuideDestination.cs` | Marks a scene transform as a destination (CropField / Meeting / Screens / Training). |
| `GuideStatusLabel.cs` | Floating "Welcome to Smart Farm VR" label above the guide. |
| `GuideArea.cs` | The `GuideArea` enum + destination data types. |
| `Editor/SmartFarmGuideSetupEditor.cs` | One-click setup tool (menu: **Tools ▸ Smart Farm ▸ Guide NPC**). |

---

## Quick start (one-click)

1. **Drag the Gardner Avatar prefab into the scene.** Place it on the ground where
   you want the guide to stand.
2. Make sure the avatar's **Rig** is set to **Humanoid** (select the model FBX ▸
   *Rig* tab ▸ *Animation Type = Humanoid* ▸ Apply). Generic rigs also work as long
   as the clips drive the same skeleton.
3. Select the avatar in the Hierarchy.
4. Run **Tools ▸ Smart Farm ▸ Guide NPC ▸ Setup Guide From Selection**.

That single command:

- Adds **NavMeshAgent** (walk speed 1.4, angular 220, accel 6, stopping distance 1.5).
- Adds a **CapsuleCollider** (so XR ray/poke can select the guide).
- Adds an **AudioSource**, **XR Simple Interactable**, **SmartFarmGuideNPC** and a
  child **GuideMenu**.
- Builds an **Animator Controller** at
  `Assets/Scripts/SmartFarm/GuideNPC/Generated/SmartFarmGuide.controller` with
  **Idle / Walk / Greet / Point** states — **no Run state** — and auto-assigns
  matching clips it finds next to the avatar.
- Creates the four **destination markers** and wires them to the guide.
- Ensures an **EventSystem** with the **XRUIInputModule** exists.

Check the Console afterward — it logs which clips were matched and warns about any
slot it couldn't fill so you can assign it by hand.

---

## 1. NavMesh setup (required for movement)

Unity 6 bakes NavMeshes with the **AI Navigation** package (the old *Navigation*
window Bake tab is gone).

1. **Window ▸ Package Manager ▸ Unity Registry ▸ search "AI Navigation" ▸ Install.**
2. Select the floor/terrain/ground objects of the farm. In the Inspector mark them
   **Static** (or just add the surface component below — it bakes everything by
   default).
3. Create an empty GameObject `NavMeshSurface`, add component **NavMesh Surface**.
   - *Collect Objects*: **All** (or *Children* if you parent the environment).
   - *Include Layers*: the layers your ground/props live on.
4. Press **Bake** on the NavMesh Surface. You should see a blue mesh over walkable
   ground.
5. **Place the guide on the baked NavMesh.** If it isn't on the mesh the script will
   try to snap it to the nearest point and warn in the Console otherwise.

### Avoiding walls, tables, screens, fences, chairs, props

- Anything with a **collider** marked for NavMesh baking becomes a *carve-out* (the
  blue mesh won't cover it), so the agent walks around it automatically.
- For **moving / placed props** add a **NavMeshObstacle** component with **Carve =
  on** so the agent re-routes around them at runtime.
- Make sure walls/fences are tall enough and have colliders, then re-bake.
- Increase the NavMesh Surface *Agent Radius* slightly (e.g. 0.3–0.4) if the guide
  clips corners.

---

## 2. Destinations

The setup tool creates these under a `GuideDestinations` root:

- `CropFieldTarget`
- `MeetingAreaTarget`
- `SmartScreensTarget`
- `TrainingRoomTarget`

**Move each one** to the real spot you want the guide to stop (keep them on the
NavMesh). Each has a `GuideDestination` component:

- **Area** — which button it maps to.
- **Label** — optional custom button text.
- **Look At On Arrival** — optional transform the guide turns toward & *points* at
  when it arrives (e.g. the centre of the crop field). If empty, it just faces the
  player.

You can also author destinations purely in the scene: add empty objects with a
`GuideDestination` component and leave the guide's *Destinations* list empty — it
auto-collects them on Start.

---

## 3. Animation (Idle / Walk / Greet / Point — no Run)

The generated controller exposes three parameters the script drives automatically:

| Parameter | Type | Driven by |
|-----------|------|-----------|
| `Speed` | Float | NavMeshAgent velocity (Idle ↔ Walk blend) |
| `Greet` | Trigger | Welcome sequence |
| `Point` | Trigger | Arrival when a *Look At On Arrival* is set |

- **Walk** plays whenever the agent is moving (`Speed > 0.1`), **Idle** when stopped.
- **Run / jog / sprint clips are intentionally ignored** by the clip finder and
  never added to the controller.
- If a Greet or Point clip isn't found, that state is simply skipped — the guide
  still works, it just won't wave/point.

**Manual clip assignment:** open the generated `SmartFarmGuide.controller`, click a
state, and drag the correct clip into its *Motion* field. To re-run auto-matching
after importing the avatar, use **Tools ▸ Smart Farm ▸ Guide NPC ▸ Rebuild Animator
Controller** (with the avatar selected).

> Tip: set **Apply Root Motion = OFF** on the Animator (the setup does this) — the
> NavMeshAgent moves the character, so root motion would fight it.

---

## 4. VR interaction

- The guide has an **XR Simple Interactable** + collider. Selecting it with an **XR
  Ray Interactor** or **XR Poke Interactor** opens the floating menu.
- The menu is **World Space UI** with a **TrackedDeviceGraphicRaycaster**, so the
  same ray/poke interactors press the buttons. Buttons are large (~13 cm tall) for
  easy VR pressing.
- **Hand tracking** works automatically if your rig uses the XRI hands interactors
  (poke/ray) — no extra code needed.

**Requirements in the scene** (the setup tool handles the EventSystem for you):

- An **XR Origin** with controllers/hands that have Ray and/or Poke interactors
  (the XRI *Starter Assets* rig already has these).
- One **EventSystem** with an **XRUIInputModule** (auto-created/added by the setup).

---

## 5. Welcome behaviour

When the player comes within **Welcome Radius** (default 3.5 m):

1. The guide turns to face the player.
2. Plays the **Greet** trigger (wave) if a clip exists.
3. Shows the floating label: **"Welcome to Smart Farm VR"**.
4. Plays the optional **Welcome Voice** clip — assign one in the inspector
   (`Welcome Voice`).

It re-arms once the player leaves the larger **Welcome Reset Radius** (default 6 m).

---

## 6. Inspector cheat-sheet (`SmartFarmGuideNPC`)

| Setting | Recommended |
|---------|-------------|
| Walk Speed | 1.2 – 1.8 (default 1.4) |
| Angular Speed | 220 |
| Acceleration | 6 |
| Stopping Distance | 1.5 |
| Welcome Radius | 3.5 |
| Welcome Reset Radius | 6 |
| Speed Parameter | `Speed` |
| Greet Trigger | `Greet` |
| Point Trigger | `Point` |

---

## Troubleshooting

- **Guide doesn't move / "not on a baked NavMesh" warning** — bake the NavMesh
  (section 1) and make sure the guide stands on the blue mesh.
- **Buttons don't respond in VR** — confirm there's exactly one EventSystem with an
  *XRUIInputModule*, and that your controllers have Ray/Poke interactors with UI
  interaction enabled.
- **Guide slides without animating** — the Animator has no clips assigned. Re-run
  *Rebuild Animator Controller* or assign clips manually (section 3).
- **Guide walks through props** — give the props colliders and either bake them into
  the NavMesh or add a *NavMeshObstacle (Carve)*.
- **Guide moonwalks / faces wrong way** — make sure *Apply Root Motion* is OFF and
  the avatar's forward (+Z) faces out of the model.
