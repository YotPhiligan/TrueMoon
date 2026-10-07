param(
    [Parameter(Mandatory)][string]$GlslangValidator,
    [Parameter(Mandatory)][string]$SpirvValidator
)
$ErrorActionPreference = 'Stop'
foreach ($name in @('fullscreen.vert', 'scene.frag', 'hud.frag')) {
    $source = Join-Path $PSScriptRoot $name
    $output = "$source.spv"
    & $GlslangValidator -V --target-env vulkan1.1 $source -o $output
    if ($LASTEXITCODE -ne 0) { throw "Shader compilation failed: $name" }
    & $SpirvValidator --target-env vulkan1.1 $output
    if ($LASTEXITCODE -ne 0) { throw "SPIR-V validation failed: $name" }
}
