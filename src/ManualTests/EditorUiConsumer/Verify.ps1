[CmdletBinding()]
param([Parameter(Mandatory)][string]$ValidationDirectory,[Parameter(Mandatory)][string]$EvidenceDirectory,[string]$PackageVerification)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
. (Join-Path $PSScriptRoot '../PackageDelivery.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'));$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
$run=Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-ARCH35-'+[Guid]::NewGuid().ToString('N'))
$validation=[IO.Path]::GetFullPath($ValidationDirectory)
if(Test-Path -LiteralPath $evidence){throw 'Use a fresh evidence directory.'}
foreach($file in @('VkLayer_khronos_validation.json','VkLayer_khronos_validation.dll')){if(!(Test-Path -LiteralPath "$validation/$file")){throw 'Existing Vulkan validation layer is required.'}}
foreach($directory in @($evidence,$run,"$run/feed","$run/cache","$run/unrelated working directory")){[void][IO.Directory]::CreateDirectory($directory)}
$profile=Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory $evidence
$sharedDelivery=Import-TrueMoonPackageDelivery -Manifest $PackageVerification -Profile $profile
$version=if($sharedDelivery){$sharedDelivery.Version}else{$profile.Version+'.arch35.'+(Get-Date -Format 'yyyyMMddHHmmss')}
$packages=@('TrueMoon.Contracts','TrueMoon.Core','TrueMoon.Cobalt','TrueMoon','TrueMoon.Extensions.DependencyInjection','TrueMoon.Enerit.Generator','TrueMoon.Enerit','TrueMoon.Mithril','TrueMoon.Argentis','TrueMoon.Alloy','TrueMoon.Alloy.Rendering.Skia','TrueMoon.Alloy.Platform.Silk','TrueMoon.Alloy.Hosting','TrueMoon.Aether.Abstractions','TrueMoon.Aether.Runtime','TrueMoon.Aether.Tooling','TrueMoon.Aether.Editor.UI')
$report=[ordered]@{Completed=$false;Stage='ARCH3.5';Version=$version;BuildProfile=$profile;RunDirectory=$run;Commands=@();Packages=@();Graphs=@();Profiles=@();Sources=@();RuntimeOnly=$null;Failure=$null}
function Save-Report{$report | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath "$evidence/verification.json" -Encoding utf8}
function Invoke-Proof([string]$name,[string[]]$arguments,[string]$cwd=$workspace,[switch]$isolated,[string]$executable='dotnet',[switch]$runtimeOnly){
    if($arguments[0] -in @('build','restore','pack','publish')){$arguments+="-bl:$evidence/$name-{}.binlog"}
    $info=[Diagnostics.ProcessStartInfo]::new($executable);$info.WorkingDirectory=$cwd;$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    $info.Environment['VK_LAYER_PATH']=$validation
    if($isolated){$info.Environment['NUGET_PACKAGES']="$run/cache";$info.Environment['NUGET_HTTP_CACHE_PATH']="$run/http-cache"}
    if($runtimeOnly){
        $root=[IO.Path]::GetDirectoryName($executable)
        $info.Environment['DOTNET_ROOT']=$root;$info.Environment['DOTNET_ROOT_X64']=$root;$info.Environment['DOTNET_HOST_PATH']=$executable
        $info.Environment['DOTNET_MULTILEVEL_LOOKUP']='0';$info.Environment['PATH']=$root+';'+[Environment]::GetFolderPath('System')
    }
    foreach($argument in $arguments){[void]$info.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::Start($info);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    try{
        if(!$process.WaitForExit(180000)){$process.Kill($true);$process.WaitForExit();throw "$name timed out"}
        [IO.File]::WriteAllText("$evidence/$name.stdout.log",$stdout.GetAwaiter().GetResult());[IO.File]::WriteAllText("$evidence/$name.stderr.log",$stderr.GetAwaiter().GetResult())
        $report.Commands+=@{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode;WorkingDirectory=$cwd;Isolated=[bool]$isolated;RuntimeOnly=[bool]$runtimeOnly};Save-Report
        if($process.ExitCode -ne 0){throw "$name failed"};Write-Host "$name passed"
    }finally{if(!$process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
function Run-Profile([string]$name,[string]$assembly,[string]$game,[string]$publisher,[switch]$isolated,[string]$runtimeHost='dotnet'){
    Invoke-Proof "$name-artifact" @($publisher,$game,$assembly,"$run/artifacts-$name",'RenderSurface.Game.dll','RenderSurface.Game.MeshGame','runtime') "$run/unrelated working directory" -isolated:$isolated
    $artifact=Get-Content -LiteralPath "$evidence/$name-artifact.stdout.log" -Raw | ConvertFrom-Json
    foreach($provider in @('cobalt','microsoft')){
        $output="$evidence/$name-$provider";$arguments=@($assembly,$artifact.DirectoryPath,$output);if($provider -eq 'microsoft'){$arguments+='microsoft'}
        Invoke-Proof "$name-$provider" $arguments "$run/unrelated working directory" -isolated:$isolated -executable $runtimeHost -runtimeOnly:($runtimeHost -ne 'dotnet')
        $proof=Get-Content -LiteralPath "$output/runtime-ui.json" -Raw | ConvertFrom-Json;$child=Get-Content -LiteralPath "$output/child.json" -Raw | ConvertFrom-Json
        if(!$proof.Completed -or $proof.Checks -lt 28 -or $proof.Captures.Count -ne 2 -or $proof.Forbidden.Count -or $proof.GameLoadedInParent -or $proof.Resources -or $proof.Handles -or
            $child.Forbidden.Count -or $child.Resources -or $child.Handles -or !$child.Validation.ValidationEnabled -or $child.Validation.Errors -or $child.Validation.Warnings -or
            $child.Validation.LiveSlots -or $child.Validation.OpenHandles -or $child.Validation.SurfacesCreated -ne $child.Validation.SurfacesReleased){throw 'Runtime UI acceptance mismatch.'}
        $report.Profiles+=@{Name="$name-$provider";Report="$output/runtime-ui.json";Sha256=(Get-FileHash -LiteralPath "$output/runtime-ui.json").Hash;ChildReport="$output/child.json";ChildSha256=(Get-FileHash -LiteralPath "$output/child.json").Hash;Checks=$proof.Checks};Save-Report
    }
}
try{
    if($PackageVerification){$report.Sources+=@{Path=[IO.Path]::GetFullPath($PackageVerification);Sha256=(Get-FileHash -LiteralPath $PackageVerification).Hash}}
    foreach($module in @($packages+'TrueMoon.Cobalt.Generator'+'ManualTests/EditorUiConsumer'+'ManualTests/RenderSurface.Game'+'ManualTests/RuntimeArtifactTool')){
        foreach($path in (& rg --files "$workspace/$module" -g '*.cs' -g '*.csproj' -g '*.ps1' -g 'PACKAGE.md')){$report.Sources+=@{Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash}}
    }
    foreach($spec in @(@{Name='probe';Path='ManualTests/EditorUiConsumer/EditorUiConsumer.csproj'},@{Name='game';Path='ManualTests/RenderSurface.Game/RenderSurface.Game.csproj'},@{Name='publisher';Path='ManualTests/RuntimeArtifactTool/RuntimeArtifactTool.csproj'})){
        Invoke-Proof ('build-'+$spec.Name) @('build',$spec.Path,'-c','Debug','--verbosity','quiet')
    }
    Run-Profile 'source' "$workspace/ManualTests/EditorUiConsumer/bin/Debug/$($profile.Framework)/EditorUiConsumer.dll" "$workspace/ManualTests/RenderSurface.Game/bin/Debug/$($profile.Framework)" "$workspace/ManualTests/RuntimeArtifactTool/bin/Debug/$($profile.Framework)/RuntimeArtifactTool.dll"
    foreach($spec in @(@{Name='probe';Path='ManualTests/EditorUiConsumer/EditorUiConsumer.csproj'},@{Name='game';Path='ManualTests/RenderSurface.Game/RenderSurface.Game.csproj'},@{Name='publisher';Path='ManualTests/RuntimeArtifactTool/RuntimeArtifactTool.csproj'})){
        Invoke-Proof ('publish-'+$spec.Name) @('publish',$spec.Path,'-c','Release','-o',"$run/published/$($spec.Name)",'--verbosity','quiet')
    }
    Run-Profile 'published' "$run/published/probe/EditorUiConsumer.dll" "$run/published/game" "$run/published/publisher/RuntimeArtifactTool.dll"
    foreach($id in $packages){
        if($sharedDelivery){Copy-TrueMoonDeliveryPackage -Delivery $sharedDelivery -Id $id -Destination "$run/feed"}
        else{Invoke-Proof ('pack-'+$id) @('pack',"$id/$id.csproj",'-c','Release','-o',"$run/feed","-p:Version=$version",'--verbosity','quiet')}
        $path="$run/feed/$id.$version.nupkg";$report.Packages+=@{Id=$id;Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash}
    }
    $external=@{}
    foreach($id in @($packages+'TrueMoon.Cobalt.Generator')){
        $assets=Get-Content -LiteralPath "$workspace/$id/obj/project.assets.json" -Raw | ConvertFrom-Json -AsHashtable
        foreach($library in $assets.libraries.GetEnumerator()){
            if($library.Value.type -ne 'package' -or $external.ContainsKey($library.Key)){continue}
            $relative=$library.Key.ToLowerInvariant();$parts=$relative.Split('/');$cached=$null
            foreach($folder in $assets.packageFolders.Keys){$candidate=Join-Path $folder "$relative/$($parts[0]).$($parts[1]).nupkg";if(Test-Path -LiteralPath $candidate){$cached=$candidate;break}}
            if(!$cached){throw "Missing exact external package $($library.Key)"};Copy-Item -LiteralPath $cached -Destination "$run/feed";$external[$library.Key]=$cached
        }
    }
    $fresh="$run/fresh";Write-TrueMoonConsumerBuildProfile -Profile $profile -Directory $fresh
    [IO.File]::WriteAllText("$fresh/NuGet.Config",'<configuration><packageSources><clear/><add key="fresh" value="'+[Security.SecurityElement]::Escape("$run/feed")+'"/></packageSources></configuration>')
    [IO.File]::WriteAllText("$fresh/Directory.Build.targets",'<Project/>')
    $specs=@(@{Name='probe';Assembly='EditorUiConsumer';Namespace='TrueMoon.ManualTests.EditorUiConsumer';References=@('TrueMoon','TrueMoon.Aether.Editor.UI','TrueMoon.Extensions.DependencyInjection');Exe=$true},@{Name='game';Assembly='RenderSurface.Game';Namespace='RenderSurface.Game';References=@('TrueMoon.Aether.Runtime');Exe=$false},@{Name='publisher';Assembly='RuntimeArtifactTool';Namespace='TrueMoon.ManualTests.RuntimeArtifactTool';References=@('TrueMoon.Aether.Tooling');Exe=$true})
    foreach($spec in $specs){
        [void][IO.Directory]::CreateDirectory("$fresh/$($spec.Name)");$refs=($spec.References|ForEach-Object {'<PackageReference Include="'+$_+'" Version="'+$version+'"/>'}) -join '';$output=if($spec.Exe){'<OutputType>Exe</OutputType>'}else{''}
        [IO.File]::WriteAllText("$fresh/$($spec.Name)/Consumer.csproj",'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'+$output+'<AssemblyName>'+$spec.Assembly+'</AssemblyName><RootNamespace>'+$spec.Namespace+'</RootNamespace></PropertyGroup><ItemGroup>'+$refs+'</ItemGroup></Project>')
        if($spec.Name -eq 'probe'){Copy-Item -LiteralPath "$PSScriptRoot/Program.cs" -Destination "$fresh/probe/Program.cs"}
        elseif($spec.Name -eq 'publisher'){Copy-Item -LiteralPath "$workspace/ManualTests/RuntimeArtifactTool/Program.cs" -Destination "$fresh/publisher/Program.cs"}
        else{foreach($file in @('MeshGame.cs','FailedMeshGame.cs')){Copy-Item -LiteralPath "$workspace/ManualTests/RenderSurface.Game/$file" -Destination "$fresh/game/$file"}}
        Invoke-Proof ('fresh-restore-'+$spec.Name) @('restore',"$($spec.Name)/Consumer.csproj",'--configfile',"$fresh/NuGet.Config",'--no-http-cache') $fresh -isolated
        $graph=Get-Content -LiteralPath "$fresh/$($spec.Name)/obj/project.assets.json" -Raw | ConvertFrom-Json -AsHashtable
        if(@($graph.libraries.Values|Where-Object type -ne 'package').Count -or $graph.packageFolders.Count -ne 1 -or [IO.Path]::GetFullPath(@($graph.packageFolders.Keys)[0]).TrimEnd('\','/') -ne [IO.Path]::GetFullPath("$run/cache").TrimEnd('\','/')){throw 'Non-isolated package graph.'}
        if($spec.Name -eq 'probe' -and @($graph.libraries.Keys|Where-Object {$_ -match '^TrueMoon\.Aether/|^TrueMoon\.Aether\.(Tooling|Editor\.Tooling)/'}).Count){throw 'Runtime UI graph contains Tooling'}
        foreach($key in @($graph.libraries.Keys|Where-Object {$_ -match '^TrueMoon(?:\.[^/]+)?/'})){
            if(!$key.EndsWith('/'+$version,[StringComparison]::Ordinal)){throw 'Stale local package'}
            $metadata=Get-Content -LiteralPath (Join-Path "$run/cache" ($graph.libraries[$key].path+'/.nupkg.metadata')) -Raw | ConvertFrom-Json
            if([IO.Path]::GetFullPath($metadata.source) -ne [IO.Path]::GetFullPath("$run/feed")){throw 'Wrong local feed'}
        }
        $report.Graphs+=@{Project=$spec.Name;ProjectReferences=0;LocalPackages=@($graph.libraries.Keys|Where-Object {$_ -like 'TrueMoon*'});AssetsSha256=(Get-FileHash -LiteralPath "$fresh/$($spec.Name)/obj/project.assets.json").Hash}
        Invoke-Proof ('fresh-publish-'+$spec.Name) @('publish',"$($spec.Name)/Consumer.csproj",'--no-restore','-c','Release','-o',"$run/fresh-published/$($spec.Name)",'--verbosity','quiet') $fresh -isolated
    }
    [void][IO.Directory]::CreateDirectory("$run/fresh-published/probe/Assets/Fonts")
    foreach($file in @('NotoSans-Regular.ttf','OFL.txt')){Copy-Item -LiteralPath "$workspace/Examples/AetherEditor/Assets/Fonts/$file" -Destination "$run/fresh-published/probe/Assets/Fonts/$file"}
    foreach($id in @('TrueMoon.Aether.Runtime','TrueMoon.Aether.Editor.UI')){
        $installed="$run/cache/$($id.ToLowerInvariant())/$version/lib/$($profile.Framework)/$id.dll"
        if((Get-FileHash -LiteralPath $installed).Hash -ne (Get-FileHash -LiteralPath "$run/fresh-published/probe/$id.dll").Hash){throw 'Installed/output bytes differ'}
    }
    Run-Profile 'fresh' "$run/fresh-published/probe/EditorUiConsumer.dll" "$run/fresh-published/game" "$run/fresh-published/publisher/RuntimeArtifactTool.dll" -isolated
    $dotnetRoot=[IO.Path]::GetDirectoryName((Get-Command dotnet).Source);$configuration=Get-Content -LiteralPath "$run/fresh-published/probe/EditorUiConsumer.runtimeconfig.json" -Raw | ConvertFrom-Json
    $runtimeVersion=$configuration.runtimeOptions.framework.version;$runtimeOnly="$run/runtime-only";[void][IO.Directory]::CreateDirectory($runtimeOnly)
    Copy-Item -LiteralPath "$dotnetRoot/dotnet.exe" -Destination "$runtimeOnly/dotnet.exe"
    foreach($relative in @("host/fxr/$runtimeVersion","shared/Microsoft.NETCore.App/$runtimeVersion")){
        $destination=Join-Path $runtimeOnly $relative;[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination));Copy-Item -LiteralPath (Join-Path $dotnetRoot $relative) -Destination $destination -Recurse
    }
    Invoke-Proof 'runtime-only-sdks' @('--list-sdks') "$run/unrelated working directory" -executable "$runtimeOnly/dotnet.exe" -runtimeOnly
    if((Test-Path -LiteralPath "$runtimeOnly/sdk") -or ![string]::IsNullOrWhiteSpace([IO.File]::ReadAllText("$evidence/runtime-only-sdks.stdout.log"))){throw 'Runtime-only root discovered an SDK'}
    foreach($forbidden in @('TrueMoon.Aether.Tooling.dll','TrueMoon.Aether.Editor.Tooling.dll','TrueMoon.Aether.dll')){if(Test-Path -LiteralPath "$run/fresh-published/probe/$forbidden"){throw 'Tooling DLL delivered to Runtime UI'}}
    Run-Profile 'sdk-free' "$run/fresh-published/probe/EditorUiConsumer.dll" "$run/fresh-published/game" "$run/fresh-published/publisher/RuntimeArtifactTool.dll" -isolated -runtimeHost "$runtimeOnly/dotnet.exe"
    $report.RuntimeOnly=@{Root=$runtimeOnly;RuntimeVersion=$runtimeVersion;SdkCount=0;ToolingDelivered=$false;CompilationAtExecution=$false}
    foreach($source in $report.Sources){if((Get-FileHash -LiteralPath $source.Path).Hash -ne $source.Sha256){throw 'Source drift; rerun from frozen inputs.'}}
    $report.Completed=$true
}catch{$report.Failure=$_.Exception.ToString();throw}finally{Save-Report;Write-Host "Evidence: $evidence"}
