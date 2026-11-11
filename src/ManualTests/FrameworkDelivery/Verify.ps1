#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ValidationDirectory,
    [string]$EvidenceDirectory,
    [string]$RunDirectory
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=if($EvidenceDirectory){[IO.Path]::GetFullPath($EvidenceDirectory)}else{Join-Path $workspace ('TestResults/FrameworkDelivery-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
$run=if($RunDirectory){[IO.Path]::GetFullPath($RunDirectory)}else{Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-FrameworkDelivery-'+[guid]::NewGuid().ToString('N'))}
if($run -eq $workspace -or $run.StartsWith($workspace+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'RunDirectory must be outside the checkout.'}
if((Test-Path -LiteralPath $evidence) -or (Test-Path -LiteralPath $run)){throw 'Use fresh evidence/run directories.'}
$validation=(Resolve-Path -LiteralPath $ValidationDirectory).Path
if(!(Test-Path -LiteralPath (Join-Path $validation 'VkLayer_khronos_validation.json'))){throw 'Validation manifest is required.'}
[void][IO.Directory]::CreateDirectory($evidence)
[void][IO.Directory]::CreateDirectory($run)
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
$profile=Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory (Join-Path $evidence 'profile')
$version=$profile.Version+'.core85.'+(Get-Date -Format 'yyyyMMddHHmmss')
$report=[ordered]@{Completed=$false;Version=$version;BuildProfile=$profile;RunDirectory=$run;Sources=@();Commands=@();Packages=@();Stages=@();Tests=@();Failure=$null}
function Save-Report {$report|ConvertTo-Json -Depth 18|Set-Content -LiteralPath (Join-Path $evidence 'verification.json') -Encoding utf8NoBOM}
function Invoke-Dotnet([string]$name,[string[]]$arguments){
    $info=[Diagnostics.ProcessStartInfo]::new('dotnet')
    $info.WorkingDirectory=$workspace;$info.UseShellExecute=$false;$info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    $info.Environment['DOTNET_CLI_UI_LANGUAGE']='en'
    foreach($argument in $arguments){[void]$info.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::Start($info)
    $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    try{
        if(!$process.WaitForExit(1800000)){throw "Command timed out: $name"}
        [IO.File]::WriteAllText((Join-Path $evidence "$name.stdout.log"),$stdout.GetAwaiter().GetResult())
        [IO.File]::WriteAllText((Join-Path $evidence "$name.stderr.log"),$stderr.GetAwaiter().GetResult())
        $report.Commands+=@{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode};Save-Report
        if($process.ExitCode -ne 0){throw "Failed $name; logs preserved."}
        if(!(Get-ChildItem -LiteralPath $evidence -Filter "$name-*.binlog")){throw "Missing binlog: $name"}
        Write-Host "$name passed"
    }finally{if(!$process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
function Add-Stage([string]$name,[string]$path){
    $result=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
    if(!$result.Completed -or $result.Failure){throw "Incomplete stage $name"}
    $report.Stages+=@{Name=$name;Manifest=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash};Save-Report
    return $result
}
try{
    $inputPaths=@(& git -C $workspace ls-files --cached --others --exclude-standard -- '*.cs' '*.csproj' '*.props' '*.targets' '*.ps1' '*.runsettings' '*.md' 'global.json' 'NuGet.Config')
    if($LASTEXITCODE){throw 'Cannot enumerate source inputs.'}
    $inputPaths=@($inputPaths | Where-Object {$_ -notmatch '^(docs|\.codex|\.agents)/' -and $_ -ne 'AGENTS.md'} | ForEach-Object {Join-Path $workspace $_})
    $inputPaths+=@((Join-Path $workspace '../README.md'),(Join-Path $workspace '../LICENSE'),(Join-Path $workspace '../materials/icon.png'))
    foreach($path in $inputPaths|Sort-Object -Unique){$report.Sources+=@{Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash}}
    foreach($configuration in @('Debug','Release')){
        $name='solution-'+$configuration
        Invoke-Dotnet $name @('build','TrueMoon.slnx','-c',$configuration,'--verbosity','minimal',"-bl:$evidence/$name-{}.binlog")
    }
    $examplesRun=Join-Path $run 'examples'
    & (Join-Path $workspace 'ManualTests/EngineSessionProbe/VerifyExampleHosts.ps1') -RunDirectory $examplesRun
    $examplesProof=Add-Stage 'source-published-examples' (Join-Path $examplesRun 'verification.json')
    if($examplesProof.Cases.Count -ne 4){throw 'Incomplete source/published runtime matrix.'}
    $configEvidence=Join-Path $evidence 'configuration'
    & (Join-Path $workspace 'ManualTests/ConfigurationDelivery/Verify.ps1') -EvidenceDirectory $configEvidence -RunDirectory (Join-Path $run 'configuration') -PackageVersion $version
    $configurationProof=Add-Stage 'configuration' (Join-Path $configEvidence 'verification.json')
    if($configurationProof.Version -ne $version -or $configurationProof.Packages.Count -ne 16 -or $configurationProof.Profiles.Count -ne 15){throw 'Incomplete common delivery.'}
    foreach($package in $configurationProof.Packages){
        $zip=[IO.Compression.ZipFile]::OpenRead($package.Path)
        try{
            $reader=[IO.StreamReader]::new($zip.GetEntry($package.Id+'.nuspec').Open())
            try{[xml]$metadata=$reader.ReadToEnd()}finally{$reader.Dispose()}
            if($metadata.package.metadata.id -ne $package.Id -or $metadata.package.metadata.version -ne $version){throw 'Package identity/version mismatch.'}
            foreach($dependency in $metadata.SelectNodes('//*[local-name()="dependency"]')){
                if($dependency.id -match '^TrueMoon(?:\.|$)' -and $dependency.version.Trim('[',']') -ne $version){throw 'Mixed framework dependency versions.'}
            }
            $runtimeEntries=@($zip.Entries | Where-Object {$_.FullName -match '^lib/[^/]+/.+\.dll$'})
            if($package.Id -ne 'TrueMoon.Enerit.Generator' -and ($runtimeEntries.Count -ne 1 -or @($runtimeEntries | Where-Object {$_.FullName -notlike "lib/$($profile.Framework)/*"}).Count)){throw 'Unexpected runtime target in package.'}
            $report.Packages+=@{Id=$package.Id;Path=$package.Path;Sha256=(Get-FileHash -LiteralPath $package.Path).Hash;RuntimeEntries=@($runtimeEntries | ForEach-Object FullName)}
        }finally{$zip.Dispose()}
    }
    $gameEvidence=Join-Path $evidence 'game'
    # The standalone legacy runner reads optional NuGet JSON keys under its own
    # normal PowerShell semantics; do not impose this orchestrator's StrictMode.
    & {
        Set-StrictMode -Off
        & (Join-Path $workspace 'ManualTests/AetherPackageConsumer/VerifyPackages.ps1') -RunDirectory (Join-Path $run 'game') -EvidenceDirectory $gameEvidence -PackageVerification (Join-Path $configEvidence 'verification.json')
    }
    $gameProof=Add-Stage 'game' (Join-Path $gameEvidence 'verification.json')
    if($gameProof.Version -ne $version -or $gameProof.Cases.Count -ne 8){throw 'Incomplete package game matrix.'}
    foreach($package in $gameProof.Packages){
        $original=@($report.Packages|Where-Object Id -eq $package.Id)
        if($original.Count -ne 1 -or $original[0].Sha256 -ne $package.Sha256){throw 'Game consumer package bytes differ from shared delivery.'}
    }
    $viewportEvidence=Join-Path $evidence 'viewport'
    & (Join-Path $workspace 'ManualTests/AetherPackageConsumer/VerifyViewport.ps1') -PackageVerification (Join-Path $gameEvidence 'verification.json') -ValidationDirectory $validation -EvidenceDirectory $viewportEvidence -RunDirectory (Join-Path $run 'viewport') -DependencyFeed $gameProof.Feed
    $viewportProof=Add-Stage 'viewport' (Join-Path $viewportEvidence 'verification.json')
    if($viewportProof.PackageVersion -ne $version -or $viewportProof.Cases.Count -ne 2){throw 'Incomplete fresh native matrix.'}
    $testProjects=@(& git -C $workspace ls-files -- '*.Tests.csproj' | Sort-Object)
    if($LASTEXITCODE -or $testProjects.Count -ne 11){throw 'Unexpected test project discovery.'}
    foreach($project in $testProjects){
        foreach($configuration in @('Debug','Release')){
            $name=[IO.Path]::GetFileNameWithoutExtension($project)+'-'+$configuration
            $output=Join-Path $evidence $name
            [void][IO.Directory]::CreateDirectory($output)
            Invoke-Dotnet $name @('test',$project,'-c',$configuration,'--settings',(Join-Path $PSScriptRoot 'serial.runsettings'),'--logger','trx;LogFileName=tests.trx','--results-directory',$output,'--verbosity','minimal',"-bl:$evidence/$name-{}.binlog")
            [xml]$trx=Get-Content -LiteralPath (Join-Path $output 'tests.trx') -Raw
            $counters=$trx.TestRun.ResultSummary.Counters
            if([int]$counters.total -le 0 -or [int]$counters.passed -ne [int]$counters.total -or [int]$counters.failed -ne 0 -or [int]$counters.notExecuted -ne 0){throw "Non-green tests: $name"}
            $report.Tests+=@{Project=$project;Configuration=$configuration;Passed=[int]$counters.passed;Failed=[int]$counters.failed;Skipped=[int]$counters.notExecuted;Trx=(Join-Path $output 'tests.trx')};Save-Report
        }
    }
    foreach($entry in @($report.Sources)+@($report.Packages)){if((Get-FileHash -LiteralPath $entry.Path).Hash -ne $entry.Sha256){throw "Input drift: $($entry.Path)"}}
    $report.Completed=$true
}catch{$report.Failure=$_.ToString();throw}finally{Save-Report;Write-Host "Framework delivery evidence: $evidence"}
