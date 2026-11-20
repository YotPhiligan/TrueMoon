#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [Parameter(Mandatory)][string]$ValidationDirectory,
    [switch]$FreshPackages,
    [switch]$ModelImports,
    [switch]$ModelReimport,
    [switch]$ModelSelectionPersistence,
    [switch]$ModelCatalog,
    [switch]$ModelPlacement,
    [switch]$RenderInspector,
    [switch]$RenderInspectorRestart,
    [switch]$RenderMaterialReset,
    [switch]$RenderReferenceDiagnostics,
    [switch]$RenderMaterialSlots,
    [switch]$InitialModelImport,
    [switch]$RenderMaterialSlotsRestart,
    [switch]$ImportedTextureRestart,
    [switch]$ImportedTextureReimport,
    [switch]$ImportedTextureReplace,
    [switch]$PlayingTextureWorkflow
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory, $workspace)
$validation = [IO.Path]::GetFullPath($ValidationDirectory, $workspace)
foreach ($file in @('VkLayer_khronos_validation.json', 'VkLayer_khronos_validation.dll')) {
    if (!(Test-Path -LiteralPath "$validation/$file" -PathType Leaf)) { throw "Missing validation layer file $file" }
}
if (Test-Path -LiteralPath $evidence) { throw 'Use a fresh evidence directory.' }
[void][IO.Directory]::CreateDirectory($evidence)
$catalogProbe = $ModelCatalog -or $ModelPlacement
$persistenceProbe = $ModelSelectionPersistence -or $catalogProbe
$reimportProbe = $ModelReimport -or $persistenceProbe
$textureReplaceProbe = $ImportedTextureReplace -or $PlayingTextureWorkflow
$textureReimportProbe = $ImportedTextureReimport -or $textureReplaceProbe
$initialImportProbe = $InitialModelImport -or $RenderMaterialSlotsRestart -or $ImportedTextureRestart -or $textureReimportProbe
$renderProbe = $RenderInspector -or $RenderInspectorRestart -or $RenderMaterialReset -or $RenderReferenceDiagnostics -or $RenderMaterialSlots -or $initialImportProbe
$modelStatusProbe = $ModelImports -or $reimportProbe -or $renderProbe
$profile = Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory $evidence
$dotnet = (Get-Command dotnet).Source
$report = [ordered]@{
    Completed = $false
    Stage = if ($PlayingTextureWorkflow) { 'ARCH1f.5-editor-playing-texture-workflow' } elseif ($ImportedTextureReplace) { 'ARCH1f.5-editor-texture-replace' } elseif ($ImportedTextureReimport) { 'ARCH1f.5-editor-texture-reimport' } elseif ($ImportedTextureRestart) { 'ARCH1f.5-editor-texture-restart' } elseif ($RenderMaterialSlotsRestart) { 'ED4i-material-slots-restart' } elseif ($InitialModelImport) { 'ARCH1g.2p' } elseif ($RenderMaterialSlots) { 'ED4i-material-slots' } elseif ($RenderReferenceDiagnostics) { 'ED4i-reference-diagnostics' } elseif ($RenderMaterialReset) { 'ED4i-material-reset' } elseif ($RenderInspectorRestart) { 'ED4i-restart' } elseif ($RenderInspector) { 'ED4i' } elseif ($ModelPlacement) { 'ARCH1g.2o' } elseif ($ModelCatalog) { 'ARCH1g.2n' } elseif ($ModelSelectionPersistence) { 'ARCH1g.2m' } elseif ($ModelReimport) { 'ARCH1g.2l' } elseif ($ModelImports) { 'ARCH1g.2k' } elseif ($FreshPackages) { 'ARCH1g.2f' } else { 'ARCH1g.2e' }
    Profile = $profile
    Runs = @()
    Commands = @()
}

function Invoke-Step([string]$name, [string[]]$arguments) {
    if ($arguments[0] -in @('build', 'publish', 'restore', 'pack')) { $arguments += "-bl:$evidence/$name-{}.binlog" }
    & $dotnet @arguments *> "$evidence/$name.log"
    $code = $LASTEXITCODE
    $report.Commands += @{ Name = $name; Arguments = $arguments; ExitCode = $code }
    if ($code -ne 0) {
        Get-Content -LiteralPath "$evidence/$name.log" -Tail 15
        throw "$name exited with code $code"
    }
    Write-Host "$name passed"
}

