# Apple Harvest Status Labels

Lets players tell, at a glance, whether an apple is ripe **before** they pick it.
There is **no objective HUD / counter** – just a floating label on each apple.

## What it does

- Every apple has a ripeness state: **Ready to Harvest** or **Not Ready Yet**.
- When the player comes within ~2 m, a floating **world-space label** appears above
  the apple and **billboards** to face the player:
  - 🟢 green "Ready to Harvest"
  - 🔴 red "Not Ready Yet"
- Optional coloured **glow** around the apple (green = ready, red = not ready).
- **Ripe apples** can be grabbed with XR hands/controllers and detach from the tree
  (optional harvest sound).
- **Unripe apples** cannot be picked (optional). The grab is rejected and the apple
  stays on the tree. This can be turned off per apple via **Block Unripe Grab**.

## One-click setup

In the Unity Editor:

> **Tools ▸ Smart Farm ▸ Harvest ▸ Setup Apple Harvest System**
> (also under **Tools ▸ Farm ▸ Setup Apple Harvest System**)

This will:

1. Use your **current selection** of apples, otherwise any existing apples in the
   scene (objects with `AppleGrabHandler` or named "apple"). If there are none, it
   spawns a small demo orchard row from the `food_Apple` prefab.
2. Add the required components to each apple: `Rigidbody`, a `Collider`,
   `XRGrabInteractable`, `AppleGrabHandler`, and `AppleHarvest`.
3. Mark the first 5 apples **Ready** and the rest **Not Ready**.

### Adjusting ripeness manually

Select one or more apples, then use:

- **Tools ▸ Smart Farm ▸ Harvest ▸ Mark Selected Apples Ready**
- **Tools ▸ Smart Farm ▸ Harvest ▸ Mark Selected Apples Not Ready**

Or change the **Harvest Status** field on the `AppleHarvest` component in the Inspector.

### Removing the old HUD / counter

If you previously ran setup with the objective panel, remove it with:

> **Tools ▸ Smart Farm ▸ Harvest ▸ Remove Harvest HUD / Counter**

This deletes the `AppleHarvestHUD` object and cleans up any leftover missing-script
components.

## Components

| Script | Role |
| --- | --- |
| `AppleHarvest` | Per-apple status, proximity label, glow, grab gating, audio. |
| `AppleHarvestLabel` | World-space billboard status label (built in code). |
| `AppleGrabHandler` | Existing physical detach; now skips unripe apples. |

## Tuning

On each `AppleHarvest`:

- **Show Distance** – when the label appears (default 2 m).
- **Use Glow / colours / range / intensity** – optional halo.
- **Harvest Sound / Warning Sound** – drop in your own clips.
- **Block Unripe Grab** – prevent picking unripe apples (default on).

## Notes for VR

- Labels are **World Space** Canvases that always face the player.
- Apples start kinematic so they rest on the tree; physics is enabled only when a
  ripe apple is grabbed, so unripe apples never fall.
- Works with both XR hands and controllers via `XRGrabInteractable`.
