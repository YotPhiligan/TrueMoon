[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory)
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
$trueMoonBuildProfile = Get-TrueMoonBuildProfile -Workspace ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))


$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if(![OperatingSystem]::IsWindows() -or [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne 'X64'){throw 'The executed NOM7 delivery scope is Windows x64.'}
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath((Join-Path $workspace $EvidenceDirectory))
if(Test-Path -LiteralPath $evidence){throw 'Use a fresh evidence directory.'}
$run=Join-Path ([IO.Path]::GetTempPath()) ('Nom-PackageDelivery-'+[Guid]::NewGuid().ToString('N'))
$feed=Join-Path $run 'feed';$work=Join-Path $run 'unrelated working directory';$packArtifacts=Join-Path $run 'pack-artifacts'
foreach($path in @($evidence,$run,$feed,$work)){[void][IO.Directory]::CreateDirectory($path)}
$version='0.2.5.1-alpha.nomstructure.'+[DateTime]::UtcNow.ToString('yyyyMMddHHmmss')+'.r'+[Guid]::NewGuid().ToString('N').Substring(0,8)
$packages=@('TrueMoon.Contracts','TrueMoon.Cobalt','TrueMoon','TrueMoon.Nom')
$summary=[ordered]@{Completed=$false;RunDirectory=$run;Version=$version;Head=(& git -C $workspace rev-parse HEAD);DirtyWorkingTree=$true;Commands=@();Packages=@();Graphs=@();Profiles=@();NegativeControls=@();Sources=@();Failure=$null;PublishedToExternalFeed=$false}
$inputs=@(Get-Item $PSCommandPath,(Join-Path $PSScriptRoot 'PublicAppConsumer.cs.template'),(Join-Path $PSScriptRoot 'PublicBufferConsumer.cs.template'),(Join-Path $PSScriptRoot 'PublicRawConsumer.cs.template'),(Join-Path $PSScriptRoot 'RawManagedRejection.cs.template'),(Join-Path $workspace 'Directory.Build.props'),(Join-Path $workspace 'Directory.Packages.props'),(Join-Path $workspace 'global.json'),(Join-Path $workspace '../LICENSE'),(Join-Path $workspace '../materials/icon.png'))
foreach($name in @('TrueMoon.Nom','TrueMoon.Contracts','TrueMoon','TrueMoon.Cobalt','TrueMoon.Cobalt.Generator','ManualTests/NomAssetTool')){
    $inputs+=Get-ChildItem (Join-Path $workspace $name) -Recurse -File|Where-Object{$_.Extension -in '.cs','.csproj','.md','.targets','.props' -and $_.FullName -notmatch '[\\/](bin|obj)[\\/]'}
}
$summary.Sources=@($inputs|ForEach-Object{@{Path=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
function Save-Report{$summary|ConvertTo-Json -Depth 14|Set-Content (Join-Path $evidence 'verification.json') -Encoding utf8}
function Invoke-Dotnet([string]$name,[string[]]$arguments,[string]$cwd=$work,[string]$cache='', [string]$expectedError=''){
    if($arguments[0] -in @('restore','build','pack','publish')){$arguments+="-bl:$evidence/$name-{}.binlog"}
    $info=[Diagnostics.ProcessStartInfo]::new('dotnet');$info.WorkingDirectory=$cwd;$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    $info.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
    if($cache){$info.Environment['NUGET_PACKAGES']=$cache;$info.Environment['NUGET_HTTP_CACHE_PATH']=Join-Path $cache 'http';$info.Environment['NUGET_PLUGINS_CACHE_PATH']=Join-Path $cache 'plugins';$info.Environment['NUGET_FALLBACK_PACKAGES']=''}
    foreach($arg in $arguments){$info.ArgumentList.Add($arg)}
    $process=[Diagnostics.Process]::new();$process.StartInfo=$info;$started=$false
    try{
        if(!$process.Start()){throw "Start failed $name"};$started=$true;$out=$process.StandardOutput.ReadToEndAsync();$err=$process.StandardError.ReadToEndAsync()
        if(!$process.WaitForExit(120000)){$process.Kill($true);if(!$process.WaitForExit(10000)){throw 'Owned process not reaped'};throw "Timeout $name"}
        $stdout=$out.GetAwaiter().GetResult();$stderr=$err.GetAwaiter().GetResult();[IO.File]::WriteAllText((Join-Path $evidence "$name.stdout.log"),$stdout);[IO.File]::WriteAllText((Join-Path $evidence "$name.stderr.log"),$stderr)
        $summary.Commands+=@{Name=$name;Arguments=$arguments;WorkingDirectory=$cwd;Cache=$cache;ExitCode=$process.ExitCode;Reaped=$true};Save-Report
        if($expectedError){if($process.ExitCode -eq 0 -or ($stdout+$stderr) -notmatch $expectedError){throw "Expected negative control failed $name"};$summary.NegativeControls+=@{Name=$name;Expected=$expectedError;ExitCode=$process.ExitCode}}
        elseif($process.ExitCode -ne 0){throw "Failed $name; see logs"}
        if($arguments[0] -in @('restore','build','pack','publish') -and !(Get-ChildItem $evidence -Filter "$name-*.binlog")){throw 'Unique binlog missing'}
        Write-Host "$name passed"
    }finally{if($started -and !$process.HasExited){$process.Kill($true);if(!$process.WaitForExit(10000)){throw 'Cleanup did not reap child'}};$process.Dispose()}
}
function Write-Config([string]$path,[string]$source,[string]$fallback=''){
    $feedEntry=if($source){'<add key="current" value="'+[Security.SecurityElement]::Escape($source)+'" />'}else{''}
    $fallbackEntry=if($fallback){'<add key="compiler-cache" value="'+[Security.SecurityElement]::Escape($fallback)+'" />'}else{''}
    [IO.File]::WriteAllText($path,"<configuration><packageSources><clear/>$feedEntry</packageSources><fallbackPackageFolders><clear/>$fallbackEntry</fallbackPackageFolders></configuration>")
}
function Zip-Bytes($zip,[string]$path){$entry=$zip.GetEntry($path);if(!$entry){throw "Missing package entry $path"};$stream=$entry.Open();try{$memory=[IO.MemoryStream]::new();$stream.CopyTo($memory);return ,$memory.ToArray()}finally{$stream.Dispose()}}
function Check-Package([string]$id){
    $path=Join-Path $feed "$id.$version.nupkg";if(!(Test-Path $path)){throw "Package not produced $id"};$zip=[IO.Compression.ZipFile]::OpenRead($path)
    try{
        $entries=@($zip.Entries.FullName);$spec=@($entries|Where-Object{$_ -like '*.nuspec'});if($spec.Count -ne 1){throw 'Nuspec count'}
        [xml]$nuspec=[Text.Encoding]::UTF8.GetString((Zip-Bytes $zip $spec[0])).TrimStart([char]0xfeff);$meta=$nuspec.SelectSingleNode('//*[local-name()="metadata"]')
        if($meta.id -ne $id -or $meta.version -ne $version -or $meta.readme -ne 'PACKAGE.md' -or $meta.license.type -ne 'file' -or $meta.license.InnerText -ne 'LICENSE' -or $meta.icon -ne 'icon.png'){throw 'Package metadata mismatch'}
        $dll=Zip-Bytes $zip "lib/$($trueMoonBuildProfile.Framework)/$id.dll";$xml=Zip-Bytes $zip "lib/$($trueMoonBuildProfile.Framework)/$id.xml";$readme=Zip-Bytes $zip 'PACKAGE.md'
        $license=Zip-Bytes $zip 'LICENSE';$icon=Zip-Bytes $zip 'icon.png'
        if([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($license)) -ne (Get-FileHash (Join-Path $workspace '../LICENSE')).Hash -or
            [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($icon)) -ne (Get-FileHash (Join-Path $workspace '../materials/icon.png')).Hash){throw 'License/icon bytes differ'}
        if($id -eq 'TrueMoon.Nom'){
            if([Text.Encoding]::UTF8.GetString($readme) -ne [IO.File]::ReadAllText((Join-Path $workspace 'TrueMoon.Nom/PACKAGE.md'))){throw 'Nom readme mismatch'}
            if(@($entries|Where-Object{$_ -match '^(runtimes|analyzers|build|buildTransitive|contentFiles)/' -or $_ -match '(Tests|Probe|Aether|Alloy|LiteDB)\.dll$'}).Count){throw 'Unexpected Nom package payload'}
            $dependencies=@($meta.SelectNodes('.//*[local-name()="dependency"]'))
            if($dependencies.Count -ne 1 -or $dependencies[0].id -ne 'TrueMoon.Contracts' -or $dependencies[0].version -notlike "*$version*"){throw 'Nom dependency graph must contain only matching Contracts'}
            $memory=[IO.MemoryStream]::new($dll,$false);$pe=[Reflection.PortableExecutable.PEReader]::new($memory)
            try{
                $reader=[Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
                $references=@($reader.AssemblyReferences|ForEach-Object{$reader.GetString($reader.GetAssemblyReference($_).Name)})
                if(@($references|Where-Object{$_ -notlike 'System.*' -and $_ -ne 'TrueMoon.Contracts'}).Count -or $references -notcontains 'TrueMoon.Contracts'){throw 'Unexpected Nom assembly reference'}
                $exports=@($reader.TypeDefinitions|ForEach-Object{$type=$reader.GetTypeDefinition($_);if(($type.Attributes -band [Reflection.TypeAttributes]::VisibilityMask) -eq [Reflection.TypeAttributes]::Public){$reader.GetString($type.Namespace)+'.'+$reader.GetString($type.Name)}})
                if(($pe.PEHeaders.CorHeader.Flags -band [Reflection.PortableExecutable.CorFlags]::ILOnly) -eq 0 -or @($exports|Where-Object{!$_.StartsWith('TrueMoon.Nom.',[StringComparison]::Ordinal)}).Count -or $exports -notcontains 'TrueMoon.Nom.IProjectAssetStore'){throw 'Nom managed/public API audit failed'}
                foreach($type in @('Catalog.AssetRecord','Resources.AssetResourceVersion','Resources.AssetStoreBufferExtensions','Resources.AssetStoreRawBufferExtensions','Retention.IAssetSnapshot','Backup.RestoreAssetStoreArguments','Hosting.NomStoreHost','Storage.NomCompactionReceipt')){
                    if($exports -notcontains ('TrueMoon.Nom.'+$type)){throw "Missing organized public API $type"}
                }
                $summary.NomAssemblyAudit=@{Managed=$true;References=$references;PublicTypes=$exports;NoEngineOrProviderReference=$true}
            }finally{$pe.Dispose();$memory.Dispose()}
        }
        if($id -eq 'TrueMoon.Cobalt'){[void](Zip-Bytes $zip 'analyzers/dotnet/cs/TrueMoon.Cobalt.Generator.dll');[void](Zip-Bytes $zip 'buildTransitive/TrueMoon.Cobalt.targets')}
        $summary.Packages+=@{Id=$id;Version=$version;Path=$path;Sha256=(Get-FileHash $path).Hash;Sha512=(Get-FileHash $path -Algorithm SHA512).Hash;DllSha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($dll));Entries=$entries;LicenseVerified=$true;ReadmeVerified=$true}
    }finally{$zip.Dispose()}
}
function New-Consumer([string]$name,[string]$consumer){
    $directory=Join-Path $run $name;[void][IO.Directory]::CreateDirectory($directory);$cache=Join-Path $directory 'cache';$config=Join-Path $directory 'NuGet.Config';Write-Config $config $feed
    '<Project />'|Set-Content (Join-Path $directory 'Directory.Build.props');'<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup></Project>'|Set-Content (Join-Path $directory 'Directory.Packages.props')
    @'
<Project><Target Name="CaptureNomDeliveryCompile" AfterTargets="CoreCompile"><WriteLinesToFile File="$(IntermediateOutputPath)nom-analyzers.txt" Lines="@(Analyzer)" Overwrite="true"/><WriteLinesToFile File="$(IntermediateOutputPath)nom-projectrefs.txt" Lines="@(ProjectReference)" Overwrite="true"/></Target></Project>
'@|Set-Content (Join-Path $directory 'Directory.Build.targets')
    Copy-Item (Join-Path $workspace 'global.json') $directory
    if($consumer -eq 'NomPublicConsumer'){Copy-Item (Join-Path $PSScriptRoot 'PublicAppConsumer.cs.template') (Join-Path $directory 'Program.cs')}
    elseif($consumer -eq 'NomBufferConsumer'){Copy-Item (Join-Path $PSScriptRoot 'PublicBufferConsumer.cs.template') (Join-Path $directory 'Program.cs')}
    elseif($consumer -eq 'NomRawConsumer'){Copy-Item (Join-Path $PSScriptRoot 'PublicRawConsumer.cs.template') (Join-Path $directory 'Program.cs')}
    elseif($consumer -eq 'NomRawManagedProbe'){Copy-Item (Join-Path $PSScriptRoot 'RawManagedRejection.cs.template') (Join-Path $directory 'Program.cs')}
    elseif($consumer -eq 'NomAssetTool'){Get-ChildItem (Join-Path $workspace 'ManualTests/NomAssetTool') -Filter '*.cs'|Copy-Item -Destination $directory}
    else{'using TrueMoon.Nom.Storage; public static class HiddenApiProbe { public static object Probe() => typeof(NomKvStore); }'|Set-Content (Join-Path $directory 'Program.cs')}
    $output=if($consumer -in @('NomInternalVisibilityProbe','NomRawManagedProbe')){'Library'}else{'Exe'}
    $project=Join-Path $directory "$consumer.csproj"
    "<Project Sdk='Microsoft.NET.Sdk'><PropertyGroup><TargetFramework>$($trueMoonBuildProfile.Framework)</TargetFramework><OutputType>$output</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><AssemblyName>$consumer</AssemblyName><RootNamespace>TrueMoon.Nom.AssetTool</RootNamespace><UseAppHost>false</UseAppHost><EnableRuntimePackDownload>false</EnableRuntimePackDownload><DisableTransitiveFrameworkReferenceDownloads>true</DisableTransitiveFrameworkReferenceDownloads></PropertyGroup><ItemGroup><PackageReference Include='TrueMoon.Nom' Version='$version'/><PackageReference Include='TrueMoon' Version='$version'/></ItemGroup></Project>"|Set-Content $project
    return [pscustomobject]@{Directory=$directory;Cache=$cache;Config=$config;Project=$project;Consumer=$consumer}
}
function Check-Graph($consumer){
    $path=Join-Path $consumer.Directory 'obj/project.assets.json';$graph=Get-Content $path -Raw|ConvertFrom-Json -AsHashtable
    $expected=@($packages|ForEach-Object{"$_/$version"}|Sort-Object);$actual=@($graph.libraries.Keys|Sort-Object)
    if(Compare-Object $expected $actual){throw 'Fresh graph must contain exactly four current TrueMoon packages'}
    foreach($library in $graph.libraries.Values){if($library.type -ne 'package'){throw 'Project dependency leaked into isolated restore'}}
    foreach($framework in $graph.project.restore.frameworks.Values){if($framework.projectReferences -and $framework.projectReferences.Count){throw 'ProjectReference leaked'}}
    $folders=@($graph.packageFolders.Keys);if($folders.Count -ne 1 -or [IO.Path]::TrimEndingDirectorySeparator($folders[0]) -ne $consumer.Cache){throw 'Unexpected package cache/fallback'}
    foreach($package in $summary.Packages){
        $cached=Join-Path $consumer.Cache ($package.Id.ToLowerInvariant()+"/$version/"+$package.Id.ToLowerInvariant()+".$version.nupkg")
        if((Get-FileHash $cached).Hash -ne $package.Sha256){throw 'Cache did not restore exact fresh package bytes'}
    }
    $summary.Graphs+=@{Project=$consumer.Project;Cache=$consumer.Cache;Libraries=$actual;ProjectReferences=0;FallbackFolders=0}
}
function Check-Compile($consumer){
    $captures=@(Get-ChildItem (Join-Path $consumer.Directory 'obj') -Recurse -Filter 'nom-analyzers.txt');if($captures.Count -eq 0){throw 'No compiler analyzer capture'}
    foreach($file in $captures){
        $analyzers=@(Get-Content $file.FullName|Where-Object{$_ -match 'TrueMoon.Cobalt.Generator.dll$'})
        $expected=Join-Path $consumer.Cache "truemoon.cobalt/$version/analyzers/dotnet/cs/TrueMoon.Cobalt.Generator.dll"
        if($analyzers.Count -ne 1 -or [IO.Path]::GetFullPath($analyzers[0]) -ne $expected){throw 'Matching packaged Cobalt analyzer missing/duplicated'}
    }
    $projectCaptures=@(Get-ChildItem (Join-Path $consumer.Directory 'obj') -Recurse -Filter 'nom-projectrefs.txt');if($projectCaptures.Count -eq 0){throw 'No project reference capture'}
    foreach($file in $projectCaptures){if(@(Get-Content $file.FullName|Where-Object{$_}).Count){throw 'Consumer compiled with a ProjectReference'}}
}
function Check-Output([string]$directory){
    if(@(Get-ChildItem $directory -Recurse -File|Where-Object{$_.Name -match '^(LiteDB|TrueMoon\.(Aether|Alloy|Argentis|Core|Enerit|Mithril))' -or $_.Extension -in '.so','.dylib'}).Count){throw 'Unwanted engine/provider/native dependency in output'}
    $nom=Join-Path $directory 'TrueMoon.Nom.dll';$package=@($summary.Packages|Where-Object Id -eq 'TrueMoon.Nom')[0]
    if((Get-FileHash $nom).Hash -ne $package.DllSha256){throw 'Loaded output differs from packaged Nom'}
    foreach($file in Get-ChildItem $directory -Filter '*.deps.json'){
        $graph=Get-Content $file.FullName -Raw|ConvertFrom-Json -AsHashtable
        if(@($graph.libraries.Keys|Where-Object{$_ -match '^(LiteDB|TrueMoon\.(Aether|Alloy|Argentis|Core|Enerit|Mithril))/'}).Count){throw 'Unwanted runtime dependency'}
    }
}
function Run-App($consumer,[string]$profile,[string]$directory){
    $expectedChecks=switch($consumer.Consumer){'NomBufferConsumer'{28} 'NomRawConsumer'{31} default{34}}
    Check-Output $directory;$report=Join-Path $consumer.Directory "$profile.json";Invoke-Dotnet "$($consumer.Consumer)-$profile" @((Join-Path $directory "$($consumer.Consumer).dll"),$report) $work $consumer.Cache
    $result=Get-Content $report -Raw|ConvertFrom-Json
    if(!$result.Completed -or $result.Consumer -ne $consumer.Consumer -or $result.Checks.Count -ne $expectedChecks -or (Get-FileHash $result.ModulePath).Hash -ne $result.ModuleSha256){throw 'Incomplete package public proof'}
    $reportName="$($consumer.Consumer)-$profile.json"
    Copy-Item $report (Join-Path $evidence $reportName);$summary.Profiles+=@{Consumer=$consumer.Consumer;Profile=$profile;Checks=$expectedChecks;Report=Join-Path $evidence $reportName}
}
function Run-Tool([string]$profile,[string]$directory,[string]$cache,[switch]$source){
    if(!$source){Check-Output $directory};$case=Join-Path $run ('tool-state-'+$profile);[void][IO.Directory]::CreateDirectory($case)
    $root=Join-Path $case 'asset root Ж';$backup=Join-Path $case 'backup';$restored=Join-Path $case 'restored';$receipt=Join-Path $case 'receipt.json';$project=[Guid]::NewGuid().ToString();$store=[Guid]::NewGuid().ToString();$dll=Join-Path $directory 'NomAssetTool.dll';$total=0
    $steps=@(@('create',$root,$project,$store,$receipt),@('verify',$root,$project,$store,$receipt),@('export',$root,$project,$store,$receipt,$backup),@('restore',$backup,$restored,$project,$store,$receipt),@('collect',$restored,$project,$store,$receipt),@('verify-current',$restored,$project,$store,$receipt),@('collect',$restored,$project,$store,$receipt),@('verify-current',$restored,$project,$store,$receipt),@('verify',$root,$project,$store,$receipt))
    for($n=0;$n -lt $steps.Count;$n++){
        $report=Join-Path $case "step-$n.json";Invoke-Dotnet "Tool-$profile-$n" (@($dll)+$steps[$n]+@('--report',$report)) $work $cache
        $result=Get-Content $report -Raw|ConvertFrom-Json
        if(!$result.Completed -or $result.ScenarioName -ne $steps[$n][0] -or $result.Consumer -ne 'NomAssetTool' -or $result.Checks.Count -eq 0 -or (Get-FileHash $result.ModulePath).Hash -ne $result.ModuleSha256){throw 'Incomplete independent tool scenario'}
        $total+=$result.Checks.Count;Copy-Item $report (Join-Path $evidence "Tool-$profile-$n.json")
    }
    $before=@(Get-ChildItem $root -Recurse -File|Sort-Object FullName|ForEach-Object{$_.FullName+' '+(Get-FileHash $_.FullName).Hash}) -join "`n"
    Invoke-Dotnet "Tool-$profile-existing-negative" @($dll,'create',$root,$project,$store,$receipt,'--report',(Join-Path $case 'existing-negative.json')) $work $cache 'Create needs an absent root'
    Invoke-Dotnet "Tool-$profile-command-negative" @($dll,'unknown',$root,$project,$store,$receipt,'--report',(Join-Path $case 'command-negative.json')) $work $cache 'Unknown command'
    $after=@(Get-ChildItem $root -Recurse -File|Sort-Object FullName|ForEach-Object{$_.FullName+' '+(Get-FileHash $_.FullName).Hash}) -join "`n"
    if($before -ne $after -or (Test-Path (Join-Path $case 'existing-negative.json')) -or (Test-Path (Join-Path $case 'command-negative.json'))){throw 'Rejected CLI admission changed source state'}
    $summary.Profiles+=@{Consumer='NomAssetTool';Profile=$profile;Processes=$steps.Count;NegativeProcesses=2;Checks=$total;Source=[bool]$source;State=$case}
}
try{
    # Only compiler packages may use the existing cache during source packing. Consumer caches below are empty/local-feed-only.
    $compilerCache=if($env:NUGET_PACKAGES){$env:NUGET_PACKAGES}else{Join-Path $env:USERPROFILE '.nuget/packages'}
    $buildConfig=Join-Path $run 'Build.NuGet.Config';Write-Config $buildConfig '' $compilerCache
    $sourceCommon=@("-p:Version=$version","-p:PackageVersion=$version",'-p:UseArtifactsOutput=true',"-p:ArtifactsPath=$packArtifacts","-p:DirectoryPackagesPropsPath=$(Join-Path $workspace 'Directory.Packages.props')",'-p:NuGetAudit=false')
    foreach($project in @('TrueMoon/TrueMoon.csproj','TrueMoon.Nom/TrueMoon.Nom.csproj','ManualTests/NomAssetTool/NomAssetTool.csproj')){
        $name=([IO.Path]::GetFileNameWithoutExtension($project));$path=Join-Path $workspace $project
        Invoke-Dotnet "source-restore-$name" (@('restore',$path,'--configfile',$buildConfig,'--packages',(Join-Path $run 'build-cache'))+$sourceCommon)
        Invoke-Dotnet "source-build-$name" (@('build',$path,'-c','Release','--no-restore')+$sourceCommon)
    }
    foreach($id in $packages){Invoke-Dotnet "pack-$id" (@('pack',(Join-Path $workspace "$id/$id.csproj"),'-c','Release','--no-build','--no-restore','-o',$feed)+$sourceCommon);Check-Package $id}
    if(@(Get-ChildItem $feed -Filter '*.nupkg').Count -ne 4){throw 'Unexpected package closure'}
    # Source Debug of the distinct application is also exercised before isolated package consumers.
    $toolProject=Join-Path $workspace 'ManualTests/NomAssetTool/NomAssetTool.csproj';Invoke-Dotnet 'source-tool-Debug' (@('build',$toolProject,'-c','Debug','--no-restore')+$sourceCommon)
    $sourceTool=@(Get-ChildItem (Join-Path $packArtifacts 'bin') -Recurse -Filter 'NomAssetTool.runtimeconfig.json'|Where-Object FullName -match '[\\/]debug[\\/]')
    if($sourceTool.Count -ne 1){throw 'Missing source tool Debug output'};Run-Tool 'SourceDebug' $sourceTool[0].DirectoryName '' -source
    foreach($name in @('NomPublicConsumer','NomBufferConsumer','NomRawConsumer','NomAssetTool')){
        $consumer=New-Consumer $name $name
        Invoke-Dotnet "$name-restore" @('restore',$consumer.Project,'--configfile',$consumer.Config,'--packages',$consumer.Cache,'-p:NuGetAudit=false') $work $consumer.Cache;Check-Graph $consumer
        Invoke-Dotnet "$name-Debug-build" @('build',$consumer.Project,'-c','Debug','--no-restore') $work $consumer.Cache;Check-Compile $consumer
        $directory=Join-Path $consumer.Directory "bin/Debug/$($trueMoonBuildProfile.TestFramework)";if($name -ne 'NomAssetTool'){Run-App $consumer 'NuGetDebug' $directory}else{Run-Tool 'NuGetDebug' $directory $consumer.Cache}
        $publish=Join-Path $consumer.Directory 'publish';Invoke-Dotnet "$name-Release-publish" @('publish',$consumer.Project,'-c','Release','--no-restore','--self-contained','false','-p:UseAppHost=false','-o',$publish) $work $consumer.Cache;Check-Compile $consumer
        if($name -ne 'NomAssetTool'){Run-App $consumer 'NuGetRelease' $publish}else{Run-Tool 'NuGetRelease' $publish $consumer.Cache}
        $rid=New-Consumer ($name+'-win-x64') $name
        Invoke-Dotnet "$name-win-x64-restore" @('restore',$rid.Project,'-r','win-x64','-p:SelfContained=false','-p:UseAppHost=false','--configfile',$rid.Config,'--packages',$rid.Cache,'-p:NuGetAudit=false') $work $rid.Cache;Check-Graph $rid
        $ridOutput=Join-Path $rid.Directory 'publish';Invoke-Dotnet "$name-win-x64-publish" @('publish',$rid.Project,'-c','Release','-r','win-x64','--no-restore','--self-contained','false','-p:UseAppHost=false','-o',$ridOutput) $work $rid.Cache;Check-Compile $rid
        if($name -ne 'NomAssetTool'){Run-App $rid 'WinX64Release' $ridOutput}else{Run-Tool 'WinX64Release' $ridOutput $rid.Cache}
    }
    $negative=New-Consumer 'InternalVisibility' 'NomInternalVisibilityProbe'
    Invoke-Dotnet 'internal-api-restore' @('restore',$negative.Project,'--configfile',$negative.Config,'--packages',$negative.Cache,'-p:NuGetAudit=false') $work $negative.Cache
    Invoke-Dotnet 'internal-api-negative' @('build',$negative.Project,'--no-restore') $work $negative.Cache 'CS0122'
    $managed=New-Consumer 'RawManagedConstraint' 'NomRawManagedProbe'
    Invoke-Dotnet 'raw-managed-restore' @('restore',$managed.Project,'--configfile',$managed.Config,'--packages',$managed.Cache,'-p:NuGetAudit=false') $work $managed.Cache
    Invoke-Dotnet 'raw-managed-negative' @('build',$managed.Project,'--no-restore') $work $managed.Cache 'CS8377'
    $managedErrors=[IO.File]::ReadAllText((Join-Path $evidence 'raw-managed-negative.stdout.log'))
    $managedSource=[IO.File]::ReadAllLines((Join-Path $PSScriptRoot 'RawManagedRejection.cs.template'))
    $managedLines=@(for($line=0;$line -lt $managedSource.Length;$line++){if($managedSource[$line] -match '_ = store\.(PublishRawResourceAsync|PublishRawResourceCopyAsync|ReadRawResourceAsync)<'){$line+1}})
    if($managedLines.Count -ne 3){throw 'Expected all three managed-type rejection operations'}
    foreach($line in $managedLines){if($managedErrors -notmatch ('Program\.cs\('+ $line +',\d+\): error CS8377')){throw "Missing unmanaged constraint rejection at source line $line"}}
    $missing=Join-Path $run 'missing-feed';[void][IO.Directory]::CreateDirectory($missing);Get-ChildItem $feed -Filter '*.nupkg'|Where-Object Name -notlike 'TrueMoon.Nom.*'|Copy-Item -Destination $missing
    $config=Join-Path $run 'Missing.NuGet.Config';Write-Config $config $missing;$negativeMissing=New-Consumer 'MissingPackage' 'NomInternalVisibilityProbe'
    Invoke-Dotnet 'missing-package-negative' @('restore',$negativeMissing.Project,'--configfile',$config,'--packages',$negativeMissing.Cache,'--no-cache','-p:NuGetAudit=false') $work $negativeMissing.Cache 'NU1101'
    foreach($inputFile in $summary.Sources){if((Get-FileHash -LiteralPath $inputFile.Path).Hash -ne $inputFile.Sha256){throw 'Delivery source drift'}}
    $delivered=Join-Path $evidence 'packages';[void][IO.Directory]::CreateDirectory($delivered)
    foreach($package in $summary.Packages){$destination=Join-Path $delivered ([IO.Path]::GetFileName($package.Path));Copy-Item -LiteralPath $package.Path -Destination $destination;if((Get-FileHash $destination).Hash -ne $package.Sha256){throw 'Delivered artifact hash mismatch'};$package.DeliveryPath=$destination}
    $summary.Platform=[ordered]@{OS=[Runtime.InteropServices.RuntimeInformation]::OSDescription;Architecture=[Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString();Runtime=[Environment]::Version.ToString();Verified=@("$($trueMoonBuildProfile.Framework) portable Debug","$($trueMoonBuildProfile.Framework) portable framework-dependent Release",'win-x64 framework-dependent Release');Unverified=@('linux','osx','win-arm64','self-contained','NativeAOT','trimming')}
    $summary.Completed=$true
}catch{$summary.Failure=$_.ToString();throw}
finally{Save-Report}
