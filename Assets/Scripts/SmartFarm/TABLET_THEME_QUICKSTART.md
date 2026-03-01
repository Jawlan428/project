# Tablet Theme Quickstart (Automatic)

Your tablet now supports automatic theming via:
- `TabletThemeAutoApplier`
- `TabletThemeProfile` (auto-created at first setup)

## 1) One-click apply

- Run: `Tools > Farm > Apply Tablet Theme (Auto)`

or run:
- `Tools > Farm > Full Platform Setup (Tablet)` (also applies theme automatically).

---

## 2) Add your photos/icons automatically

Place sprites in:

`Assets/Resources/SmartFarmTablet/Sprites/`

Use these exact file names (Sprite type):

- `app_background`
- `header_background`
- `tabbar_background`
- `pinbar_background`
- `modal_background`
- `card_background`
- `button_background`
- `badge_background`
- `icon_overview`
- `icon_irrigation`
- `icon_alerts`
- `icon_polls`
- `icon_history`

Then run:
- `Tools > Farm > Apply Tablet Theme (Auto)`

No manual assignment required.

---

## 3) Import settings (recommended)

For each texture:
- Texture Type: `Sprite (2D and UI)`
- Mip Maps: Off
- sRGB: On
- Filter Mode: Bilinear

For Quest:
- Keep UI textures compact (256-1024 where possible)
- Prefer compressed formats for Android builds

