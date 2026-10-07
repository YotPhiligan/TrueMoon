param(
    [Parameter(Mandatory)][string]$SkiaSourcePath,
    [switch]$CheckOnly
)
$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $SkiaSourcePath).Path
$revision = & git -C $source rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Could not read the Skia source revision.' }
$pins = Import-PowerShellDataFile -LiteralPath (Join-Path $PSScriptRoot 'SourcePins.psd1')
$pin = $pins.Values | Where-Object { $_.SkiaRevision -eq $revision } | Select-Object -First 1
if (!$pin) { throw "Unsupported Skia revision $revision. See SourcePins.psd1 for verified source pins." }
$surfacePath = Join-Path $source 'src/c/sk_surface.cpp'
$surface = [IO.File]::ReadAllText($surfacePath)
$include = '#include "src/c/truemoon_vk_interop.inc"'
if (!$surface.Contains('SkSurfaces::WrapBackendTexture')) { throw 'Unexpected surface implementation.' }
if ($CheckOnly) {
    Write-Output "Source revision and insertion point verified: SkiaSharp $($pin.Version), $revision. No files changed."
    exit 0
}
# Keep changes off upstream protected branches; do not commit or publish them.
$branch = & git -C $source branch --show-current
if ([string]::IsNullOrEmpty($branch) -or $branch -in @('main', 'skiasharp')) {
    & git -C $source switch -c truemoon-vulkan-interop
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the local interop branch.' }
}
foreach ($entry in @(@('truemoon_vk_interop.h', 'include/c'), @('truemoon_vk_interop.inc', 'src/c'))) {
    $target = Join-Path (Join-Path $source $entry[1]) $entry[0]
    $content = [IO.File]::ReadAllText((Join-Path $PSScriptRoot $entry[0]))
    if ((Test-Path -LiteralPath $target) -and [IO.File]::ReadAllText($target) -ne $content) {
        throw "Refusing to overwrite different content: $target"
    }
    [IO.File]::WriteAllText($target, $content, [Text.UTF8Encoding]::new($false))
}
if (!$surface.Contains($include)) {
    [IO.File]::WriteAllText($surfacePath, $surface.TrimEnd() + "`n`n" + $include + "`n", [Text.UTF8Encoding]::new($false))
}
Write-Output 'Native extension applied. Rebuild libSkiaSharp from source; packaged DLLs do not contain it.'
