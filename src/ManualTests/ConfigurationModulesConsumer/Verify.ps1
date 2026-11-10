[CmdletBinding()]
param([string]$EvidenceDirectory, [string]$RunDirectory)

# Standalone configuration public API acceptance. Every consumer and cache is created in a fresh directory.
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$cpuWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$cpuRun=if($RunDirectory){[IO.Path]::GetFullPath($RunDirectory)}else{Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-ConfigurationModulesConsumer-'+[Guid]::NewGuid().ToString('N'))}
$cpuEvidence=if($EvidenceDirectory){[IO.Path]::GetFullPath($EvidenceDirectory)}else{Join-Path $cpuWorkspace ('TestResults/ConfigurationModulesConsumer-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
if($cpuRun -eq $cpuWorkspace -or $cpuRun.StartsWith($cpuWorkspace+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'RunDirectory must be outside the checkout.'}
if((Test-Path -LiteralPath $cpuRun) -or (Test-Path -LiteralPath $cpuEvidence)){throw 'Use fresh run and evidence directories.'}
$cpuDotnet=(Get-Command dotnet -ErrorAction Stop).Source
$cpuFeed=Join-Path $cpuRun 'feed';$cpuCache=Join-Path $cpuRun 'cache';$cpuSource=Join-Path $cpuRun 'source';$cpuFresh=Join-Path $cpuRun 'fresh';$cpuCwd=Join-Path $cpuRun 'unrelated working directory'
foreach($directory in @($cpuRun,$cpuEvidence,$cpuFeed,$cpuCache,$cpuSource,$cpuFresh,$cpuCwd)){[void][IO.Directory]::CreateDirectory($directory)}
$cpuVersion='0.2.5.2-core75.'+(Get-Date -Format 'yyyyMMddHHmmss')
$cpuPackages=@('TrueMoon.Contracts','TrueMoon.Core','TrueMoon.Cobalt','TrueMoon','TrueMoon.Extensions.DependencyInjection','TrueMoon.Enerit.Generator','TrueMoon.Enerit','TrueMoon.Mithril','TrueMoon.Argentis','TrueMoon.Alloy','TrueMoon.Alloy.Rendering.Skia','TrueMoon.Alloy.Platform.Silk','TrueMoon.Alloy.Hosting','TrueMoon.Aether','TrueMoon.Aether.Editor','TrueMoon.Nom')
$cpuReport=[ordered]@{Completed=$false;RunDirectory=$cpuRun;Version=$cpuVersion;Commands=@();Packages=@();ExternalPackages=@();Graphs=@();Sources=@();Profiles=@();Failure=$null;GpuRun=$false;WindowRun=$false}
function Save-CpuReport {$cpuReport|ConvertTo-Json -Depth 12|Set-Content -LiteralPath (Join-Path $cpuEvidence 'verification.json') -Encoding utf8}
function Invoke-Cpu([string]$name,[string[]]$arguments,[string]$cwd=$cpuWorkspace,[switch]$isolated){
    $info=[Diagnostics.ProcessStartInfo]::new($cpuDotnet);$info.WorkingDirectory=$cwd;$info.UseShellExecute=$false;$info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    $info.Environment['APPDATA']=Join-Path $cpuRun 'settings';[void][IO.Directory]::CreateDirectory($info.Environment['APPDATA']);$info.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1';[void]$info.Environment.Remove('TM_ENGINE_BOOTSTRAP');$info.Environment['TRUEMOON_CORE75_NOM__MAXIMUMOPENSTORES']='4'
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
    if(!$proof.Completed -or $proof.GpuRun -or $proof.WindowRun -or $proof.Checks -lt 70 -or $proof.ChildProcessRun -or $proof.Providers.Count -ne 2){throw 'Consumer scope mismatch.'}
    $cpuReport.Profiles+=@{Name=$name;Report=$path};Save-CpuReport
}
try{
    $program=Join-Path $PSScriptRoot 'Program.cs'
    $cpuReport.Sources+=@{Path=$program;Sha256=(Get-FileHash -LiteralPath $program).Hash}
    foreach($sourceDirectory in $cpuPackages) {
        Get-ChildItem (Join-Path $cpuWorkspace $sourceDirectory) -Recurse -File -Filter '*.cs' | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | ForEach-Object { $cpuReport.Sources+=@{Path=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} }
    }
    foreach($rootSource in @('TrueMoon/AppBuilder.cs','TrueMoon/AppConfigurationContext.cs','TrueMoon/DefaultApp.cs','TrueMoon.Aether.Editor/TrueMoon.Aether.Editor.csproj')) { $path=Join-Path $cpuWorkspace $rootSource; $cpuReport.Sources+=@{Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash} }
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
        [IO.File]::WriteAllText((Join-Path $directory 'global.json'),'{"sdk":{"version":"10.0.100","allowPrerelease":false,"rollForward":"latestMinor"}}')
        [IO.File]::WriteAllText((Join-Path $directory 'Directory.Build.props'),'<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><LangVersion>latest</LangVersion><NuGetAudit>false</NuGetAudit></PropertyGroup></Project>')
        [IO.File]::WriteAllText((Join-Path $directory 'Directory.Packages.props'),'<Project/>')
        [IO.File]::WriteAllText((Join-Path $directory 'Directory.Build.targets'),'<Project/>')
        Copy-Item -LiteralPath $program -Destination (Join-Path $directory 'Program.cs')
    }
    $aether=[Security.SecurityElement]::Escape((Join-Path $cpuWorkspace 'TrueMoon.Contracts/TrueMoon.Contracts.csproj'))
    $bootstrap=[Security.SecurityElement]::Escape((Join-Path $cpuWorkspace 'TrueMoon/TrueMoon.csproj'))
    $mithril=[Security.SecurityElement]::Escape((Join-Path $cpuWorkspace 'TrueMoon.Aether.Editor/TrueMoon.Aether.Editor.csproj'))
    $nom=[Security.SecurityElement]::Escape((Join-Path $cpuWorkspace 'TrueMoon.Nom/TrueMoon.Nom.csproj'))
    $adapter=[Security.SecurityElement]::Escape((Join-Path $cpuWorkspace 'TrueMoon.Extensions.DependencyInjection/TrueMoon.Extensions.DependencyInjection.csproj'))
    [IO.File]::WriteAllText((Join-Path $cpuSource 'Consumer.csproj'),"<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup><OutputType>Exe</OutputType><AssemblyName>ConfigurationModulesConsumer</AssemblyName><RootNamespace>ConfigurationModulesConsumer</RootNamespace></PropertyGroup><ItemGroup><ProjectReference Include=`"$aether`"/><ProjectReference Include=`"$bootstrap`"/><ProjectReference Include=`"$mithril`"/><ProjectReference Include=`"$nom`"/><ProjectReference Include=`"$adapter`"/></ItemGroup></Project>")
    Invoke-Cpu 'source-restore' @('restore','Consumer.csproj','--no-dependencies','--configfile',"$cpuSource/NuGet.Config","-bl:$cpuEvidence/source-restore-{}.binlog") $cpuSource
    Invoke-Cpu 'source-Debug' @('build','Consumer.csproj','--no-restore','-p:BuildProjectReferences=false','-c','Debug',"-bl:$cpuEvidence/source-Debug-{}.binlog") $cpuSource
    Add-CpuProfile 'source-proof' "$cpuSource/bin/Debug/net10.0/ConfigurationModulesConsumer.dll" "$cpuEvidence/source-proof"
    Invoke-Cpu 'published-Release' @('publish','Consumer.csproj','--no-restore','-p:BuildProjectReferences=false','-c','Release','-o',"$cpuRun/published",'-p:UseAppHost=false',"-bl:$cpuEvidence/published-Release-{}.binlog") $cpuSource
    Add-CpuProfile 'published-proof' "$cpuRun/published/ConfigurationModulesConsumer.dll" "$cpuEvidence/published-proof"
    foreach($id in $cpuPackages){
        $name='pack-'+$id
        Invoke-Cpu $name @('pack',"$id/$id.csproj",'--no-build','--no-restore','-c','Release','-o',$cpuFeed,"-p:Version=$cpuVersion","-bl:$cpuEvidence/$name-{}.binlog")
        $path=Join-Path $cpuFeed "$id.$cpuVersion.nupkg"
        if(!(Test-Path -LiteralPath $path)){throw "Missing package: $id"}
        $cpuReport.Packages+=@{Id=$id;Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash}
    }
    [IO.File]::WriteAllText((Join-Path $cpuFresh 'Consumer.csproj'),"<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup><OutputType>Exe</OutputType><AssemblyName>ConfigurationModulesConsumer</AssemblyName><RootNamespace>ConfigurationModulesConsumer</RootNamespace></PropertyGroup><ItemGroup><PackageReference Include=`"TrueMoon`" Version=`"$cpuVersion`"/><PackageReference Include=`"TrueMoon.Contracts`" Version=`"$cpuVersion`"/><PackageReference Include=`"TrueMoon.Aether.Editor`" Version=`"$cpuVersion`"/><PackageReference Include=`"TrueMoon.Nom`" Version=`"$cpuVersion`"/><PackageReference Include=`"TrueMoon.Enerit.Generator`" Version=`"$cpuVersion`" PrivateAssets=`"all`"/><PackageReference Include=`"TrueMoon.Extensions.DependencyInjection`" Version=`"$cpuVersion`"/></ItemGroup></Project>")
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
    Add-CpuProfile 'fresh-proof' "$cpuRun/fresh-published/ConfigurationModulesConsumer.dll" "$cpuEvidence/fresh-proof" -isolated
    foreach($source in $cpuReport.Sources){if((Get-FileHash -LiteralPath $source.Path).Hash -ne $source.Sha256){throw 'Consumer source drift.'}}
    $installed=Join-Path $cpuCache ($graph.libraries["TrueMoon/$cpuVersion"].path+'/lib/net10.0/TrueMoon.dll')
    $loaded=Join-Path $cpuRun 'fresh-published/TrueMoon.dll'
    if((Get-FileHash -LiteralPath $installed).Hash -ne (Get-FileHash -LiteralPath $loaded).Hash){throw 'Installed/output configuration mismatch.'}
    $cpuReport.Completed=$true
}catch{$cpuReport.Failure=$_.Exception.ToString();throw}
finally{Save-CpuReport;Write-Host "Evidence: $cpuEvidence; consumer: $cpuRun"}
