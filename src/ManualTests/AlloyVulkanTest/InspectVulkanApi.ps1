param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot 'bin/Debug/net10.0/SkiaSharp.dll')
)

$ErrorActionPreference = 'Stop'
$resolvedPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
$assembly = [System.Reflection.Assembly]::LoadFrom($resolvedPath)
Write-Output "Assembly: $($assembly.FullName)"
Write-Output "Path: $resolvedPath"

$flags = [System.Reflection.BindingFlags]'Public,Static,Instance,DeclaredOnly'
foreach ($name in @('SKSurface', 'SKImage', 'GRBackendTexture', 'GRBackendRenderTarget', 'GRContext')) {
    $type = $assembly.GetType("SkiaSharp.$name", $true)
    $methods = @($type.GetMethods($flags) | Where-Object {
        $_.Name -match 'BackendTexture|BackendRenderTarget|VkImage|ImageLayout|MutableState|Semaphore|TextureHandle|RenderTargetHandle|Flush|Submit'
    } | ForEach-Object { $_.ToString() } | Sort-Object)
    Write-Output "${name}: relevant public methods"
    if ($methods.Count) { $methods } else { Write-Output '  (none)' }
}

Write-Output 'Public constructors accepting GRVkImageInfo:'
foreach ($name in @('GRBackendTexture', 'GRBackendRenderTarget')) {
    $assembly.GetType("SkiaSharp.$name", $true).GetConstructors() |
        Where-Object { $_.ToString() -match 'GRVkImageInfo' } |
        ForEach-Object { "  ${name}: $_" }
}

Write-Output 'Public types for GPU semaphores or mutable backend state:'
$syncTypes = @($assembly.GetExportedTypes() | Where-Object { $_.Name -match 'Semaphore|Backend.*State|Mutable.*State' } |
    ForEach-Object { $_.FullName } | Sort-Object)
if ($syncTypes.Count) { $syncTypes } else { Write-Output '  (none)' }
