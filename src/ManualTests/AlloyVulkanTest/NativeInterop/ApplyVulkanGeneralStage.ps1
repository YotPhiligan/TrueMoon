param(
    [Parameter(Mandatory)][string]$SkiaSourcePath,
    [switch]$CheckOnly
)
$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $SkiaSourcePath).Path
$revision = & git -C $source rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Could not read the Skia source revision.' }
$pins = Import-PowerShellDataFile -LiteralPath (Join-Path $PSScriptRoot 'SourcePins.psd1')
if ($revision -ne $pins['4.155.0'].SkiaRevision) {
    throw 'The Vulkan GENERAL host-stage patch is verified only for the pinned SkiaSharp4.155.0 source.'
}
$patch = Join-Path $PSScriptRoot 'vulkan-general-host-stage.patch'
# Reject unexpected source edits; allow only the exact forward or already-applied patch.
& git -C $source apply --reverse --check --whitespace=error-all $patch 2>$null
if ($LASTEXITCODE -eq 0) { Write-Output 'Vulkan GENERAL host-stage patch already applied.'; return }
& git -C $source apply --check --whitespace=error-all $patch
if ($LASTEXITCODE -ne 0) { throw 'Vulkan GENERAL patch does not match the pinned source; no files changed.' }
if ($CheckOnly) { Write-Output 'Vulkan GENERAL host-stage patch applicability verified; no files changed.'; return }
& git -C $source apply --whitespace=error-all $patch
if ($LASTEXITCODE -ne 0) { throw 'Could not apply Vulkan GENERAL host-stage patch.' }
Write-Output 'Vulkan GENERAL host-stage patch applied.'
