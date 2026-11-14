# Runtime + Editor UI independent consumer

`EditorUiConsumer.csproj` references TrueMoon, Editor.UI and the Microsoft DI adapter;
it has no Tooling/Editor.Tooling reference. The same executable implements an explicit
protected child-runtime role. The host does not load game code or invoke a compiler.

Run `Verify.ps1 -ValidationDirectory <existing-layer-directory> -EvidenceDirectory <fresh-directory>`
with PowerShell7 on Windows x64. BuildProfile.ps1 supplies SDK/TFM/language/features
and independent CPM isolation. The runner builds/publishes a mesh game and separate
artifact publisher, then executes source/published/fresh-package/SDK-free profiles
with Cobalt and Microsoft DI. All MSBuild commands keep unique binlogs.

Each native profile checks lazy startup, disabled build/replace, retained selection
and captured read-only property values, actual mesh pixels/render-view acknowledgement,
Start/Step/Play/Pause/Stop, child reap, zero logical owners and Vulkan validation.
Fresh consumers restore exclusively from a local feed/cache and verify deployment bytes.
SDK-free execution uses a copied runtime-only dotnet root; the publisher runs separately
before execution. Completed verification.json contains exact inputs, package hashes,
graphs, child diagnostics and per-profile reports. Do not read active reports with an
exclusive file handle. This is a bounded Windows native proof, not release/soak coverage.
