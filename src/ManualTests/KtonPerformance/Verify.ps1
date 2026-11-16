#Requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory,[string]$ComparisonReport)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath((Join-Path $workspace $EvidenceDirectory))
if(Test-Path -LiteralPath $evidence){throw 'Use a fresh evidence directory.'}
$run=Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-PHYS81-'+[Guid]::NewGuid().ToString('N'))
foreach($directory in @($evidence,$run,"$run/feed","$run/working")){[void][IO.Directory]::CreateDirectory($directory)}
$profile=Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory $evidence
$version=$profile.Version
$family=@('TrueMoon.Contracts','TrueMoon.Core','TrueMoon.Cobalt','TrueMoon','TrueMoon.Extensions.DependencyInjection',
 'TrueMoon.Aether.Abstractions','TrueMoon.Enerit.Generator','TrueMoon.Enerit','TrueMoon.Mithril','TrueMoon.Aether.Runtime',
 'TrueMoon.Kton','TrueMoon.Kton.Bepu','TrueMoon.Aether.Kton','TrueMoon.Aether.Kton.Runtime')
$feed=[IO.Path]::GetFullPath("$run/feed");$config="$run/NuGet.Config"
[IO.File]::WriteAllText($config,'<configuration><packageSources><clear/><add key="closed" value="'+[Security.SecurityElement]::Escape($feed)+'"/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders></configuration>')
$report=[ordered]@{Completed=$false;Stage='PHYS8.3';Version=$version;BuildProfile=$profile;RunDirectory=$run;Commands=@();Reports=@();Packages=@();ExternalPackages=@();Graph=$null;Sources=@();BeforeGraph=$null;BeforePackages=@();
 Machine=@{Cpu=[Microsoft.Win32.Registry]::GetValue('HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0','ProcessorNameString',$null)};Failure=$null;
 Scope='Bounded Windows x64 macrobaseline plus optional allocation attribution against immutable earlier packages;1worker/current limits. Debug smoke and Release baseline are separate. No numeric readiness gate.'}
