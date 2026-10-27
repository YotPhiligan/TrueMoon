# AetherEditor

Minimal Windows x64/net10.0 executable composing `UseAetherEditor` and
`UseAetherEditorWindow` through App/Cobalt. The host owns CLI, deployment paths
and the explicit font catalog. It has no runtime branch, demo game or smoke modes.

```text
AetherEditor --runtime path/to/AetherRuntimeHost.exe [--project project.aetherproj] [--font font.ttf] [--report directory]
```

`--help` prints usage without creating an App/window. The runtime apphost and its
sibling DLL must be deployed together. The default Noto Sans font and its SIL OFL
are in [Assets/Fonts](Assets/Fonts); both are copied beside the executable on build
and publish. `--font` selects a host-supplied licensed font instead.

See the [example guide](../README.md) for build, publish, resource owners and the
headless deployment verifier. GPU/window acceptance remains separate from that
verifier. Directed editor smoke workflows stay in
[AetherEditorHost](../../ManualTests/AetherEditorHost/README.md).
