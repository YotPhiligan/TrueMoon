#Requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ValidationDirectory, [Parameter(Mandatory)][string]$EvidenceDirectory,
    [switch]$SourceOnly)
$ErrorActionPreference='Stop'; Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory); $validation=[IO.Path]::GetFullPath($ValidationDirectory)
if(Test-Path -LiteralPath $evidence){throw 'Use a fresh evidence directory.'}
foreach($name in @('VkLayer_khronos_validation.json','VkLayer_khronos_validation.dll')){
    if(!(Test-Path -LiteralPath "$validation/$name")){throw 'Provide an existing validation layer.'}
}
$run=Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-ENG7B3-'+[Guid]::NewGuid().ToString('N'))
foreach($path in @($evidence,$run,"$run/feed","$run/cache","$run/unrelated working directory")){[void][IO.Directory]::CreateDirectory($path)}
$profile=Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory $evidence
$version=$profile.Version; $dotnet=(Get-Command dotnet.exe).Source
$report=[ordered]@{Completed=$false;Stage='ENG7b.3';Version=$version;BuildProfile=$profile;SourceOnly=[bool]$SourceOnly;
    RunDirectory=$run;Commands=@();Packages=@();Graphs=@();Profiles=@();ExampleWindows=@();Sources=@();Failure=$null}
