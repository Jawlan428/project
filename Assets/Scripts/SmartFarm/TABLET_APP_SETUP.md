# Smart Farm Tablet App Setup

This guide wires a tablet-style world-space app with tabs:
- Overview
- Irrigation
- Alerts
- Polls
- History

## 1) Scene Objects

1. Create `FarmDataHub` (empty GameObject).
2. Add `FarmDataManager` to `FarmDataHub`.
3. Ensure your existing objects exist:
   - `FarmSimulationHub` with `FarmSimulationManager`
   - `PollVoteManager`
   - `FarmSimulationNetworkSync` (optional/networked)

`FarmDataManager` can auto-find these if left unassigned.

---

## 2) Build Tablet Container

1. Create `SmartFarmTablet` (world-space canvas root).
2. Add:
   - `Canvas` (World Space)
   - `GraphicRaycaster`
   - `TrackedDeviceGraphicRaycaster`
3. Add `TabletAppController`.
4. Add `SimpleUIAnimationHelper`.
5. Add optional `VRHapticsHelper`.

### Header
- Add `AppTitleText`, `ConnectionStatusText`, and status icon image.
- Assign them to `TabletAppController`.

### Pin Mode
- Create/assign:
  - `LeftWristAnchor` (under left hand/controller)
  - `DeskAnchor` (world anchor object)
- Assign `Pin`, `Wrist`, `Desk` buttons + `PinButtonLabel`.

---

## 3) Tabs + Pages

Create page roots under tablet:
- `OverviewPage`
- `IrrigationPage`
- `AlertsPage`
- `PollsPage`
- `HistoryPage`

Add page scripts:
- `OverviewPage` -> `OverviewUI`
- `IrrigationPage` -> `IrrigationUI`
- `AlertsPage` -> `AlertsUI`
- `PollsPage` -> `PollPageUI`
- `HistoryPage` -> `HistoryUI`

Assign each page GameObject + tab button into `TabletAppController`.

---

## 4) Overview Page UI

Create card widgets and assign:
- Soil moisture text + progress image + trend text
- Crop health text + progress image + trend text
- Temperature text + trend text
- Predicted yield text
- Irrigation status text

Assign `FarmDataManager` reference in `OverviewUI`.

---

## 5) Alerts Page UI

Create:
- Bell badge root + badge text
- Scroll content root (`listRoot`)
- Empty state panel (`emptyStateRoot`)
- Alert item prefab with `AlertListItemUI`:
  - Severity text
  - Timestamp text
  - Message text
  - Acknowledge button

Assign these in `AlertsUI`.

---

## 6) Polls Page UI

Main area:
- Question text
- Results text
- Voters A text
- Voters B text
- Open Poll button

Modal:
- Modal root panel
- Question text
- Countdown text
- Vote submitted text
- Option A button
- Option B button
- Close & Apply button

Assign in `PollPageUI`.

Behavior:
- Open Poll -> modal opens + 15s countdown
- Vote -> "Vote submitted"
- Countdown end or Close -> applies result + shows results

---

## 7) Irrigation Page UI

Create and assign:
- Irrigation status text
- Toggle button + label text
- Boost 30s button
- Morning / Noon / Evening preset buttons

Assign in `IrrigationUI`.

Notes:
- Presets are placeholders (logs only)
- Boost performs a temporary moisture boost and auto-restores irrigation state

---

## 8) History Page UI

Create:
- Scroll content root (`listRoot`)
- Empty state panel
- History item prefab with `HistoryListItemUI`:
  - Timestamp text
  - Message text

Assign in `HistoryUI`.

`HistoryUI` reads from `EventLogger.OnEventLogged` via `FarmDataManager`.

---

## 9) XR UI Requirements

In scene EventSystem:
- Keep **XR UI Input Module**
- Remove Standalone input module (if conflicts)

On tablet canvas:
- `TrackedDeviceGraphicRaycaster` present

On XR Ray Interactors:
- `Enable UI Interaction` ON
- Raycast mask includes UI layer

---

## 10) Polish (Hover/Click/Haptics)

Add `TabletUIButtonFeedback` to key buttons and assign:
- target image
- optional click audio source/clip
- optional `VRHapticsHelper`

This gives:
- hover highlight
- click sound
- optional haptic pulse

---

## 11) Recommended Script Placement

`Assets/Scripts/SmartFarm/TabletUI/`
- `FarmDataManager.cs`
- `TabletAppController.cs`
- `OverviewUI.cs`
- `AlertsUI.cs`
- `PollPageUI.cs`
- `IrrigationUI.cs`
- `HistoryUI.cs`
- `SimpleUIAnimationHelper.cs`
- `VRHapticsHelper.cs`

`Assets/Scripts/SmartFarm/TabletUI/Components/`
- `AlertListItemUI.cs`
- `HistoryListItemUI.cs`
- `TabletUIButtonFeedback.cs`

