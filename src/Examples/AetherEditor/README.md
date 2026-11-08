# AetherEditor

Minimal Windows x64/net10.0 executable composing `UseAetherEditor` and
`UseAetherEditorWindow` through App/Cobalt. The host owns CLI, deployment paths
and the explicit font catalog. It has no runtime branch, demo game or smoke modes.

```text
AetherEditor --runtime path/to/AetherRuntimeHost.exe [--project project.aetherproj] [--font font.ttf] [--report directory]
```

`--help` prints usage without creating an App/window. The runtime apphost and its
sibling DLL must be deployed together. Default typography uses installed Segoe UI
Variable Text, then Segoe UI. The final Noto Sans fallback and its SIL OFL are in
[Assets/Fonts](Assets/Fonts); both are copied beside the executable on build and
publish. `--font` selects a host-supplied licensed file-only catalog instead of
system matching.

See the [example guide](../README.md) for build, publish, resource owners and the
headless deployment verifier. GPU/window acceptance remains separate from that
verifier. Directed editor smoke workflows stay in
[AetherEditorHost](../../ManualTests/AetherEditorHost/README.md).

Edit Preview supports RMB orbit, MMB pan, wheel dolly, F on the focused canvas to
frame a selected cube, and LMB selection/background clear. Camera movement preserves
document/history and inspector drafts; Esc/focus loss cancels a navigation gesture.
Selection uses the same tree/inspector owner. This applies to the bounded opaque-cube
Edit Preview profile; gameplay input and gizmos remain separate features.