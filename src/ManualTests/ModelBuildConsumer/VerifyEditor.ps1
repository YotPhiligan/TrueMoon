#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [Parameter(Mandatory)][string]$ValidationDirectory
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
$profile = Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory $evidence
$dotnet = (Get-Command dotnet).Source
$report = [ordered]@{ Completed = $false; Stage = 'ARCH1g.2d'; Profile = $profile; Runs = @(); Commands = @() }

function Invoke-Step([string]$name, [string[]]$arguments) {
    if ($arguments[0] -in @('build', 'publish')) { $arguments += "-bl:$evidence/$name-{}.binlog" }
    & $dotnet @arguments *> "$evidence/$name.log"
    $code = $LASTEXITCODE
    $report.Commands += @{ Name = $name; Arguments = $arguments; ExitCode = $code }
    if ($code -ne 0) {
        Get-Content -LiteralPath "$evidence/$name.log" -Tail 15
        throw "$name exited with code $code"
    }
    Write-Host "$name passed"
}

$previousLayerPath = $env:VK_LAYER_PATH
$env:VK_LAYER_PATH = $validation
Push-Location $workspace
try {
    $worker = "$evidence/worker"
    Invoke-Step worker @('publish', 'TrueMoon.Aether.Importing.Assimp/TrueMoon.Aether.Importing.Assimp.csproj',
        '-c', 'Release', '-r', 'win-x64', '--self-contained', 'false', '-p:UseAppHost=false', '-o', $worker, '-v:q')
    foreach ($configuration in @('Debug', 'Release')) {
        $directory = "$evidence/$configuration"
        Invoke-Step "$configuration-authoring" @('build', "$PSScriptRoot/Authoring/Authoring.csproj", '-c', $configuration, '-o', "$directory/authoring", '-v:q')
        $verb = if ($configuration -eq 'Debug') { 'build' } else { 'publish' }
        Invoke-Step "$configuration-editor" @($verb, 'ManualTests/AetherEditorHost/AetherEditorHost.csproj', '-c', $configuration, '-o', "$directory/editor", '-v:q')
        foreach ($provider in @('cobalt', 'microsoft')) {
            $name = "$configuration-$provider"
            $output = "$directory/$provider"
            Invoke-Step $name @("$directory/authoring/Authoring.dll", $provider, $output, $worker, "$directory/editor/AetherEditorHost.dll",
                $dotnet, $profile.TestFramework, $profile.Sdk, '--editor')
            $path = "$output/editor-model.json"
            $proof = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
            if (!$proof.Completed -or $proof.Captures.Count -ne 5 -or !$proof.RollbackSucceeded -or !$proof.CorruptRejectedBeforeHandoff) {
                throw "Incomplete Editor evidence for $name"
            }
            $report.Runs += @{
                Name = $name; Path = $path; Sha256 = (Get-FileHash -LiteralPath $path).Hash
                Captures = @($proof.Captures.Name | ForEach-Object {
                    @{ Path = "$output/$_.png"; Sha256 = (Get-FileHash -LiteralPath "$output/$_.png").Hash }
                })
            }
        }
    }
    $report.Completed = $true
} finally {
    Pop-Location
    $env:VK_LAYER_PATH = $previousLayerPath
    $report | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath "$evidence/verification.json" -Encoding utf8
}
