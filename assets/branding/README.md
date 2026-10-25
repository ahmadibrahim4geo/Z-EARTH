# Z EARTH — canonical application branding

**Source of truth:** the approved icon **actually used by the Windows EXE** is `src/ZEarth.App/Assets/zearth_icon.ico`, selected by `ApplicationIcon` in `src/ZEarth.App/ZEarth.App.csproj`. Do not replace it with artwork derived from older project mockups.

These repository files must remain **byte-identical to the corresponding EXE build source assets**:
- `assets/branding/zearth_icon.ico` ↔ `src/ZEarth.App/Assets/zearth_icon.ico`
- `assets/branding/zearth_approved_icon.png` ↔ `src/ZEarth.App/Assets/zearth_approved_icon.png`

`zearth_icon.svg` and `zearth_logo.svg` are **legacy editable vector art** and have **not** been certified as visually identical to the EXE icon. Do not use them for the application's icon or new marketing material until an approved vector master matches the binary exactly. A repository unit test checks that the official ICO and PNG copies never diverge from the executable-source files.

The application assembly name, Windows taskbar icon and branding remain Z EARTH. Rebuild a new preview after icon changes, and test Explorer/taskbar appearance at multiple DPI sizes on Windows before publishing a signed release.
