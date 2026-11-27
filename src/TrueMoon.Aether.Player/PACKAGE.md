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

The default `PlayerView` is created by the generated factory. Its single
`PlayerCanvas` is built when the UI session first attaches the view, not in the
constructor. Use `PlayerView(PlayerViewModel, PlayerInput)` for explicit
construction and `ViewCreation.Prepare(view, style)` for a custom view factory.
Access `view.Canvas` only after content preparation. To customize the default
path, register `IViewStyle<PlayerView>` or `IFactory<PlayerView>` before
`UseAetherPlayer`. To customize the canvas, register `IFactory<PlayerCanvas>`;
Hosting adds this key for Player sessions without replacing or mutating a custom
`IUiControlFactories`. The window uses a typed viewport port that is active only
while the canvas is attached. It acknowledges a drawn frame only after the
window confirms presentation.

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

Use `PlayerScenarioRunner.RunAsync` in a host executable to run the shared
version 1 JSON scenario and write `report.json` to a fresh directory. Pass a
font catalog factory for windowed captures or `null` for headless execution.
Publish the Build for the exact managed entry assembly and pass cancellation
from the host. The runner drains the App before publishing the report.

Local runtime types now reside in the separate Runtime assembly; Player has no
workflow Aether dependency. Expanded
combined-exe/replace/crash/release matrix is ENG7b.3.
