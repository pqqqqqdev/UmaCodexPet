# Troubleshooting

Use this guide to diagnose common UmaCodexPet installation, picker, preview,
animation, framing, and transparency problems.

[← Back to the main README](../README.md)

## Pressing F8 does nothing

- Make sure UmaViewer is the focused window.
- Wait until UmaViewer has finished populating its lists and can display a Mini
  model.
- Confirm `UmaCodexPet.dll` is under
  `BepInEx\plugins\UmaCodexPet\`, not one extra nested folder down.
- Open `BepInEx\LogOutput.log` and search for `UmaCodexPet`.
- Confirm you installed the Mono x64 build of BepInEx 5, not an IL2CPP build.

## The F6 picker does not appear

- Keep the UmaViewer window focused when pressing <kbd>F6</kbd>.
- Wait for the green `UmaCodexPet ready` message and for UmaViewer's character
  list to finish loading.
- Press <kbd>F6</kbd> again if the picker was already open but hidden behind
  another window.
- Check `BepInEx\LogOutput.log` for `UmaCodexPet picker failed`.
- If you upgraded from UmaPetForge, remove every old `UmaPetForge.dll` under
  `BepInEx\plugins`. BepInEx blocks the renamed plugin while the incompatible
  legacy plugin is still installed, preventing two F6 exporters from running.

## A character is missing or fails to load

- Run **Download All** again after a game update.
- Confirm UmaViewer itself can load that character as a Mini model.
- Check UmaViewer's data path, region, and work mode.
- Try the numeric character ID in `Characters` to avoid a translation mismatch.

## The motion does not fit the state

Open <kbd>F6</kbd>, select the character, and use **Animations/Face** to choose
a different compatible clip for any canonical state. To debug selection
precedence, check the state entry in
`resolved-clips.json` and the candidates in `mini-animation-catalog.json`.
When reporting a bad choice, share those asset **names and IDs only**—never the
asset bundles themselves.

## The face controls do not preview or save

Wait for the automatic matching-Mini load to finish; face controls remain
locked until UmaViewer has created the model and its Mini face materials.
UmaCodexPet uses the character and clothes currently chosen in F6. If the load
fails, use **Retry preview**. If the wrong or stale outfit is visible after a
clothes change, use **Reload selected clothes**. Confirm UmaViewer can load the
same character manually under **Characters → Mini** if retries still fail.
Remember that a custom face is static for the selected state; use **Auto /
default face** to remove the static override.

## The animation still looks like 1–2 FPS

That cadence comes from the Windows Codex desktop renderer's built-in timing,
especially for idle. The app currently ignores a custom pet's requested `fps`
and frame map, so changing exporter metadata cannot make the displayed pet run
at 16 FPS. Other built-in states may appear faster, but their timing is still
host-controlled.

[`codex-animations-v012-repair.json`](../config/codex-animations-v012-repair.json)
is retained only as a legacy Codex TUI/runtime compatibility reference for
hosts that honor custom animation metadata. It is ineffective in the Windows
desktop app and is not a Windows FPS repair.

## The pet looks pixelated

Windows displays each atlas cell as a `192 × 208` raster image. UmaCodexPet
renders at 4× and alpha-correctly downsamples to reduce jagged edges, but it
cannot increase that final resolution. A larger pet-size setting therefore
also enlarges the existing pixels.

## The model is clipped or too small

Version 0.2 and newer calibrate framing over every selected pose, then reject
captures that touch a three-pixel final safety margin. Final frames are rendered
at 4× after calibration.
If a character fails this check, keep the manifests and the `camera calibration` lines from
`BepInEx\LogOutput.log`, then report the affected character and state.

## The background looks black

Some image viewers display transparent pixels as black. Inspect `atlas.png` in
an editor with a checkerboard transparency view before treating it as a capture
failure.
