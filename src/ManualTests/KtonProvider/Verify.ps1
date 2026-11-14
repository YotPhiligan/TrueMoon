[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory,[string]$PackageDirectory)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if($PSVersionTable.PSVersion.Major -lt 7){throw 'PowerShell 7 is required.'}
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath((Join-Path $workspace $EvidenceDirectory))
if(Test-Path -LiteralPath $evidence){throw 'Use a fresh evidence directory.'}
[void][IO.Directory]::CreateDirectory($evidence)
$summary=[ordered]@{Completed=$false;Utc=[DateTimeOffset]::UtcNow;Profile=$null;Sources=@();Commands=@();Reports=@();Failure=$null;Packages=@();Scope='PHYS3 real BEPU bounded queries and gameplay contacts, with PHYS2 regression. App/ECS unimplemented.'}
$sourceProject=Join-Path $workspace 'TrueMoon.Kton.Bepu/TrueMoon.Kton.Bepu.csproj'
$artifacts=Join-Path $evidence 'source-artifacts'
$feed=Join-Path $evidence 'feed';[void][IO.Directory]::CreateDirectory($feed)
$cache=Join-Path $evidence 'packages';$config=Join-Path $evidence 'NuGet.Config'
[IO.File]::WriteAllText($config,"<configuration><packageSources><clear/><add key='kton' value='$([Security.SecurityElement]::Escape($feed))'/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders></configuration>")
function Invoke-Checked([string]$name,[string[]]$arguments){
    $build=$arguments[0] -in 'restore','build','pack','publish'
    if($build){$arguments+="-bl:$evidence/$name-{}.binlog"}
    $info=[Diagnostics.ProcessStartInfo]::new('dotnet');$info.WorkingDirectory=$workspace;$info.UseShellExecute=$false;$info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($arg in $arguments){[void]$info.ArgumentList.Add($arg)}
    $process=[Diagnostics.Process]::new();$process.StartInfo=$info;$started=$false
    Write-Host "Starting $name"
    try{
        $started=$process.Start();if(!$started){throw "Could not start $name"}
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        $timer=[Diagnostics.Stopwatch]::StartNew()
        while(!$process.WaitForExit(1000)){
            if($timer.Elapsed.TotalSeconds -ge 120){$process.Kill($true);$process.WaitForExit();throw "Timeout, reaped: $name"}
            if([int]$timer.Elapsed.TotalSeconds % 10 -eq 0){Write-Host "$name running $([int]$timer.Elapsed.TotalSeconds)s"}
        }
        $out=$stdout.GetAwaiter().GetResult();$err=$stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path $evidence "$name.stdout.log"),$out);[IO.File]::WriteAllText((Join-Path $evidence "$name.stderr.log"),$err)
        $summary.Commands+=@{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode;Seconds=$timer.Elapsed.TotalSeconds}
        if($process.ExitCode -ne 0){
            $errors=@($out -split '\r?\n'|Where-Object{$_ -match ': error ' }|Select-Object -Unique -First 12)
            if($errors.Count){$errors|ForEach-Object{Write-Host $_}}else{$out -split '\r?\n'|Select-Object -Last 12|ForEach-Object{Write-Host $_}}
            Write-Host $err;throw "$name failed: $($process.ExitCode)"
        }
        if($build -and !(Get-ChildItem -LiteralPath $evidence -Filter "$name-*.binlog")){throw "Missing binlog: $name"}
        Write-Host "Completed $name"
    }finally{if($started -and !$process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
function New-Consumer([string]$name,[string]$reference){
    $directory=Join-Path $evidence $name
    Write-TrueMoonConsumerBuildProfile -Profile $summary.Profile -Directory $directory
    Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs'|Copy-Item -Destination $directory
    $project=[xml](Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Probe.csproj') -Raw)
    $group=$project.CreateElement('ItemGroup');$element=$project.CreateElement($reference)
    if($reference -eq 'ProjectReference'){$element.SetAttribute('Include',$sourceProject)}
    else{$element.SetAttribute('Include','TrueMoon.Kton.Bepu');$element.SetAttribute('Version',"[$packageVersion]")}
    [void]$group.AppendChild($element);[void]$project.DocumentElement.AppendChild($group)
    $path=Join-Path $directory 'Probe.csproj';$project.Save($path);return $path
}
function Run-Probe([string]$name,[string]$dll,[bool]$fresh){
    $path=Join-Path $evidence "$name.json";Invoke-Checked $name @($dll,$path)
    $report=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
    if(!$report.Completed -or $report.Metrics.Cleanup.ActiveResourceOwners -ne 0 -or $report.Metrics.Cleanup.PublishedWorlds -ne 0){throw "Unexpected report: $name"}
    foreach($assembly in $report.Assemblies){
        if(@($assembly.References|Where-Object{$_ -match 'Aether|Alloy|Cobalt|Argentis|Silk|Skia'}).Count){throw 'Unexpected engine/UI reference'}
        if($fresh){$package=@($summary.Packages|Where-Object Id -eq $assembly.Name);if($package.Count -ne 1 -or $assembly.Sha256 -ne $package[0].DllSha256){throw "Loaded DLL mismatch: $($assembly.Name)"}}
    }
    $summary.Reports+=@{Name=$name;Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash;Checks=$report.CheckCount}
}
try{
    . (Join-Path $PSScriptRoot '../BuildProfile.ps1')
    $summary.Profile=Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory (Join-Path $evidence 'profile')
    $packageVersion=$summary.Profile.Version+'.phys3.'+[DateTime]::UtcNow.ToString('yyyyMMddHHmmss')
    foreach($directory in @($PSScriptRoot,(Join-Path $workspace 'TrueMoon.Kton'),(Join-Path $workspace 'TrueMoon.Kton.Bepu'),(Join-Path $workspace 'TrueMoon.Contracts'))){
        $summary.Sources+=@(Get-ChildItem -LiteralPath $directory -Recurse -File|Where-Object{$_.FullName -notmatch '[\\/](obj|bin)[\\/]'}|ForEach-Object{
            @{Path=$_.FullName.Substring($workspace.Length+1).Replace('\','/');Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
        })
    }
    $bepu=@{BepuPhysics='9C1DB047CD7B3597CD808D7B94A1EF6626E0041E305BB3D8FE41AA658C3F1D03';BepuUtilities='D1CF1BB261720FEF05D197B4DE9F17E06BAC6D2A6C7574A6DE88BC21122C26EA'}
    foreach($id in $bepu.Keys){
        $file=$id.ToLowerInvariant()+'.2.5.0-beta.33.nupkg';$path=Join-Path $feed $file
        if($PackageDirectory){Copy-Item -LiteralPath (Join-Path $PackageDirectory $file) -Destination $path}
        else{Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/$($id.ToLowerInvariant())/2.5.0-beta.33/$file" -OutFile $path -TimeoutSec 60}
        if((Get-FileHash -LiteralPath $path).Hash -ne $bepu[$id]){throw "BEPU archive mismatch: $id"}
    }
    $source=New-Consumer 'source-consumer' 'ProjectReference'
    Invoke-Checked 'source-restore' @('restore',$source,'--configfile',$config,'--packages',$cache,'--artifacts-path',$artifacts,'-p:NuGetAudit=false')
    $sourceOutput=Join-Path $evidence 'source-Debug'
    Invoke-Checked 'source-build-Debug' @('build',$source,'--no-restore','-c','Debug','--artifacts-path',$artifacts,'-o',$sourceOutput)
    Run-Probe 'source-Debug' (Join-Path $sourceOutput 'KtonProviderProbe.dll') $false
    foreach($id in @('TrueMoon.Contracts','TrueMoon.Kton','TrueMoon.Kton.Bepu')){
        Invoke-Checked "pack-$id" @('pack',(Join-Path $workspace "$id/$id.csproj"),'--no-restore','-c','Release','--artifacts-path',$artifacts,'-o',$feed,"-p:Version=$packageVersion")
    }
    foreach($archive in Get-ChildItem -LiteralPath $feed -Filter '*.nupkg'){
        $zip=[IO.Compression.ZipFile]::OpenRead($archive.FullName)
        try{
            $nuspec=@($zip.Entries|Where-Object FullName -like '*.nuspec');$reader=[IO.StreamReader]::new($nuspec[0].Open())
            try{$xml=[xml]$reader.ReadToEnd()}finally{$reader.Dispose()}
            $metadata=$xml.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]');$id=[string]$metadata.id
            $dll=@($zip.Entries|Where-Object FullName -like "lib/*/$id.dll");if($dll.Count -ne 1){throw "Unexpected DLL layout: $id"}
            $stream=$dll[0].Open();try{$sha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}
            $summary.Packages+=@{Id=$id;Version=[string]$metadata.version;Sha256=(Get-FileHash -LiteralPath $archive.FullName).Hash;DllSha256=$sha;
                Dependencies=@($xml.SelectNodes('//*[local-name()="dependency"]')|ForEach-Object{@{Id=$_.id;Version=$_.version}})}
        }finally{$zip.Dispose()}
    }
    $fresh=New-Consumer 'package-consumer' 'PackageReference';$freshArtifacts=Join-Path $evidence 'package-artifacts'
    Invoke-Checked 'package-restore' @('restore',$fresh,'--configfile',$config,'--packages',$cache,'--artifacts-path',$freshArtifacts)
    $assets=@(Get-ChildItem -LiteralPath (Join-Path $freshArtifacts 'obj') -Recurse -Filter project.assets.json)
    $graph=Get-Content -LiteralPath $assets[0].FullName -Raw|ConvertFrom-Json -AsHashtable
    if($assets.Count -ne 1 -or $graph.libraries.Count -ne 5){throw 'Expected exact 5-package graph'}
    foreach($id in @('BepuPhysics','BepuUtilities')){if(!$graph.libraries.ContainsKey("$id/2.5.0-beta.33")){throw 'BEPU pin drift'}}
    foreach($id in @('TrueMoon.Contracts','TrueMoon.Kton','TrueMoon.Kton.Bepu')){if(!$graph.libraries.ContainsKey("$id/$packageVersion")){throw 'TrueMoon package-family drift'}}
    foreach($configuration in @('Debug','Release')){
        $output=Join-Path $evidence "package-$configuration"
        if($configuration -eq 'Debug'){Invoke-Checked 'package-build-Debug' @('build',$fresh,'--no-restore','-c',$configuration,'--artifacts-path',$freshArtifacts,'-o',$output)}
        else{Invoke-Checked 'package-publish-Release' @('publish',$fresh,'--no-restore','-c',$configuration,'--self-contained','false','--artifacts-path',$freshArtifacts,'-o',$output)}
        Run-Probe "package-$configuration" (Join-Path $output 'KtonProviderProbe.dll') $true
    }
    foreach($source in $summary.Sources){if((Get-FileHash -LiteralPath (Join-Path $workspace $source.Path)).Hash -ne $source.Sha256){throw "Changed source: $($source.Path)"}}
    $summary.Completed=$true
}catch{$summary.Failure=$_.ToString();throw}
finally{$summary|ConvertTo-Json -Depth 14|Set-Content -LiteralPath (Join-Path $evidence 'verification.json') -Encoding utf8}
