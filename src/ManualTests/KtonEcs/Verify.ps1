#Requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath($(if([IO.Path]::IsPathRooted($EvidenceDirectory)){$EvidenceDirectory}else{Join-Path $workspace $EvidenceDirectory}))
if(Test-Path -LiteralPath $evidence){throw 'Use a fresh evidence directory.'}
$run=Join-Path $evidence 'run'
foreach($directory in @($evidence,$run,"$run/feed","$run/working")){[void][IO.Directory]::CreateDirectory($directory)}
$family=@('TrueMoon.Contracts','TrueMoon.Core','TrueMoon.Cobalt','TrueMoon','TrueMoon.Extensions.DependencyInjection',
 'TrueMoon.Aether.Abstractions','TrueMoon.Enerit.Generator','TrueMoon.Enerit','TrueMoon.Mithril','TrueMoon.Aether.Runtime',
 'TrueMoon.Kton','TrueMoon.Kton.Bepu','TrueMoon.Aether.Kton','TrueMoon.Aether.Kton.Runtime')
$feed=[IO.Path]::GetFullPath("$run/feed");$config="$run/NuGet.Config"
[IO.File]::WriteAllText($config,'<configuration><packageSources><clear/><add key="closed" value="'+[Security.SecurityElement]::Escape($feed)+'"/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders></configuration>')
$report=[ordered]@{Completed=$false;Stage='PHYS6';Version=$null;BuildProfile=$null;RunDirectory=$run;Feed=$feed;Commands=@();Reports=@();T3Report=$null;Packages=@();ExternalPackages=@();CharacterLicenseFiles=@();Graph=$null;Sources=@();Failure=$null;Scope='PHYS5 regression, PHYS6 capsule/auto-step, and T3 static mesh through ECS; headless CPU only. No native Player or session protocol acceptance.'}
$providers=@('Cobalt','Microsoft')
$qualities=@('Baseline60','FastLinear60')
$t3Cases=@('Flat','StairsUp','StairsDown','RampUp','RampDown','RampToRoom','DoorCenter','DoorPost','BackEdge','Ledge25','Ledge30')
$t3ArchiveSha='CB03D329829DC33FDCA344E1B2ED1607CC1CDCD8816DB66DC49FC6B402240AA1'
$autoStepNames=foreach($quality in $qualities){
 foreach($case in @('legacy-v1','default-v2','enabled-v2','tall-v2')){
  foreach($check in @('settings','settled','outcome')){"auto-step-$quality-$case-$check"}
 }
 "auto-step-$quality-cleanup"
}
$direct=@('TrueMoon.Aether.Kton.Runtime','TrueMoon.Kton.Bepu','TrueMoon','TrueMoon.Extensions.DependencyInjection')
$witnesses=@('TrueMoon.Kton','TrueMoon.Kton.Bepu','TrueMoon.Aether.Kton','TrueMoon.Aether.Kton.Runtime','TrueMoon.Aether.Runtime','BepuPhysics','BepuUtilities')
function Save-Report{$report|ConvertTo-Json -Depth 14|Set-Content -LiteralPath "$evidence/verification.json" -Encoding utf8}
function Invoke-Checked([string]$name,[string[]]$arguments,[string]$cache="$run/cache"){
 $msbuild=$arguments[0] -in @('build','publish','pack','restore','msbuild')
 if($msbuild){$arguments+="-bl:$evidence/$name-{}.binlog"}
 $info=[Diagnostics.ProcessStartInfo]::new('dotnet');$info.WorkingDirectory="$run/working";$info.UseShellExecute=$false;$info.CreateNoWindow=$true
 $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
 $info.Environment['NUGET_PACKAGES']=$cache;$info.Environment['NUGET_HTTP_CACHE_PATH']="$cache/http"
 foreach($argument in $arguments){[void]$info.ArgumentList.Add($argument)}
 $process=[Diagnostics.Process]::Start($info);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
 try{
  $timedOut=!$process.WaitForExit(180000)
  if($timedOut){$process.Kill($true);$process.WaitForExit()}
  [IO.File]::WriteAllText("$evidence/$name.stdout.log",$stdout.GetAwaiter().GetResult());[IO.File]::WriteAllText("$evidence/$name.stderr.log",$stderr.GetAwaiter().GetResult())
  $binlogs=@()
  if($msbuild){$binlogs=@(Get-ChildItem -LiteralPath $evidence -Filter "$name-*.binlog" -File|Where-Object Length -gt 0)}
  $report.Commands+=@{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode;Binlogs=@($binlogs|ForEach-Object FullName)};Save-Report
  if($timedOut){throw "Timed out and reaped: $name"}
  if($process.ExitCode){throw "$name failed. See its logs."}
  if($msbuild -and !$binlogs.Count){throw "$name produced no nonempty binlog."}
  Write-Host "$name passed"
 }finally{if(!$process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
function Consumer([string]$name,[bool]$fresh){
 $directory="$run/$name";Write-TrueMoonConsumerBuildProfile -Profile $profile -Directory $directory
 Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs'|Copy-Item -Destination $directory
 $project=[xml](Get-Content -LiteralPath "$PSScriptRoot/Probe.csproj" -Raw);$group=$project.CreateElement('ItemGroup')
 foreach($id in $direct){
  $element=$project.CreateElement($(if($fresh){'PackageReference'}else{'ProjectReference'}));$element.SetAttribute('Include',$(if($fresh){$id}else{"$workspace/$id/$id.csproj"}))
  if($fresh){$element.SetAttribute('Version',"[$version]")};[void]$group.AppendChild($element)
 }
 [void]$project.DocumentElement.AppendChild($group);$path="$directory/Probe.csproj";$project.Save($path);return $path
}
function Probe([string]$name,[string]$output,[string]$cache="$run/cache"){
 $path="$evidence/$name.json";Invoke-Checked $name @("$output/KtonEcsProbe.dll",$path) $cache
 $proof=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
 if($proof.Completed -cne $true -or $proof.Stage -cne 'PHYS6' -or $proof.NativeRendering -cne $false){
  throw "${name}: invalid stage/completion/rendering"
 }
 if($proof.CheckCount -ne 104 -or $proof.Checks.Count -ne 104){throw "${name}: expected 104 checks"}
 $checks=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
 foreach($check in $proof.Checks){
  if($check.Provider -cnotin $providers -or !$check.Name -or !$checks.Add("$($check.Provider)/$($check.Name)")){
   throw "${name}: unexpected or duplicate check $($check.Provider)/$($check.Name)"
  }
 }
 foreach($provider in $providers){
  $byProvider=@($proof.Checks|Where-Object Provider -CEQ $provider)
  $expectedCount=if($provider -ceq 'Cobalt'){51}else{53}
  if($byProvider.Count -ne $expectedCount -or @($byProvider|Where-Object Name -Like 'auto-step-*').Count -ne $autoStepNames.Count){
   throw "${name}: unexpected check totals for $provider"
  }
  foreach($check in $autoStepNames){
   if(!$checks.Contains("$provider/$check")){throw "${name}: missing $provider/$check"}
  }
 }
 $owners=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
 foreach($owner in $proof.Owners){
  $key="$($owner.Provider)/$($owner.Profile)"
  if($owner.Provider -cnotin $providers -or $owner.Profile -cnotin (@('legacy')+$qualities) -or !$owners.Add($key)){
   throw "${name}: unexpected or duplicate owner $key"
  }
  $resources=$owner.Resources
  if($resources.PublishedWorlds -ne 0 -or $resources.ActiveResourceOwners -ne 0 -or
      $resources.MaximumPoolBytesAfterCleanup -ne 0 -or $resources.PendingFailedCreationCleanup -cne $false){
   throw "${name}: cleanup incomplete for $key"
  }
 }
 if($owners.Count -ne 6){throw "${name}: expected 6 owners"}
 if(@($proof.Assemblies|Where-Object Name -match 'Tooling|Editor|Alloy|Argentis|Skia').Count){throw 'Unexpected workflow/UI dependency'}
 $report.Reports+=@{Name=$name;Path=$path;Checks=$proof.CheckCount;Sha256=(Get-FileHash -LiteralPath $path).Hash;RuntimeWitnesses=@()}
 Save-Report
 return $proof
}
function Assert-PackageFolder($graph,[string]$expected,[string]$label){
 $folders=@($graph.packageFolders.Keys)
 if($folders.Count -ne 1 -or [IO.Path]::GetFullPath($folders[0]).TrimEnd('/','\') -ine $expected){
  throw "${label}: expected only package folder $expected; got $($folders -join ', ')"
 }
}
function Get-ExactLibrary($graph,[string]$id,[string]$version){
 $entries=@($graph.libraries.Keys|Where-Object{($_ -split '/',2)[0] -ieq $id})
 if($entries.Count -ne 1 -or $entries[0] -ine "$id/$version"){
  throw "Fresh graph: expected only $id/$version; got $($entries -join ', ')"
 }
 return $entries[0]
}
function Get-ZipEntryHash($archive,[string]$path){
 $entries=@($archive.Entries|Where-Object FullName -CEQ $path)
 if($entries.Count -ne 1 -or !$entries[0].Length){throw "Expected one nonempty archive entry: $path"}
 $stream=$entries[0].Open()
 try{return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}
}
function Read-RuntimeWitnesses([string]$label,$loadedAssemblies,[string]$output){
 if(@($loadedAssemblies|Where-Object Name -match 'Tooling|Editor|Alloy|Argentis|Skia').Count){throw "${label}: unexpected workflow/UI dependency"}
 foreach($name in $witnesses){
  $assemblies=@($loadedAssemblies|Where-Object Name -IEQ $name)
  if($assemblies.Count -ne 1){throw "${label}: expected one loaded $name"}
  $assembly=$assemblies[0]
  $outputDll=[IO.Path]::GetFullPath("$output/$name.dll")
  if([IO.Path]::GetFullPath($assembly.Location) -ine $outputDll -or (Get-FileHash -LiteralPath $outputDll).Hash -ne $assembly.Sha256){
   throw "${label}: wrong output path or SHA for $name ($($assembly.Location))"
  }
  $isTrueMoon=$name.StartsWith('TrueMoon.',[StringComparison]::Ordinal)
  $expectedVersion=if($isTrueMoon){$version}else{$bepuVersion}
  $key=Get-ExactLibrary $graph $name $expectedVersion
  $targetKeys=@($target.Keys|Where-Object{$_ -ieq $key})
  if($targetKeys.Count -ne 1 -or !$target[$targetKeys[0]].Contains('runtime')){throw "${label}: missing runtime target for $key"}
  $entries=@($target[$targetKeys[0]].runtime.Keys|Where-Object{[IO.Path]::GetFileName($_) -ieq "$name.dll"})
  if($entries.Count -ne 1 -or ($isTrueMoon -and $entries[0] -cne "lib/$framework/$name.dll")){
   throw "${label}: unexpected runtime asset for ${key}: $($entries -join ', ')"
  }
  $asset=$entries[0]
  $packageDll=Join-Path $cache "$($graph.libraries[$key].path)/$asset"
  if((Get-FileHash -LiteralPath $packageDll).Hash -ne $assembly.Sha256){throw "${label}: cache runtime SHA mismatch: $key/$asset"}
  $archive=[IO.Compression.ZipFile]::OpenRead($feedArchives[$key])
  try{$archiveHash=Get-ZipEntryHash $archive $asset}finally{$archive.Dispose()}
  if($archiveHash -ne $assembly.Sha256){throw "${label}: feed runtime SHA mismatch: $key/$asset"}
  @{Name=$name;PackageId=($key -split '/',2)[0];Version=$expectedVersion;Asset=$asset;Location=$assembly.Location;Sha256=$assembly.Sha256}
 }
 foreach($assembly in $loadedAssemblies|Where-Object Name -NotIn $witnesses){
  $installed=@(Get-ChildItem -LiteralPath $cache -Recurse -Filter ($assembly.Name+'.dll')|Where-Object{(Get-FileHash -LiteralPath $_.FullName).Hash -eq $assembly.Sha256})
  if(!$installed.Count){throw "${label}: output DLL not from closed cache: $($assembly.Name)"}
 }
}
function Assert-T3Report($proof,[string]$fixtureSha,$fixtureData){
 if($proof.Stage -cne 'KTON-ECS-T3' -or $proof.Completed -isnot [bool] -or !$proof.Completed -or
    $proof.NativeRendering -isnot [bool] -or $proof.NativeRendering -or $null -ne $proof.Failure){
  throw 'T3: invalid stage/completion/rendering or non-null failure'
 }
 if($proof.FixtureSha256 -cne $fixtureSha -or $proof.ArchiveSha256 -cne $t3ArchiveSha -or
    $proof.ArchiveRead -isnot [bool] -or !$proof.ArchiveRead){
  throw 'T3: fixture SHA or verified archive provenance mismatch'
 }
 $sampleCounts=@{}
 foreach($scenario in $fixtureData.Scenarios){
  $count=1
  foreach($segment in $scenario.Input){$count+=$segment.Ticks}
  $sampleCounts[$scenario.Id]=$count
 }
 $rows=[Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
 foreach($row in $proof.Rows){
  $key="$($row.Provider)/$($row.Profile)/$($row.Case)/$($row.AutoStep)"
  if($row.Provider -cnotin $providers -or $row.Profile -cnotin $qualities -or $row.Case -cnotin $t3Cases -or
     $row.AutoStep -isnot [bool] -or !$rows.TryAdd($key,$row)){
   throw "T3: unexpected or duplicate row $key"
  }
  if($row.Passed -isnot [bool] -or !$row.Passed -or $row.RepeatExact -isnot [bool] -or !$row.RepeatExact -or
     $row.ProviderExact -isnot [bool] -or !$row.ProviderExact -or $row.Trace -isnot [array] -or
     $null -eq $row.Landmarks){
   throw "T3: failed, inexact, or incomplete row $key"
  }
  $expected=$sampleCounts[$row.Case]
  if($row.Trace.Length -ne $expected){throw "T3: expected $expected trace samples for $key; got $($row.Trace.Length)"}
  for($tick=0;$tick -lt $expected;$tick++){
   $sampleTick=$row.Trace[$tick].Tick
   if(($sampleTick -isnot [int] -and $sampleTick -isnot [long]) -or $sampleTick -ne $tick){
    throw "T3: expected sample tick $tick for $key; got $sampleTick"
   }
  }
 }
 if($rows.Count -ne 88){throw "T3: expected 88 unique rows, got $($rows.Count)"}
 $offOnExact=0
 foreach($provider in $providers){
  foreach($quality in $qualities){
   foreach($case in $t3Cases){
    $key="$provider/$quality/$case"
    $off=ConvertTo-Json -InputObject ($rows["$key/False"].Trace) -Compress -Depth 14
    $on=ConvertTo-Json -InputObject ($rows["$key/True"].Trace) -Compress -Depth 14
    $exact=$off -ceq $on
    if($exact){$offOnExact++}
    if($exact -ne ($case -cnotin @('StairsUp','Ledge25'))){throw "T3: unexpected off/on equality ($exact) for $key"}
   }
  }
 }
 if($offOnExact -ne 36 -or $offOnExact -cne $proof.OffOnExactCount){throw 'T3: expected 36 exact off/on pairs'}
 $owners=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
 foreach($owner in $proof.Owners){
  $key="$($owner.Provider)/$($owner.Profile)"
  if($owner.Provider -cnotin $providers -or $owner.Profile -cnotin $qualities -or !$owners.Add($key)){
   throw "T3: unexpected or duplicate cleanup row $key"
  }
  $resources=$owner.Resources
  if($resources.PublishedWorlds -ne 0 -or $resources.ActiveResourceOwners -ne 0 -or
     $resources.MaximumPoolBytesAfterCleanup -ne 0 -or
     $resources.PendingFailedCreationCleanup -isnot [bool] -or $resources.PendingFailedCreationCleanup){
   throw "T3: cleanup incomplete for $key"
  }
 }
 if($owners.Count -ne 4){throw "T3: expected 4 unique cleanup rows, got $($owners.Count)"}
}
function Probe-T3([string]$output,[string]$fixture,[string]$fixtureSha,$fixtureData){
 $name='t3-fresh-Release';$path="$evidence/$name.json"
 $entry=[ordered]@{Stage=$null;Path=$path;Sha256=$null;FixturePath=$fixture;FixtureSha256=$fixtureSha;FixtureSha256After=$null;
   ArchiveSha256=$t3ArchiveSha;Rows=0;OffOnExactCount=$null;RuntimeWitnesses=@()}
 $report.T3Report=$entry
 try{
  Invoke-Checked $name @("$output/KtonEcsProbe.dll",'--t3',$fixture,$path) $cache
 }finally{
  if(Test-Path -LiteralPath $path -PathType Leaf){$entry.Sha256=(Get-FileHash -LiteralPath $path).Hash}
  if(Test-Path -LiteralPath $fixture -PathType Leaf){$entry.FixtureSha256After=(Get-FileHash -LiteralPath $fixture).Hash}
 }
 if($entry.FixtureSha256After -cne $fixtureSha){throw 'T3: fixture drift during verification'}
 $proof=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
 $entry.Stage=$proof.Stage
 Assert-T3Report $proof $fixtureSha $fixtureData
 $entry.Rows=$proof.Rows.Count;$entry.OffOnExactCount=$proof.OffOnExactCount
 $entry.RuntimeWitnesses=@(Read-RuntimeWitnesses $name $proof.Assemblies $output)
}
try{
 $profile=Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory $evidence
 $version=$profile.Version
 $report.BuildProfile=$profile;$report.Version=$version
 Write-TrueMoonConsumerBuildProfile -Profile $profile -Directory "$run/working"
 $t3Fixture=Join-Path $PSScriptRoot 'T3Fixture.json'
 $t3FixtureSha=(Get-FileHash -LiteralPath $t3Fixture).Hash
 $fixture=Get-Content -LiteralPath $t3Fixture -Raw|ConvertFrom-Json
 if($fixture.ArchiveSha256 -cne $t3ArchiveSha){throw 'T3 fixture does not identify the verified source archive'}
 $report.Sources+=@{Path=$t3Fixture;Sha256=$t3FixtureSha}
 foreach($directory in @('TrueMoon.Kton','TrueMoon.Kton.Bepu','TrueMoon.Aether.Kton','TrueMoon.Aether.Kton.Runtime','ManualTests/KtonEcs')){
  foreach($file in Get-ChildItem -LiteralPath "$workspace/$directory" -File -Recurse|Where-Object{ $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in @('.cs','.csproj','.ps1','.md','.txt','.tt')}){
   $report.Sources+=@{Path=$file.FullName;Sha256=(Get-FileHash -LiteralPath $file.FullName).Hash}
  }
 }
 $report.Sources+=@{Path="$workspace/Directory.Build.props";Sha256=(Get-FileHash -LiteralPath "$workspace/Directory.Build.props").Hash}
 $source=Consumer 'source' $false
 $seedCache="$run/seed-cache";$seedArtifacts="$run/seed-artifacts";$sourceArtifacts="$run/source-artifacts"
 # Producer projects inherit the workspace Version; the generated probe is not a package.
 Invoke-Checked 'seed-restore' @('restore',$source,'--configfile',"$workspace/NuGet.Config",'--artifacts-path',$seedArtifacts) $seedCache
 $seedProjects=@($family+'TrueMoon.Cobalt.Generator'|ForEach-Object{[IO.Path]::GetFullPath("$workspace/$_/$_.csproj")})
 $seedProjects+=[IO.Path]::GetFullPath($source)
 $graphs=@{}
 foreach($file in Get-ChildItem -LiteralPath "$seedArtifacts/obj" -Recurse -File -Filter 'project.assets.json'){
  $asset=Get-Content -LiteralPath $file.FullName -Raw|ConvertFrom-Json -AsHashtable
  $projectPath=[IO.Path]::GetFullPath($asset.project.restore.projectPath)
  if($projectPath -notin $seedProjects){continue}
  if($graphs.ContainsKey($projectPath)){throw "Duplicate seed graph: $projectPath"}
  Assert-PackageFolder $asset ([IO.Path]::GetFullPath($seedCache)) $projectPath
  $graphs[$projectPath]=$asset
 }
 $external=@{}
 $feedArchives=@{}
 foreach($projectPath in $seedProjects){
  if(!$graphs.ContainsKey($projectPath)){throw "Missing seed graph: $projectPath"}
  foreach($library in $graphs[$projectPath].libraries.GetEnumerator()){
   if($library.Value.type -ne 'package'){continue}
   $key=$library.Key
   $id,$resolved=$key.ToLowerInvariant().Split('/')
   if($id -in $family -or $id -ieq 'TrueMoon.Cobalt.Generator'){throw "Unexpected own package in seed closure: $key"}
   $name="$id.$resolved.nupkg"
   $cached=Join-Path $seedCache "$($library.Value.path)/$name"
   if(!(Test-Path -LiteralPath $cached -PathType Leaf) -or !(Get-Item -LiteralPath $cached).Length){
    throw "Missing nonempty seed archive for ${key}: $cached"
   }
   if($external.ContainsKey($key) -and $external[$key].Path -eq $cached){continue}
   $hash=(Get-FileHash -LiteralPath $cached).Hash
   $destination=Join-Path $feed $name
   if(Test-Path -LiteralPath $destination){
    if((Get-FileHash -LiteralPath $destination).Hash -ne $hash){throw "Seed archive collision for ${key}: $name"}
   }else{Copy-Item -LiteralPath $cached -Destination $destination}
   if(!$external.ContainsKey($key)){
    $external[$key]=@{Id=$key;Path=$cached;Sha256=$hash}
    $feedArchives[$key]=$destination
    $report.ExternalPackages+=@{Id=$key;Path=$destination;Sha256=$hash}
   }
  }
 }
 Invoke-Checked 'source-restore' @('restore',$source,'--configfile',$config,'--artifacts-path',$sourceArtifacts)
 Invoke-Checked 'source-build' @('build',$source,'--no-restore','-c','Debug','--artifacts-path',$sourceArtifacts,'-o',"$run/source-debug",'--verbosity','quiet')
 $null=Probe 'source-debug' "$run/source-debug"
 Invoke-Checked 'source-publish' @('publish',$source,'--no-restore','-c','Release','--artifacts-path',$sourceArtifacts,'-o',"$run/source-release",'--self-contained','false','--verbosity','quiet')
 $null=Probe 'source-published' "$run/source-release"
 foreach($id in $family){
  Invoke-Checked "pack-$id" @('pack',"$workspace/$id/$id.csproj",'-c','Release','--no-restore','--artifacts-path',$sourceArtifacts,'-o',$feed,'--verbosity','quiet')
  $archive="$feed/$id.$version.nupkg";$report.Packages+=@{Id=$id;Version=$version;Path=$archive;Sha256=(Get-FileHash -LiteralPath $archive).Hash}
  $feedArchives["$id/$version"]=$archive
 }
 $archive=[IO.Compression.ZipFile]::OpenRead("$feed/TrueMoon.Kton.Bepu.$version.nupkg")
 try{
  $licenses=@()
  foreach($name in @('LICENSE.BEPU.txt','THIRD-PARTY.md','CharacterMotionConstraint.tt')){
   $entryPath="licenses/characters/$name"
   $hash=Get-ZipEntryHash $archive $entryPath
   if($hash -ne (Get-FileHash -LiteralPath "$workspace/TrueMoon.Kton.Bepu/Characters/$name").Hash){throw "Controller notice content mismatch: $name"}
   $licenses+=@{Path=$entryPath;Sha256=$hash}
  }
  $report.CharacterLicenseFiles=$licenses
 }finally{$archive.Dispose()}
 Save-Report
 $fresh=Consumer 'fresh' $true;$cache="$run/fresh-cache";$freshArtifacts="$run/fresh-artifacts"
 if((Test-Path -LiteralPath $cache) -or (Test-Path -LiteralPath $freshArtifacts)){throw 'Fresh cache or artifacts already exist.'}
 Invoke-Checked 'fresh-restore' @('restore',$fresh,'--configfile',$config,'--artifacts-path',$freshArtifacts) $cache
 $assetFiles=@(Get-ChildItem -LiteralPath "$freshArtifacts/obj" -Recurse -Filter project.assets.json)
 if($assetFiles.Count -ne 1){throw 'Expected one independent package project'}
 $graph=Get-Content -LiteralPath $assetFiles[0].FullName -Raw|ConvertFrom-Json -AsHashtable
 Assert-PackageFolder $graph ([IO.Path]::GetFullPath($cache)) 'fresh graph'
 if(@($graph.libraries.Values|Where-Object type -ne 'package').Count -ne 0){throw 'Fresh graph is not package-only'}
 $framework=$profile.Framework
 $restoreFramework=$graph.project.restore.frameworks[$framework]
 if($null -eq $restoreFramework -or $restoreFramework.projectReferences.Count -ne 0){throw 'Fresh graph is not independent'}
 $directDependencies=$graph.project.frameworks[$framework].dependencies
 if($directDependencies.Count -ne $direct.Count){throw 'Unexpected number of direct package references'}
 foreach($id in $direct){
  $dependency=$directDependencies[$id]
  if($null -eq $dependency -or $dependency.target -ne 'Package' -or $dependency.version -notin @("[$version]","[$version, $version]")){
   throw "Fresh graph: expected direct exact package reference $id [$version]"
  }
 }
 # Enerit.Generator is a build-time analyzer used to produce Runtime, not a consumer runtime dependency.
 foreach($id in $family){
  if($id -ne 'TrueMoon.Enerit.Generator'){$null=Get-ExactLibrary $graph $id $version}
 }
 $bepuVersion='2.5.0-beta.33'
 foreach($id in @('BepuPhysics','BepuUtilities')){$null=Get-ExactLibrary $graph $id $bepuVersion}
 foreach($library in $graph.libraries.GetEnumerator()){
  $metadata=Get-Content -LiteralPath (Join-Path $cache ($library.Value.path+'/.nupkg.metadata')) -Raw|ConvertFrom-Json
  if([IO.Path]::GetFullPath($metadata.source).TrimEnd('/','\') -ine $feed){throw "Unexpected package source for $($library.Key): $($metadata.source)"}
 }
 $report.Graph=@{Path=$assetFiles[0].FullName;Libraries=@($graph.libraries.Keys);ProjectReferences=0;PackageFolder=$cache;Sha256=(Get-FileHash -LiteralPath $assetFiles[0].FullName).Hash}
 $target=$graph.targets[$framework]
 if($null -eq $target){throw "Missing runtime target: $framework"}
 foreach($configuration in @('Debug','Release')){
  $output="$run/fresh-$configuration";$operation=if($configuration -eq 'Release'){'publish'}else{'build'}
  Invoke-Checked "fresh-$configuration-build" @($operation,$fresh,'--no-restore','-c',$configuration,'--artifacts-path',$freshArtifacts,'-o',$output,'--verbosity','quiet') $cache
  $proof=Probe "fresh-$configuration" $output $cache
  $report.Reports[-1].RuntimeWitnesses=@(Read-RuntimeWitnesses "fresh-$configuration" $proof.Assemblies $output)
  Save-Report
 }
 Probe-T3 "$run/fresh-Release" $t3Fixture $t3FixtureSha $fixture
 foreach($source in $report.Sources){if((Get-FileHash -LiteralPath $source.Path).Hash -ne $source.Sha256){throw "Source drift: $($source.Path)"}}
 foreach($profileSource in $profile.Sources){if((Get-FileHash -LiteralPath $profileSource.Path).Hash -ne $profileSource.Sha256){throw 'Shared profile drift'}}
 $report.Completed=$true
}catch{$report.Failure=$_.ToString();throw}finally{Save-Report}
