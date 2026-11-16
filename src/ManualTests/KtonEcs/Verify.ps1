#Requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath((Join-Path $workspace $EvidenceDirectory))
if(Test-Path -LiteralPath $evidence){throw 'Use a fresh evidence directory.'}
$run=Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-PHYS5-'+[Guid]::NewGuid().ToString('N'))
foreach($directory in @($evidence,$run,"$run/feed","$run/working")){[void][IO.Directory]::CreateDirectory($directory)}
$profile=Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory $evidence
$version=$profile.Version
$family=@('TrueMoon.Contracts','TrueMoon.Core','TrueMoon.Cobalt','TrueMoon','TrueMoon.Extensions.DependencyInjection',
 'TrueMoon.Aether.Abstractions','TrueMoon.Enerit.Generator','TrueMoon.Enerit','TrueMoon.Mithril','TrueMoon.Aether.Runtime',
 'TrueMoon.Kton','TrueMoon.Kton.Bepu','TrueMoon.Aether.Kton','TrueMoon.Aether.Kton.Runtime')
$feed=[IO.Path]::GetFullPath("$run/feed");$config="$run/NuGet.Config"
[IO.File]::WriteAllText($config,'<configuration><packageSources><clear/><add key="closed" value="'+[Security.SecurityElement]::Escape($feed)+'"/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders></configuration>')
$report=[ordered]@{Completed=$false;Stage='PHYS6';Version=$version;BuildProfile=$profile;RunDirectory=$run;Commands=@();Reports=@();Packages=@();ExternalPackages=@();Graph=$null;Sources=@();Failure=$null;Scope='PHYS5 regression and PHYS6 capsule controller scene integration; headless CPU snapshots. No new GPU/physical/device-loss acceptance.'}
function Save-Report{$report|ConvertTo-Json -Depth 14|Set-Content -LiteralPath "$evidence/verification.json" -Encoding utf8}
function Invoke-Checked([string]$name,[string[]]$arguments,[string]$cache="$run/cache"){
 if($arguments[0] -in @('build','publish','pack','restore','msbuild')){$arguments+="-bl:$evidence/$name-{}.binlog"}
 $info=[Diagnostics.ProcessStartInfo]::new('dotnet');$info.WorkingDirectory="$run/working";$info.UseShellExecute=$false;$info.CreateNoWindow=$true
 $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
 $info.Environment['NUGET_PACKAGES']=$cache;$info.Environment['NUGET_HTTP_CACHE_PATH']="$cache/http"
 foreach($argument in $arguments){[void]$info.ArgumentList.Add($argument)}
 $process=[Diagnostics.Process]::Start($info);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
 try{
  if(!$process.WaitForExit(180000)){$process.Kill($true);$process.WaitForExit();throw "Timed out and reaped: $name"}
  [IO.File]::WriteAllText("$evidence/$name.stdout.log",$stdout.GetAwaiter().GetResult());[IO.File]::WriteAllText("$evidence/$name.stderr.log",$stderr.GetAwaiter().GetResult())
  $report.Commands+=@{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode};Save-Report
  if($process.ExitCode){throw "$name failed. See its logs."};Write-Host "$name passed"
 }finally{if(!$process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
function Consumer([string]$name,[bool]$fresh){
 $directory="$run/$name";Write-TrueMoonConsumerBuildProfile -Profile $profile -Directory $directory
 Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs'|Copy-Item -Destination $directory
 $project=[xml](Get-Content -LiteralPath "$PSScriptRoot/Probe.csproj" -Raw);$group=$project.CreateElement('ItemGroup')
 foreach($id in @('TrueMoon.Aether.Kton.Runtime','TrueMoon.Kton.Bepu','TrueMoon','TrueMoon.Extensions.DependencyInjection')){
  $element=$project.CreateElement($(if($fresh){'PackageReference'}else{'ProjectReference'}));$element.SetAttribute('Include',$(if($fresh){$id}else{"$workspace/$id/$id.csproj"}))
  if($fresh){$element.SetAttribute('Version',"[$version]")};[void]$group.AppendChild($element)
 }
 [void]$project.DocumentElement.AppendChild($group);$path="$directory/Probe.csproj";$project.Save($path);return $path
}
function Probe([string]$name,[string]$output,[string]$cache="$run/cache"){
 $path="$evidence/$name.json";Invoke-Checked $name @("$output/KtonEcsProbe.dll",$path) $cache
 $proof=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
 if(!$proof.Completed -or @($proof.Checks.Provider|Sort-Object -Unique).Count -ne 2){throw 'Incomplete both-DI report'}
 foreach($owner in $proof.Owners){if($owner.Resources.PublishedWorlds -ne 0 -or $owner.Resources.ActiveResourceOwners -ne 0 -or $owner.Resources.MaximumPoolBytesAfterCleanup -ne 0){throw 'Native cleanup not complete'}}
 if(@($proof.Assemblies|Where-Object Name -match 'Tooling|Editor|Alloy|Argentis|Skia').Count){throw 'Unexpected workflow/UI dependency'}
 $report.Reports+=@{Name=$name;Path=$path;Checks=$proof.CheckCount;Sha256=(Get-FileHash -LiteralPath $path).Hash};Save-Report
}
try{
 foreach($directory in @('TrueMoon.Kton','TrueMoon.Kton.Bepu','TrueMoon.Aether.Kton','TrueMoon.Aether.Kton.Runtime','ManualTests/KtonEcs')){
  foreach($file in Get-ChildItem -LiteralPath "$workspace/$directory" -File -Recurse|Where-Object{ $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in @('.cs','.csproj','.ps1','.md','.txt','.tt')}){
   $report.Sources+=@{Path=$file.FullName;Sha256=(Get-FileHash -LiteralPath $file.FullName).Hash}
  }
 }
 $report.Sources+=@{Path="$workspace/Directory.Build.props";Sha256=(Get-FileHash -LiteralPath "$workspace/Directory.Build.props").Hash}
 $external=@{}
 foreach($id in @($family+'TrueMoon.Cobalt.Generator')){
  $assets=Get-Content -LiteralPath "$workspace/$id/obj/project.assets.json" -Raw|ConvertFrom-Json -AsHashtable
  foreach($library in $assets.libraries.GetEnumerator()){
   if($library.Value.type -ne 'package' -or $external.ContainsKey($library.Key)){continue}
   $parts=$library.Key.Split('/');$cached=$null
   foreach($folder in $assets.packageFolders.Keys){$candidate=Join-Path $folder ($library.Value.path+'/'+$parts[0]+'.'+$parts[1]+'.nupkg');if(Test-Path -LiteralPath $candidate){$cached=$candidate;break}}
   if(!$cached){throw "Missing cached exact external archive: $($library.Key)"}
   Copy-Item -LiteralPath $cached -Destination $feed;$external[$library.Key]=$cached
   $report.ExternalPackages+=@{Id=$library.Key;Sha256=(Get-FileHash -LiteralPath $cached).Hash}
  }
 }
 $source=Consumer 'source' $false;$sourceArtifacts="$run/source-artifacts"
 Invoke-Checked 'source-restore' @('restore',$source,'--configfile',$config,'--artifacts-path',$sourceArtifacts,"-p:Version=$version")
 Invoke-Checked 'source-build' @('build',$source,'--no-restore','-c','Debug','--artifacts-path',$sourceArtifacts,'-o',"$run/source-debug","-p:Version=$version",'--verbosity','quiet')
 Probe 'source-debug' "$run/source-debug"
 Invoke-Checked 'source-publish' @('publish',$source,'--no-restore','-c','Release','--artifacts-path',$sourceArtifacts,'-o',"$run/source-release","-p:Version=$version",'--self-contained','false','--verbosity','quiet')
 Probe 'source-published' "$run/source-release"
 foreach($id in $family){
  Invoke-Checked "pack-$id" @('pack',"$workspace/$id/$id.csproj",'-c','Release','--no-restore','--artifacts-path',$sourceArtifacts,'-o',$feed,"-p:Version=$version",'--verbosity','quiet')
  $archive="$feed/$id.$version.nupkg";$report.Packages+=@{Id=$id;Version=$version;Path=$archive;Sha256=(Get-FileHash -LiteralPath $archive).Hash}
 }
 $archive=[IO.Compression.ZipFile]::OpenRead("$feed/TrueMoon.Kton.Bepu.$version.nupkg")
 try{
  $report.CharacterLicenseFiles=@()
  foreach($name in @('LICENSE.BEPU.txt','THIRD-PARTY.md','CharacterMotionConstraint.tt')){
   $entry=$archive.GetEntry("licenses/characters/$name");if(!$entry){throw "Missing controller license/provenance: $name"}
   $stream=$entry.Open();try{$hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}
   if($hash -ne (Get-FileHash -LiteralPath "$workspace/TrueMoon.Kton.Bepu/Characters/$name").Hash){throw "Controller notice content mismatch: $name"}
   $report.CharacterLicenseFiles+=@{Path=$entry.FullName;Sha256=$hash}
  }
 }finally{$archive.Dispose()}
 Save-Report
 $fresh=Consumer 'fresh' $true;$cache="$run/fresh-cache";$freshArtifacts="$run/fresh-artifacts"
 Invoke-Checked 'fresh-restore' @('restore',$fresh,'--configfile',$config,'--artifacts-path',$freshArtifacts) $cache
 $assetFiles=@(Get-ChildItem -LiteralPath "$freshArtifacts/obj" -Recurse -Filter project.assets.json)
 if($assetFiles.Count -ne 1){throw 'Expected one independent package project'}
 $graph=Get-Content -LiteralPath $assetFiles[0].FullName -Raw|ConvertFrom-Json -AsHashtable
 if(@($graph.libraries.Values|Where-Object type -ne 'package').Count -ne 0 -or $graph.packageFolders.Count -ne 1){throw 'Fresh graph is not package-only and isolated'}
 # Enerit.Generator is a build-time analyzer used to produce Runtime, not a consumer runtime dependency.
 foreach($id in @($family|Where-Object{$_ -ne 'TrueMoon.Enerit.Generator'})){if(!$graph.libraries.ContainsKey("$id/$version")){throw "Stale/missing local package: $id"}}
 foreach($id in @('BepuPhysics','BepuUtilities')){if(!$graph.libraries.ContainsKey("$id/2.5.0-beta.33")){throw 'BEPU version drift'}}
 foreach($library in $graph.libraries.Values){$metadata=Get-Content -LiteralPath (Join-Path $cache ($library.path+'/.nupkg.metadata')) -Raw|ConvertFrom-Json;if([IO.Path]::GetFullPath($metadata.source).TrimEnd('/','\') -ne $feed){throw 'Unexpected package source'}}
 $report.Graph=@{Libraries=@($graph.libraries.Keys);ProjectReferences=0;Sha256=(Get-FileHash -LiteralPath $assetFiles[0].FullName).Hash}
 foreach($configuration in @('Debug','Release')){
  $output="$run/fresh-$configuration";$operation=if($configuration -eq 'Release'){'publish'}else{'build'}
  Invoke-Checked "fresh-$configuration-build" @($operation,$fresh,'--no-restore','-c',$configuration,'--artifacts-path',$freshArtifacts,'-o',$output,'--verbosity','quiet') $cache
  Probe "fresh-$configuration" $output $cache
  foreach($assembly in (Get-Content -LiteralPath "$evidence/fresh-$configuration.json" -Raw|ConvertFrom-Json).Assemblies){
   $installed=@(Get-ChildItem -LiteralPath $cache -Recurse -Filter ($assembly.Name+'.dll')|Where-Object{(Get-FileHash -LiteralPath $_.FullName).Hash -eq $assembly.Sha256})
   if(!$installed.Count){throw "Output DLL not from closed cache: $($assembly.Name)"}
  }
 }
 foreach($source in $report.Sources){if((Get-FileHash -LiteralPath $source.Path).Hash -ne $source.Sha256){throw "Source drift: $($source.Path)"}}
 foreach($profileSource in $profile.Sources){if((Get-FileHash -LiteralPath $profileSource.Path).Hash -ne $profileSource.Sha256){throw 'Shared profile drift'}}
 $report.Completed=$true
}catch{$report.Failure=$_.ToString();throw}finally{Save-Report}
