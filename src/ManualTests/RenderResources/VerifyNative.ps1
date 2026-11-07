[CmdletBinding()]
param([Parameter(Mandatory)][string]$ConsumerEvidenceDirectory,[Parameter(Mandatory)][string]$ValidationDirectory,[string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$resourceRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$resourceConsumer=[IO.Path]::GetFullPath($ConsumerEvidenceDirectory)
$resourceValidation=[IO.Path]::GetFullPath($ValidationDirectory)
$resourceEvidence=if($EvidenceDirectory){[IO.Path]::GetFullPath($EvidenceDirectory)}else{Join-Path $resourceConsumer 'native'}
if(Test-Path -LiteralPath $resourceEvidence){throw 'Use a fresh native evidence directory.'}
$previous=Get-Content -LiteralPath (Join-Path $resourceConsumer 'verification.json') -Raw|ConvertFrom-Json
if(!$previous.Completed){throw 'Complete CPU source/package acceptance first.'}
$validationFiles=@('VkLayer_khronos_validation.json','VkLayer_khronos_validation.dll')|ForEach-Object {
    $path=Join-Path $resourceValidation $_;if(!(Test-Path -LiteralPath $path)){throw "Missing existing validation file: $path"}
    @{Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash}
}
foreach($source in $previous.Sources){if((Get-FileHash -LiteralPath $source.Path).Hash -ne $source.Sha256){throw 'Consumer source drift.'}}
[void][IO.Directory]::CreateDirectory($resourceEvidence)
$resourceDotnet=(Get-Command dotnet -ErrorAction Stop).Source
$resourceReport=[ordered]@{Completed=$false;GpuRun=$true;WindowRun=$false;ConsumerEvidence=$resourceConsumer;Sources=$previous.Sources;Validation=$validationFiles;Commands=@();Profiles=@();Failure=$null}
function Save-ResourceReport {$resourceReport|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $resourceEvidence 'verification.json') -Encoding utf8}
try{
    $profiles=@(
        @{Name='source';Assembly=(Join-Path $previous.RunDirectory 'source/bin/Debug/net10.0/RenderResourcesConsumer.dll')},
        @{Name='published';Assembly=(Join-Path $previous.RunDirectory 'published/RenderResourcesConsumer.dll')},
        @{Name='fresh';Assembly=(Join-Path $previous.RunDirectory 'fresh-published/RenderResourcesConsumer.dll')}
    )
    foreach($profile in $profiles){
        if(!(Test-Path -LiteralPath $profile.Assembly)){throw 'Missing frozen consumer artifact.'}
        $output=Join-Path $resourceEvidence $profile.Name
        $info=[Diagnostics.ProcessStartInfo]::new($resourceDotnet);$info.UseShellExecute=$false;$info.CreateNoWindow=$true
        $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true;$info.WorkingDirectory=Join-Path $previous.RunDirectory 'unrelated working directory'
        $info.Environment['VK_LAYER_PATH']=$resourceValidation
        foreach($argument in @($profile.Assembly,$output,'native')){[void]$info.ArgumentList.Add($argument)}
        $process=[Diagnostics.Process]::Start($info);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync();$timedOut=$false
        try{
            if(!$process.WaitForExit(120000)){$timedOut=$true;$process.Kill($true);$process.WaitForExit()}
            [IO.File]::WriteAllText((Join-Path $resourceEvidence ($profile.Name+'.stdout.log')),$stdout.GetAwaiter().GetResult())
            [IO.File]::WriteAllText((Join-Path $resourceEvidence ($profile.Name+'.stderr.log')),$stderr.GetAwaiter().GetResult())
            $resourceReport.Commands+=@{Name=$profile.Name;Assembly=$profile.Assembly;AssemblySha256=(Get-FileHash -LiteralPath $profile.Assembly).Hash;ExitCode=$process.ExitCode;TimedOut=$timedOut}
            Save-ResourceReport
            if($timedOut -or $process.ExitCode -ne 0){throw "Native $($profile.Name) failed; outputs preserved."}
            $path=Join-Path $output 'verification.json';$proof=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
            if(!$proof.Completed -or !$proof.GpuRun -or $proof.WindowRun -or $proof.BackendKind -ne 'Vulkan' -or $proof.Generations.Count -ne 2 -or !$proof.DeniedBeforeAllocation.ExpectedFailureObserved){throw 'Native proof scope differs.'}
            foreach($entry in @($proof.Generations)+@($proof.DeniedBeforeAllocation)){
                $native=$entry.Native
                if(!$entry.Released -or !$native.ValidationEnabled -or $native.Errors -ne 0 -or $native.Warnings -ne 0 -or $native.LiveUploads -ne 0 -or
                    $native.DevicesCreated -ne $native.DevicesReleased -or $native.BuffersCreated -ne $native.BuffersReleased -or $native.AllocationsCreated -ne $native.AllocationsReleased){throw 'Native ownership/validation differs.'}
            }
            $resourceReport.Profiles+=@{Name=$profile.Name;Report=$path};Write-Host "Native $($profile.Name) passed"
        }finally{if(!$process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
    }
    $resourceReport.Completed=$true
}catch{$resourceReport.Failure=$_.Exception.ToString();throw}
finally{Save-ResourceReport}
