[CmdletBinding()]
param([Parameter(Mandatory)][string]$ConsumerEvidenceDirectory,
    [Parameter(Mandatory)][string]$ValidationDirectory,[string]$EvidenceDirectory)
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
$trueMoonBuildProfile = Get-TrueMoonBuildProfile -Workspace ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))



# Uses the frozen source/published artifacts from Verify.ps1. Does not build, restore or alter driver/settings.
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$primitiveWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$primitiveConsumers=[IO.Path]::GetFullPath($ConsumerEvidenceDirectory)
$primitiveAggregate=Get-Content -LiteralPath (Join-Path $primitiveConsumers 'verification.json') -Raw | ConvertFrom-Json
if(!$primitiveAggregate.Completed){throw 'Complete Verify.ps1 before native acceptance.'}
$primitiveValidation=[IO.Path]::GetFullPath($ValidationDirectory)
foreach($file in @('VkLayer_khronos_validation.json','VkLayer_khronos_validation.dll')){
    if(!(Test-Path -LiteralPath (Join-Path $primitiveValidation $file))){throw 'Provide the existing extracted Khronos validation layer directory.'}
}
$primitiveNativeEvidence=if($EvidenceDirectory){[IO.Path]::GetFullPath($EvidenceDirectory)}else{Join-Path $primitiveConsumers 'Native'}
if(Test-Path -LiteralPath $primitiveNativeEvidence){throw 'Use a fresh native evidence directory.'}
[void][IO.Directory]::CreateDirectory($primitiveNativeEvidence)
$primitiveDotnet=(Get-Command dotnet.exe -ErrorAction Stop).Source
$primitiveRun=$primitiveAggregate.RunDirectory
$primitiveNativeReport=[ordered]@{Completed=$false;ConsumerEvidence=$primitiveConsumers;Commands=@();Profiles=@();Sources=@();Validation=@();Failure=$null;GpuRun=$true;FreshPackageGpuRun=$false}
function Save-PrimitiveNativeReport{$primitiveNativeReport|ConvertTo-Json -Depth 25|Set-Content -LiteralPath (Join-Path $primitiveNativeEvidence 'verification.json') -Encoding utf8}
try{
    foreach($file in @('VkLayer_khronos_validation.json','VkLayer_khronos_validation.dll')){
        $path=Join-Path $primitiveValidation $file
        $primitiveNativeReport.Validation+=@{Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash}
    }
    foreach($relative in @('ManualTests/EngineSessionProbe/PrimitiveNativeProbe.cs','ManualTests/EngineSessionProbe/PrimitiveRuntimeProbe.cs','ManualTests/Game.Logic/PrimitiveProbeGame.cs')){
        $path=Join-Path $primitiveWorkspace $relative
        $primitiveNativeReport.Sources+=@{Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash}
    }
    foreach($profile in @('source','published')){
        $parent=if($profile -eq 'source'){Join-Path $primitiveWorkspace "ManualTests/EngineSessionProbe/bin/Debug/$($trueMoonBuildProfile.TestFramework)/EngineSessionProbe.dll"}else{"$primitiveRun/published-parent/EngineSessionProbe.dll"}
        $runtime=if($profile -eq 'source'){Join-Path $primitiveWorkspace "Examples/AetherRuntimeHost/bin/Debug/$($trueMoonBuildProfile.Framework)/AetherRuntimeHost.dll"}else{"$primitiveRun/published-runtime/AetherRuntimeHost.dll"}
        $game=if($profile -eq 'source'){Join-Path $primitiveWorkspace "ManualTests/Game.Logic/bin/Debug/$($trueMoonBuildProfile.TestFramework)"}else{"$primitiveRun/published-parent"}
        $output=Join-Path $primitiveNativeEvidence ($profile+'-proof')
        $info=[Diagnostics.ProcessStartInfo]::new($primitiveDotnet)
        $info.WorkingDirectory=Join-Path $primitiveRun 'unrelated working directory'
        $info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
        $info.Environment['TRUEMOON_GPU_VALIDATION']='1'
        $info.Environment['VK_LAYER_PATH']=$primitiveValidation
        [void]$info.Environment.Remove('TM_ENGINE_BOOTSTRAP')
        $arguments=@($parent,'--ecs-primitive-native',$runtime,$game,$output)
        foreach($argument in $arguments){[void]$info.ArgumentList.Add($argument)}
        $process=[Diagnostics.Process]::Start($info)
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync();$timeout=$false
        try{
            if(!$process.WaitForExit(240000)){$timeout=$true;$process.Kill($true);$process.WaitForExit()}
            [IO.File]::WriteAllText((Join-Path $primitiveNativeEvidence "$profile.stdout.log"),$stdout.GetAwaiter().GetResult())
            [IO.File]::WriteAllText((Join-Path $primitiveNativeEvidence "$profile.stderr.log"),$stderr.GetAwaiter().GetResult())
            $primitiveNativeReport.Commands+=@{Name=$profile;Arguments=$arguments;ExitCode=$process.ExitCode;TimedOut=$timeout}
            Save-PrimitiveNativeReport
            if($timeout -or $process.ExitCode -ne 0){throw "Native $profile failed; see preserved outputs."}
        }finally{if(!$process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
        $result=Get-Content -LiteralPath (Join-Path $output 'verification.json') -Raw|ConvertFrom-Json
        if(!$result.Completed){throw "Native $profile reported incomplete acceptance."}
        $primitiveNativeReport.Profiles+=@{Name=$profile;Report=(Join-Path $output 'verification.json')}
        Write-Host "Native $profile passed"
    }
    foreach($source in @($primitiveNativeReport.Sources+$primitiveNativeReport.Validation)){if((Get-FileHash -LiteralPath $source.Path).Hash -ne $source.Sha256){throw 'Native proof input changed during acceptance.'}}
    $primitiveNativeReport.Completed=$true
}catch{$primitiveNativeReport.Failure=$_.Exception.ToString();throw}
finally{Save-PrimitiveNativeReport;Write-Host "Native evidence: $primitiveNativeEvidence"}
