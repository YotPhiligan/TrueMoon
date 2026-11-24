# TrueMoon.Aether.Player

Minimal standalone local Player App/window integration. Uses `TrueMoon.Aether.Runtime` plus
Alloy/Argentis/Skia; it has no dependency on `TrueMoon.Aether.Editor`. Windows x64
Vulkan/OpenGL external-memory profile, matching GPU UUID and explicit game artifact.

Capture a `Runtime.PlayerStart` before App creation, then call
`context.UseAetherPlayer(start, fonts, options => ...)`. Window/input/view/factories
come from DI. App startup loads one local game, shows its first actual current frame,
then admits Playing. No owned child, parent environment, pipe or source/SDK lookup.
Click the viewport to capture WASD/E and bounded mouse look; Escape/focus loss clears
input. Stop/close drains GPU use before World/game/context teardown; failed cleanup
retains ownership for retry. A game implementing `IGameHudSource` can supply
a bounded text prompt, status and journal for the active scene. The HUD is drawn
over the existing Player viewport and shares its scale without receiving input.

`UseConfiguredAetherPlayer` uses the exact `PlayerOptions.Schema` in an explicit
common configuration profile. Artifact/scene identity and renderer callbacks stay
explicit. Resource limits retain the shared Aether render profile.

For repeatable scenarios, enable `PlayerOptions.ManualTicks` on the windowed
Player or register `UseAetherHeadlessPlayer(start)`. Both profiles provide
`IPlayerScenarioDriver` from `TrueMoon.Aether.Player.Scenarios`. After the App
starts, advance one tick with `AdvanceScenarioAsync` and request a game
observation. The headless driver steps the paused session and reports
`SupportsFrameCapture == false` with a null frame. The App owns the game
session and releases it on stop.

Local runtime types now reside in the separate Runtime assembly; Player has no
workflow Aether dependency. Expanded
combined-exe/replace/crash/release matrix is ENG7b.3.
