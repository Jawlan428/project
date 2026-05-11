# VR Smart Farm — Meeting Room Setup

This module turns the round table + chairs area into a fully interactive VR
collaboration space.

## What you get

| Script | Purpose |
|--------|---------|
| `SmartFarmReportData` | ScriptableObject describing one farming document (title, charts, recommendations). |
| `SmartFarmReportManager` | Tick-based feeder that pulls live data from `FarmSimulationManager`, `WeatherManager`, `SmartIrrigationManager` and pushes it into the reports. |
| `VRDocumentInteractable` | Per-document component. Builds the world-space TMP canvas, configures `XRGrabInteractable`, returns the page to its rest pose, refreshes UI on live updates. |
| `DocumentReaderSystem` | Watches held documents. When the user holds one close to their head, the page enlarges (`readingZoom`) and gently faces the camera. |
| `ChairSitSystem` | Per-chair sit / stand component. Builds a "Press to Sit" prompt, blends the XR rig to the sit anchor, releases on the next trigger. |
| `MeetingInteractionManager` | Master orchestrator. Creates the sub-systems if absent and rescans the meeting root for chairs and documents. |
| `MeetingAmbience` | Optional looping ambience + random "discussion murmur" emitter. |
| `Editor/MeetingRoomSetupWindow` | One-click scene scaffold (table + chairs + 6 documents + manager). |

## One-click setup

1. Open Unity → menu **Tools → Smart Farm → Setup Meeting Room…**
2. The window auto-fills with your `Tables and Chairs/Prefabs/Table4.prefab` and
   `Chair2.prefab`. Adjust the chair count, table radius and document count as
   you like.
3. Press **Build Meeting Area in Scene** — a new `VR Smart Farm Meeting Area`
   root is created in front of the scene-view camera. It contains:
   - `MeetingTable`
   - `Chairs/Chair_1..N` (each with `ChairSitSystem`)
   - `Documents/Doc_*` (each with `VRDocumentInteractable` and a report assigned)
   - `Decorations` (empty — drop coffee cups, tablets, etc. here)
   - `MeetingInteractionManager` GameObject with `SmartFarmReportManager`,
     `DocumentReaderSystem` and `MeetingAmbience` attached.
4. The default report assets are created under
   `Assets/SmartFarm/MeetingRoom/Reports/`. Re-run the menu item any time to
   regenerate missing ones.

## Hooking up live data

`SmartFarmReportManager` auto-finds the existing managers in the scene at
`Start()`. If you have multiple instances or use prefab variants, drag them
into its inspector slots:

- **Simulation Manager** → `FarmSimulationManager`
- **Weather Manager** → `WeatherManager`
- **Irrigation Manager** → `SmartIrrigationManager`

Reports refresh on a fixed tick (default 1 s) and immediately when a user picks
up a document.

## XR rig integration

`ChairSitSystem.SitDown()` moves the **rig root** so the head camera ends up
directly above the sit anchor. If your project uses the XR Origin from the XR
Interaction Toolkit, leave the **Player Rig** field empty — the component will
auto-find `XR Origin (XR Rig)`, `OVRCameraRig`, or fall back to the camera's
ancestor.

The chair uses `XRSimpleInteractable` so any standard XR ray or direct
interactor will trigger sit/stand on `Select` or `Activate`.

## Documents

Each `VRDocumentInteractable`:

- Auto-adds a `BoxCollider` sized to `pageWidth × pageHeight` (default
  0.28 × 0.38 m).
- Sets the `Rigidbody` mass to 0.08 kg and increases drag so the page feels
  like paper, not a brick.
- Builds a 512 × 720 px world-space canvas above the GameObject's local +Y axis,
  rotated so the page lies flat on the table.
- Subscribes to `SmartFarmReportManager.OnReportUpdated` and re-paints the
  bars, title, body and recommendations whenever the data changes.

You can replace the auto-built canvas with your own custom UI: just delete the
runtime `ReportCanvas` child after `Awake()` and bind your TMP fields by
listening to `SmartFarmReportManager.Instance.OnReportUpdated`.

## Reading mode

`DocumentReaderSystem.readingDistance` (default 0.45 m) controls when the page
enlarges. Pulling the document closer than that triggers a smooth zoom up to
`SmartFarmReportData.readingZoom` (default 1.35). The page also rotates to face
your head so reading stays comfortable.

## Performance notes (Quest)

- Every sub-system is **tick-based** (≥ 0.25 s) — no per-frame allocations.
- The world-space canvas uses one mesh per text element + lightweight images;
  no `ContentSizeFitter` or layout groups that re-layout each frame.
- Colliders are simple box / sphere; the chair adds a single trigger box and
  re-uses the existing mesh collider for visuals.
- Document highlight uses `MaterialPropertyBlock` so it does not create
  per-instance material copies.

## Adding decoration props

Drop your existing meshes (coffee cup, pen, tablet, blueprint roll) under the
`Decorations` GameObject. Items you want grabbable can simply have
`VRDocumentInteractable` swapped for `XRGrabInteractable` + `Rigidbody` +
`Collider` — they don't need to be documents.

## Custom reports

Right-click in the Project window → **Create → SmartFarm → Meeting Room →
Smart Farm Report**. Set `reportType = Custom` to keep the manager from
overwriting your authored values.