function Test-EditorConsumers([string]$name, [string]$directory, [string]$authoring, [string]$editor) {
    foreach ($provider in @('cobalt', 'microsoft')) {
        $output = "$directory/$provider"
        $mode = if ($PlayingTextureWorkflow) { '--editor-playing-texture-workflow' } elseif ($ImportedTextureReplace) { '--editor-imported-texture-replace' } elseif ($ImportedTextureReimport) { '--editor-imported-texture-reimport' } elseif ($ImportedTextureRestart) { '--editor-imported-texture-restart' } elseif ($RenderMaterialSlotsRestart) { '--editor-render-material-slots-restart' } elseif ($InitialModelImport) { '--editor-initial-model-import' } elseif ($RenderMaterialSlots) { '--editor-render-material-slots' } elseif ($RenderReferenceDiagnostics) { '--editor-render-reference-diagnostics' } elseif ($RenderMaterialReset) { '--editor-render-material-reset' } elseif ($RenderInspectorRestart) { '--editor-render-inspector-restart' } elseif ($RenderInspector) { '--editor-render-inspector' } elseif ($ModelPlacement) { '--editor-model-placement' } elseif ($ModelCatalog) { '--editor-model-catalog' } elseif ($ModelSelectionPersistence) { '--editor-persisted-imports' } elseif ($ModelReimport) { '--editor-reimport' } elseif ($ModelImports) { '--editor-imports' } else { '--editor' }
        # The frozen compiler mirrors ancestors; keep its fixture outside the checkout at a short path.
        $fixture = if ($modelStatusProbe) { Join-Path ([IO.Path]::GetTempPath()) ('tme-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)) } else { $output }
        Invoke-Step "$name-$provider" @($authoring, $provider, $fixture, $worker, $editor,
            $dotnet, $profile.TestFramework, $profile.Sdk, $mode)
        if ($renderProbe) {
            [void][IO.Directory]::CreateDirectory($output)
            $run = @{ Name = "$name-$provider"; Fixture = $fixture }
            if ($textureReimportProbe) {
                $childDirectory = "$fixture/import-reimported"
                $path = "$output/editor-texture-reimport.json"
                Copy-Item -LiteralPath "$childDirectory/editor-texture-reimport.json" -Destination $path
                $proof = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
                $before = $proof.Before
                $after = $proof.After
                $children = @(Get-ChildItem -LiteralPath $childDirectory -Filter 'runtime-*.json' -File)
                if (!$proof.Completed -or ((!$textureReplaceProbe -or $PlayingTextureWorkflow) -and !$proof.BuildBlocked) -or $proof.Statuses.Count -ne 3 -or
                    !$proof.Statuses[0].EndsWith(' · Актуальна') -or !$proof.Statuses[1].EndsWith(' · Импорт устарел') -or
                    $proof.Statuses[0] -ne $proof.Statuses[2] -or
                    $proof.ChangedDependency -ne 'textures/color.png' -or $children.Count -ne 2 -or
                    $before.EditorPid -ne $after.EditorPid -or $before.OpeningId -ne $after.OpeningId -or
                    $before.Generation + 1 -ne $after.Generation -or $before.Texture.AssetId -ne $after.Texture.AssetId -or
                    $before.Texture.Revision -eq $after.Texture.Revision -or $before.Frame.Pixels.Sha256 -eq $after.Frame.Pixels.Sha256 -or
                    $before.Frame.Pixels.Texture.TopLeft.Rgba -ne 'D51006FF' -or $after.Frame.Pixels.Texture.TopLeft.Rgba -ne '2194D5FF' -or
                    $before.Frame.Pixels.Slots.RightRgba -ne $after.Frame.Pixels.Slots.RightRgba) {
                    throw "Incomplete PNG reimport, stable texture identity or GPU evidence for $name-$provider"
                }
                $run.Path = $path
                $run.Sha256 = (Get-FileHash -LiteralPath $path).Hash
                $images = @("$childDirectory/initial-model.png", "$childDirectory/texture-reimported.png")
                if ($textureReplaceProbe) {
                    $retained = $proof.RetainedBeforeReplace
                    if (!$retained -or $retained.Pid -ne $before.Frame.Pid -or $retained.Generation -ne $before.Frame.Generation -or
                        $retained.BuildId -ne $before.Frame.BuildId -or $retained.ManifestHash -ne $before.Frame.ManifestHash -or
                        $retained.Pixels.Sha256 -ne $before.Frame.Pixels.Sha256 -or $retained.Tick -le $before.Frame.Tick -or
                        $after.Frame.Tick -lt $retained.Tick -or $after.Frame.BuildId -eq $retained.BuildId -or
                        $after.Frame.Generation -ne ($retained.Generation + 1) -or $after.Frame.Pid -eq $retained.Pid) {
                        throw "Incomplete playing texture retention or hot Replace evidence for $name-$provider"
                    }
                    $images += "$childDirectory/texture-before-replace.png"
                    if ($PlayingTextureWorkflow) {
                        $rejected = $proof.AfterBlockedBuild
                        if (!$rejected -or $rejected.Pid -ne $retained.Pid -or $rejected.Generation -ne $retained.Generation -or
                            $rejected.BuildId -ne $retained.BuildId -or $rejected.Pixels.Sha256 -ne $retained.Pixels.Sha256 -or
                            $rejected.Tick -gt $retained.Tick) {
                            throw "The rejected Build did not retain the original playing Runtime for $name-$provider"
                        }
                        $images += "$childDirectory/texture-after-blocked-build.png"
                    }
                }
            } elseif ($initialImportProbe) {
                $savedPath = "$output/import-saved.json"
                $reopenedPath = "$output/import-reopened.json"
                Copy-Item -LiteralPath "$fixture/import-saved/editor-initial-model-import.json" -Destination $savedPath
                Copy-Item -LiteralPath "$fixture/import-reopened/editor-initial-model-import.json" -Destination $reopenedPath
                $saved = Get-Content -LiteralPath $savedPath -Raw | ConvertFrom-Json
                $reopened = Get-Content -LiteralPath $reopenedPath -Raw | ConvertFrom-Json
                $savedChildren = @(Get-ChildItem -LiteralPath "$fixture/import-saved" -Filter 'runtime-*.json' -File)
                $reopenedChildren = @(Get-ChildItem -LiteralPath "$fixture/import-reopened" -Filter 'runtime-*.json' -File)
                if (!$saved.Completed -or !$reopened.Completed -or !$saved.RegistrationRetry -or $savedChildren.Count -ne 1 -or $reopenedChildren.Count -ne 1 -or
                    $saved.EditorPid -eq $reopened.EditorPid -or $saved.SourceId -ne $reopened.SourceId -or $saved.CatalogSha256 -ne $reopened.CatalogSha256 -or
                    $saved.LedgerSha256 -ne $reopened.LedgerSha256 -or $saved.Slots.Count -ne 2 -or $reopened.Slots.Count -ne 2 -or
                    $saved.Slots[0].Id -ne $reopened.Slots[0].Id -or $saved.Slots[1].Id -ne $reopened.Slots[1].Id -or
                    !$saved.Frame.Pixels -or !$reopened.Frame.Pixels -or $saved.Frame.Pixels.Sha256 -ne $reopened.Frame.Pixels.Sha256) {
                    throw "Incomplete initial import, exact slot restoration or GPU evidence for $name-$provider"
                }
                if ($ImportedTextureRestart) {
                    if (!$saved.Texture -or !$reopened.Texture -or
                        $saved.Texture.AssetId -ne $reopened.Texture.AssetId -or $saved.Texture.Revision -ne $reopened.Texture.Revision -or
                        !$saved.Frame.Pixels.Texture -or !$reopened.Frame.Pixels.Texture) {
                        throw "Incomplete texture identity or GPU evidence for $name-$provider"
                    }
                    $run.Texture = $saved.Texture
                } elseif ($RenderMaterialSlotsRestart -and (!$saved.OverrideVerified -or !$reopened.OverrideVerified -or
                    $saved.Binding.AssetId -ne $reopened.Binding.AssetId -or $saved.Binding.Revision -ne $reopened.Binding.Revision -or
                    $saved.SelectionSha256 -ne $reopened.SelectionSha256 -or
                    $saved.Frame.Pixels.Slots.WhitePixels -le 100 -or $saved.Frame.Mask.GreenPixels -le 100 -or $saved.Frame.Mask.RedPixels -ne 0)) {
                    throw "Incomplete saved material override, inspector restoration or edited GPU evidence for $name-$provider"
                }
                $children = $savedChildren + $reopenedChildren
                $run.Saved = @{ Path = $savedPath; Sha256 = (Get-FileHash -LiteralPath $savedPath).Hash }
                $run.Reopened = @{ Path = $reopenedPath; Sha256 = (Get-FileHash -LiteralPath $reopenedPath).Hash }
                $images = @("$fixture/import-saved/initial-model.png", "$fixture/import-reopened/initial-model.png")
            } elseif ($RenderMaterialSlots) {
                $path = "$output/editor-render-material-slots.json"
                $importPath = "$output/import-render-material-slots.json"
                Copy-Item -LiteralPath "$fixture/editor-render-material-slots.json" -Destination $path
                Copy-Item -LiteralPath "$fixture/import-render-material-slots.json" -Destination $importPath
                $proof = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
                $import = Get-Content -LiteralPath $importPath -Raw | ConvertFrom-Json
                $children = @(Get-ChildItem -LiteralPath $fixture -Filter 'runtime-*.json' -File)
                if (!$proof.Completed -or !$import.Completed -or $proof.Captures.Count -ne 2 -or $children.Count -ne 2 -or
                    $import.Slots.Count -ne 2 -or $import.Slots[0].Id -ne $proof.EditedSlot -or $import.Slots[1].Id -ne $proof.PreservedSlot -or
                    $import.Mesh -ne $proof.Mesh.AssetId -or $import.OldGeneration -ne 1 -or $import.NewGeneration -ne 2 -or
                    $proof.Captures[0].Pixels.Slots.RedMask -ne $proof.Captures[1].Pixels.Slots.WhiteMask -or
                    $proof.Captures[0].Pixels.Slots.RightRgba -ne $proof.Captures[1].Pixels.Slots.RightRgba) {
                    throw "Incomplete persistent slot or GPU evidence for $name-$provider"
                }
                $run.Path = $path
                $run.Sha256 = (Get-FileHash -LiteralPath $path).Hash
                $run.Import = @{ Path = $importPath; Sha256 = (Get-FileHash -LiteralPath $importPath).Hash }
                $images = @($proof.Captures.Name | ForEach-Object { "$fixture/$_.png" })
            } elseif ($RenderReferenceDiagnostics) {
                $path = "$output/editor-render-reference-diagnostics.json"
                Copy-Item -LiteralPath "$fixture/editor-render-reference-diagnostics.json" -Destination $path
                $proof = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
                $children = @(Get-ChildItem -LiteralPath $fixture -Filter 'runtime-*.json' -File)
                if (!$proof.Completed -or $proof.Captures.Count -ne 2 -or $children.Count -ne 2 -or
                    !$proof.MissingMesh.Contains($proof.Mesh) -or !$proof.MissingBinding.Contains($proof.Binding) -or
                    $proof.Captures[0].Pixels.Sha256 -ne $proof.Captures[1].Pixels.Sha256) {
                    throw "Incomplete missing mesh, binding or GPU recovery evidence for $name-$provider"
                }
                $run.Path = $path
                $run.Sha256 = (Get-FileHash -LiteralPath $path).Hash
                $images = @($proof.Captures.Name | ForEach-Object { "$fixture/$_.png" })
            } elseif ($RenderMaterialReset) {
                $path = "$output/editor-render-material-reset.json"
                Copy-Item -LiteralPath "$fixture/editor-render-material-reset.json" -Destination $path
                $proof = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
                $children = @(Get-ChildItem -LiteralPath $fixture -Filter 'runtime-*.json' -File)
                if (!$proof.Completed -or $proof.States.Count -ne 4 -or $proof.Captures.Count -ne 5 -or $children.Count -ne 5 -or
                    @($proof.States.Mesh.Revision | Select-Object -Unique).Count -ne 1 -or
                    @($proof.States.Frame.Pixels.BlueSha256 | Select-Object -Unique).Count -ne 1 -or
                    $proof.States[0].Frame.Pixels.Sha256 -eq $proof.States[1].Frame.Pixels.Sha256 -or
                    $proof.States[0].Frame.Pixels.Sha256 -ne $proof.States[2].Frame.Pixels.Sha256 -or
                    $proof.States[1].Frame.Pixels.Sha256 -ne $proof.States[3].Frame.Pixels.Sha256) {
                    throw "Incomplete material reset, Undo or Redo evidence for $name-$provider"
                }
                $run.Path = $path
                $run.Sha256 = (Get-FileHash -LiteralPath $path).Hash
                $images = @($proof.Captures.Name | ForEach-Object { "$fixture/$_.png" })
            } elseif ($RenderInspectorRestart) {
                $savedPath = "$output/inspector-saved.json"
                $reopenedPath = "$output/inspector-reopened.json"
                Copy-Item -LiteralPath "$fixture/editor-saved/inspector-restart.json" -Destination $savedPath
                Copy-Item -LiteralPath "$fixture/editor-reopened/inspector-restart.json" -Destination $reopenedPath
                $saved = Get-Content -LiteralPath $savedPath -Raw | ConvertFrom-Json
                $reopened = Get-Content -LiteralPath $reopenedPath -Raw | ConvertFrom-Json
                $savedChildren = @(Get-ChildItem -LiteralPath "$fixture/editor-saved" -Filter 'runtime-*.json' -File)
                $reopenedChildren = @(Get-ChildItem -LiteralPath "$fixture/editor-reopened" -Filter 'runtime-*.json' -File)
                if ($savedChildren.Count -ne 2 -or $reopenedChildren.Count -ne 1 -or $saved.EditorPid -eq $reopened.EditorPid -or
                    $saved.OpeningId -eq $reopened.OpeningId -or $saved.Binding.AssetId -ne $reopened.Binding.AssetId -or
                    $saved.SceneSha256 -ne $reopened.SceneSha256 -or $saved.SelectionSha256 -ne $reopened.SelectionSha256 -or
                    !$saved.Frame.Pixels -or !$reopened.Frame.Pixels -or $saved.Frame.Pixels.Sha256 -ne $reopened.Frame.Pixels.Sha256) {
                    throw "Incomplete MeshRenderer restart evidence for $name-$provider"
                }
                $children = $savedChildren + $reopenedChildren
                $run.Saved = @{ Path = $savedPath; Sha256 = (Get-FileHash -LiteralPath $savedPath).Hash }
                $run.Reopened = @{ Path = $reopenedPath; Sha256 = (Get-FileHash -LiteralPath $reopenedPath).Hash }
                $images = @("$fixture/editor-saved/inspector-before.png", "$fixture/editor-saved/inspector-saved.png",
                    "$fixture/editor-reopened/inspector-reopened.png")
            } else {
                $path = "$output/editor-render-inspector.json"
                Copy-Item -LiteralPath "$fixture/editor-render-inspector.json" -Destination $path
                $proof = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
                $children = @(Get-ChildItem -LiteralPath $fixture -Filter 'runtime-*.json' -File)
                if (!$proof.Completed -or $proof.Captures.Count -ne 3 -or $children.Count -ne 3 -or
                    $proof.Captures[0].BuildId -eq $proof.Captures[1].BuildId -or
                    $proof.Captures[0].Mask.Sha256 -eq $proof.Captures[1].Mask.Sha256 -or
                    $proof.Captures[0].Mask.Sha256 -ne $proof.Captures[2].Mask.Sha256) {
                    throw "Incomplete MeshRenderer inspector evidence for $name-$provider"
                }
                $run.Path = $path
                $run.Sha256 = (Get-FileHash -LiteralPath $path).Hash
                $images = @($proof.Captures.Name | ForEach-Object { "$fixture/$_.png" })
            }
            foreach ($child in $children) {
                $state = Get-Content -LiteralPath $child.FullName -Raw | ConvertFrom-Json
                $gpu = $state.Validation
                if ($state.Resources -ne 0 -or $state.Handles -ne 0 -or !$gpu.ValidationEnabled -or $gpu.Errors -ne 0 -or
                    $gpu.Warnings -ne 0 -or $gpu.DevicesCreated -ne $gpu.DevicesReleased -or
                    $gpu.SurfacesCreated -ne $gpu.SurfacesReleased -or $gpu.SlotsCreated -ne $gpu.SlotsReleased -or
                    $gpu.OpenHandles -ne 0 -or $gpu.LiveSlots -ne 0) {
                    throw "Runtime ownership or validation mismatch in $($child.FullName)"
                }
            }
            $run.Children = @($children | ForEach-Object { @{ Path = $_.FullName; Sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash } })
            $run.Captures = @($images | ForEach-Object { @{ Path = $_; Sha256 = (Get-FileHash -LiteralPath $_).Hash } })
            $report.Runs += $run
            continue
        }
        if ($modelStatusProbe) {
            [void][IO.Directory]::CreateDirectory($output)
            $path = "$output/editor-imports.json"
            Copy-Item -LiteralPath "$fixture/editor-imports.json" -Destination $path
            $proof = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
            if (!$proof.Completed -or !$proof.MatchingContent -or !$proof.Blocked -or
                $proof.BeforeEdit -eq $proof.AfterEdit -or $proof.BeforeEdit -ne $proof.AfterReimport -or
                $proof.OldBuild -eq $proof.NewBuild) { throw "Incomplete model import evidence for $name-$provider" }
            $run = @{ Name = "$name-$provider"; Path = $path; Sha256 = (Get-FileHash -LiteralPath $path).Hash; Fixture = $fixture }
            if ($reimportProbe) {
                if (!$proof.CancelledAfterLedger -or !$proof.MissingSourceRejected -or $proof.Captures.Count -ne 3) {
                    throw "Incomplete Editor reimport evidence for $name-$provider"
                }
                $run.Captures = @($proof.Captures.Name | ForEach-Object {
                    @{ Path = "$fixture/$_.png"; Sha256 = (Get-FileHash -LiteralPath "$fixture/$_.png").Hash }
                })
                $run.Children = @(Get-ChildItem -LiteralPath $fixture -Filter 'runtime-*.json' -File | ForEach-Object {
                    @{ Path = $_.FullName; Sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash }
                })
                if ($run.Children.Count -ne 3) { throw "Missing reimport runtime child evidence for $name-$provider" }
            }
            if ($persistenceProbe -and (!$proof.RestoredPriorGeneration -or !$proof.RestoredNewGeneration -or
                !$proof.ConflictRejected -or !$proof.CorruptRejected)) { throw "Incomplete persisted import evidence for $name-$provider" }
            if ($catalogProbe -and (!$proof.CatalogSelectionVerified -or !$proof.MissingModelRejected -or !$proof.EmptySelectionRestored)) {
                throw "Incomplete project model catalog evidence for $name-$provider"
            }
            if ($ModelPlacement -and (!$proof.Placement -or $proof.Placement.InstanceIds.Count -ne 2 -or
                $proof.Captures[1].BuildId -ne $proof.NewBuild -or $proof.Captures[1].Mask.RedPixels -le 200)) {
                throw "Incomplete placement or GPU evidence for $name-$provider"
            }
            $report.Runs += $run
            continue
        }
        $path = "$output/editor-model.json"
        $proof = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        if (!$proof.Completed -or $proof.Captures.Count -ne 5 -or !$proof.RollbackSucceeded -or !$proof.CorruptRejectedBeforeHandoff) {
            throw "Incomplete Editor evidence for $name-$provider"
        }
        $children = @(Get-ChildItem -LiteralPath $output -Filter 'runtime-*.json' -File)
        if ($children.Count -ne 5) { throw "Missing runtime child evidence for $name-$provider" }
        foreach ($child in $children) {
            $state = Get-Content -LiteralPath $child.FullName -Raw | ConvertFrom-Json
            $gpu = $state.Validation
            if ($state.Resources -ne 0 -or $state.Handles -ne 0 -or $state.EditorLoaded -or $state.ToolingLoaded -or
                !$gpu.ValidationEnabled -or $gpu.Errors -ne 0 -or $gpu.Warnings -ne 0 -or
                $gpu.DevicesCreated -ne $gpu.DevicesReleased -or $gpu.SurfacesCreated -ne $gpu.SurfacesReleased -or
                $gpu.SlotsCreated -ne $gpu.SlotsReleased -or $gpu.OpenHandles -ne 0 -or $gpu.LiveSlots -ne 0) {
                throw "Runtime ownership or validation mismatch in $($child.FullName)"
            }
            if ($child.BaseName -eq "runtime-$($proof.CancelledGeneration)" -and $gpu.SurfacesCreated -ne 0) {
                throw 'Cancelled candidate allocated a surface.'
            }
        }
        $report.Runs += @{
            Name = "$name-$provider"; Path = $path; Sha256 = (Get-FileHash -LiteralPath $path).Hash
            Children = @($children | ForEach-Object {
                @{ Path = $_.FullName; Sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash }
            })
            Captures = @($proof.Captures.Name | ForEach-Object {
                @{ Path = "$output/$_.png"; Sha256 = (Get-FileHash -LiteralPath "$output/$_.png").Hash }
            })
        }
    }
}

function Test-FreshEditor {
    $packages = @('TrueMoon.Contracts', 'TrueMoon.Core', 'TrueMoon.Cobalt', 'TrueMoon',
        'TrueMoon.Extensions.DependencyInjection', 'TrueMoon.Aether.Abstractions',
        'TrueMoon.Enerit.Generator', 'TrueMoon.Enerit', 'TrueMoon.Mithril', 'TrueMoon.Aether.Runtime',
        'TrueMoon.Aether.Tooling', 'TrueMoon.Nom', 'TrueMoon.Aether.Nom', 'TrueMoon.Argentis', 'TrueMoon.Alloy',
        'TrueMoon.Alloy.Rendering.Skia', 'TrueMoon.Alloy.Platform.Silk', 'TrueMoon.Alloy.Hosting',
        'TrueMoon.Aether.Editor.UI', 'TrueMoon.Aether.Editor.Tooling')
    $version = $profile.Version
    $feed = "$evidence/feed"
    $root = Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-EditorPackages-' + [Guid]::NewGuid().ToString('N'))
    $cache = "$root/cache"
    $report.PackageConsumerRoot = $root
    $report.Packages = @()
    $report.ExternalPackages = @()
    $report.Graphs = @()
    [void][IO.Directory]::CreateDirectory($feed)
    Write-TrueMoonConsumerBuildProfile -Profile $profile -Directory $root
    [IO.File]::WriteAllText("$root/Directory.Build.targets", '<Project/>')
    $escapedFeed = [Security.SecurityElement]::Escape($feed)
    $config = "$root/NuGet.Config"
    [IO.File]::WriteAllText($config, "<configuration><packageSources><clear/><add key='closed' value='$escapedFeed'/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders></configuration>")

    foreach ($id in $packages) {
        Invoke-Step "pack-$id" @('pack', "$id/$id.csproj", '-c', 'Release', '-o', $feed, '-v:q')
        $archive = "$feed/$id.$version.nupkg"
        $report.Packages += @{ Id = $id; Version = $version; Path = $archive; Sha256 = (Get-FileHash -LiteralPath $archive).Hash }
    }
    $external = @{}
    foreach ($id in @($packages + 'TrueMoon.Cobalt.Generator')) {
        $assets = Get-Content -LiteralPath "$workspace/$id/obj/project.assets.json" -Raw | ConvertFrom-Json -AsHashtable
        foreach ($library in $assets.libraries.GetEnumerator()) {
            if ($library.Value.type -ne 'package' -or $external.ContainsKey($library.Key)) { continue }
            $file = ($library.Key.Replace('/', '.') + '.nupkg').ToLowerInvariant()
            $archive = $null
            foreach ($folder in $assets.packageFolders.Keys) {
                $candidate = Join-Path (Join-Path $folder $library.Value.path) $file
                if (Test-Path -LiteralPath $candidate) { $archive = $candidate; break }
            }
            if (!$archive) { throw "Missing exact archive $($library.Key)." }
            Copy-Item -LiteralPath $archive -Destination "$feed/$file"
            $external[$library.Key] = $true
            $report.ExternalPackages += @{ Id = $library.Key; Path = "$feed/$file"; Sha256 = (Get-FileHash -LiteralPath "$feed/$file").Hash }
        }
    }

    [void][IO.Directory]::CreateDirectory("$root/Authoring")
    [void][IO.Directory]::CreateDirectory("$root/Editor")
    Copy-Item -LiteralPath "$PSScriptRoot/Authoring/Program.cs", "$PSScriptRoot/Authoring/ModelWatchProbe.cs",
        "$PSScriptRoot/../ModelImportResearch/Fixtures.cs" -Destination "$root/Authoring"
    $editorSources = "$workspace/ManualTests/AetherEditorHost"
    Copy-Item -LiteralPath "$editorSources/EditorModelBuildSmoke.cs", "$editorSources/EditorHeaderSmoke.cs",
        "$editorSources/EditorHierarchySmoke.cs", "$editorSources/RuntimeEntryPoint.cs" -Destination "$root/Editor"
    [IO.File]::WriteAllText("$root/Editor/Program.cs", @'
using TrueMoon.ManualTests.AetherEditorHost;

try
{
    if (args is ["--runtime"]) await RuntimeEntryPoint.RunAsync();
    else if (args.FirstOrDefault() == "--model-build-probe") await EditorModelBuildSmoke.RunAsync(args[1..]);
    else throw new ArgumentException("Unknown Editor probe role.");
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    return 1;
}
'@)

    Push-Location $root
    try {
        foreach ($consumer in @(
            @{ Name = 'Authoring'; Assembly = 'Authoring'; Features = @('TrueMoon.Aether.Nom') },
            @{ Name = 'Editor'; Assembly = 'ModelBuildEditorProbe'; Features = @('TrueMoon.Aether.Editor.Tooling','TrueMoon.Aether.Nom') }
        )) {
            $name = $consumer.Name
            $directory = "$root/$name"
            $project = "$directory/Consumer.csproj"
            $references = (@('TrueMoon', 'TrueMoon.Extensions.DependencyInjection') + $consumer.Features) | ForEach-Object {
                "<PackageReference Include='$($_)' Version='[$version]'/>"
            }
            [IO.File]::WriteAllText($project, @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><AssemblyName>$($consumer.Assembly)</AssemblyName><AllowUnsafeBlocks>true</AllowUnsafeBlocks></PropertyGroup>
  <ItemGroup>$references</ItemGroup>
</Project>
"@)
            Invoke-Step "fresh-$name-restore" @('restore', $project, '--configfile', $config, '--packages', $cache, '--no-http-cache', '-v:q')
            $assetsPath = "$directory/obj/project.assets.json"
            $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json -AsHashtable
            if ($assets.packageFolders.Count -ne 1 -or
                [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath(@($assets.packageFolders.Keys)[0])) -ne [IO.Path]::GetFullPath($cache) -or
                @($assets.libraries.Values | Where-Object type -ne 'package').Count -ne 0) {
                throw "Non-isolated package graph for $name."
            }
            $frameworkPackages = @()
            foreach ($library in $assets.libraries.GetEnumerator()) {
                $installed = Join-Path $cache $library.Value.path
                $metadata = Get-Content -LiteralPath "$installed/.nupkg.metadata" -Raw | ConvertFrom-Json
                if ([IO.Path]::GetFullPath($metadata.source) -ne [IO.Path]::GetFullPath($feed)) { throw 'Restore accessed another feed.' }
                $id, $resolvedVersion = $library.Key.Split('/')
                if ($id -notin $packages) { continue }
                if ($resolvedVersion -ne $version) { throw "Wrong TrueMoon version $($library.Key)." }
                $file = "$id.$version.nupkg".ToLowerInvariant()
                if ((Get-FileHash -LiteralPath "$installed/$file").Hash -ne (Get-FileHash -LiteralPath "$feed/$file").Hash) {
                    throw "Installed archive differs from the closed feed for $id."
                }
                $frameworkPackages += $id
            }
            $published = "$root/$name-output"
            Invoke-Step "fresh-$name-publish" @('publish', $project, '--no-restore', '-c', 'Release', '--self-contained', 'false',
                '-p:UseAppHost=false', '-o', $published, '-v:q')
            $binaries = @()
            foreach ($id in $frameworkPackages) {
                $installed = "$cache/$($id.ToLowerInvariant())/$version/lib/$($profile.Framework)/$id.dll"
                if (!(Test-Path -LiteralPath $installed)) { continue }
                $dll = "$published/$id.dll"
                $sha = (Get-FileHash -LiteralPath $dll).Hash
                if ($sha -ne (Get-FileHash -LiteralPath $installed).Hash) { throw "Published DLL differs from the package for $id." }
                $binaries += @{ Id = $id; Path = $dll; Sha256 = $sha }
            }
            $report.Graphs += @{
                Name = $name; PackageCount = $assets.libraries.Count; FrameworkPackages = $frameworkPackages
                AssetsSha256 = (Get-FileHash -LiteralPath $assetsPath).Hash; Binaries = $binaries
            }
        }
        [void][IO.Directory]::CreateDirectory("$root/Editor-output/Assets/Fonts")
        Copy-Item -LiteralPath "$workspace/Examples/AetherEditor/Assets/Fonts/NotoSans-Regular.ttf",
            "$workspace/Examples/AetherEditor/Assets/Fonts/OFL.txt" -Destination "$root/Editor-output/Assets/Fonts"
        Test-EditorConsumers 'fresh-Release' "$evidence/fresh-Release" "$root/Authoring-output/Authoring.dll" "$root/Editor-output/ModelBuildEditorProbe.dll"
    } finally { Pop-Location }
}

$previousLayerPath = $env:VK_LAYER_PATH
$env:VK_LAYER_PATH = $validation
Push-Location $workspace
try {
    $worker = "$evidence/worker"
    Invoke-Step worker @('publish', 'TrueMoon.Aether.Importing.Assimp/TrueMoon.Aether.Importing.Assimp.csproj',
        '-c', 'Release', '-r', 'win-x64', '--self-contained', 'false', '-p:UseAppHost=false', '-o', $worker, '-v:q')
    $report.NativeSha256 = (Get-FileHash -LiteralPath "$worker/assimp.dll").Hash
    if ($report.NativeSha256 -ne '90984E886CF7394560E44FECD5A5666BECD2A93E98251974A562ECB79CFBA386') { throw 'Native worker drift.' }
    if ($FreshPackages) {
        Test-FreshEditor
    } else {
        foreach ($configuration in @('Debug', 'Release')) {
            $directory = "$evidence/$configuration"
            Invoke-Step "$configuration-authoring" @('build', "$PSScriptRoot/Authoring/Authoring.csproj", '-c', $configuration, '-o', "$directory/authoring", '-v:q')
            $verb = if ($configuration -eq 'Debug') { 'build' } else { 'publish' }
            Invoke-Step "$configuration-editor" @($verb, 'ManualTests/AetherEditorHost/AetherEditorHost.csproj', '-c', $configuration, '-o', "$directory/editor", '-v:q')
            Test-EditorConsumers $configuration $directory "$directory/authoring/Authoring.dll" "$directory/editor/AetherEditorHost.dll"
        }
    }
    $report.Completed = $true
} finally {
    Pop-Location
    $env:VK_LAYER_PATH = $previousLayerPath
    $report | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath "$evidence/verification.json" -Encoding utf8
}
