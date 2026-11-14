# ARCH3.6 managed Mod SDK proof

Run `Verify.ps1 -EvidenceDirectory <fresh path>` with PowerShell 7, after any exclusive Aether regression runner has exited. It captures the common SDK/TFM/language/runtime-async profile through `BuildProfile.ps1`, records binlogs and hashes, then checks:

- source and published Debug/Release hosts with Cobalt and Microsoft DI;
- a fresh package-only SDK mod (Abstractions + Contracts), private dependency, Tooling publisher and Runtime-only host in an isolated NuGet cache;
- execution from an unrelated working directory and a host containing only dotnet/fxr/shared runtime, without SDK;
- actual scalar component import, typed system ticks (7 then 8), stop/dispose order and weak-reference collection after roots are released, including retained disposed group/session handles.

The Runtime consumer receives no Tooling/Editor/UI. The publisher uses Tooling separately before runtime-only execution. Native code, GPU rendering, live replacement and World migration are outside this managed proof. Failure paths and admission/cleanup retry are checked in `TrueMoon.Aether.Tests/Mods`.

The fixture projects are build assets, not additional test runners. Runtime version and target framework come from the common profile; no independent SDK/TFM pins are introduced.
