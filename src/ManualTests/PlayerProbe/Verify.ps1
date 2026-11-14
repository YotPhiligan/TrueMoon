[CmdletBinding()]
param([Parameter(Mandatory)][string]$ValidationDirectory,[Parameter(Mandatory)][string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
$run=Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-PlayerDelivery-'+[Guid]::NewGuid().ToString('N'))
if(Test-Path -LiteralPath $evidence){throw 'Use a fresh evidence directory.'}
$validation=[IO.Path]::GetFullPath($ValidationDirectory)
foreach($file in @('VkLayer_khronos_validation.json','VkLayer_khronos_validation.dll')){if(!(Test-Path -LiteralPath (Join-Path $validation $file))){throw 'Existing validation layer is required.'}}
foreach($directory in @($evidence,$run,"$run/feed","$run/cache","$run/unrelated working directory")){[void][IO.Directory]::CreateDirectory($directory)}
$profile=Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory $evidence
$version=$profile.Version+'.player.'+(Get-Date -Format 'yyyyMMddHHmmss')
$packages=@('TrueMoon.Contracts','TrueMoon.Core','TrueMoon.Cobalt','TrueMoon','TrueMoon.Extensions.DependencyInjection','TrueMoon.Enerit.Generator','TrueMoon.Enerit','TrueMoon.Mithril','TrueMoon.Argentis','TrueMoon.Alloy','TrueMoon.Alloy.Rendering.Skia','TrueMoon.Alloy.Platform.Silk','TrueMoon.Alloy.Hosting','TrueMoon.Aether.Abstractions','TrueMoon.Aether.Runtime','TrueMoon.Aether.Tooling','TrueMoon.Aether.Player')
$report=[ordered]@{Completed=$false;Stage='ARCH3.3 Player';Version=$version;BuildProfile=$profile;RunDirectory=$run;Commands=@();Packages=@();Graphs=@();Profiles=@();Sources=@();Failure=$null}
function Save-Report{$report | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath "$evidence/verification.json" -Encoding utf8}
function Invoke-Proof([string]$name,[string[]]$arguments,[string]$cwd=$workspace,[switch]$isolated){
    if($arguments[0] -in @('build','publish','pack','restore')){$arguments+="-bl:$evidence/$name-{}.binlog"}
    $info=[Diagnostics.ProcessStartInfo]::new('dotnet');$info.WorkingDirectory=$cwd;$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    $info.Environment['VK_LAYER_PATH']=$validation
    if($isolated){$info.Environment['NUGET_PACKAGES']="$run/cache";$info.Environment['NUGET_HTTP_CACHE_PATH']="$run/http-cache"}
    foreach($argument in $arguments){[void]$info.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::Start($info);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    try{
        if(!$process.WaitForExit(240000)){$process.Kill($true);$process.WaitForExit();throw "$name timed out"}
        [IO.File]::WriteAllText("$evidence/$name.stdout.log",$stdout.GetAwaiter().GetResult());[IO.File]::WriteAllText("$evidence/$name.stderr.log",$stderr.GetAwaiter().GetResult())
        $report.Commands+=@{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode;WorkingDirectory=$cwd;Isolated=[bool]$isolated};Save-Report
        if($process.ExitCode -ne 0){throw "$name failed; outputs preserved."};Write-Host "$name passed"
    }finally{if(!$process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
function Run-Profile([string]$name,[string]$probe,[string]$game,[string]$publisher,[switch]$isolated){
    Invoke-Proof "$name-publish-artifact" @($publisher,$game,$probe,"$run/artifacts-$name",'RenderSurface.Game.dll','RenderSurface.Game.MeshGame','runtime') "$run/unrelated working directory" -isolated:$isolated
    $artifact=(Get-Content -LiteralPath "$evidence/$name-publish-artifact.stdout.log" -Raw | ConvertFrom-Json).DirectoryPath
    foreach($provider in @('cobalt','microsoft')){
        $output="$evidence/$name-$provider";$arguments=@($probe,$artifact,$output);if($provider -eq 'microsoft'){$arguments+='microsoft'}
        Invoke-Proof "$name-$provider" $arguments "$run/unrelated working directory" -isolated:$isolated
        $proof=Get-Content -LiteralPath "$output/player.json" -Raw | ConvertFrom-Json
        if($proof.Error -or $proof.EditorLoaded -or !$proof.GameLoaded -or $proof.Captures.Count -ne 5 -or $proof.State.State -ne 5 -or $proof.Validation.Errors -ne 0 -or $proof.Validation.Warnings -ne 0 -or $proof.Validation.OpenHandles -ne 0 -or $proof.Validation.LiveSlots -ne 0 -or $proof.Validation.SurfacesCreated -ne $proof.Validation.SurfacesReleased){throw 'Player acceptance mismatch.'}
        $report.Profiles+=@{Name="$name-$provider";Report="$output/player.json";Sha256=(Get-FileHash -LiteralPath "$output/player.json").Hash};Save-Report
    }
}
try{
    foreach($module in @($packages+'TrueMoon.Cobalt.Generator'+'ManualTests/PlayerProbe'+'ManualTests/RenderSurface.Game'+'ManualTests/RuntimeArtifactTool')){
        foreach($path in (& rg --files "$workspace/$module" -g '*.cs' -g '*.csproj' -g '*.spv')){$report.Sources+=@{Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash}}
    }
    Invoke-Proof 'source-build-publisher' @('build','ManualTests/RuntimeArtifactTool/RuntimeArtifactTool.csproj','-c','Debug','--verbosity','quiet')
    Invoke-Proof 'source-build-probe' @('build','ManualTests/PlayerProbe/PlayerProbe.csproj','-c','Debug','--verbosity','quiet')
    Invoke-Proof 'source-build-game' @('build','ManualTests/RenderSurface.Game/RenderSurface.Game.csproj','-c','Debug','--verbosity','quiet')
    Run-Profile 'source' "$workspace/ManualTests/PlayerProbe/bin/Debug/$($profile.Framework)/PlayerProbe.dll" "$workspace/ManualTests/RenderSurface.Game/bin/Debug/$($profile.Framework)" "$workspace/ManualTests/RuntimeArtifactTool/bin/Debug/$($profile.Framework)/RuntimeArtifactTool.dll"
    foreach($item in @(@{Name='probe';Path='ManualTests/PlayerProbe/PlayerProbe.csproj'},@{Name='game';Path='ManualTests/RenderSurface.Game/RenderSurface.Game.csproj'},@{Name='publisher';Path='ManualTests/RuntimeArtifactTool/RuntimeArtifactTool.csproj'})){
        Invoke-Proof ('publish-'+$item.Name) @('publish',$item.Path,'-c','Release','-o',"$run/published/$($item.Name)",'--verbosity','quiet')
    }
    Run-Profile 'published' "$run/published/probe/PlayerProbe.dll" "$run/published/game" "$run/published/publisher/RuntimeArtifactTool.dll"
    foreach($id in $packages){
        Invoke-Proof ('pack-'+$id) @('pack',"$id/$id.csproj",'-c','Release','-o',"$run/feed","-p:Version=$version",'--verbosity','quiet')
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
    $specs=@(@{Name='probe';Assembly='PlayerProbe';Namespace='TrueMoon.ManualTests.PlayerProbe';References=@('TrueMoon','TrueMoon.Aether.Player','TrueMoon.Extensions.DependencyInjection');Exe=$true},@{Name='game';Assembly='RenderSurface.Game';Namespace='RenderSurface.Game';References=@('TrueMoon.Aether.Runtime');Exe=$false},@{Name='publisher';Assembly='RuntimeArtifactTool';Namespace='TrueMoon.ManualTests.RuntimeArtifactTool';References=@('TrueMoon.Aether.Tooling');Exe=$true})
    foreach($spec in $specs){
        [void][IO.Directory]::CreateDirectory("$fresh/$($spec.Name)")
        $refs=($spec.References | ForEach-Object {'<PackageReference Include="'+$_+'" Version="'+$version+'"/>'}) -join '';$output=if($spec.Exe){'<OutputType>Exe</OutputType>'}else{''}
        [IO.File]::WriteAllText("$fresh/$($spec.Name)/Consumer.csproj",'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'+$output+'<AssemblyName>'+$spec.Assembly+'</AssemblyName><RootNamespace>'+$spec.Namespace+'</RootNamespace></PropertyGroup><ItemGroup>'+$refs+'</ItemGroup></Project>')
        if($spec.Name -eq 'probe'){Copy-Item -LiteralPath "$PSScriptRoot/Program.cs" -Destination "$fresh/probe/Program.cs"}
        elseif($spec.Name -eq 'publisher'){Copy-Item -LiteralPath "$workspace/ManualTests/RuntimeArtifactTool/Program.cs" -Destination "$fresh/publisher/Program.cs"}
        else{foreach($file in @('MeshGame.cs','FailedMeshGame.cs')){Copy-Item -LiteralPath "$workspace/ManualTests/RenderSurface.Game/$file" -Destination "$fresh/game/$file"}}
        Invoke-Proof ('fresh-restore-'+$spec.Name) @('restore',"$($spec.Name)/Consumer.csproj",'--configfile',"$fresh/NuGet.Config",'--no-http-cache') $fresh -isolated
        $graph=Get-Content -LiteralPath "$fresh/$($spec.Name)/obj/project.assets.json" -Raw | ConvertFrom-Json -AsHashtable
        if(@($graph.libraries.Values | Where-Object type -ne 'package').Count -or $graph.packageFolders.Count -ne 1 -or [IO.Path]::GetFullPath(@($graph.packageFolders.Keys)[0]).TrimEnd('\','/') -ne [IO.Path]::GetFullPath("$run/cache").TrimEnd('\','/')){throw 'Non-isolated package graph.'}
        if($spec.Name -eq 'probe' -and @($graph.libraries.Keys | Where-Object {$_ -match '^TrueMoon\.Aether/|^TrueMoon\.Aether\.(Tooling|Editor)/'}).Count){throw 'Player graph contains workflow/UI editor dependency'}
        foreach($key in @($graph.libraries.Keys | Where-Object {$_ -match '^TrueMoon(?:\.[^/]+)?/'})){
            if(!$key.EndsWith('/'+$version,[StringComparison]::Ordinal)){throw 'Stale local package'}
            $metadata=Get-Content -LiteralPath (Join-Path "$run/cache" ($graph.libraries[$key].path+'/.nupkg.metadata')) -Raw | ConvertFrom-Json
            if([IO.Path]::GetFullPath($metadata.source) -ne [IO.Path]::GetFullPath("$run/feed")){throw 'Wrong local feed'}
        }
        $report.Graphs+=@{Project=$spec.Name;ProjectReferences=0;AssetsSha256=(Get-FileHash -LiteralPath "$fresh/$($spec.Name)/obj/project.assets.json").Hash}
        Invoke-Proof ('fresh-publish-'+$spec.Name) @('publish',"$($spec.Name)/Consumer.csproj",'--no-restore','-c','Release','-o',"$run/fresh-published/$($spec.Name)",'--verbosity','quiet') $fresh -isolated
    }
    [void][IO.Directory]::CreateDirectory("$run/fresh-published/probe/Assets/Fonts")
    foreach($file in @('NotoSans-Regular.ttf','OFL.txt')){Copy-Item -LiteralPath "$workspace/Examples/AetherEditor/Assets/Fonts/$file" -Destination "$run/fresh-published/probe/Assets/Fonts/$file"}
    foreach($name in @('probe','game')){if((Get-FileHash -LiteralPath "$run/cache/truemoon.aether.runtime/$version/lib/$($profile.Framework)/TrueMoon.Aether.Runtime.dll").Hash -ne (Get-FileHash -LiteralPath "$run/fresh-published/$name/TrueMoon.Aether.Runtime.dll").Hash){throw 'Installed/output Aether mismatch'}}
    Run-Profile 'fresh' "$run/fresh-published/probe/PlayerProbe.dll" "$run/fresh-published/game" "$run/fresh-published/publisher/RuntimeArtifactTool.dll" -isolated
    foreach($source in $report.Sources){if((Get-FileHash -LiteralPath $source.Path).Hash -ne $source.Sha256){throw 'Source drift; repeat from a frozen snapshot.'}}
    $report.Completed=$true
}catch{$report.Failure=$_.Exception.ToString();throw}finally{Save-Report;Write-Host "Evidence: $evidence"}
