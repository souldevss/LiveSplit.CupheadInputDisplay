# LiveSplit.CupheadInputDisplay

Native Cuphead input-display component for LiveSplit. **One DLL**, embedded artwork, and standard Windows Forms settings. Based on [souldevss/CupheadInputDisplay](https://github.com/souldevss/CupheadInputDisplay).

## Install

1. Close LiveSplit.
2. Download [LiveSplit.CupheadInputDisplay.dll](dist/LiveSplit.CupheadInputDisplay.dll) using GitHub's download button.
3. Place the DLL in LiveSplit's `Components` directory, then reopen LiveSplit.
4. Choose **Edit Layout → + → Information → Cuphead Input Display**.
5. Configure it in **Layout Settings → Cuphead Input Display**.

The existing LiveSplit installation must provide `LiveSplit.Cuphead.dll`, `LiveSplit.Core.dll`, `UpdateManager.dll`, `SharpDX.dll` and `SharpDX.DirectInput.dll`. WebView2 is not used. No asset directory is installed.

## Features

- Always-on character and plane detection, including Ms. Chalice's active-player flag.
- Reads Cuphead's resolved in-game keyboard/controller action states automatically.
- Keyboard, XInput and DirectInput fallback bindings when game-state reading is unavailable.
- Native Windows Forms settings; no Config tab or separate settings window.
- Fully opaque solid background color with a green-screen preset. No gradients or transparent background option.
- Dark inactive / bright pressed appearance by default, with inversion.
- Independent inactive/pressed colors, brightness, opacity and 0–100% color strength.
- Original, multiply, colorize and solid coloring modes; per-element color overrides.
- Colored input backgrounds, sprite-outline or box borders, configurable thickness and colored soft drop shadows.
- Draggable artwork layout with per-element size, visibility and numeric offsets.
- Live-input previews by default, with isolated inactive/pressed simulations using the same renderer.
- Settings save with the LiveSplit layout.

## Build

On Windows, run:

```powershell
.\build.ps1 -LiveSplitRoot 'C:\path\to\LiveSplit'
```

The script uses the Windows .NET Framework C# compiler and your existing LiveSplit assemblies. No .NET SDK or NuGet packages are needed. Output: `dist/LiveSplit.CupheadInputDisplay.dll`.

## Tests

```powershell
.\tests\run.ps1 -LiveSplitRoot 'C:\path\to\LiveSplit'
```

The native integration tests and rendering checks run locally on Windows. They create their own off-screen test window and write reports/previews under `artifacts/`.

Current version: **1.2.1**. The prepared build passed **105 checks**, including pixel-for-pixel preview/display comparisons in live, inactive and pressed modes. Live reading was also checked against a running Cuphead game reporting Cuphead / Plane with neutral input states. Physical controller presses and Ms. Chalice were not observed in that live check.

## Performance

One shared PNG is used for both input states; there are 30 embedded sprites and no separate pressed-image copies. Effect masks and display-size rasterizations are cached. The display avoids redundant invalidations and skips fallback device polling while in-game input is available, except during fallback binding configuration.

A local 300-frame test measured approximately **0.34 ms per cached display render** with borders and shadows enabled. Cold effect generation took about **288 ms**. These measurements are machine- and settings-dependent and do not measure the entire LiveSplit application.

## References and credits

See [THIRD_PARTY.md](THIRD_PARTY.md) for artwork and reader references. Game memory is accessed read-only; the component does not inject code, execute game methods, change bindings, advance splits or alter game time.
