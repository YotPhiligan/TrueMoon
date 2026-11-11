[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageVerification,
    [Parameter(Mandatory)][string]$ValidationDirectory,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [string]$RunDirectory,
    [ValidateSet('viewport','translation')][string]$Profile = 'viewport',
    [string]$DependencyFeed = 'https://api.nuget.org/v3/index.json'
)
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
$trueMoonBuildProfile = Get-TrueMoonBuildProfile -Workspace ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))


$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$package=Get-Content -LiteralPath $PackageVerification -Raw|ConvertFrom-Json
if(!$package.Completed-or$package.Failure){throw 'A completed fresh package verification is required.'}
$run=if($RunDirectory){[IO.Path]::GetFullPath($RunDirectory)}else{Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-ViewportPackage-'+[guid]::NewGuid().ToString('N'))}
if($run-eq$workspace-or$run.StartsWith($workspace+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Use an independent run outside checkout.'}
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
if((Test-Path -LiteralPath $run)-or(Test-Path -LiteralPath $evidence)){throw 'Use fresh run/evidence paths.'}
$validation=(Resolve-Path -LiteralPath $ValidationDirectory).Path
if(!(Test-Path -LiteralPath (Join-Path $validation 'VkLayer_khronos_validation.json'))){throw 'Validation layer manifest missing.'}
foreach($p in $package.Packages){if((Get-FileHash -LiteralPath $p.Path -Algorithm SHA256).Hash-ne$p.Sha256){throw "Changed package $($p.Id)"}}
# Framework C# binaries must correspond to the supplied package build; documentation may be updated independently.
foreach($source in $package.Sources|Where-Object {$_.Path.StartsWith($workspace,[StringComparison]::OrdinalIgnoreCase)-and$_.Path.EndsWith('.cs')-and$_.Path-notmatch '[\\/]ManualTests[\\/]'}){
    if((Get-FileHash -LiteralPath $source.Path -Algorithm SHA256).Hash-ne$source.Sha256){throw "Changed framework source $($source.Path)"}
}
New-Item -ItemType Directory -Path $run,$evidence | Out-Null
$viewportCache=Join-Path $run 'cache'
$hostRoot=Join-Path $run 'Host';New-Item -ItemType Directory -Path $hostRoot,(Join-Path $hostRoot 'Assets/Fonts')|Out-Null
Write-TrueMoonConsumerBuildProfile -Profile $trueMoonBuildProfile -Directory $hostRoot
Get-ChildItem -LiteralPath (Join-Path $workspace 'ManualTests/AetherEditorHost') -Filter '*.cs' -File|Copy-Item -Destination $hostRoot
Copy-Item -LiteralPath (Join-Path $workspace 'Examples/AetherEditor/Assets/Fonts/NotoSans-Regular.ttf'),(Join-Path $workspace 'Examples/AetherEditor/Assets/Fonts/OFL.txt') -Destination (Join-Path $hostRoot 'Assets/Fonts')
$feed=[Security.SecurityElement]::Escape($package.Feed)
$dependencies=[Security.SecurityElement]::Escape($DependencyFeed)
$nugetConfig=if($DependencyFeed -eq $package.Feed){
    "<configuration><packageSources><clear/><add key='fresh' value='$feed'/></packageSources><packageSourceMapping><packageSource key='fresh'><package pattern='*'/></packageSource></packageSourceMapping></configuration>"
}else{
    "<configuration><packageSources><clear/><add key='fresh' value='$feed'/><add key='nuget.org' value='$dependencies'/></packageSources><packageSourceMapping><packageSource key='fresh'><package pattern='TrueMoon'/><package pattern='TrueMoon.*'/></packageSource><packageSource key='nuget.org'><package pattern='*'/></packageSource></packageSourceMapping></configuration>"
}
[IO.File]::WriteAllText((Join-Path $run 'NuGet.Config'),$nugetConfig)
[IO.File]::WriteAllText((Join-Path $run 'global.json'),'{"sdk":{"version":"'+$package.Sdk+'","rollForward":"disable"}}')
[IO.File]::WriteAllText((Join-Path $hostRoot 'Host.csproj'),@"
<Project Sdk="Microsoft.NET.Sdk">
 <PropertyGroup><TargetFramework>$($trueMoonBuildProfile.Framework)</TargetFramework><OutputType>WinExe</OutputType><AssemblyName>ViewportPackageHost</AssemblyName><RootNamespace>TrueMoon.ManualTests.AetherEditorHost</RootNamespace><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><AllowUnsafeBlocks>true</AllowUnsafeBlocks></PropertyGroup>
 <ItemGroup><PackageReference Include="TrueMoon" Version="$($package.Version)"/><PackageReference Include="TrueMoon.Aether.Editor" Version="$($package.Version)"/></ItemGroup>
 <ItemGroup><Content Include="Assets/Fonts/*" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest"/></ItemGroup>
</Project>
"@)
$dotnet=(Get-Command dotnet.exe).Source
$commands=[Collections.Generic.List[object]]::new();$cases=[Collections.Generic.List[object]]::new()
function Invoke-Owned([string]$name,[string[]]$arguments,[int]$seconds=240){
    $info=[Diagnostics.ProcessStartInfo]::new($dotnet);$info.WorkingDirectory=$run;$info.UseShellExecute=$false;$info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($arg in $arguments){[void]$info.ArgumentList.Add($arg)}
    $info.Environment['NUGET_PACKAGES']=$viewportCache;$info.Environment['VK_LAYER_PATH']=$validation
    [void]$info.Environment.Remove('TM_ENGINE_BOOTSTRAP')
    $process=[Diagnostics.Process]::new();$process.StartInfo=$info
    try{
        [void]$process.Start();$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if(!$process.WaitForExit($seconds*1000)){throw "Owned process timeout: $name"}
        [IO.File]::WriteAllText((Join-Path $evidence "$name.log"),$stdout.GetAwaiter().GetResult()+$stderr.GetAwaiter().GetResult())
        $commands.Add([pscustomobject]@{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode})
        if($process.ExitCode-ne0){throw "Failed $name; see evidence log"}
    }finally{if(!$process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
$report=[ordered]@{Completed=$false;PackageVersion=$package.Version;BuildProfile=$trueMoonBuildProfile;RunDirectory=$run;Cache=$viewportCache;SourceFallback=$false;ProjectReferences=0;Commands=$commands;Cases=$cases;Failure=$null}
try{
    Invoke-Owned 'restore' @('restore',"$hostRoot/Host.csproj",'--configfile',"$run/NuGet.Config",'--no-http-cache',"-bl:$evidence/restore-{}.binlog")
    [xml]$project=Get-Content "$hostRoot/Host.csproj" -Raw;if($project.SelectNodes('//ProjectReference|//Compile[@Include]').Count){throw 'Source/project references are forbidden.'}
    $assets=Get-Content "$hostRoot/obj/project.assets.json" -Raw|ConvertFrom-Json -AsHashtable
    if(@($assets.libraries.Values|Where-Object type -ne 'package').Count -or $assets.packageFolders.Keys.Count -ne 1 -or [IO.Path]::GetFullPath(@($assets.packageFolders.Keys)[0]).TrimEnd('\','/') -ne $viewportCache.TrimEnd('\','/')){throw 'Viewport graph/cache isolation failed.'}
    foreach($key in $assets.libraries.Keys|Where-Object {$_-match '^TrueMoon(?:\.[^/]+)?/'}){
        if(!$key.EndsWith('/'+$package.Version,[StringComparison]::Ordinal)){throw "Stale dependency $key"}
        $metadata=Get-Content (Join-Path $viewportCache ($assets.libraries[$key].path+'/.nupkg.metadata')) -Raw|ConvertFrom-Json
        if([IO.Path]::GetFullPath($metadata.source) -ne [IO.Path]::GetFullPath($package.Feed)){throw "Wrong package feed $key"}
    }
    Invoke-Owned 'build-debug' @('build',"$hostRoot/Host.csproj",'-c','Debug','--no-restore',"-bl:$evidence/build-debug-{}.binlog")
    Invoke-Owned 'publish-release' @('publish',"$hostRoot/Host.csproj",'-c','Release','--no-restore','-o',"$run/published","-bl:$evidence/publish-release-{}.binlog")
    foreach($viewportProfile in 'Debug','Release'){
        $target=Join-Path $run "Fixture-$viewportProfile"
        foreach($suffix in '','Other','Bad'){
            $path=if($suffix){Join-Path $target $suffix}else{$target}
            New-Item -ItemType Directory -Path (Join-Path $path 'Scenes'),(Join-Path $path 'Game'),(Join-Path $path 'Content')|Out-Null
            Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Viewport/Transform.aetherscene') -Destination (Join-Path $path 'Scenes/Transform.aetherscene')
            Copy-Item -LiteralPath (Join-Path $workspace 'ManualTests/Game.Logic/RoomGame.cs') -Destination (Join-Path $path 'Game/RoomGame.cs')
            $entry=if($suffix-eq'Bad'){'Game.Logic.MissingEntry'}else{'Game.Logic.RoomGame'}
            [IO.File]::WriteAllText((Join-Path $path 'fixture.aetherproj'),'{"formatVersion":3,"id":"'+[guid]::NewGuid().ToString('D')+'","name":"Viewport package '+$viewportProfile+' '+$suffix+'","gameProject":"Game/Game.csproj","entryAssembly":"Game.dll","entryType":"'+$entry+'","scenesRoot":"Scenes","contentRoot":"Content","dependencies":[],"packageDirectory":null}')
            [IO.File]::WriteAllText((Join-Path $path 'Game/Game.csproj'),"<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup><TargetFramework>$($trueMoonBuildProfile.Framework)</TargetFramework><AssemblyName>Game</AssemblyName><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><Reference Include=`"TrueMoon.Aether`" HintPath=`"`$(AetherContractPath)`" Private=`"false`"/><Reference Include=`"TrueMoon.Contracts`" HintPath=`"`$(TrueMoonContractsPath)`" Private=`"false`"/></ItemGroup></Project>")
        }
        $assembly=if($viewportProfile-eq'Debug'){"$hostRoot/bin/Debug/$($trueMoonBuildProfile.Framework)/ViewportPackageHost.dll"}else{"$run/published/ViewportPackageHost.dll"}
        $case=Join-Path $evidence "native-$viewportProfile"
        $profileFlag=if($Profile-eq'translation'){'--smoke-translation'}else{'--smoke-viewport'}
        $profileReport=if($Profile-eq'translation'){'translation.json'}else{'viewport-acceptance.json'}
        Invoke-Owned "native-$viewportProfile" @($assembly,'--project',"$target/fixture.aetherproj",'--smoke',$case,$profileFlag,'--validation') 420
        $cleanup=Get-Content "$case/report.json" -Raw|ConvertFrom-Json;$acceptance=Get-Content "$case/$profileReport" -Raw|ConvertFrom-Json
        if(!$cleanup.Passed-or!$acceptance.Passed-or$cleanup.UiSessions-ne0-or$cleanup.GpuResources-ne0-or$cleanup.GpuHandles-ne0-or$cleanup.Mappings-ne0-or!$cleanup.Reaped-or$cleanup.ValidationErrors-ne0-or$cleanup.ValidationWarnings-ne0){throw "Failed native cleanup $viewportProfile"}
        foreach($id in 'TrueMoon.Aether','TrueMoon.Aether.Editor'){
            $installed=Join-Path $viewportCache ($id.ToLowerInvariant()+'/'+$package.Version+"/lib/$($trueMoonBuildProfile.Framework)/"+$id+'.dll')
            $actual=Join-Path (Split-Path $assembly) ($id+'.dll')
            if((Get-FileHash $installed -Algorithm SHA256).Hash-ne(Get-FileHash $actual -Algorithm SHA256).Hash){throw "Non-package binary $id"}
        }
        $checks=if($Profile-eq'translation'){$acceptance.Captures}else{$acceptance.Cases}
        $cases.Add([pscustomobject]@{Profile=$viewportProfile;Passed=$true;Report=$case;Checks=$checks;Children=$cleanup.Children;FailedBeforeGpu=$cleanup.FailedBeforeGpu;ValidationReports=$cleanup.ValidationReports})
    }
    $report.Completed=$true
}catch{$report.Failure=$_.ToString();throw}finally{$report|ConvertTo-Json -Depth 20|Set-Content (Join-Path $evidence 'verification.json') -Encoding utf8NoBOM;Write-Host "Viewport package evidence: $evidence"}
