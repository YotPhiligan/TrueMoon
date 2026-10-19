#Requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)] [string] $RunDirectory)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot 'Integration/Common.ps1')
. (Join-Path $PSScriptRoot 'Integration/Package.ps1')
. (Join-Path $PSScriptRoot 'Integration/Verify.ps1')
$run = Get-ProbePath ([IO.Path]::GetFullPath($RunDirectory, $workspace)) (Join-Path $workspace 'TestResults/AlloyInfrastructure')
$manifest = Get-Content -LiteralPath (Join-Path $run 'summary.json') -Raw | ConvertFrom-Json
if ($manifest.RunDirectory -ne $run -or $manifest.Workspace -ne $workspace) { throw 'Manifest belongs to a different workspace/run.' }
Assert-ProbeRun $manifest
Write-Host "Verified ($($manifest.Profile)): $run"
