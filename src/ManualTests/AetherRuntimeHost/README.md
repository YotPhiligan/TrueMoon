# AetherRuntimeHost

Independent `net10.0` executable using only TrueMoon bootstrap and Aether project
references. It validates `RuntimeBootstrap.ReadEnvironment()` before creating an
App, runs a selected game artifact or built-in EditPreview, and writes
`runtime-{generation}.json` after stop/drain. The completion record includes actual
entry/counterpart paths, loaded assembly flags and GPU resource/handle counts.
There is no Editor/UI dependency, legacy fixed-game fallback, smoke mode or fault
implementation in this host.

The parent supplies `TM_ENGINE_BOOTSTRAP` and the Mithril parent/control environment.
Invoke either the apphost with no arguments or an absolute dotnet executable with
`AetherRuntimeHost.dll`. `--runtime` is accepted explicitly but is not required.
Other arguments return exit code 2 before App creation.

After building the test projects separately, the independent executable proof is:

```powershell
dotnet path/to/EngineSessionProbe.dll --runtime-launch-proof E:/runtime/AetherRuntimeHost.dll E:/reports/fresh-runtime-proof
```

When passing a DLL, the parent must itself run through dotnet; the proof uses its
absolute `Environment.ProcessPath`, never a PATH-resolved runtime launcher.
Passing `AetherRuntimeHost.exe` selects apphost launch instead. The report directory
must be new. `runtime-launch-report.json` and failure artifacts are retained.

The proof publishes the already-built `Game.Logic.dll` from the parent output as a
selected artifact; it does not run the SDK/compiler. It exercises two preview
processes, a selected-game process, parent/child identity rejection, startup
cancellation, immediate exit and explicit same-executable preview regression.
No frame surface, window or GPU is requested. This is not GPU/presentation,
compiler/Replace, parent-exit or independent NuGet acceptance.
