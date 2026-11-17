#Requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory,[Parameter(Mandatory)][string]$ValidationDirectory)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory,$workspace)
$validation=[IO.Path]::GetFullPath($ValidationDirectory,$workspace)
if(Test-Path $evidence){throw 'Use a fresh evidence directory.'}
foreach($file in @('VkLayer_khronos_validation.json','VkLayer_khronos_validation.dll')){if(!(Test-Path "$validation/$file")){throw 'An existing validation layer is required.'}}
$run=Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-PHYS84-'+[Guid]::NewGuid().ToString('N'))
foreach($path in @($evidence,$run,"$run/feed","$run/working")){[void][IO.Directory]::CreateDirectory($path)}
$profile=Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory $evidence
Write-TrueMoonConsumerBuildProfile -Profile $profile -Directory $run
$version=$profile.Version;$feed=[IO.Path]::GetFullPath("$run/feed")
$family=@('TrueMoon.Contracts','TrueMoon.Core','TrueMoon.Cobalt','TrueMoon','TrueMoon.Extensions.DependencyInjection',
 'TrueMoon.Aether.Abstractions','TrueMoon.Enerit.Generator','TrueMoon.Enerit','TrueMoon.Mithril','TrueMoon.Aether.Runtime',
 'TrueMoon.Kton','TrueMoon.Kton.Bepu','TrueMoon.Aether.Kton','TrueMoon.Aether.Kton.Runtime',
 'TrueMoon.Argentis','TrueMoon.Alloy','TrueMoon.Alloy.Rendering.Skia','TrueMoon.Alloy.Platform.Silk','TrueMoon.Alloy.Hosting')
$roots=@('TrueMoon','TrueMoon.Extensions.DependencyInjection','TrueMoon.Aether.Kton.Runtime','TrueMoon.Kton.Bepu','TrueMoon.Alloy.Hosting','TrueMoon.Cobalt.Generator')
$config="$run/NuGet.Config"
[IO.File]::WriteAllText($config,'<configuration><packageSources><clear/><add key="closed" value="'+[Security.SecurityElement]::Escape($feed)+'"/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders></configuration>')
$report=[ordered]@{Completed=$false;Stage='PHYS8.4';Utc=[DateTimeOffset]::UtcNow;Version=$version;BuildProfile=$profile;RunDirectory=$run;
 Sources=@();Validation=@();Packages=@();ExternalPackages=@();Commands=@();Profiles=@();Graph=$null;Failure=$null;
 Scope='Windows x64 local real PrimitiveGameSession/Kton plus Vulkan/OpenGL surface; stepped capsule proxy; routed input, not physical keyboard. No new production API, native code or framework version. No timing SLA, other OS, device-loss or overall PHYS8 readiness.'}
