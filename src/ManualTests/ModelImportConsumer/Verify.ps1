#Requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'));$evidence=[IO.Path]::GetFullPath($EvidenceDirectory,$workspace)
if(Test-Path -LiteralPath $evidence){throw 'Use a fresh evidence directory.'}
$run=Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-ModelImport-'+[Guid]::NewGuid().ToString('N'))
foreach($path in @($evidence,$run,"$run/feed","$run/working")){[void][IO.Directory]::CreateDirectory($path)}
$profile=Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory $evidence
Write-TrueMoonConsumerBuildProfile -Profile $profile -Directory $run
$version=$profile.Version;$feed=[IO.Path]::GetFullPath("$run/feed");$dotnet=(Get-Command dotnet).Source
$family=@('TrueMoon.Contracts','TrueMoon.Core','TrueMoon.Cobalt','TrueMoon','TrueMoon.Extensions.DependencyInjection',
 'TrueMoon.Aether.Abstractions','TrueMoon.Enerit.Generator','TrueMoon.Enerit','TrueMoon.Mithril','TrueMoon.Aether.Runtime','TrueMoon.Aether.Tooling')
$roots=@('TrueMoon','TrueMoon.Extensions.DependencyInjection','TrueMoon.Aether.Tooling');$config="$run/NuGet.Config"
[IO.File]::WriteAllText($config,'<configuration><packageSources><clear/><add key="closed" value="'+[Security.SecurityElement]::Escape($feed)+'"/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders></configuration>')
$report=[ordered]@{Completed=$false;Stage='ARCH1g.1b';Utc=[DateTimeOffset]::UtcNow;Version=$version;BuildProfile=$profile;RunDirectory=$run;
 Sources=@();Packages=@();ExternalPackages=@();Commands=@();Profiles=@();Graph=$null;Worker=$null;Failure=$null;
 Scope='Windows x64 glTF2 static opaque-unlit CPU candidates. No stable IDs/ledger, Nom, build/runtime delivery, GPU or overall g.1 acceptance.'}
