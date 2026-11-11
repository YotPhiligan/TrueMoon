[CmdletBinding()]
param([string]$EvidenceDirectory, [string]$RunDirectory)
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
$trueMoonBuildProfile = Get-TrueMoonBuildProfile -Workspace ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))



# Headless e.3b acceptance: source Debug, published Release and an independent fresh NuGet consumer.
# Uses only cached external packages. Creates new directories and preserves all evidence.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$sceneWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sceneRun = if ($RunDirectory) { [IO.Path]::GetFullPath($RunDirectory) } else {
    Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-EcsRuntimeConsumer-' + [Guid]::NewGuid().ToString('N'))
}
$sceneEvidence = if ($EvidenceDirectory) { [IO.Path]::GetFullPath($EvidenceDirectory) } else {
    Join-Path $sceneWorkspace ('TestResults/EcsRuntimeConsumer-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
if ($sceneRun -eq $sceneWorkspace -or $sceneRun.StartsWith($sceneWorkspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'RunDirectory must be outside the checkout.'
}
if ((Test-Path -LiteralPath $sceneRun) -or (Test-Path -LiteralPath $sceneEvidence)) { throw 'Use fresh run and evidence directories.' }
if (![OperatingSystem]::IsWindows()) { throw 'The shared-frame/native-process profile requires Windows.' }
$sceneDotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source
$sceneFeed = Join-Path $sceneRun 'feed'; $sceneCache = Join-Path $sceneRun 'cache'
$sceneConsumer = Join-Path $sceneRun 'consumer'; $sceneCwd = Join-Path $sceneRun 'unrelated working directory'
foreach ($directory in @($sceneRun,$sceneEvidence,$sceneFeed,$sceneCache,$sceneConsumer,$sceneCwd)) { [void][IO.Directory]::CreateDirectory($directory) }
$sceneVersion = '0.2.2.7-ecsload.' + (Get-Date -Format 'yyyyMMddHHmmss')
$scenePackages = @('TrueMoon.Contracts','TrueMoon.Core','TrueMoon.Cobalt','TrueMoon','TrueMoon.Enerit','TrueMoon.Mithril','TrueMoon.Aether')
$sceneReport = [ordered]@{Completed=$false;RunDirectory=$sceneRun;Version=$sceneVersion;Commands=@();Packages=@();ExternalPackages=@();Graphs=@();Sources=@();Profiles=@();Failure=$null;GpuRun=$false;WindowRun=$false}
function Save-SceneReport { $sceneReport | ConvertTo-Json -Depth 25 | Set-Content -LiteralPath (Join-Path $sceneEvidence 'verification.json') -Encoding utf8 }
function Invoke-SceneCommand([string]$name,[string[]]$arguments,[string]$cwd=$sceneWorkspace,[switch]$isolated) {
    $info = [Diagnostics.ProcessStartInfo]::new($sceneDotnet)
    $info.WorkingDirectory=$cwd; $info.UseShellExecute=$false; $info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true
    [void]$info.Environment.Remove('TM_ENGINE_BOOTSTRAP')
    $info.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
    if ($isolated) {
        $info.Environment['NUGET_PACKAGES']=$sceneCache
        $info.Environment['NUGET_HTTP_CACHE_PATH']=Join-Path $sceneRun 'http-cache'
        $info.Environment['NUGET_PLUGINS_CACHE_PATH']=Join-Path $sceneRun 'plugins-cache'
    }
    foreach ($argument in $arguments) { [void]$info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($info)
    $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
    $timeout=$false
    try {
        if (!$process.WaitForExit(240000)) { $timeout=$true; $process.Kill($true); $process.WaitForExit() }
        [IO.File]::WriteAllText((Join-Path $sceneEvidence "$name.stdout.log"),$stdout.GetAwaiter().GetResult())
        [IO.File]::WriteAllText((Join-Path $sceneEvidence "$name.stderr.log"),$stderr.GetAwaiter().GetResult())
        $sceneReport.Commands += [ordered]@{Name=$name;Arguments=$arguments;WorkingDirectory=$cwd;Isolated=[bool]$isolated;ExitCode=$process.ExitCode;TimedOut=$timeout}
        Save-SceneReport
        if ($timeout -or $process.ExitCode -ne 0) { throw "$name failed; see its preserved stdout/stderr." }
        if ($arguments[0] -in @('build','publish','pack','restore') -and !(Get-ChildItem -LiteralPath $sceneEvidence -Filter "$name-*.binlog")) { throw "$name did not produce a binlog." }
        Write-Host "$name passed"
    }
    finally { if (!$process.HasExited) { $process.Kill($true); $process.WaitForExit() }; $process.Dispose() }
}
function Save-SceneSource([string]$relative) {
    $path=Join-Path $sceneWorkspace $relative
    $sceneReport.Sources += [ordered]@{Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash}
}
try {
    foreach ($relative in @('ManualTests/Game.Logic/WorldProbeGame.cs','ManualTests/EngineSessionProbe/SceneWorldRuntimeProbe.cs','Examples/AetherRuntimeHost/Program.cs')) { Save-SceneSource $relative }
    foreach ($project in @('ManualTests/EngineSessionProbe/EngineSessionProbe.csproj','Examples/AetherRuntimeHost/AetherRuntimeHost.csproj')) {
        $name=if($project.StartsWith('ManualTests')){'source-parent'}else{'source-runtime'}
        Invoke-SceneCommand $name @('build',$project,'--no-restore','-c','Debug',"-bl:$sceneEvidence/$name-{}.binlog")
    }
    $sourceParent=Join-Path $sceneWorkspace "ManualTests/EngineSessionProbe/bin/Debug/$($trueMoonBuildProfile.TestFramework)/EngineSessionProbe.dll"
    $sourceRuntime=Join-Path $sceneWorkspace "Examples/AetherRuntimeHost/bin/Debug/$($trueMoonBuildProfile.Framework)/AetherRuntimeHost.dll"
    $sourceGame=Join-Path $sceneWorkspace "ManualTests/Game.Logic/bin/Debug/$($trueMoonBuildProfile.TestFramework)"
    $sourceOutput=Join-Path $sceneEvidence 'source-proof'
    Invoke-SceneCommand 'source-proof' @($sourceParent,'--ecs-world-proof',$sourceRuntime,$sourceGame,$sourceOutput) $sceneCwd
    $sceneReport.Profiles += @{Name='Source Debug';Report=(Join-Path $sourceOutput 'verification.json')}
    foreach ($project in @('ManualTests/EngineSessionProbe/EngineSessionProbe.csproj','Examples/AetherRuntimeHost/AetherRuntimeHost.csproj')) {
        $name=if($project.StartsWith('ManualTests')){'published-parent'}else{'published-runtime'}
        Invoke-SceneCommand $name @('publish',$project,'--no-restore','-c','Release','-o',"$sceneRun/$name",'-p:UseAppHost=false',"-bl:$sceneEvidence/$name-{}.binlog")
    }
    $publishedOutput=Join-Path $sceneEvidence 'published-proof'
    Invoke-SceneCommand 'published-proof' @("$sceneRun/published-parent/EngineSessionProbe.dll",'--ecs-world-proof',"$sceneRun/published-runtime/AetherRuntimeHost.dll","$sceneRun/published-parent",$publishedOutput) $sceneCwd
    $sceneReport.Profiles += @{Name='Published Release';Report=(Join-Path $publishedOutput 'verification.json')}

    foreach ($id in $scenePackages) {
        $name='pack-'+$id
        Invoke-SceneCommand $name @('pack',"$id/$id.csproj",'--no-build','--no-restore','-c','Release','-o',$sceneFeed,"-p:Version=$sceneVersion","-bl:$sceneEvidence/$name-{}.binlog")
        $path=Join-Path $sceneFeed "$id.$sceneVersion.nupkg"
        if (!(Test-Path -LiteralPath $path)) { throw "Missing new package $id" }
        $sceneReport.Packages += @{Id=$id;Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash}
    }
    # Populate a local feed from exact already-restored external package identities, not from a live source.
    $external=@{}
    foreach ($id in @($scenePackages + 'TrueMoon.Cobalt.Generator')) {
        $assets=Get-Content -LiteralPath (Join-Path $sceneWorkspace "$id/obj/project.assets.json") -Raw | ConvertFrom-Json -AsHashtable
        foreach ($library in $assets.libraries.GetEnumerator()) {
            if ($library.Value.type -ne 'package' -or $external.ContainsKey($library.Key)) { continue }
            $packageRelative=$library.Key.ToLowerInvariant()
            $parts=$packageRelative.Split('/'); $file="$($parts[0]).$($parts[1]).nupkg"
            $cached=$null
            foreach ($folder in $assets.packageFolders.Keys) {
                $candidate=Join-Path $folder "$packageRelative/$file"
                if (Test-Path -LiteralPath $candidate) { $cached=$candidate; break }
            }
            if ($null -eq $cached) { throw "Exact cached package unavailable: $($library.Key)" }
            Copy-Item -LiteralPath $cached -Destination (Join-Path $sceneFeed $file)
            $external[$library.Key]=$cached
            $sceneReport.ExternalPackages += @{Identity=$library.Key;Source=$cached;Sha256=(Get-FileHash -LiteralPath $cached).Hash}
        }
    }
    [IO.File]::WriteAllText("$sceneConsumer/NuGet.Config",'<configuration><packageSources><clear/><add key="local" value="'+[Security.SecurityElement]::Escape($sceneFeed)+'"/></packageSources></configuration>')
    [IO.File]::WriteAllText("$sceneConsumer/global.json","{`"sdk`":{`"version`":`"$($trueMoonBuildProfile.Sdk)`",`"allowPrerelease`":false,`"rollForward`":`"latestMinor`"}}")
    [IO.File]::WriteAllText("$sceneConsumer/Directory.Build.props","<Project><PropertyGroup><TargetFramework>$($trueMoonBuildProfile.Framework)</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><LangVersion>latest</LangVersion><NuGetAudit>false</NuGetAudit></PropertyGroup></Project>")
    foreach ($project in @('Parent','Runtime','Game')) { [void][IO.Directory]::CreateDirectory("$sceneConsumer/$project") }
    foreach ($project in @('Parent','Runtime','Game')) {
        $output=if($project -eq 'Game'){''}else{'<OutputType>Exe</OutputType>'}
        $assembly=if($project -eq 'Game'){'Game.Logic'}else{$project}
        $root=if($project -eq 'Game'){'Game.Logic'}elseif($project -eq 'Runtime'){'TrueMoon.Examples.AetherRuntimeHost'}else{'EngineSessionProbe'}
        [IO.File]::WriteAllText("$sceneConsumer/$project/$project.csproj",@"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>$output<AssemblyName>$assembly</AssemblyName><RootNamespace>$root</RootNamespace></PropertyGroup><ItemGroup><PackageReference Include="TrueMoon" Version="$sceneVersion"/><PackageReference Include="TrueMoon.Aether" Version="$sceneVersion"/></ItemGroup></Project>
"@)
    }
    Copy-Item -LiteralPath (Join-Path $sceneWorkspace 'ManualTests/Game.Logic/WorldProbeGame.cs') -Destination "$sceneConsumer/Game/WorldProbeGame.cs"
    Copy-Item -LiteralPath (Join-Path $sceneWorkspace 'ManualTests/EngineSessionProbe/SceneWorldRuntimeProbe.cs') -Destination "$sceneConsumer/Parent/SceneWorldRuntimeProbe.cs"
    Copy-Item -LiteralPath (Join-Path $sceneWorkspace 'Examples/AetherRuntimeHost/Program.cs') -Destination "$sceneConsumer/Runtime/Program.cs"
    [IO.File]::WriteAllText("$sceneConsumer/Parent/Program.cs",'return await EngineSessionProbe.SceneWorldRuntimeProbe.RunAsync(Path.GetFullPath(args[0]), Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));')
    [IO.File]::WriteAllText("$sceneConsumer/Consumer.slnx",'<Solution><Project Path="Parent/Parent.csproj"/><Project Path="Runtime/Runtime.csproj"/><Project Path="Game/Game.csproj"/></Solution>')
    Invoke-SceneCommand 'fresh-restore' @('restore','Consumer.slnx','--configfile',"$sceneConsumer/NuGet.Config",'--no-http-cache',"-bl:$sceneEvidence/fresh-restore-{}.binlog") $sceneConsumer -isolated
    foreach ($project in @('Parent','Runtime','Game')) {
        $assets=Get-Content -LiteralPath "$sceneConsumer/$project/obj/project.assets.json" -Raw | ConvertFrom-Json -AsHashtable
        if ($assets.libraries.Values.Where({$_.type -eq 'project'}).Count) { throw 'Fresh consumer contains a ProjectReference.' }
        foreach ($id in $scenePackages) {
            $key="$id/$sceneVersion"
            if (!$assets.libraries.ContainsKey($key)) { throw "Fresh graph missing $key" }
            $metadata=Get-Content -LiteralPath (Join-Path $sceneCache ($assets.libraries[$key].path+'/.nupkg.metadata')) -Raw | ConvertFrom-Json
            if ([IO.Path]::GetFullPath($metadata.source) -ne $sceneFeed) { throw 'Package was not restored from the new local feed.' }
        }
        $sceneReport.Graphs += @{Project=$project;ProjectReferences=0;LocalPackages=$scenePackages;AssetsSha256=(Get-FileHash -LiteralPath "$sceneConsumer/$project/obj/project.assets.json").Hash}
        Invoke-SceneCommand ('fresh-publish-'+$project) @('publish',"$project/$project.csproj",'--no-restore','-c','Release','-o',"$sceneRun/fresh/$project",'-p:UseAppHost=false',"-bl:$sceneEvidence/fresh-publish-$project-{}.binlog") $sceneConsumer -isolated
    }
    $freshOutput=Join-Path $sceneEvidence 'fresh-proof'
    Invoke-SceneCommand 'fresh-proof' @("$sceneRun/fresh/Parent/Parent.dll","$sceneRun/fresh/Runtime/Runtime.dll","$sceneRun/fresh/Game",$freshOutput) $sceneCwd -isolated
    $sceneReport.Profiles += @{Name='Fresh isolated NuGet Release';Report=(Join-Path $freshOutput 'verification.json')}
    foreach ($source in $sceneReport.Sources) { if ((Get-FileHash -LiteralPath $source.Path).Hash -ne $source.Sha256) { throw 'Source changed during consumer acceptance.' } }
    foreach ($profile in $sceneReport.Profiles) {
        $result=Get-Content -LiteralPath $profile.Report -Raw | ConvertFrom-Json
        if (!$result.Completed) { throw "Incomplete profile $($profile.Name)" }
    }
    $sceneReport.Completed=$true
}
catch { $sceneReport.Failure=$_.Exception.ToString(); throw }
finally { Save-SceneReport; Write-Host "Evidence: $sceneEvidence; independent consumer: $sceneRun" }
