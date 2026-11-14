#Requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ValidationDirectory,[Parameter(Mandatory)][string]$EvidenceDirectory,
    [string]$RegressionVerification,[ValidateRange(20,1000)][int]$Cycles=20)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'));$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
if(Test-Path -LiteralPath $evidence){throw 'Use a fresh architecture evidence directory.'}
$validation=[IO.Path]::GetFullPath($ValidationDirectory)
foreach($name in @('VkLayer_khronos_validation.json','VkLayer_khronos_validation.dll')){if(!(Test-Path -LiteralPath "$validation/$name")){throw 'Provide an existing Vulkan validation layer.'}}
[void][IO.Directory]::CreateDirectory($evidence)
$profile=Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory $evidence
$version=$profile.Version+'.arch39.'+(Get-Date -Format yyyyMMddHHmmss)
$report=[ordered]@{Completed=$false;Stage='ARCH3.9';Version=$version;BuildProfile=$profile;Cycles=$Cycles;ValidationDirectory=$validation;Regression=$null;Commands=@();Stages=@();Sources=@();Failure=$null}
function Save-Report{$report|ConvertTo-Json -Depth 25|Set-Content -LiteralPath "$evidence/verification.json" -Encoding utf8}
function Invoke-Stage([string]$name,[string]$script,[string[]]$arguments){
    $info=[Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path);$info.WorkingDirectory=$workspace;$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($argument in @('-NoProfile','-File',(Join-Path $workspace $script))+$arguments){[void]$info.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::Start($info);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    try{
        if(!$process.WaitForExit(3600000)){$process.Kill($true);$process.WaitForExit();throw "$name timed out"}
        [IO.File]::WriteAllText("$evidence/$name.stdout.log",$stdout.GetAwaiter().GetResult());[IO.File]::WriteAllText("$evidence/$name.stderr.log",$stderr.GetAwaiter().GetResult())
        $report.Commands+=@{Name=$name;Script=$script;Arguments=$arguments;ExitCode=$process.ExitCode};Save-Report
        if($process.ExitCode -ne 0){throw "$name failed"}
        $path="$evidence/$name/verification.json";$proof=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
        if(!$proof.Completed -or $proof.Failure){throw "$name proof incomplete"}
        $report.Stages+=@{Name=$name;Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash};Save-Report;Write-Host "$name passed"
    }finally{if(!$process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
try{
    foreach($path in (& rg --files $workspace -g '*.cs' -g '*.csproj' -g '*.props' -g '*.targets' -g '*.ps1' -g 'global.json')){$report.Sources+=@{Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash}}
    if(!$RegressionVerification){Invoke-Stage 'regression' 'ManualTests/ArchitectureDelivery/Regression.ps1' @('-EvidenceDirectory',"$evidence/regression");$RegressionVerification="$evidence/regression/verification.json"}
    $regression=Get-Content -LiteralPath $RegressionVerification -Raw|ConvertFrom-Json
    if(!$regression.Completed -or $regression.Failure -or $regression.Tests.Count -ne 28){throw 'Completed full Debug/Release regression is required.'}
    foreach($source in $regression.Sources){if((Get-FileHash -LiteralPath $source.Path).Hash -ne $source.Sha256){throw 'Regression inputs changed.'}}
    $report.Regression=@{Path=[IO.Path]::GetFullPath($RegressionVerification);Sha256=(Get-FileHash -LiteralPath $RegressionVerification).Hash};Save-Report
    Invoke-Stage 'configuration' 'ManualTests/ConfigurationDelivery/Verify.ps1' @('-EvidenceDirectory',"$evidence/configuration",'-PackageVersion',$version,'-AdditionalPackages','TrueMoon.Aether.Player')
    $delivery="$evidence/configuration/verification.json"
    foreach($spec in @(@{Name='sdk';Script='Abstractions.Host/Verify.ps1'},@{Name='runtime';Script='RuntimeConsumer/Verify.ps1'},@{Name='tooling';Script='ToolingConsumer/Verify.ps1'},@{Name='editor-ui';Script='EditorUiConsumer/Verify.ps1'},@{Name='player';Script='PlayerProbe/Verify.ps1'},@{Name='mod-stable';Script='ModConsumer/Verify.ps1';Mode='Reload'},@{Name='mod-migration';Script='ModConsumer/Verify.ps1';Mode='Migration'},@{Name='mod-legacy';Script='ModConsumer/Verify.ps1'})){
        $arguments=@('-EvidenceDirectory',"$evidence/$($spec.Name)",'-PackageVerification',$delivery)
        if($spec.Name -in @('editor-ui','player')){$arguments+=@('-ValidationDirectory',$validation)}
        if($spec.ContainsKey('Mode')){$arguments+=@("-$($spec.Mode)",'-Cycles',[string]$Cycles)}
        Invoke-Stage $spec.Name ('ManualTests/'+$spec.Script) $arguments
        $child=Get-Content -LiteralPath "$evidence/$($spec.Name)/verification.json" -Raw|ConvertFrom-Json
        if($child.Version -ne $version){throw 'Consumer version differs from the coherent delivery.'}
        $common=Get-Content -LiteralPath $delivery -Raw|ConvertFrom-Json
        foreach($package in $child.Packages){$origin=@($common.Packages|Where-Object Id -eq $package.Id);if($origin.Count -ne 1 -or $origin[0].Sha256 -ne $package.Sha256){throw 'A consumer repacked a shared archive.'}}
    }
    Invoke-Stage 'full-editor-packages' 'ManualTests/AetherPackageConsumer/VerifyPackages.ps1' @('-EvidenceDirectory',"$evidence/full-editor-packages",'-PackageVerification',$delivery)
    $editorPackages="$evidence/full-editor-packages/verification.json"
    Invoke-Stage 'full-editor-source' 'ManualTests/AetherPackageConsumer/VerifyGui.ps1' @('-EvidenceDirectory',"$evidence/full-editor-source",'-PackageVerification',$editorPackages,'-ValidationDirectory',$validation,'-RunProfile','SourceDebug','-Scenario','Workflow')
    Invoke-Stage 'full-editor-published' 'ManualTests/AetherPackageConsumer/VerifyGui.ps1' @('-EvidenceDirectory',"$evidence/full-editor-published",'-PackageVerification',$editorPackages,'-ValidationDirectory',$validation,'-RunProfile','PackageRelease','-Scenario','Workflow')
    foreach($source in $report.Sources){if((Get-FileHash -LiteralPath $source.Path).Hash -ne $source.Sha256){throw 'Architecture inputs changed during acceptance.'}}
    $report.Completed=$true
}catch{$report.Failure=$_.Exception.ToString();throw}finally{Save-Report;Write-Host "Evidence: $evidence"}
