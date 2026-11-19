[CmdletBinding()]
param([Parameter(Mandatory)][string]$ValidationDirectory, [string]$EvidenceDirectory, [string]$RunDirectory, [switch]$BaseColorTexture)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = if ($EvidenceDirectory) { [IO.Path]::GetFullPath($EvidenceDirectory) } else { Join-Path $workspace ('TestResults/RenderSurface-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$run = if ($RunDirectory) { [IO.Path]::GetFullPath($RunDirectory) } else { Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-RenderSurface-' + [Guid]::NewGuid().ToString('N')) }
if ((Test-Path -LiteralPath $evidence) -or (Test-Path -LiteralPath $run)) { throw 'Use fresh run/evidence directories.' }
if ($run -eq $workspace -or $run.StartsWith($workspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'RunDirectory must be outside the checkout.' }
$validation = [IO.Path]::GetFullPath($ValidationDirectory)
foreach ($file in @('VkLayer_khronos_validation.json','VkLayer_khronos_validation.dll')) { if (!(Test-Path -LiteralPath (Join-Path $validation $file))) { throw 'Existing validation layer is required.' } }
foreach ($path in @($evidence, $run, "$run/feed", "$run/cache", "$run/unrelated working directory")) { [void][IO.Directory]::CreateDirectory($path) }
$surfaceBuildProfile = Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory $evidence
$version = if ($BaseColorTexture) { $surfaceBuildProfile.Version } else { $surfaceBuildProfile.Version + '.meshsurface.' + (Get-Date -Format 'yyyyMMddHHmmss') }
$stage = if ($BaseColorTexture) { 'ARCH1f.5' } else { 'ARCH1f.4' }
$textureProject = "$run/texture-game/TextureGame.csproj"
$dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source
$packages = @('TrueMoon.Contracts','TrueMoon.Core','TrueMoon.Cobalt','TrueMoon','TrueMoon.Extensions.DependencyInjection','TrueMoon.Enerit.Generator',
    'TrueMoon.Enerit','TrueMoon.Mithril','TrueMoon.Argentis','TrueMoon.Alloy','TrueMoon.Alloy.Rendering.Skia','TrueMoon.Alloy.Platform.Silk',
    'TrueMoon.Alloy.Hosting','TrueMoon.Aether.Abstractions','TrueMoon.Aether.Runtime','TrueMoon.Aether.Tooling','TrueMoon.Aether.Editor.UI','TrueMoon.Aether.Editor.Tooling')
$report = [ordered]@{ Completed=$false; Stage=$stage; RunDirectory=$run; Version=$version; BuildProfile=$surfaceBuildProfile; Commands=@(); Packages=@(); ExternalPackages=@();
    Sources=@(); Graphs=@(); Profiles=@(); Validation=@(); Failure=$null }
function Save-Report { $report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath "$evidence/verification.json" -Encoding utf8 }
function Invoke-Proof([string]$name, [string[]]$arguments, [string]$cwd=$workspace, [switch]$isolated) {
    if ($arguments[0] -in @('build','publish','pack','restore')) { $arguments += "-bl:$evidence/$name-{}.binlog" }
    $info = [Diagnostics.ProcessStartInfo]::new($dotnet); $info.WorkingDirectory=$cwd; $info.UseShellExecute=$false; $info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true
    $info.Environment['TRUEMOON_GPU_VALIDATION']='1'; $info.Environment['VK_LAYER_PATH']=$validation
    if ($isolated) { $info.Environment['NUGET_PACKAGES']="$run/cache"; $info.Environment['NUGET_HTTP_CACHE_PATH']="$run/http-cache"; $info.Environment['NUGET_PLUGINS_CACHE_PATH']="$run/plugins-cache" }
    foreach ($argument in $arguments) { [void]$info.ArgumentList.Add($argument) }
    $process=[Diagnostics.Process]::Start($info); $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync(); $timeout=$false
    try {
        if (!$process.WaitForExit(240000)) { $timeout=$true; $process.Kill($true); $process.WaitForExit() }
        [IO.File]::WriteAllText("$evidence/$name.stdout.log",$stdout.GetAwaiter().GetResult()); [IO.File]::WriteAllText("$evidence/$name.stderr.log",$stderr.GetAwaiter().GetResult())
        $report.Commands+=@{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode;TimedOut=$timeout;WorkingDirectory=$cwd;Isolated=[bool]$isolated}; Save-Report
        if ($timeout -or $process.ExitCode -ne 0) { throw "$name failed; outputs preserved." }
        if ($arguments[0] -in @('build','publish','pack','restore') -and !(Get-ChildItem -LiteralPath $evidence -Filter "$name-*.binlog")) { throw 'Missing unique binlog.' }
        Write-Host "$name passed"
    } finally { if (!$process.HasExited) { $process.Kill($true); $process.WaitForExit() }; $process.Dispose() }
}
function Run-Profile([string]$name,[string]$probe,[string]$runtime,[string]$game,[switch]$isolated) {
    $output="$evidence/$name"
    $arguments = @($probe,'--mesh-surface-proof',$runtime,$game,$output)
    if ($BaseColorTexture) { $arguments += $textureProject }
    Invoke-Proof $name $arguments "$run/unrelated working directory" -isolated:$isolated
    $proof=Get-Content -LiteralPath "$output/verification.json" -Raw | ConvertFrom-Json
    if (!$proof.Completed -or $proof.RuntimeCleanup.Count -ne 3 -or $proof.Resources -ne 0 -or
        $proof.Handles -ne 0 -or $proof.ActiveUiSessions -ne 0 -or $proof.GameLoadedInParent) { throw 'Native proof scope mismatch.' }
    if ($BaseColorTexture) {
        if ($proof.Scenario -ne 'base-color-texture' -or $proof.Captures.Count -ne 3 -or
            @($proof.Captures | Where-Object { !$_.TextureRevision -or $_.Samples.Count -ne 6 }).Count) { throw 'Texture proof scope mismatch.' }
    } elseif (!$proof.FailedStartupObserved -or !$proof.UnsupportedReplacePreservedGeneration -or $proof.Captures.Count -ne 10) {
        throw 'Mesh proof scope mismatch.'
    }
    $report.Profiles+=@{Name=$name;Report="$output/verification.json";Sha256=(Get-FileHash -LiteralPath "$output/verification.json").Hash;Captures=$proof.Captures.Count;Generations=$proof.RuntimeCleanup.Count}
    Save-Report
}
try {
    $inputs = @('Directory.Build.props','Directory.Packages.props','global.json','TrueMoon.slnx','ManualTests/BuildProfile.ps1','ManualTests/RenderSurface/Verify.ps1',
        'ManualTests/EngineSessionProbe/MeshSurfaceProbe.cs','ManualTests/EngineSessionProbe/GpuCapabilityCollector.cs','ManualTests/EngineSessionProbe/EngineSessionProbe.csproj','Examples/AetherRuntimeHost/Program.cs',
        'Examples/AetherRuntimeHost/AetherRuntimeHost.csproj','ManualTests/RenderSurface.Game/RenderSurface.Game.csproj','ManualTests/RenderSurface.Game/MeshGame.cs','ManualTests/RenderSurface.Game/FailedMeshGame.cs',
        'ManualTests/RenderSurface.Game/TexturedMeshGame.cs')
    $projects = @('TrueMoon.Aether.Tooling','TrueMoon.Aether.Editor.UI','TrueMoon.Aether.Editor.Tooling','TrueMoon.Aether.Abstractions','TrueMoon.Aether.Runtime')
    $inputs += Get-ChildItem ($projects | ForEach-Object { Join-Path $workspace $_ }) -Recurse -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in @('.cs','.csproj','.spv','.vert','.frag','.md') } | ForEach-Object { [IO.Path]::GetRelativePath($workspace,$_.FullName) }
    foreach ($sourcePath in $inputs | Sort-Object -Unique) { $path=Join-Path $workspace $sourcePath; $report.Sources+=@{Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash} }
    if ($BaseColorTexture) {
        $textureSource = Split-Path -Parent $textureProject
        Write-TrueMoonConsumerBuildProfile -Profile $surfaceBuildProfile -Directory $textureSource
        Copy-Item -LiteralPath "$workspace/ManualTests/RenderSurface.Game/TexturedMeshGame.cs" -Destination "$textureSource/TexturedMeshGame.cs"
        [IO.File]::WriteAllText("$textureSource/NuGet.Config",'<configuration><packageSources><clear/></packageSources></configuration>')
        [IO.File]::WriteAllText("$textureSource/Directory.Build.targets",'<Project/>')
        [IO.File]::WriteAllText($textureProject, @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><AssemblyName>RenderSurface.Game</AssemblyName><DebugType>none</DebugType></PropertyGroup>
  <ItemGroup>
    <Reference Include="TrueMoon.Aether.Abstractions" HintPath="$(AetherAbstractionsPath)" Private="false" />
    <Reference Include="TrueMoon.Aether.Runtime" HintPath="$(AetherRuntimePath)" Private="false" />
    <Reference Include="TrueMoon.Contracts" HintPath="$(TrueMoonContractsPath)" Private="false" />
  </ItemGroup>
</Project>
'@)
    }
    foreach ($file in @('VkLayer_khronos_validation.json','VkLayer_khronos_validation.dll')) { $path=Join-Path $validation $file; $report.Validation+=@{Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash} }
    Invoke-Proof 'source-runtime' @('build','Examples/AetherRuntimeHost/AetherRuntimeHost.csproj','-c','Debug','--verbosity','quiet')
    Invoke-Proof 'source-game' @('build','ManualTests/RenderSurface.Game/RenderSurface.Game.csproj','-c','Debug','--verbosity','quiet')
    Invoke-Proof 'source-probe' @('build','ManualTests/EngineSessionProbe/EngineSessionProbe.csproj','-c','Debug','--verbosity','quiet')
    Run-Profile 'source-native' "$workspace/ManualTests/EngineSessionProbe/bin/Debug/$($surfaceBuildProfile.TestFramework)/EngineSessionProbe.dll" "$workspace/Examples/AetherRuntimeHost/bin/Debug/$($surfaceBuildProfile.Framework)/AetherRuntimeHost.dll" "$workspace/ManualTests/RenderSurface.Game/bin/Debug/$($surfaceBuildProfile.Framework)"
    foreach ($item in @(@('runtime','Examples/AetherRuntimeHost/AetherRuntimeHost.csproj'),@('game','ManualTests/RenderSurface.Game/RenderSurface.Game.csproj'),@('probe','ManualTests/EngineSessionProbe/EngineSessionProbe.csproj'))) {
        Invoke-Proof ('publish-'+$item[0]) @('publish',$item[1],'-c','Release','-o',"$run/published/$($item[0])",'-p:UseAppHost=false','--verbosity','quiet')
    }
    Run-Profile 'published-native' "$run/published/probe/EngineSessionProbe.dll" "$run/published/runtime/AetherRuntimeHost.dll" "$run/published/game"
    # All packages are freshly compiled with one version; exact restored external bytes populate an offline feed.
    foreach ($id in $packages) {
        Invoke-Proof ('pack-'+$id) @('pack',"$id/$id.csproj",'-c','Release','-o',"$run/feed","-p:Version=$version",'--verbosity','quiet')
        $path="$run/feed/$id.$version.nupkg"; $report.Packages+=@{Id=$id;Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash}
    }
    $external=@{}
    foreach ($id in @($packages+'TrueMoon.Cobalt.Generator')) {
        $assets=Get-Content -LiteralPath "$workspace/$id/obj/project.assets.json" -Raw | ConvertFrom-Json -AsHashtable
        foreach ($library in $assets.libraries.GetEnumerator()) {
            if ($library.Value.type -ne 'package' -or $external.ContainsKey($library.Key)) { continue }
            $relative=$library.Key.ToLowerInvariant(); $parts=$relative.Split('/'); $file="$($parts[0]).$($parts[1]).nupkg"; $cached=$null
            foreach ($folder in $assets.packageFolders.Keys) { $candidate=Join-Path $folder "$relative/$file"; if (Test-Path -LiteralPath $candidate) { $cached=$candidate; break } }
            if (!$cached) { throw "Missing exact cached package $($library.Key)" }
            Copy-Item -LiteralPath $cached -Destination "$run/feed/$file"; $external[$library.Key]=$cached
            $report.ExternalPackages+=@{Identity=$library.Key;Source=$cached;Sha256=(Get-FileHash -LiteralPath $cached).Hash}
        }
    }
    $fresh="$run/fresh"; Write-TrueMoonConsumerBuildProfile -Profile $surfaceBuildProfile -Directory $fresh
    [IO.File]::WriteAllText("$fresh/NuGet.Config",'<configuration><packageSources><clear/><add key="fresh" value="'+[Security.SecurityElement]::Escape("$run/feed")+'"/></packageSources></configuration>')
    [IO.File]::WriteAllText("$fresh/Directory.Build.targets",'<Project/>')
    foreach ($name in @('probe','runtime','game')) { [void][IO.Directory]::CreateDirectory("$fresh/$name") }
    Copy-Item -LiteralPath "$workspace/ManualTests/EngineSessionProbe/MeshSurfaceProbe.cs" -Destination "$fresh/probe/MeshSurfaceProbe.cs"
    Copy-Item -LiteralPath "$workspace/ManualTests/EngineSessionProbe/GpuCapabilityCollector.cs" -Destination "$fresh/probe/GpuCapabilityCollector.cs"
    [IO.File]::WriteAllText("$fresh/probe/Program.cs",'return await EngineSessionProbe.MeshSurfaceProbe.RunAsync(Path.GetFullPath(args[1]),Path.GetFullPath(args[2]),Path.GetFullPath(args[3]),args.Length == 5 ? Path.GetFullPath(args[4]) : null);')
    Copy-Item -LiteralPath "$workspace/Examples/AetherRuntimeHost/Program.cs" -Destination "$fresh/runtime/Program.cs"
    foreach ($file in @('MeshGame.cs','FailedMeshGame.cs')) { Copy-Item -LiteralPath "$workspace/ManualTests/RenderSurface.Game/$file" -Destination "$fresh/game/$file" }
    $specs=@(
        @{Name='probe';Assembly='MeshSurfaceConsumer';Namespace='EngineSessionProbe';References=@('TrueMoon','TrueMoon.Aether.Editor.UI','TrueMoon.Aether.Editor.Tooling');Exe=$true},
        @{Name='runtime';Assembly='AetherRuntimeHost';Namespace='TrueMoon.Examples.AetherRuntimeHost';References=@('TrueMoon','TrueMoon.Aether.Tooling');Exe=$true},
        @{Name='game';Assembly='RenderSurface.Game';Namespace='RenderSurface.Game';References=@('TrueMoon.Aether.Tooling');Exe=$false})
    foreach ($spec in $specs) {
        $refs=($spec.References | ForEach-Object { '<PackageReference Include="'+$_+'" Version="'+$version+'"/>' }) -join ''
        $output=if($spec.Exe){'<OutputType>Exe</OutputType>'}else{''}
        [IO.File]::WriteAllText("$fresh/$($spec.Name)/Consumer.csproj",'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'+$output+'<AllowUnsafeBlocks>true</AllowUnsafeBlocks><AssemblyName>'+$spec.Assembly+'</AssemblyName><RootNamespace>'+$spec.Namespace+'</RootNamespace></PropertyGroup><ItemGroup>'+$refs+'</ItemGroup></Project>')
        Invoke-Proof ('fresh-restore-'+$spec.Name) @('restore',"$($spec.Name)/Consumer.csproj",'--configfile',"$fresh/NuGet.Config",'--no-http-cache') $fresh -isolated
        $graph=Get-Content -LiteralPath "$fresh/$($spec.Name)/obj/project.assets.json" -Raw | ConvertFrom-Json -AsHashtable
        if (@($graph.libraries.Values | Where-Object type -ne 'package').Count -or $graph.packageFolders.Count -ne 1 -or
            [IO.Path]::GetFullPath(@($graph.packageFolders.Keys)[0]).TrimEnd('\','/') -ne [IO.Path]::GetFullPath("$run/cache").TrimEnd('\','/')) { throw 'Fresh graph is not package/cache isolated.' }
        foreach ($key in @($graph.libraries.Keys | Where-Object { $_ -match '^TrueMoon(?:\.[^/]+)?/' })) {
            if (!$key.EndsWith('/'+$version,[StringComparison]::Ordinal)) { throw 'Stale local package.' }
            $metadata=Get-Content -LiteralPath (Join-Path "$run/cache" ($graph.libraries[$key].path+'/.nupkg.metadata')) -Raw | ConvertFrom-Json
            if ([IO.Path]::GetFullPath($metadata.source) -ne [IO.Path]::GetFullPath("$run/feed")) { throw 'Wrong package feed.' }
        }
        $report.Graphs+=@{Project=$spec.Name;ProjectReferences=0;AssetsSha256=(Get-FileHash -LiteralPath "$fresh/$($spec.Name)/obj/project.assets.json").Hash}
        Invoke-Proof ('fresh-publish-'+$spec.Name) @('publish',"$($spec.Name)/Consumer.csproj",'--no-restore','-c','Release','-o',"$run/fresh-published/$($spec.Name)",'-p:UseAppHost=false','--verbosity','quiet') $fresh -isolated
    }
    [void][IO.Directory]::CreateDirectory("$run/fresh-published/probe/Assets/Fonts")
    foreach ($file in @('NotoSans-Regular.ttf','OFL.txt')) { Copy-Item -LiteralPath "$workspace/ManualTests/AlloyTest/Assets/Fonts/$file" -Destination "$run/fresh-published/probe/Assets/Fonts/$file" }
    foreach ($id in @('TrueMoon.Aether.Abstractions','TrueMoon.Aether.Runtime','TrueMoon.Aether.Tooling')) {
        $installed="$run/cache/$($id.ToLowerInvariant())/$version/lib/$($surfaceBuildProfile.Framework)/$id.dll"
        $hash=(Get-FileHash -LiteralPath $installed).Hash
        foreach ($name in @('probe','runtime','game')) {
            if ($hash -ne (Get-FileHash -LiteralPath "$run/fresh-published/$name/$id.dll").Hash) { throw "Installed/output $id bytes differ." }
        }
    }
    Run-Profile 'fresh-native' "$run/fresh-published/probe/MeshSurfaceConsumer.dll" "$run/fresh-published/runtime/AetherRuntimeHost.dll" "$run/fresh-published/game" -isolated
    foreach ($source in $report.Sources) { if ((Get-FileHash -LiteralPath $source.Path).Hash -ne $source.Sha256) { throw 'Source drift; rerun against one frozen source snapshot.' } }
    $report.Completed=$true
} catch { $report.Failure=$_.Exception.ToString(); throw }
finally { Save-Report; Write-Host "Evidence: $evidence; run: $run" }
