# Aether executable examples

These non-packable hosts compose the reusable Aether and Editor modules through
App. Neither package references an executable. Directed smoke/fault scenarios
remain in `ManualTests`; the editor example has no runtime branch, demo game,
smoke implementation or source-checkout lookup.

- [AetherEditor](AetherEditor/README.md): Main/CLI, diagnostics paths, host-selected
  font catalog, `UseAetherEditor` and `UseAetherEditorWindow`.
- [AetherRuntimeHost](AetherRuntimeHost/README.md): independent selected-game/Edit
  Preview runtime, bootstrap validation, App composition and completion diagnostics.
  No Editor, Alloy, Argentis or game project reference.

## Build and publish

Supported example profile: Windows x64, .NET 10, framework-dependent, without RID,
trimming, single-file or NativeAOT. UI uses OpenGL with the existing Vulkan GPU
preview bridge and requires a compatible installed driver/loader. Vulkan validation
is a separate environment prerequisite; normal startup does not prove validation.

From `src` (binlog placeholders are quoted for PowerShell):

```powershell
dotnet build Examples/AetherEditor/AetherEditor.csproj -c Debug '-bl:editor-build-{}.binlog'
dotnet build Examples/AetherRuntimeHost/AetherRuntimeHost.csproj -c Debug '-bl:runtime-build-{}.binlog'
dotnet run --no-build --project Examples/AetherEditor -- --runtime Examples/AetherRuntimeHost/bin/Debug/net10.0/AetherRuntimeHost.exe

# Choose a fresh deployment directory; keep both published closures intact.
$deployment = Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-Aether-' + [Guid]::NewGuid().ToString('N'))
dotnet publish Examples/AetherEditor/AetherEditor.csproj -c Release -o "$deployment/editor" '-bl:editor-publish-{}.binlog'
dotnet publish Examples/AetherRuntimeHost/AetherRuntimeHost.csproj -c Release -o "$deployment/runtime" '-bl:runtime-publish-{}.binlog'
& "$deployment/editor/AetherEditor.exe" --runtime "$deployment/runtime/AetherRuntimeHost.exe"
```

The explicit `--runtime` selects a Windows apphost; its sibling managed DLL is
used for exact counterpart identity. Different entry pairs require rebuilt game
artifacts/checkpoints. Paths are normalized once at composition, never discovered
from the repository. Runtime/game assemblies are not loaded into the editor.
Library consumers can also configure a dotnet/DLL launch through
`NativeRuntimeLaunch`, as described in the [Aether API](../TrueMoon.Aether/PACKAGE.md).

Optional arguments are `--project path/to/project.aetherproj`, `--font path/to/font.ttf`
and `--report path/to/report-directory`. An empty workspace needs no project, SDK,
compiler or game. Defaults load the font beside the executable and put reports in
the current user's LocalApplicationData/TrueMoon/AetherEditor/runs. App owns services
and work; the window owns View products. Normal close applies the existing document
Save/Discard/Cancel policy, and terminal shutdown drains App. Copy the *whole*
published editor/runtime directories to run them from another working directory.

## Resource owners

| Resource | Owner and delivery |
| --- | --- |
| Noto Sans Regular and SIL OFL | `AetherEditor/Assets/Fonts`, copied to build/publish by the example. The host can select another licensed font with `--font`; the Editor package performs no font lookup |
| Editor styles and Alloy themes | Managed library code, compiled into their owning assemblies; no external theme file |
| Vulkan primitive SPIR-V | Embedded in `TrueMoon.Aether.dll`; runtime shader compiler/source files are not needed |
| Extended native Skia | Rendering.Skia delivers `truemoon-native/win-x64/libSkiaSharp.dll` transitively for build/publish; its build provenance stays with that module |
| Silk/GLFW native assets | Platform.Silk and its NuGet dependencies deliver the existing native runtime payloads; keep the complete output |
| Vulkan/OpenGL driver and loader | Installed host platform prerequisites; not copied from ManualTests or bundled by the Editor |
| Application/game content | Explicitly selected host/project/artifact inputs; no demo source lookup |

Independent NuGet consumption and the complete native acceptance matrix remain
ARCH2e. Building/publishing these ProjectReference examples does not establish that
package proof or close the hardware/K7/VRAM/release gates.

## Verify deployment without opening a window

From `src`, with PowerShell 7 and the .NET 10 SDK:

```powershell
pwsh -NoProfile -File ManualTests/EngineSessionProbe/VerifyExampleHosts.ps1
```

The [verifier](../ManualTests/EngineSessionProbe/VerifyExampleHosts.ps1) builds the
source Debug hosts and probe, publishes fresh Release closures into a new temporary
directory outside the source workspace, and runs from an unrelated working directory.
An optional `-RunDirectory <fresh absolute directory>` selects the evidence location.
Each build/publish has a separate binlog; failures preserve logs and deployment files.

It checks dependency closures, host-owned font/OFL and native payload hashes,
managed theme/style owners and exact embedded SPIR-V bytes using PE metadata.
It exercises CLI help/rejection before App/window startup and the independent
headless runtime proof with DLL/apphost launches in both profiles. The summary
records all commands, resource hashes, source-input hashes and process reports.

This verifies resource delivery and headless lifecycle. It does not open the editor,
decode/render the font, load native Skia/GLFW, create a GPU device or exercise the
Open/Transform/preview UI workflow. Those checks and independent NuGet consumption
remain explicit acceptance gates.