function Hash([string]$path){(Get-FileHash -LiteralPath $path).Hash}
function Save{$report|ConvertTo-Json -Depth 20|Set-Content "$evidence/verification.json" -Encoding utf8}
function Run([string]$name,[string[]]$arguments,[string]$cache="$run/source-cache"){
 if($arguments[0] -in @('build','publish','pack','restore','msbuild')){$arguments+="-bl:$evidence/$name-{}.binlog"}
 $info=[Diagnostics.ProcessStartInfo]::new('dotnet');$info.WorkingDirectory="$run/working";$info.UseShellExecute=$false;$info.CreateNoWindow=$true
 $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
 $info.Environment['NUGET_PACKAGES']=$cache;$info.Environment['NUGET_HTTP_CACHE_PATH']="$cache/http";$info.Environment['VK_LAYER_PATH']=$validation
 foreach($argument in $arguments){[void]$info.ArgumentList.Add($argument)}
 $process=[Diagnostics.Process]::Start($info);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
 try{
  if(!$process.WaitForExit(240000)){$process.Kill($true);$process.WaitForExit();throw "Timed out and reaped: $name"}
  [IO.File]::WriteAllText("$evidence/$name.stdout.log",$stdout.GetAwaiter().GetResult());[IO.File]::WriteAllText("$evidence/$name.stderr.log",$stderr.GetAwaiter().GetResult())
  $report.Commands+=@{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode};Save
  if($process.ExitCode -ne 0){throw "$name failed; see its logs."};Write-Host "$name passed"
 }finally{if(!$process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
function Consumer([string]$name,[bool]$fresh){
 $directory="$run/$name";[void][IO.Directory]::CreateDirectory($directory)
 Copy-Item "$PSScriptRoot/*.cs" $directory
 $xml=[xml]'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><AssemblyName>KtonVisualProbe</AssemblyName><RootNamespace>TrueMoon.ManualTests.KtonVisual</RootNamespace><GenerateDocumentationFile>false</GenerateDocumentationFile></PropertyGroup><ItemGroup/></Project>'
 $xml.Project.PropertyGroup.AppendChild($xml.CreateElement('Version')).InnerText=$version
 $items=$xml.DocumentElement.LastChild
 foreach($id in $roots){
  $reference=if($fresh -and $id -eq 'TrueMoon.Cobalt.Generator'){'TrueMoon.Cobalt'}else{$id}
  $node=$xml.CreateElement($(if($fresh){'PackageReference'}else{'ProjectReference'}))
  $node.SetAttribute('Include',$(if($fresh){$reference}else{"$workspace/$id/$id.csproj"}))
  if($fresh){$node.SetAttribute('Version',"[$version]")}
  if($id -eq 'TrueMoon.Cobalt.Generator'){
   if(!$fresh){$node.SetAttribute('PrivateAssets','all');$node.SetAttribute('OutputItemType','Analyzer');$node.SetAttribute('ReferenceOutputAssembly','false')}
  }
  [void]$items.AppendChild($node)
 }
 foreach($file in @('NotoSans-Regular.ttf','OFL.txt')){
  $node=$xml.CreateElement('Content');$node.SetAttribute('Include',"$workspace/ManualTests/AlloyTest/Assets/Fonts/$file")
  $node.SetAttribute('Link',"Assets/Fonts/$file");$node.SetAttribute('CopyToOutputDirectory','PreserveNewest');$node.SetAttribute('CopyToPublishDirectory','PreserveNewest');[void]$items.AppendChild($node)
 }
 $path="$directory/Probe.csproj";$xml.Save($path);return $path
}
function Proof([string]$name,[string]$output,[string]$cache){
 foreach($provider in @('cobalt','microsoft')){
  foreach($mode in @('cpu','verify')){
   $tag="$name-$provider-$mode";$directory="$evidence/$tag";$command=if($provider -eq 'microsoft'){"$mode-msdi"}else{$mode}
   Run $tag @("$output/KtonVisualProbe.dll",$command,$directory) $cache
   $path="$directory/verification.json";$proof=Get-Content $path -Raw|ConvertFrom-Json
   if(!$proof.Completed){throw "Incomplete $tag"}
   if($mode -eq 'cpu' -and $proof.Count -ne 12){throw 'CPU assertion set drift'}
   if($mode -eq 'verify'){
    if($proof.Captures.Count -ne 12 -or $proof.GpuOwners -ne 0 -or $proof.Handles -ne 0 -or $proof.UiSessions -ne 0 -or $proof.Physics.PublishedWorlds -ne 0){throw 'Incomplete native/cleanup proof'}
    foreach($surface in $proof.RetiredSurfaces){if(!$surface.ValidationEnabled -or $surface.Errors -ne 0 -or $surface.Warnings -ne 0 -or $surface.LiveSlots -ne 0 -or $surface.DevicesCreated -ne $surface.DevicesReleased){throw 'Validation or ownership failure'}}
   }
   $report.Profiles+=@{Name=$tag;Path=$path;Sha256=(Hash $path);Mode=$mode;Provider=$provider}
   if($name -like 'fresh*'){
    foreach($assembly in $proof.Assemblies){
     $matches=@(Get-ChildItem $cache -Recurse -Filter ($assembly.Name+'.dll')|Where-Object {(Hash $_.FullName) -eq $assembly.Sha256})
     if(!$matches.Count){throw "Loaded DLL outside isolated cache: $($assembly.Name)"}
    }
   }
  }
 }
}
try{
 foreach($id in @($family+'TrueMoon.Cobalt.Generator'+'ManualTests/KtonVisual')){
  foreach($file in Get-ChildItem "$workspace/$id" -File -Recurse|Where-Object {$_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in @('.cs','.csproj','.md','.ps1','.tt','.txt')}){
   $report.Sources+=@{Path=$file.FullName;Sha256=(Hash $file.FullName)}
  }
 }
 foreach($path in @('TrueMoon.Alloy.Rendering.Skia/NativeAssets/win-x64/libSkiaSharp.dll','TrueMoon.Alloy.Rendering.Skia/NativeAssets/win-x64/libSkiaSharp.build.json','ManualTests/AlloyTest/Assets/Fonts/NotoSans-Regular.ttf','ManualTests/AlloyTest/Assets/Fonts/OFL.txt')){
  $report.Sources+=@{Path="$workspace/$path";Sha256=(Hash "$workspace/$path")}
 }
 foreach($file in @('VkLayer_khronos_validation.json','VkLayer_khronos_validation.dll')){$report.Validation+=@{Path="$validation/$file";Sha256=(Hash "$validation/$file")}}
 $external=@{}
 foreach($id in @($family+'TrueMoon.Cobalt.Generator')){
  $assets=Get-Content "$workspace/$id/obj/project.assets.json" -Raw|ConvertFrom-Json -AsHashtable
  foreach($library in $assets.libraries.GetEnumerator()){
   if($library.Value.type -ne 'package' -or $external.ContainsKey($library.Key)){continue}
   $parts=$library.Key.Split('/');$cached=$null
   foreach($folder in $assets.packageFolders.Keys){$candidate=Join-Path $folder ($library.Value.path+'/'+$parts[0]+'.'+$parts[1]+'.nupkg');if(Test-Path $candidate){$cached=$candidate;break}}
   if(!$cached){throw "Missing cached external archive: $($library.Key)"}
   Copy-Item $cached $feed;$external[$library.Key]=$cached;$report.ExternalPackages+=@{Id=$library.Key;Sha256=(Hash $cached)}
  }
 }
 $source=Consumer 'source' $false;$artifacts="$run/source-artifacts"
 Run 'source-restore' @('restore',$source,'--configfile',$config,'--artifacts-path',$artifacts,"-p:Version=$version")
 Run 'source-build' @('build',$source,'--no-restore','-c','Debug','--artifacts-path',$artifacts,'-o',"$run/source-debug","-p:Version=$version",'--verbosity','quiet')
 Proof 'source-debug' "$run/source-debug" "$run/source-cache"
 Run 'source-publish' @('publish',$source,'--no-restore','-c','Release','--artifacts-path',$artifacts,'-o',"$run/source-release","-p:Version=$version",'--self-contained','false','--verbosity','quiet')
 Proof 'source-release' "$run/source-release" "$run/source-cache"
 foreach($id in $family){
  Run "pack-$id" @('pack',"$workspace/$id/$id.csproj",'-c','Release','--no-restore','--artifacts-path',$artifacts,'-o',$feed,"-p:Version=$version",'--verbosity','quiet')
  $path="$feed/$id.$version.nupkg";$report.Packages+=@{Id=$id;Version=$version;Path=$path;Sha256=(Hash $path)}
 }
 $fresh=Consumer 'fresh' $true;$cache="$run/fresh-cache";$freshArtifacts="$run/fresh-artifacts"
 Run 'fresh-restore' @('restore',$fresh,'--configfile',$config,'--artifacts-path',$freshArtifacts) $cache
 $files=@(Get-ChildItem "$freshArtifacts/obj" -Recurse -Filter project.assets.json)
 if($files.Count -ne 1){throw 'Expected a single package-only consumer'}
 $graph=Get-Content $files[0].FullName -Raw|ConvertFrom-Json -AsHashtable
 if(@($graph.libraries.Values|Where-Object type -ne 'package').Count -ne 0 -or $graph.packageFolders.Count -ne 1){throw 'Fresh graph is not isolated/package-only'}
 foreach($key in $graph.libraries.Keys){if($key -like 'TrueMoon*' -and !$key.EndsWith('/'+$version)){throw "Version drift: $key"}}
 foreach($library in $graph.libraries.Values){$metadata=Get-Content (Join-Path $cache ($library.path+'/.nupkg.metadata')) -Raw|ConvertFrom-Json;if([IO.Path]::GetFullPath($metadata.source).TrimEnd('/','\') -ne $feed){throw 'Unexpected package source'}}
 $report.Graph=@{ProjectReferences=0;Libraries=@($graph.libraries.Keys);Sha256=(Hash $files[0].FullName)}
 foreach($configuration in @('Debug','Release')){
  $operation=if($configuration -eq 'Debug'){'build'}else{'publish'};$output="$run/fresh-$configuration"
  Run "fresh-$configuration-build" @($operation,$fresh,'--no-restore','-c',$configuration,'--artifacts-path',$freshArtifacts,'-o',$output,'--verbosity','quiet') $cache
  Proof "fresh-$configuration" $output $cache
 }
 foreach($source in @($report.Sources)+@($profile.Sources)+@($report.Validation)){if((Hash $source.Path) -ne $source.Sha256){throw "Source drift: $($source.Path)"}}
 $report.Completed=$true;Save
}catch{$report.Failure=$_.ToString();Save;throw}
