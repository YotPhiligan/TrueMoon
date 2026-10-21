param([Parameter(Mandatory)][string]$GlslangValidator, [Parameter(Mandatory)][string]$SpirvValidator)
$ErrorActionPreference = 'Stop'
foreach ($name in @('cube.vert', 'cube.frag')) {
    $source = Join-Path $PSScriptRoot $name
    & $GlslangValidator -V --target-env vulkan1.1 $source -o "$source.spv"
    if ($LASTEXITCODE -ne 0) { throw "Shader compilation failed: $name" }
    & $SpirvValidator --target-env vulkan1.1 "$source.spv"
    if ($LASTEXITCODE -ne 0) { throw "SPIR-V validation failed: $name" }
}