function Save-Report{$report|ConvertTo-Json -Depth 18|Set-Content -LiteralPath "$evidence/verification.json" -Encoding utf8}
function Invoke-Checked([string]$name,[string[]]$arguments,[string]$cache="$run/cache"){
 if($arguments[0] -in @('build','publish','pack','restore','msbuild')){$arguments+="-bl:$evidence/$name-{}.binlog"}
 $info=[Diagnostics.ProcessStartInfo]::new('dotnet');$info.WorkingDirectory="$run/working";$info.UseShellExecute=$false;$info.CreateNoWindow=$true
 $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
 $info.Environment['NUGET_PACKAGES']=$cache;$info.Environment['NUGET_HTTP_CACHE_PATH']="$cache/http"
 foreach($argument in $arguments){[void]$info.ArgumentList.Add($argument)}
 $process=[Diagnostics.Process]::Start($info);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
 try{
  if(!$process.WaitForExit(300000)){$process.Kill($true);$process.WaitForExit();throw "Timed out and reaped: $name"}
  [IO.File]::WriteAllText("$evidence/$name.stdout.log",$stdout.GetAwaiter().GetResult());[IO.File]::WriteAllText("$evidence/$name.stderr.log",$stderr.GetAwaiter().GetResult())
  $report.Commands+=@{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode};Save-Report
  if($process.ExitCode){throw "$name failed. See its logs."};Write-Host "$name passed"
 }finally{if(!$process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
function Consumer([string]$name,[bool]$fresh,[string]$cohort=$version){
 $directory="$run/$name";Write-TrueMoonConsumerBuildProfile -Profile $profile -Directory $directory
 Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs'|Copy-Item -Destination $directory
 $project=[xml](Get-Content -LiteralPath "$PSScriptRoot/Probe.csproj" -Raw);$group=$project.CreateElement('ItemGroup')
 foreach($id in @('TrueMoon.Aether.Kton.Runtime','TrueMoon.Kton.Bepu','TrueMoon','TrueMoon.Extensions.DependencyInjection')){
  $element=$project.CreateElement($(if($fresh){'PackageReference'}else{'ProjectReference'}));$element.SetAttribute('Include',$(if($fresh){$id}else{"$workspace/$id/$id.csproj"}))
  if($fresh){$element.SetAttribute('Version',"[$cohort]")};[void]$group.AppendChild($element)
 }
 [void]$project.DocumentElement.AppendChild($group);$path="$directory/Probe.csproj";$project.Save($path);return $path
}
function Probe([string]$name,[string]$output,[string]$mode,[string]$cache="$run/cache"){
 $path="$evidence/$name.json";Invoke-Checked $name @("$output/KtonPerformanceProbe.dll",$path,$mode) $cache
 $proof=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
 $expected=if($mode -eq 'baseline'){28}else{12}
 if(!$proof.Completed -or @($proof.Results).Count -ne $expected){throw 'Incomplete performance matrix'}
 foreach($entry in $proof.Results){
  if($entry.ResourcesAfterStop.PublishedWorlds -ne 0 -or $entry.ResourcesAfterStop.ActiveResourceOwners -ne 0 -or $entry.ResourcesAfterStop.MaximumPoolBytesAfterCleanup -ne 0){throw 'Cleanup not complete'}
 }
 if(@($proof.Assemblies|Where-Object Name -match 'Tooling|Editor|Alloy|Argentis|Skia').Count){throw 'Unexpected workflow/UI dependency'}
 $report.Reports+=@{Name=$name;Mode=$mode;Path=$path;Workloads=$expected;Sha256=(Get-FileHash -LiteralPath $path).Hash};Save-Report
}
try{
 foreach($directory in @('TrueMoon.Aether.Runtime','TrueMoon.Kton','TrueMoon.Kton.Bepu','TrueMoon.Aether.Kton','TrueMoon.Aether.Kton.Runtime','ManualTests/KtonPerformance')){
  foreach($file in Get-ChildItem -LiteralPath "$workspace/$directory" -File -Recurse|Where-Object{ $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in @('.cs','.csproj','.ps1','.md','.tt','.txt')}){
   $report.Sources+=@{Path=$file.FullName;Sha256=(Get-FileHash -LiteralPath $file.FullName).Hash}
  }
 }
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
 $source=Consumer 'source' $false;$artifacts="$run/source-artifacts"
 Invoke-Checked 'source-restore' @('restore',$source,'--configfile',$config,'--artifacts-path',$artifacts,"-p:Version=$version")
 Invoke-Checked 'source-build' @('build',$source,'--no-restore','-c','Debug','--artifacts-path',$artifacts,'-o',"$run/source-debug","-p:Version=$version",'--verbosity','quiet')
 Probe 'source-debug' "$run/source-debug" 'smoke'
 Invoke-Checked 'source-publish' @('publish',$source,'--no-restore','-c','Release','--artifacts-path',$artifacts,'-o',"$run/source-release","-p:Version=$version",'--self-contained','false','--verbosity','quiet')
 Probe 'source-published' "$run/source-release" 'baseline'
 foreach($id in $family){
  Invoke-Checked "pack-$id" @('pack',"$workspace/$id/$id.csproj",'-c','Release','--no-restore','--artifacts-path',$artifacts,'-o',$feed,"-p:Version=$version",'--verbosity','quiet')
  $archive="$feed/$id.$version.nupkg";$report.Packages+=@{Id=$id;Version=$version;Path=$archive;Sha256=(Get-FileHash -LiteralPath $archive).Hash}
 }
 $fresh=Consumer 'fresh' $true;$cache="$run/fresh-cache";$freshArtifacts="$run/fresh-artifacts"
 Invoke-Checked 'fresh-restore' @('restore',$fresh,'--configfile',$config,'--artifacts-path',$freshArtifacts) $cache
 $assetFiles=@(Get-ChildItem -LiteralPath "$freshArtifacts/obj" -Recurse -Filter project.assets.json)
 if($assetFiles.Count -ne 1){throw 'Expected one independent package project'}
 $graph=Get-Content -LiteralPath $assetFiles[0].FullName -Raw|ConvertFrom-Json -AsHashtable
 if(@($graph.libraries.Values|Where-Object type -ne 'package').Count -ne 0 -or $graph.packageFolders.Count -ne 1){throw 'Fresh graph is not package-only and isolated'}
 foreach($id in @($family|Where-Object{$_ -ne 'TrueMoon.Enerit.Generator'})){if(!$graph.libraries.ContainsKey("$id/$version")){throw "Stale/missing package: $id"}}
 foreach($id in @('BepuPhysics','BepuUtilities')){if(!$graph.libraries.ContainsKey("$id/2.5.0-beta.33")){throw 'BEPU version drift'}}
 foreach($library in $graph.libraries.Values){$metadata=Get-Content -LiteralPath (Join-Path $cache ($library.path+'/.nupkg.metadata')) -Raw|ConvertFrom-Json;if([IO.Path]::GetFullPath($metadata.source).TrimEnd('/','\') -ne $feed){throw 'Unexpected package source'}}
 $report.Graph=@{Libraries=@($graph.libraries.Keys);ProjectReferences=0;Sha256=(Get-FileHash -LiteralPath $assetFiles[0].FullName).Hash}
 foreach($configuration in @('Debug','Release')){
  $output="$run/fresh-$configuration";$operation=if($configuration -eq 'Release'){'publish'}else{'build'}
  Invoke-Checked "fresh-$configuration-build" @($operation,$fresh,'--no-restore','-c',$configuration,'--artifacts-path',$freshArtifacts,'-o',$output,'--verbosity','quiet') $cache
  Probe "fresh-$configuration" $output $(if($configuration -eq 'Release'){'baseline'}else{'smoke'}) $cache
  foreach($assembly in (Get-Content -LiteralPath "$evidence/fresh-$configuration.json" -Raw|ConvertFrom-Json).Assemblies){
   if(!@(Get-ChildItem -LiteralPath $cache -Recurse -Filter ($assembly.Name+'.dll')|Where-Object{(Get-FileHash -LiteralPath $_.FullName).Hash -eq $assembly.Sha256}).Count){throw "Output DLL not from isolated cache: $($assembly.Name)"}
  }
 }
 Probe 'after-attribution' "$run/fresh-Release" 'attribution' $cache
 if($ComparisonReport){
  $beforePath=[IO.Path]::GetFullPath($ComparisonReport,$workspace)
  $before=Get-Content -LiteralPath $beforePath -Raw|ConvertFrom-Json
  if(!$before.Completed -or $before.Version -eq $version){throw 'Comparison requires an earlier completed, distinct package cohort'}
  foreach($key in @('Sdk','Framework','LanguageVersion','Features')){if($before.BuildProfile.$key -ne $profile.$key){throw "Comparison profile mismatch: $key"}}
  $report.BeforeVerification=@{Path=$beforePath;Version=$before.Version;Sha256=(Get-FileHash -LiteralPath $beforePath).Hash}
  foreach($package in $before.Packages){
   if((Get-FileHash -LiteralPath $package.Path).Hash -ne $package.Sha256){throw "Before archive hash mismatch: $($package.Id)"}
   $destination=Join-Path $feed ([IO.Path]::GetFileName($package.Path))
   if(Test-Path -LiteralPath $destination){throw 'Comparison cannot overwrite a feed archive'}
   Copy-Item -LiteralPath $package.Path -Destination $destination
   $report.BeforePackages+=@{Id=$package.Id;Version=$package.Version;Sha256=$package.Sha256}
  }
  $beforeProject=Consumer 'before' $true $before.Version;$beforeCache="$run/before-cache";$beforeArtifacts="$run/before-artifacts"
  Invoke-Checked 'before-restore' @('restore',$beforeProject,'--configfile',$config,'--artifacts-path',$beforeArtifacts) $beforeCache
  $beforeAssets=@(Get-ChildItem -LiteralPath "$beforeArtifacts/obj" -Recurse -Filter project.assets.json)
  if($beforeAssets.Count -ne 1){throw 'Unexpected comparison project graph'}
  $beforeGraph=Get-Content -LiteralPath $beforeAssets[0].FullName -Raw|ConvertFrom-Json -AsHashtable
  if(@($beforeGraph.libraries.Values|Where-Object type -ne 'package').Count -ne 0 -or $beforeGraph.packageFolders.Count -ne 1){throw 'Comparison graph is not isolated/package-only'}
  foreach($id in @($family|Where-Object{$_ -ne 'TrueMoon.Enerit.Generator'})){if(!$beforeGraph.libraries.ContainsKey("$id/$($before.Version)")){throw "Comparison package drift: $id"}}
  foreach($library in $beforeGraph.libraries.Values){$metadata=Get-Content -LiteralPath (Join-Path $beforeCache ($library.path+'/.nupkg.metadata')) -Raw|ConvertFrom-Json;if([IO.Path]::GetFullPath($metadata.source).TrimEnd('/','\') -ne $feed){throw 'Unexpected comparison package source'}}
  $report.BeforeGraph=@{Libraries=@($beforeGraph.libraries.Keys);ProjectReferences=0;Sha256=(Get-FileHash -LiteralPath $beforeAssets[0].FullName).Hash}
  Invoke-Checked 'before-publish' @('publish',$beforeProject,'--no-restore','-c','Release','--artifacts-path',$beforeArtifacts,'-o',"$run/before-release",'--verbosity','quiet') $beforeCache
  Probe 'before-attribution' "$run/before-release" 'attribution' $beforeCache
  foreach($assembly in (Get-Content -LiteralPath "$evidence/before-attribution.json" -Raw|ConvertFrom-Json).Assemblies){
   if(!@(Get-ChildItem -LiteralPath $beforeCache -Recurse -Filter ($assembly.Name+'.dll')|Where-Object{(Get-FileHash -LiteralPath $_.FullName).Hash -eq $assembly.Sha256}).Count){throw "Comparison DLL not from isolated cache: $($assembly.Name)"}
  }
 }
 foreach($sourceFile in @($report.Sources)+@($profile.Sources)){if((Get-FileHash -LiteralPath $sourceFile.Path).Hash -ne $sourceFile.Sha256){throw "Source changed during probe: $($sourceFile.Path)"}}
 $report.Completed=$true;Save-Report
}catch{$report.Failure=$_.ToString();Save-Report;throw}