function Hash([string]$path){(Get-FileHash -LiteralPath $path).Hash}
function Save{$report|ConvertTo-Json -Depth 25|Set-Content "$evidence/verification.json" -Encoding utf8}
function Run([string]$name,[string[]]$arguments,[string]$cache="$run/source-cache"){
 if($arguments[0] -in @('build','publish','pack','restore','msbuild')){$arguments+="-bl:$evidence/$name-{}.binlog"}
 $info=[Diagnostics.ProcessStartInfo]::new($dotnet);$info.WorkingDirectory="$run/working";$info.UseShellExecute=$false;$info.CreateNoWindow=$true
 $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true;$info.Environment['NUGET_PACKAGES']=$cache;$info.Environment['NUGET_HTTP_CACHE_PATH']="$cache/http"
 foreach($argument in $arguments){[void]$info.ArgumentList.Add($argument)}
 $process=[Diagnostics.Process]::Start($info);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
 try{
  if(!$process.WaitForExit(240000)){$process.Kill($true);if(!$process.WaitForExit(10000)){throw "Failed to reap $name"};throw "Timed out $name"}
  [IO.File]::WriteAllText("$evidence/$name.stdout.log",$stdout.GetAwaiter().GetResult());[IO.File]::WriteAllText("$evidence/$name.stderr.log",$stderr.GetAwaiter().GetResult())
  $report.Commands+=@{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode};Save
  if($process.ExitCode -ne 0){throw "$name failed; see logs."};Write-Host "$name passed"
 }finally{if(!$process.HasExited){$process.Kill($true);if(!$process.WaitForExit(10000)){throw "Cleanup failed to reap $name"}};$process.Dispose()}
}
function Consumer([string]$name,[bool]$fresh){
 $directory="$run/$name";[void][IO.Directory]::CreateDirectory($directory)
 Copy-Item -LiteralPath "$PSScriptRoot/Program.cs" -Destination $directory
 Copy-Item -LiteralPath "$PSScriptRoot/../ModelImportResearch/Fixtures.cs" -Destination $directory
 $xml=[xml]'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><AssemblyName>ModelImportProbe</AssemblyName><RootNamespace>TrueMoon.ManualTests.ModelImportConsumer</RootNamespace><GenerateDocumentationFile>false</GenerateDocumentationFile></PropertyGroup><ItemGroup/></Project>'
 $xml.Project.PropertyGroup.AppendChild($xml.CreateElement('Version')).InnerText=$version;$items=$xml.DocumentElement.LastChild
 foreach($id in $roots){$node=$xml.CreateElement($(if($fresh){'PackageReference'}else{'ProjectReference'}));$node.SetAttribute('Include',$(if($fresh){$id}else{"$workspace/$id/$id.csproj"}));if($fresh){$node.SetAttribute('Version',"[$version]")};[void]$items.AppendChild($node)}
 $path="$directory/Probe.csproj";$xml.Save($path);return $path
}
function Proof([string]$name,[string]$output,[string]$cache,[bool]$fresh,[string]$worker="$run/worker-release"){
 foreach($provider in @('cobalt','microsoft')){
  $tag="$name-$provider";$directory="$evidence/$tag";Run $tag @("$output/ModelImportProbe.dll",$provider,$worker,"$run/fake",$dotnet,$directory) $cache
  $path="$directory/report.json";$proof=Get-Content $path -Raw|ConvertFrom-Json -AsHashtable
  if(!$proof.Completed -or $proof.Groups.Count -ne 5 -or $proof.ReapedWorkers -ne 21 -or $proof.Assertions -lt 87){throw 'Import proof mismatch'}
  if($proof.WorkerFiles['assimp.dll'] -ne '4EC73281A4FDDB735A0A59180EBFCEAB122EC8A8EF136814B2FCCCFB7B78E944'){throw 'Native drift'}
  $report.Profiles+=@{Name=$tag;Path=$path;Sha256=(Hash $path);Provider=$provider;Assertions=$proof.Assertions;ReapedWorkers=$proof.ReapedWorkers;Revision=$proof.Revision;Worker=$worker}
  if($fresh){foreach($assembly in $proof.LoadedFramework.GetEnumerator()){
   $matches=@(Get-ChildItem $cache -Recurse -Filter ($assembly.Key+'.dll')|Where-Object {(Hash $_.FullName) -eq $assembly.Value});if(!$matches.Count){throw "Loaded DLL outside fresh cache: $($assembly.Key)"}
  }}
 }
}
try{
 foreach($id in @($family+'TrueMoon.Cobalt.Generator'+'TrueMoon.Aether.Importing.Assimp'+'ManualTests/ModelImportConsumer')){
  foreach($file in Get-ChildItem "$workspace/$id" -File -Recurse|Where-Object {$_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in @('.cs','.csproj','.md','.ps1','.tt','.txt','.props','.targets')}){$report.Sources+=@{Path=$file.FullName;Sha256=(Hash $file.FullName)}}
 }
 $report.Sources+=@{Path="$workspace/ManualTests/ModelImportResearch/Fixtures.cs";Sha256=(Hash "$workspace/ManualTests/ModelImportResearch/Fixtures.cs")}
 $external=@{}
 foreach($id in @($family+'TrueMoon.Cobalt.Generator'+'TrueMoon.Aether.Importing.Assimp')){
  $assets=Get-Content "$workspace/$id/obj/project.assets.json" -Raw|ConvertFrom-Json -AsHashtable
  foreach($library in $assets.libraries.GetEnumerator()){
   if($library.Value.type -ne 'package' -or $external.ContainsKey($library.Key)){continue};$parts=$library.Key.Split('/');$cached=$null
   foreach($folder in $assets.packageFolders.Keys){$candidate=Join-Path $folder ($library.Value.path+'/'+$parts[0]+'.'+$parts[1]+'.nupkg');if(Test-Path $candidate){$cached=$candidate;break}}
   if(!$cached){throw "Missing cached archive: $($library.Key)"};Copy-Item -LiteralPath $cached -Destination $feed;$external[$library.Key]=$cached;$report.ExternalPackages+=@{Id=$library.Key;Sha256=(Hash $cached)}
  }
 }
 if($external.Keys -notcontains 'AssimpNetter/6.0.6'){throw 'Pinned Assimp archive missing'}
 if((Hash $external['AssimpNetter/6.0.6']) -ne 'AAE39E82146260CF191B0C50F76A60E39DD9F33326C0CB7174646356A79B31ED'){throw 'Assimp archive changed'}
 $workerProject="$workspace/TrueMoon.Aether.Importing.Assimp/TrueMoon.Aether.Importing.Assimp.csproj";$workerArtifacts="$run/worker-artifacts"
 # SDK downloadDependencies are not ordinary libraries in project.assets.json. Resolve their exact SDK-selected versions without downloading.
 Run 'worker-graph' @('msbuild',$workerProject,'-target:GenerateRestoreGraphFile',"-p:RestoreGraphOutputPath=$run/worker.restoregraph.json",'-p:RuntimeIdentifier=win-x64','-p:SelfContained=false','-p:UseAppHost=false',"-p:ArtifactsPath=$workerArtifacts",'-v:quiet')
 $restoreGraph=Get-Content "$run/worker.restoregraph.json" -Raw|ConvertFrom-Json -AsHashtable
 $cacheFolders=(Get-Content "$workspace/TrueMoon.Aether.Importing.Assimp/obj/project.assets.json" -Raw|ConvertFrom-Json -AsHashtable).packageFolders.Keys
 foreach($project in $restoreGraph.projects.Values){foreach($framework in $project.frameworks.Values){
  if(!$framework.ContainsKey('downloadDependencies')){continue}
  foreach($download in $framework.downloadDependencies){
   $range=$download.version.Replace('[','').Replace(']','').Replace(' ','').Split(',');$sdkVersion=$range[0]
   if($range.Count -eq 2 -and $range[1] -ne $sdkVersion){throw 'Expected exact SDK download version'}
   $key=$download.name+'/'+$sdkVersion;if($external.ContainsKey($key)){continue};$cached=$null
   foreach($folder in $cacheFolders){$candidate=Join-Path $folder ($download.name.ToLowerInvariant()+'/'+$sdkVersion+'/'+$download.name.ToLowerInvariant()+'.'+$sdkVersion+'.nupkg');if(Test-Path $candidate){$cached=$candidate;break}}
   if(!$cached){throw "Missing cached SDK-selected archive: $key"}
   Copy-Item -LiteralPath $cached -Destination $feed;$external[$key]=$cached;$report.ExternalPackages+=@{Id=$key;Role='SDK download dependency';Sha256=(Hash $cached)}
  }
 }}
 # dotnet restore -r sets the plural RuntimeIdentifiers and eagerly downloads unused runtime packs.
 # Match publish's single-RID framework-dependent profile exactly.
 Run 'worker-restore' @('restore',$workerProject,'-p:RuntimeIdentifier=win-x64','-p:SelfContained=false','-p:UseAppHost=false','--configfile',$config,'--artifacts-path',$workerArtifacts)
 foreach($configuration in @('Debug','Release')){Run "worker-$configuration" @('publish',$workerProject,'--no-restore','-c',$configuration,'-r','win-x64','--self-contained','false','-p:UseAppHost=false','--artifacts-path',$workerArtifacts,'-o',"$run/worker-$($configuration.ToLowerInvariant())",'-v:quiet')}
 Run 'fake-publish' @('publish',"$PSScriptRoot/FakeWorker/FakeWorker.csproj",'-c','Release','--artifacts-path',"$run/fake-artifacts",'-o',"$run/fake",'--configfile',$config,'-p:UseAppHost=false','-v:quiet')
 $source=Consumer 'source' $false;$artifacts="$run/source-artifacts"
 Run 'source-restore' @('restore',$source,'--configfile',$config,'--artifacts-path',$artifacts)
 Run 'source-build' @('build',$source,'--no-restore','-c','Debug','--artifacts-path',$artifacts,'-o',"$run/source-debug",'-v:quiet')
 Proof 'source-debug' "$run/source-debug" "$run/source-cache" $false
 Proof 'worker-debug' "$run/source-debug" "$run/source-cache" $false "$run/worker-debug"
 Run 'source-publish' @('publish',$source,'--no-restore','-c','Release','--artifacts-path',$artifacts,'-o',"$run/source-release",'--self-contained','false','-v:quiet')
 Proof 'source-release' "$run/source-release" "$run/source-cache" $false
 foreach($id in $family){Run "pack-$id" @('pack',"$workspace/$id/$id.csproj",'-c','Release','--no-restore','--artifacts-path',$artifacts,'-o',$feed,'-v:quiet');$path="$feed/$id.$version.nupkg";$report.Packages+=@{Id=$id;Version=$version;Path=$path;Sha256=(Hash $path)}}
 $fresh=Consumer 'fresh' $true;$cache="$run/fresh-cache";$freshArtifacts="$run/fresh-artifacts"
 Run 'fresh-restore' @('restore',$fresh,'--configfile',$config,'--artifacts-path',$freshArtifacts) $cache
 $files=@(Get-ChildItem "$freshArtifacts/obj" -Recurse -Filter project.assets.json);if($files.Count -ne 1){throw 'Expected one package-only consumer'}
 $graph=Get-Content $files[0].FullName -Raw|ConvertFrom-Json -AsHashtable
 if(@($graph.libraries.Values|Where-Object type -ne 'package').Count -ne 0 -or $graph.packageFolders.Count -ne 1 -or @($graph.libraries.Keys|Where-Object {$_ -like '*Assimp*'}).Count -ne 0){throw 'Fresh graph is not isolated/package-only/native-free'}
 foreach($key in $graph.libraries.Keys){if($key -like 'TrueMoon*' -and !$key.EndsWith('/'+$version)){throw "Version drift: $key"}}
 foreach($library in $graph.libraries.Values){$metadata=Get-Content (Join-Path $cache ($library.path+'/.nupkg.metadata')) -Raw|ConvertFrom-Json;if([IO.Path]::GetFullPath($metadata.source).TrimEnd('/','\') -ne $feed){throw 'Unexpected package source'}}
 $report.Graph=@{ProjectReferences=0;Libraries=@($graph.libraries.Keys);Sha256=(Hash $files[0].FullName)}
 foreach($configuration in @('Debug','Release')){$operation=if($configuration -eq 'Debug'){'build'}else{'publish'};$output="$run/fresh-$configuration";Run "fresh-$configuration-build" @($operation,$fresh,'--no-restore','-c',$configuration,'--artifacts-path',$freshArtifacts,'-o',$output,'-v:quiet') $cache;Proof "fresh-$configuration" $output $cache $true}
 if(@($report.Profiles.Revision|Sort-Object -Unique).Count -ne 1){throw 'CPU revision differs between worker/build/provider profiles'}
 foreach($source in @($report.Sources)+@($profile.Sources)){if((Hash $source.Path) -ne $source.Sha256){throw "Source drift: $($source.Path)"}}
 $delivery="$evidence/delivery";[void][IO.Directory]::CreateDirectory($delivery)
 Copy-Item -LiteralPath $feed -Destination "$delivery/feed" -Recurse
 Copy-Item -LiteralPath "$run/worker-release" -Destination "$delivery/worker" -Recurse
 $manifest=@{};foreach($file in Get-ChildItem "$delivery/worker" -File -Recurse){$manifest[[IO.Path]::GetRelativePath("$delivery/worker",$file.FullName).Replace('\','/')]=Hash $file.FullName}
 $manifest|ConvertTo-Json -Depth 5|Set-Content "$delivery/worker-manifest.json" -Encoding utf8
 if(!(Test-Path "$delivery/worker/TrueMoon.LICENSE") -or !(Test-Path "$delivery/worker/AssimpNetter.License.txt")){throw 'Worker licenses missing'}
 $report.Worker=@{Path="$delivery/worker";Manifest="$delivery/worker-manifest.json";Sha256=(Hash "$delivery/worker-manifest.json")}
 $report.Completed=$true;Save
}catch{$report.Failure=$_.ToString();Save;throw}
