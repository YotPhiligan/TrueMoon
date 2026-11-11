[CmdletBinding()]
param([string]$EvidenceDirectory, [string]$RunDirectory)
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
$trueMoonBuildProfile = Get-TrueMoonBuildProfile -Workspace ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))



# CPU-only public API acceptance. Every consumer and cache is created in a fresh directory.
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$cpuWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$cpuRun=if($RunDirectory){[IO.Path]::GetFullPath($RunDirectory)}else{Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-RenderSnapshotsConsumer-'+[Guid]::NewGuid().ToString('N'))}
$cpuEvidence=if($EvidenceDirectory){[IO.Path]::GetFullPath($EvidenceDirectory)}else{Join-Path $cpuWorkspace ('TestResults/RenderSnapshotsConsumer-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
if($cpuRun -eq $cpuWorkspace -or $cpuRun.StartsWith($cpuWorkspace+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'RunDirectory must be outside the checkout.'}
if((Test-Path -LiteralPath $cpuRun) -or (Test-Path -LiteralPath $cpuEvidence)){throw 'Use fresh run and evidence directories.'}
$cpuDotnet=(Get-Command dotnet -ErrorAction Stop).Source
$cpuFeed=Join-Path $cpuRun 'feed';$cpuCache=Join-Path $cpuRun 'cache';$cpuSource=Join-Path $cpuRun 'source';$cpuFresh=Join-Path $cpuRun 'fresh';$cpuCwd=Join-Path $cpuRun 'unrelated working directory'
foreach($directory in @($cpuRun,$cpuEvidence,$cpuFeed,$cpuCache,$cpuSource,$cpuFresh,$cpuCwd)){[void][IO.Directory]::CreateDirectory($directory)}
$cpuVersion='0.2.5.2-rendersnapshots.'+(Get-Date -Format 'yyyyMMddHHmmss')
$cpuPackages=@('TrueMoon.Contracts','TrueMoon.Core','TrueMoon.Cobalt','TrueMoon','TrueMoon.Enerit','TrueMoon.Mithril','TrueMoon.Aether')
$cpuReport=[ordered]@{Completed=$false;RunDirectory=$cpuRun;Version=$cpuVersion;Commands=@();Packages=@();ExternalPackages=@();Graphs=@();Sources=@();Profiles=@();Failure=$null;GpuRun=$false;WindowRun=$false}
function Save-CpuReport {$cpuReport|ConvertTo-Json -Depth 12|Set-Content -LiteralPath (Join-Path $cpuEvidence 'verification.json') -Encoding utf8}
function Invoke-Cpu([string]$name,[string[]]$arguments,[string]$cwd=$cpuWorkspace,[switch]$isolated){
    $info=[Diagnostics.ProcessStartInfo]::new($cpuDotnet);$info.WorkingDirectory=$cwd;$info.UseShellExecute=$false;$info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    $info.Environment['APPDATA']=Join-Path $cpuRun 'settings';[void][IO.Directory]::CreateDirectory($info.Environment['APPDATA']);$info.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1';[void]$info.Environment.Remove('TM_ENGINE_BOOTSTRAP')
    if($isolated){$info.Environment['NUGET_PACKAGES']=$cpuCache;$info.Environment['NUGET_HTTP_CACHE_PATH']=Join-Path $cpuRun 'http-cache';$info.Environment['NUGET_PLUGINS_CACHE_PATH']=Join-Path $cpuRun 'plugins-cache'}
    foreach($argument in $arguments){[void]$info.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::Start($info);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync();$timeout=$false
    try{
        if(!$process.WaitForExit(240000)){$timeout=$true;$process.Kill($true);$process.WaitForExit()}
        [IO.File]::WriteAllText((Join-Path $cpuEvidence "$name.stdout.log"),$stdout.GetAwaiter().GetResult())
        [IO.File]::WriteAllText((Join-Path $cpuEvidence "$name.stderr.log"),$stderr.GetAwaiter().GetResult())
        $cpuReport.Commands+=@{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode;TimedOut=$timeout;Isolated=[bool]$isolated;WorkingDirectory=$cwd};Save-CpuReport
        if($timeout -or $process.ExitCode -ne 0){throw "$name failed; stdout/stderr preserved."}
        if($arguments[0] -in @('build','publish','pack','restore') -and !(Get-ChildItem -LiteralPath $cpuEvidence -Filter "$name-*.binlog")){throw 'Missing unique binlog.'}
        Write-Host "$name passed"
    }finally{if(!$process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
function Add-CpuProfile([string]$name,[string]$assembly,[string]$reportDirectory,[switch]$isolated){
    Invoke-Cpu $name @($assembly,$reportDirectory) $cpuCwd -isolated:$isolated
    $path=Join-Path $reportDirectory 'verification.json';$proof=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
    if(!$proof.Completed -or $proof.GpuRun -or $proof.WindowRun -or $proof.DrawRanges -ne 2 -or !$proof.SubmissionCompleted -or $proof.FinalSnapshotBytes -ne 0 -or $proof.FinalResourceLeases -ne 0 -or $proof.FinalGeometryBytes -ne 0){throw 'Consumer scope mismatch.'}
    $cpuReport.Profiles+=@{Name=$name;Report=$path};Save-CpuReport
}
try{
    $program=Join-Path $PSScriptRoot 'Program.cs'
    $cpuReport.Sources+=@{Path=$program;Sha256=(Get-FileHash -LiteralPath $program).Hash}
    Get-ChildItem (Join-Path $cpuWorkspace 'TrueMoon.Aether') -Recurse -File -Filter '*.cs' | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | ForEach-Object { $cpuReport.Sources+=@{Path=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} }
    # Exact external dependencies from the existing restored graph only; no live feed or package upgrades.
    $external=@{}
    foreach($id in @($cpuPackages+'TrueMoon.Cobalt.Generator')){
        $assets=Get-Content -LiteralPath (Join-Path $cpuWorkspace "$id/obj/project.assets.json") -Raw|ConvertFrom-Json -AsHashtable
        foreach($library in $assets.libraries.GetEnumerator()){
            if($library.Value.type -ne 'package' -or $external.ContainsKey($library.Key)){continue}
            $relative=$library.Key.ToLowerInvariant();$parts=$relative.Split('/');$file="$($parts[0]).$($parts[1]).nupkg";$cached=$null
            foreach($folder in $assets.packageFolders.Keys){$candidate=Join-Path $folder "$relative/$file";if(Test-Path -LiteralPath $candidate){$cached=$candidate;break}}
            if(!$cached){throw "Exact cached dependency missing: $($library.Key)"}
            Copy-Item -LiteralPath $cached -Destination (Join-Path $cpuFeed $file);$external[$library.Key]=$cached
            $cpuReport.ExternalPackages+=@{Identity=$library.Key;Source=$cached;Sha256=(Get-FileHash -LiteralPath $cached).Hash}
        }
    }
    foreach($directory in @($cpuSource,$cpuFresh)){
        [IO.File]::WriteAllText((Join-Path $directory 'NuGet.Config'),'<configuration><packageSources><clear/><add key="local" value="'+[Security.SecurityElement]::Escape($cpuFeed)+'"/></packageSources></configuration>')
        [IO.File]::WriteAllText((Join-Path $directory 'global.json'),"{`"sdk`":{`"version`":`"$($trueMoonBuildProfile.Sdk)`",`"allowPrerelease`":false,`"rollForward`":`"latestMinor`"}}")
        [IO.File]::WriteAllText((Join-Path $directory 'Directory.Build.props'),"<Project><PropertyGroup><TargetFramework>$($trueMoonBuildProfile.Framework)</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><LangVersion>latest</LangVersion><NuGetAudit>false</NuGetAudit></PropertyGroup></Project>")
        [IO.File]::WriteAllText((Join-Path $directory 'Directory.Packages.props'),'<Project/>')
        [IO.File]::WriteAllText((Join-Path $directory 'Directory.Build.targets'),'<Project/>')
        Copy-Item -LiteralPath $program -Destination (Join-Path $directory 'Program.cs')
    }
    $aether=[Security.SecurityElement]::Escape((Join-Path $cpuWorkspace 'TrueMoon.Aether/TrueMoon.Aether.csproj'))
    $bootstrap=[Security.SecurityElement]::Escape((Join-Path $cpuWorkspace 'TrueMoon/TrueMoon.csproj'))
    [IO.File]::WriteAllText((Join-Path $cpuSource 'Consumer.csproj'),"<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup><OutputType>Exe</OutputType><AssemblyName>RenderSnapshotsConsumer</AssemblyName><RootNamespace>RenderSnapshotsConsumer</RootNamespace></PropertyGroup><ItemGroup><ProjectReference Include=`"$aether`"/><ProjectReference Include=`"$bootstrap`"/></ItemGroup></Project>")
    Invoke-Cpu 'source-restore' @('restore','Consumer.csproj','--no-dependencies','--configfile',"$cpuSource/NuGet.Config","-bl:$cpuEvidence/source-restore-{}.binlog") $cpuSource
    Invoke-Cpu 'source-Debug' @('build','Consumer.csproj','--no-restore','-p:BuildProjectReferences=false','-c','Debug',"-bl:$cpuEvidence/source-Debug-{}.binlog") $cpuSource
    Add-CpuProfile 'source-proof' "$cpuSource/bin/Debug/$($trueMoonBuildProfile.Framework)/RenderSnapshotsConsumer.dll" "$cpuEvidence/source-proof"
    Invoke-Cpu 'published-Release' @('publish','Consumer.csproj','--no-restore','-p:BuildProjectReferences=false','-c','Release','-o',"$cpuRun/published",'-p:UseAppHost=false',"-bl:$cpuEvidence/published-Release-{}.binlog") $cpuSource
    Add-CpuProfile 'published-proof' "$cpuRun/published/RenderSnapshotsConsumer.dll" "$cpuEvidence/published-proof"
    foreach($id in $cpuPackages){
        $name='pack-'+$id
        Invoke-Cpu $name @('pack',"$id/$id.csproj",'--no-build','--no-restore','-c','Release','-o',$cpuFeed,"-p:Version=$cpuVersion","-bl:$cpuEvidence/$name-{}.binlog")
        $path=Join-Path $cpuFeed "$id.$cpuVersion.nupkg"
        if(!(Test-Path -LiteralPath $path)){throw "Missing package: $id"}
        $cpuReport.Packages+=@{Id=$id;Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash}
    }
    [IO.File]::WriteAllText((Join-Path $cpuFresh 'Consumer.csproj'),"<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup><OutputType>Exe</OutputType><AssemblyName>RenderSnapshotsConsumer</AssemblyName><RootNamespace>RenderSnapshotsConsumer</RootNamespace></PropertyGroup><ItemGroup><PackageReference Include=`"TrueMoon`" Version=`"$cpuVersion`"/><PackageReference Include=`"TrueMoon.Aether`" Version=`"$cpuVersion`"/></ItemGroup></Project>")
    Invoke-Cpu 'fresh-restore' @('restore','Consumer.csproj','--configfile',"$cpuFresh/NuGet.Config",'--no-http-cache',"-bl:$cpuEvidence/fresh-restore-{}.binlog") $cpuFresh -isolated
    $graph=Get-Content -LiteralPath "$cpuFresh/obj/project.assets.json" -Raw|ConvertFrom-Json -AsHashtable
    if($graph.libraries.Values.Where({$_.type -eq 'project'}).Count){throw 'Fresh consumer contains ProjectReferences.'}
    foreach($id in $cpuPackages){
        $key="$id/$cpuVersion";if(!$graph.libraries.ContainsKey($key)){throw "Missing fresh local dependency: $key"}
        $metadata=Get-Content -LiteralPath (Join-Path $cpuCache ($graph.libraries[$key].path+'/.nupkg.metadata')) -Raw|ConvertFrom-Json
        if([IO.Path]::GetFullPath($metadata.source) -ne $cpuFeed){throw 'Fresh package source mismatch.'}
    }
    $cpuReport.Graphs+=@{Project='Fresh';ProjectReferences=0;LocalPackages=$cpuPackages;AssetsSha256=(Get-FileHash -LiteralPath "$cpuFresh/obj/project.assets.json").Hash}
    Invoke-Cpu 'fresh-publish' @('publish','Consumer.csproj','--no-restore','-c','Release','-o',"$cpuRun/fresh-published",'-p:UseAppHost=false',"-bl:$cpuEvidence/fresh-publish-{}.binlog") $cpuFresh -isolated
    Add-CpuProfile 'fresh-proof' "$cpuRun/fresh-published/RenderSnapshotsConsumer.dll" "$cpuEvidence/fresh-proof" -isolated
    foreach($source in $cpuReport.Sources){if((Get-FileHash -LiteralPath $source.Path).Hash -ne $source.Sha256){throw 'Consumer source drift.'}}
    $installed=Join-Path $cpuCache ($graph.libraries["TrueMoon.Aether/$cpuVersion"].path+"/lib/$($trueMoonBuildProfile.Framework)/TrueMoon.Aether.dll")
    $loaded=Join-Path $cpuRun 'fresh-published/TrueMoon.Aether.dll'
    if((Get-FileHash -LiteralPath $installed).Hash -ne (Get-FileHash -LiteralPath $loaded).Hash){throw 'Installed/output Aether mismatch.'}
    $cpuReport.Completed=$true
}catch{$cpuReport.Failure=$_.Exception.ToString();throw}
finally{Save-CpuReport;Write-Host "Evidence: $cpuEvidence; consumer: $cpuRun"}
