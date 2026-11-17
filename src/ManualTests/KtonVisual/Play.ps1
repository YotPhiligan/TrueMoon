#Requires -Version 7.2
[CmdletBinding()]
param([string]$EvidenceDirectory,[switch]$Microsoft)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$EvidenceDirectory){$EvidenceDirectory=Join-Path $workspace ('TestResults/KtonVisual-'+[DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N'))}
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory,$workspace)
if(Test-Path $evidence){throw 'Use a fresh evidence directory.'}
Push-Location $workspace
try{
 $profile=Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory $evidence
 dotnet build "$PSScriptRoot/Probe.csproj" -c Release "-bl:$evidence/build-{}.binlog" *> "$evidence/build.log"
 if($LASTEXITCODE -ne 0){throw "Build failed: $evidence/build.log"}
 $mode=if($Microsoft){'play-msdi'}else{'play'}
 dotnet "$PSScriptRoot/bin/Release/$($profile.Framework)/KtonVisualProbe.dll" $mode "$evidence/session"
 if($LASTEXITCODE -ne 0){throw "Playground failed: $evidence/session"}
}finally{Pop-Location}