function Save{$report|ConvertTo-Json -Depth 25|Set-Content -LiteralPath "$evidence/verification.json" -Encoding utf8}
function Check-Payload([string]$name,[hashtable]$paths){
    foreach($role in @('probe','player','combined','example-player')){
        $root=Split-Path -Parent $paths[$role]
        foreach($file in @('NotoSans-Regular.ttf','OFL.txt')){
            if((Get-FileHash -LiteralPath "$root/Assets/Fonts/$file").Hash -ne (Get-FileHash -LiteralPath "$workspace/Examples/AetherEditor/Assets/Fonts/$file").Hash){throw "Font/license payload mismatch: $name/$role"}
        }
        if((Get-FileHash -LiteralPath "$root/truemoon-native/win-x64/libSkiaSharp.dll").Hash -ne (Get-FileHash -LiteralPath "$workspace/TrueMoon.Alloy.Rendering.Skia/NativeAssets/win-x64/libSkiaSharp.dll").Hash){throw "Extended native payload mismatch: $name/$role"}
    }
    foreach($role in @('probe','player','publisher','combined','example-player','runtime')){
        $root=Split-Path -Parent $paths[$role]
        foreach($assembly in @('TrueMoon.Aether.Runtime.dll','TrueMoon.Aether.Abstractions.dll','TrueMoon.Contracts.dll','TrueMoon.Enerit.dll','TrueMoon.Mithril.dll')){
            if((Get-FileHash -LiteralPath "$root/$assembly").Hash -ne (Get-FileHash -LiteralPath "$(Split-Path -Parent $paths.probe)/$assembly").Hash){throw "Mixed shared assembly bytes: $name/$role/$assembly"}
        }
    }
}
function Run([string]$name,[string[]]$arguments,[string]$cwd=$workspace,[string]$provider='cobalt',
    [string]$executable=$dotnet,[int]$expected=0,[switch]$isolated,[int]$timeout=240){
    if($arguments[0] -in @('build','publish','pack','restore','test')){$arguments+="-bl:$evidence/$name-{}.binlog"}
    $info=[Diagnostics.ProcessStartInfo]::new($executable);$info.WorkingDirectory=$cwd;$info.UseShellExecute=$false;$info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($key in @('TM_ENGINE_BOOTSTRAP','-p','TrueMoon.Mithril.ControlPipe')){[void]$info.Environment.Remove($key)}
    $info.Environment['VK_LAYER_PATH']=$validation;$info.Environment['TRUEMOON_GPU_VALIDATION']='1'
    $info.Environment['TRUEMOON_LAUNCH_PROVIDER']=$provider;$info.Environment['DOTNET_CLI_UI_LANGUAGE']='en'
    if($isolated){$info.Environment['NUGET_PACKAGES']="$run/cache";$info.Environment['NUGET_HTTP_CACHE_PATH']="$run/http-cache"}
    if($executable -ne $dotnet -and [IO.Path]::GetFileName($executable) -eq 'dotnet.exe'){
        $runtimeRoot=Split-Path -Parent $executable
        foreach($key in @('DOTNET_ROOT','DOTNET_ROOT_X64')){$info.Environment[$key]=$runtimeRoot}
        $info.Environment['DOTNET_HOST_PATH']=$executable;$info.Environment['DOTNET_MULTILEVEL_LOOKUP']='0';$info.Environment['PATH']=$runtimeRoot
    }
    foreach($argument in $arguments){[void]$info.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::Start($info);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    $timer=[Diagnostics.Stopwatch]::StartNew()
    try{
        if(!$process.WaitForExit($timeout*1000)){$process.Kill($true);$process.WaitForExit();throw "$name timed out/reaped"}
        $output=$stdout.GetAwaiter().GetResult();$errors=$stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText("$evidence/$name.stdout.log",$output);[IO.File]::WriteAllText("$evidence/$name.stderr.log",$errors)
        $report.Commands+=@{Name=$name;Executable=$executable;Arguments=$arguments;WorkingDirectory=$cwd;ExitCode=$process.ExitCode;Seconds=$timer.Elapsed.TotalSeconds;Pid=$process.Id};Save
        if($process.ExitCode -ne $expected){throw "$name exit $($process.ExitCode), expected $expected; see saved logs"}
        Write-Host "$name passed ($([int]$timer.Elapsed.TotalSeconds)s)"
        return $output
    }finally{if(!$process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
$specs=@(
    @{Name='probe';Project='ManualTests/LaunchProfiles/LaunchProfiles.csproj';Assembly='LaunchProfiles';Namespace='TrueMoon.ManualTests.LaunchProfiles';Modules=@('ManualTests/LaunchProfiles');Extra=@('ManualTests/PlayerProbe/Program.cs','ManualTests/AetherEditorHost/RuntimeEntryPoint.cs');References=@('TrueMoon.Aether.Editor.Tooling','TrueMoon.Aether.Player','TrueMoon.Extensions.DependencyInjection');Startup='TrueMoon.ManualTests.LaunchProfiles.Program'},
    @{Name='player';Project='ManualTests/PlayerProbe/PlayerProbe.csproj';Assembly='PlayerProbe';Namespace='TrueMoon.ManualTests.PlayerProbe';Modules=@('ManualTests/PlayerProbe');References=@('TrueMoon','TrueMoon.Aether.Player','TrueMoon.Extensions.DependencyInjection')},
    @{Name='game';Project='ManualTests/RenderSurface.Game/RenderSurface.Game.csproj';Assembly='RenderSurface.Game';Namespace='RenderSurface.Game';Modules=@('ManualTests/RenderSurface.Game');References=@('TrueMoon.Aether.Runtime');Library=$true},
    @{Name='publisher';Project='ManualTests/RuntimeArtifactTool/RuntimeArtifactTool.csproj';Assembly='RuntimeArtifactTool';Namespace='TrueMoon.ManualTests.RuntimeArtifactTool';Modules=@('ManualTests/RuntimeArtifactTool');References=@('TrueMoon.Aether.Tooling')},
    @{Name='runtime';Project='Examples/AetherRuntimeHost/AetherRuntimeHost.csproj';Assembly='AetherRuntimeHost';Namespace='TrueMoon.Examples.AetherRuntimeHost';Modules=@('Examples/AetherRuntimeHost');References=@('TrueMoon','TrueMoon.Aether.Runtime')},
    @{Name='combined';Project='Examples/AetherHost/AetherHost.csproj';Assembly='AetherHost';Namespace='TrueMoon.Examples.AetherHost';Modules=@('Examples/AetherHost');Extra=@('Examples/AetherPlayer/PlayerApplication.cs');References=@('TrueMoon','TrueMoon.Aether.Editor.Tooling','TrueMoon.Aether.Player')},
    @{Name='example-player';Project='Examples/AetherPlayer/AetherPlayer.csproj';Assembly='AetherPlayer';Namespace='TrueMoon.Examples.AetherPlayer';Modules=@('Examples/AetherPlayer');References=@('TrueMoon','TrueMoon.Aether.Player')})
$packages=@('TrueMoon.Contracts','TrueMoon.Core','TrueMoon.Cobalt','TrueMoon','TrueMoon.Extensions.DependencyInjection','TrueMoon.Enerit.Generator','TrueMoon.Enerit','TrueMoon.Mithril','TrueMoon.Argentis','TrueMoon.Alloy','TrueMoon.Alloy.Rendering.Skia','TrueMoon.Alloy.Platform.Silk','TrueMoon.Alloy.Hosting','TrueMoon.Aether.Abstractions','TrueMoon.Aether.Runtime','TrueMoon.Aether.Tooling','TrueMoon.Aether.Player','TrueMoon.Aether.Editor.UI','TrueMoon.Aether.Editor.Tooling')
function Native-Profile([string]$name,[hashtable]$paths,[switch]$isolated,[switch]$PlayerOnly,[string]$runtimeHost=$dotnet){
    Check-Payload $name $paths
    foreach($provider in @('cobalt','microsoft')){
        if(!$PlayerOnly){
        foreach($topology in @('same-executable','combined-runtime','separate-runtime')){
            $case="$name-$topology-$provider";$output="$evidence/$case";[void][IO.Directory]::CreateDirectory($output)
            $counterpart=switch($topology){'same-executable'{$paths.probe};'combined-runtime'{$paths.combined};'separate-runtime'{$paths.runtime}}
            $published=Run "$case-artifact" @($paths.probe,'--publish-artifact',$paths.game,$counterpart,"$run/$case-artifacts") "$run/unrelated working directory" -isolated:$isolated
            $artifact=($published|ConvertFrom-Json).DirectoryPath
            [IO.File]::WriteAllText("$output/artifact.txt",$artifact)
            $fixture="$output/project";foreach($relative in @('','Scenes','Content')){[void][IO.Directory]::CreateDirectory((Join-Path $fixture $relative))}
            [IO.File]::WriteAllText("$fixture/Game.csproj",'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>'+$profile.Framework+'</TargetFramework></PropertyGroup></Project>')
            [IO.File]::WriteAllText("$fixture/global.json",(@{sdk=@{version=$profile.Sdk;rollForward='disable'}}|ConvertTo-Json -Depth 4))
            [IO.File]::WriteAllText("$fixture/Selected.aetherproj",(@{formatVersion=1;id=[Guid]::NewGuid();name='Selected launch fixture';gameProject='Game.csproj';entryAssembly='RenderSurface.Game.dll';entryType='RenderSurface.Game.MeshGame';scenesRoot='Scenes';contentRoot='Content'}|ConvertTo-Json))
            $arguments=@($paths.probe,'--report',$output,'--project',"$fixture/Selected.aetherproj")
            # Also exercise the combined host's default Editor role.
            if($topology -ne 'same-executable'){$arguments+=@('--editor','--runtime-host',[IO.Path]::ChangeExtension($counterpart,'.exe'))}
            $null=Run $case $arguments "$run/unrelated working directory" $provider -isolated:$isolated
            $proof=Get-Content -LiteralPath "$output/editor.json" -Raw|ConvertFrom-Json
            if(!$proof.Passed -or $proof.Error -or $proof.GameLoadedInParent -or $proof.Cycles.Count -ne 3 -or $proof.IdentityFaults.Count -ne 2 -or $proof.UndoCount -ne 1 -or $proof.RedoCount -ne 1 -or $proof.PixelProof.WhitePixels -le 200){throw "Incomplete Editor profile $case"}
            foreach($cycle in $proof.Cycles){if(!$cycle.Reaped -or !$cycle.ResourcesReleased -or !$cycle.DocumentPreserved){throw 'Incomplete child cleanup/document proof'}}
            foreach($fault in $proof.IdentityFaults){if(!$fault.Passed -or !$fault.Reaped -or !$fault.RejectedBeforeApp){throw 'Incomplete child identity proof'}}
            $report.Profiles+=@{Name=$case;Role='Editor';Provider=$provider;Topology=$topology;Report="$output/editor.json";Sha256=(Get-FileHash -LiteralPath "$output/editor.json").Hash};Save
        }
        }
        foreach($kind in @('player','probe')){
            $case="$name-$kind-player-$provider";$output="$evidence/$case";[void][IO.Directory]::CreateDirectory($output)
            $published=Run "$case-artifact" @($paths.publisher,$paths.game,$paths[$kind],"$run/$case-artifacts",'RenderSurface.Game.dll','RenderSurface.Game.MeshGame','runtime') "$run/unrelated working directory" -isolated:$isolated -executable $runtimeHost
            $artifact=($published|ConvertFrom-Json).DirectoryPath
            $scene="$output/selected.json";$sceneId="selected-$case"
            [IO.File]::WriteAllText($scene,(@{formatVersion=1;id=$sceneId;revision=41;entities=@()}|ConvertTo-Json -Compress))
            $arguments=if($kind -eq 'probe'){@($paths.probe,'--player','--artifact',$artifact,'--scene',$scene,'--scene-format','snapshot','--report',$output)}else{@($paths.player,$artifact,$output,$provider,$scene)}
            $null=Run $case $arguments "$run/unrelated working directory" $provider -isolated:$isolated -executable $runtimeHost
            $proof=Get-Content -LiteralPath "$output/player.json" -Raw|ConvertFrom-Json
            if($proof.Error -or !$proof.GameLoaded -or $proof.EditorLoaded -or $proof.WorkflowLoaded -or $proof.Captures.Count -ne 5 -or $proof.State.State -ne 5 -or $proof.SceneId -ne $sceneId -or $proof.SceneRevision -ne 41 -or $proof.Validation.Errors -ne 0 -or $proof.Validation.Warnings -ne 0 -or $proof.Validation.LiveSlots -ne 0 -or $proof.Validation.OpenHandles -ne 0 -or $proof.Validation.SurfacesCreated -ne $proof.Validation.SurfacesReleased){throw "Incomplete Player profile $case"}
            $report.Profiles+=@{Name=$case;Role='Player';Provider=$provider;Topology=$kind;Report="$output/player.json";Sha256=(Get-FileHash -LiteralPath "$output/player.json").Hash};Save
        }
    }
    if(!$PlayerOnly){
        $null=Run "$name-cli" @('-NoProfile','-File',"$workspace/ManualTests/PlayerProbe/VerifyLaunch.ps1",'-PlayerAssembly',$paths.'example-player','-CombinedAssembly',$paths.combined,'-EvidenceDirectory',"$evidence/$name-cli") $workspace -executable (Get-Process -Id $PID).Path
        Example-Windows $name $paths -isolated:$isolated
    }
}
function Example-Windows([string]$name,[hashtable]$paths,[switch]$isolated){
    foreach($role in @('default-editor','explicit-editor','combined-player','separate-player')){
        $case="$name-example-$role";$output="$evidence/$case";[void][IO.Directory]::CreateDirectory($output)
        $entry=if($role -eq 'separate-player'){$paths.'example-player'}else{$paths.combined}
        $arguments=@($entry,'--report',"$output/app")
        if($role.EndsWith('player')){
            $published=Run "$case-artifact" @($paths.publisher,$paths.game,$entry,"$run/$case-artifacts",'RenderSurface.Game.dll','RenderSurface.Game.MeshGame','runtime') "$run/unrelated working directory" -isolated:$isolated
            $artifact=($published|ConvertFrom-Json).DirectoryPath
            [IO.File]::WriteAllText("$output/scene.json",'{"formatVersion":1,"id":"actual-example","revision":41,"entities":[]}')
            $arguments+=@('--artifact',$artifact,'--scene',"$output/scene.json",'--scene-format','snapshot','--validation')
            if($role -eq 'combined-player'){$arguments+='--player'}
        }elseif($role -eq 'explicit-editor'){$arguments+=@('--editor','--runtime-host',[IO.Path]::ChangeExtension($paths.runtime,'.exe'))}
        $info=[Diagnostics.ProcessStartInfo]::new($dotnet);$info.WorkingDirectory="$run/unrelated working directory";$info.UseShellExecute=$false;$info.CreateNoWindow=$true
        $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
        foreach($key in @('TM_ENGINE_BOOTSTRAP','-p','TrueMoon.Mithril.ControlPipe')){[void]$info.Environment.Remove($key)}
        $info.Environment['VK_LAYER_PATH']=$validation;$info.Environment['TRUEMOON_GPU_VALIDATION']='1'
        if($isolated){$info.Environment['NUGET_PACKAGES']="$run/cache"}
        foreach($argument in $arguments){[void]$info.ArgumentList.Add($argument)}
        $process=[Diagnostics.Process]::Start($info);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync();$timer=[Diagnostics.Stopwatch]::StartNew()
        try{
            while($true){
                if($process.HasExited){throw "$case exited before creating its native window: $($stderr.GetAwaiter().GetResult())"}
                $process.Refresh();if($process.MainWindowHandle -ne 0 -and $timer.Elapsed.TotalSeconds -ge 3){break}
                if($timer.Elapsed.TotalSeconds -ge 30){throw "$case window startup timed out"};Start-Sleep -Milliseconds 50
            }
            $handle=$process.MainWindowHandle
            if(!$process.CloseMainWindow()){throw "$case owned window refused close"}
            if(!$process.WaitForExit(30000)){throw "$case did not complete after window close"}
            if($process.ExitCode -ne 0){throw "$case failed: $($stderr.GetAwaiter().GetResult())"}
            $report.ExampleWindows+=@{Name=$case;Arguments=$arguments;Pid=$process.Id;WindowHandle=$handle.ToInt64();ExitCode=$process.ExitCode;Reaped=$true};Save
            Write-Host "$case native window/close passed"
        }finally{
            if(!$process.HasExited){$process.Kill($true);$process.WaitForExit()}
            [IO.File]::WriteAllText("$output/stdout.log",$stdout.GetAwaiter().GetResult());[IO.File]::WriteAllText("$output/stderr.log",$stderr.GetAwaiter().GetResult());$process.Dispose()
        }
    }
}
try{
    foreach($module in @($packages+'TrueMoon.Cobalt.Generator')+@($specs.Modules|ForEach-Object{$_}|Sort-Object -Unique)){
        foreach($path in (& rg --files "$workspace/$module" -g '*.cs' -g '*.csproj' -g '*.ps1' -g '*.spv')){$report.Sources+=@{Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash}}
    }
    foreach($file in @('TrueMoon.Alloy.Rendering.Skia/NativeAssets/win-x64/libSkiaSharp.dll','TrueMoon.Alloy.Rendering.Skia/NativeAssets/win-x64/libSkiaSharp.build.json','Examples/AetherEditor/Assets/Fonts/NotoSans-Regular.ttf','Examples/AetherEditor/Assets/Fonts/OFL.txt','ManualTests/PlayerProbe/VerifyLaunch.ps1')){
        $path=Join-Path $workspace $file;$report.Sources+=@{Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash}
    }
    $source=@{}
    foreach($spec in $specs){
        $null=Run "build-$($spec.Name)" @('build',$spec.Project,'-c','Debug','-m','-v:q')
        $source[$spec.Name]=Join-Path $workspace ((Split-Path -Parent $spec.Project)+"/bin/Debug/$($profile.Framework)/$($spec.Assembly).dll")
    }
    $source.game=Split-Path -Parent $source.game
    Native-Profile 'source' $source
    if(!$SourceOnly){
        $published=@{}
        foreach($spec in $specs){
            $null=Run "publish-$($spec.Name)" @('publish',$spec.Project,'-c','Release','-o',"$run/published/$($spec.Name)",'-v:q')
            $published[$spec.Name]="$run/published/$($spec.Name)/$($spec.Assembly).dll"
        }
        $published.game=Split-Path -Parent $published.game
        Native-Profile 'published' $published
        foreach($id in $packages){
            $null=Run "pack-$id" @('pack',"$id/$id.csproj",'-c','Release','-o',"$run/feed",'-v:q')
            $path="$run/feed/$id.$version.nupkg";$report.Packages+=@{Id=$id;Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash};Save
        }
        $external=@{}
        foreach($id in @($packages+'TrueMoon.Cobalt.Generator')){
            $assets=Get-Content -LiteralPath "$workspace/$id/obj/project.assets.json" -Raw|ConvertFrom-Json -AsHashtable
            foreach($library in $assets.libraries.GetEnumerator()){
                if($library.Value.type -ne 'package' -or $external.ContainsKey($library.Key)){continue}
                $relative=$library.Key.ToLowerInvariant();$parts=$relative.Split('/');$archive=$null
                foreach($folder in $assets.packageFolders.Keys){$candidate=Join-Path $folder "$relative/$($parts[0]).$($parts[1]).nupkg";if(Test-Path -LiteralPath $candidate){$archive=$candidate;break}}
                if(!$archive){throw "Missing exact cached archive: $($library.Key)"}
                Copy-Item -LiteralPath $archive -Destination "$run/feed";$external[$library.Key]=$archive
            }
        }
        $fresh="$run/fresh";Write-TrueMoonConsumerBuildProfile -Profile $profile -Directory $fresh
        [IO.File]::WriteAllText("$fresh/NuGet.Config",'<configuration><packageSources><clear/><add key="closed" value="'+[Security.SecurityElement]::Escape("$run/feed")+'"/></packageSources></configuration>')
        [IO.File]::WriteAllText("$fresh/Directory.Build.targets",'<Project/>')
        $freshPaths=@{}
        foreach($spec in $specs){
            $root="$fresh/$($spec.Name)";[void][IO.Directory]::CreateDirectory($root)
            $refs=($spec.References|ForEach-Object{'<PackageReference Include="'+$_+'" Version="'+$version+'"/>'}) -join ''
            $output=if($spec.ContainsKey('Library')){''}else{'<OutputType>Exe</OutputType>'}
            $startup=if($spec.ContainsKey('Startup')){'<StartupObject>'+$spec.Startup+'</StartupObject>'}else{''}
            [IO.File]::WriteAllText("$root/Consumer.csproj",'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'+$output+$startup+'<Version>'+$version+'</Version><AssemblyName>'+$spec.Assembly+'</AssemblyName><RootNamespace>'+$spec.Namespace+'</RootNamespace></PropertyGroup><ItemGroup>'+$refs+'</ItemGroup></Project>')
            foreach($module in $spec.Modules){foreach($file in Get-ChildItem -LiteralPath "$workspace/$module" -Filter '*.cs' -File){Copy-Item -LiteralPath $file.FullName -Destination $root}}
            if($spec.ContainsKey('Extra')){foreach($file in $spec.Extra){$destination=if($file -eq 'ManualTests/PlayerProbe/Program.cs'){'PlayerProfile.cs'}else{Split-Path -Leaf $file};Copy-Item -LiteralPath "$workspace/$file" -Destination "$root/$destination"}}
            $null=Run "fresh-restore-$($spec.Name)" @('restore',"$root/Consumer.csproj",'--configfile',"$fresh/NuGet.Config",'--no-http-cache') $fresh -isolated
            $graph=Get-Content -LiteralPath "$root/obj/project.assets.json" -Raw|ConvertFrom-Json -AsHashtable
            if(@($graph.libraries.Values|Where-Object type -ne 'package').Count -or $graph.packageFolders.Count -ne 1 -or [IO.Path]::GetFullPath(@($graph.packageFolders.Keys)[0]).TrimEnd('\','/') -ne [IO.Path]::GetFullPath("$run/cache").TrimEnd('\','/')){throw 'Non-isolated package graph'}
            foreach($library in $graph.libraries.GetEnumerator()|Where-Object Key -match '^TrueMoon(?:\.[^/]+)?/'){
                if(!$library.Key.EndsWith('/'+$version,[StringComparison]::Ordinal)){throw 'Mixed TrueMoon package versions'}
                $metadata=Get-Content -LiteralPath "$run/cache/$($library.Value.path)/.nupkg.metadata" -Raw|ConvertFrom-Json
                if([IO.Path]::GetFullPath($metadata.source) -ne [IO.Path]::GetFullPath("$run/feed")){throw 'Wrong TrueMoon package origin'}
            }
            if($spec.Name -in @('runtime','game') -and @($graph.libraries.Keys|Where-Object{$_ -match '^TrueMoon\.Aether\.(Tooling|Editor|Player)|^TrueMoon\.Alloy|^SkiaSharp/'}).Count){throw 'Runtime-only consumer contains UI/Tooling'}
            if($spec.Name -in @('player','example-player') -and @($graph.libraries.Keys|Where-Object{$_ -match '^TrueMoon\.Aether\.(Tooling|Editor)'}).Count){throw 'Player-only consumer contains Editor/Tooling'}
            $report.Graphs+=@{Name=$spec.Name;ProjectReferences=0;PackageFolders=1;Sha256=(Get-FileHash -LiteralPath "$root/obj/project.assets.json").Hash}
            $null=Run "fresh-publish-$($spec.Name)" @('publish',"$root/Consumer.csproj",'--no-restore','-c','Release','-o',"$run/fresh-published/$($spec.Name)",'-v:q') $fresh -isolated
            $freshPaths[$spec.Name]="$run/fresh-published/$($spec.Name)/$($spec.Assembly).dll"
            if($spec.Name -in @('probe','player','combined','example-player')){
                [void][IO.Directory]::CreateDirectory("$run/fresh-published/$($spec.Name)/Assets/Fonts")
                foreach($file in @('NotoSans-Regular.ttf','OFL.txt')){Copy-Item -LiteralPath "$workspace/Examples/AetherEditor/Assets/Fonts/$file" -Destination "$run/fresh-published/$($spec.Name)/Assets/Fonts/$file"}
            }
        }
        $freshPaths.game=Split-Path -Parent $freshPaths.game
        Native-Profile 'fresh' $freshPaths -isolated
        $runtimeConfig=Get-Content -LiteralPath "$run/fresh-published/player/PlayerProbe.runtimeconfig.json" -Raw|ConvertFrom-Json
        $runtimeVersion=$runtimeConfig.runtimeOptions.framework.version;$runtimeOnly="$run/runtime-only";$dotnetRoot=Split-Path -Parent $dotnet
        [void][IO.Directory]::CreateDirectory($runtimeOnly);Copy-Item -LiteralPath "$dotnetRoot/dotnet.exe" -Destination "$runtimeOnly/dotnet.exe"
        foreach($relative in @("host/fxr/$runtimeVersion","shared/Microsoft.NETCore.App/$runtimeVersion")){
            $destination=Join-Path $runtimeOnly $relative;[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
            Copy-Item -LiteralPath (Join-Path $dotnetRoot $relative) -Destination $destination -Recurse
        }
        $sdks=Run 'runtime-only-sdks' @('--list-sdks') "$run/unrelated working directory" -executable "$runtimeOnly/dotnet.exe"
        if(![string]::IsNullOrWhiteSpace($sdks) -or (Test-Path -LiteralPath "$runtimeOnly/sdk")){throw 'Runtime-only profile discovered an SDK'}
        Native-Profile 'sdk-free' $freshPaths -isolated -PlayerOnly -runtimeHost "$runtimeOnly/dotnet.exe"
        $report.RuntimeOnly=@{Root=$runtimeOnly;RuntimeVersion=$runtimeVersion;SdkCount=0}
        foreach($package in $report.Packages){if((Get-FileHash -LiteralPath $package.Path).Hash -ne $package.Sha256){throw 'Package archive changed during acceptance'}}
    }
    foreach($sourceFile in $report.Sources){if((Get-FileHash -LiteralPath $sourceFile.Path).Hash -ne $sourceFile.Sha256){throw "Source changed during acceptance: $($sourceFile.Path)"}}
    foreach($sourceFile in $profile.Sources){if((Get-FileHash -LiteralPath $sourceFile.Path).Hash -ne $sourceFile.Sha256){throw "Build profile changed during acceptance: $($sourceFile.Path)"}}
    $report.Completed=$true
}catch{$report.Failure=$_.Exception.ToString();throw}finally{Save;Write-Host "Evidence: $evidence"}
