[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory,[string]$PackageVerification)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
. (Join-Path $PSScriptRoot '../PackageDelivery.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
$run=Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-ARCH34-delivery-'+[Guid]::NewGuid().ToString('N'))
if(Test-Path -LiteralPath $evidence){throw 'Use a fresh evidence directory.'}
foreach($directory in @($evidence,$run,"$run/feed","$run/cache","$run/unrelated working directory")){[void][IO.Directory]::CreateDirectory($directory)}
$profile=Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory $evidence
$sharedDelivery=Import-TrueMoonPackageDelivery -Manifest $PackageVerification -Profile $profile
$version=if($sharedDelivery){$sharedDelivery.Version}else{$profile.Version+'.arch34.'+(Get-Date -Format 'yyyyMMddHHmmss')}
$packages=@('TrueMoon.Contracts','TrueMoon.Core','TrueMoon.Cobalt','TrueMoon','TrueMoon.Extensions.DependencyInjection','TrueMoon.Aether.Abstractions','TrueMoon.Enerit.Generator','TrueMoon.Enerit','TrueMoon.Mithril','TrueMoon.Aether.Runtime','TrueMoon.Aether.Tooling')
$report=[ordered]@{Completed=$false;Stage='ARCH3.4';Version=$version;RunDirectory=$run;BuildProfile=$profile;Commands=@();Packages=@();Graphs=@();Profiles=@();Sources=@();Failure=$null}
function Save-Report{$report | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath "$evidence/verification.json" -Encoding utf8}
function Invoke-Proof([string]$name,[string[]]$arguments,[string]$cwd=$workspace,[switch]$isolated,[string]$executable='dotnet',[switch]$runtimeOnly){
    if($arguments[0] -in @('build','publish','pack','restore')){$arguments+="-bl:$evidence/$name-{}.binlog"}
    $info=[Diagnostics.ProcessStartInfo]::new($executable);$info.WorkingDirectory=$cwd;$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    if($isolated){$info.Environment['NUGET_PACKAGES']="$run/cache";$info.Environment['NUGET_HTTP_CACHE_PATH']="$run/http-cache"}
    if($runtimeOnly){
        $runtimeRoot=[IO.Path]::GetDirectoryName($executable)
        foreach($key in @('DOTNET_ROOT','DOTNET_ROOT_X64','DOTNET_HOST_PATH')){$info.Environment[$key]=if($key -eq 'DOTNET_HOST_PATH'){$executable}else{$runtimeRoot}}
        $info.Environment['DOTNET_MULTILEVEL_LOOKUP']='0';$info.Environment['PATH']=$runtimeRoot+';'+[Environment]::GetFolderPath('System')
    }
    foreach($argument in $arguments){[void]$info.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::Start($info);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    try{
        if(!$process.WaitForExit(180000)){$process.Kill($true);$process.WaitForExit();throw "$name timed out"}
        [IO.File]::WriteAllText("$evidence/$name.stdout.log",$stdout.GetAwaiter().GetResult());[IO.File]::WriteAllText("$evidence/$name.stderr.log",$stderr.GetAwaiter().GetResult())
        $report.Commands+=@{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode;WorkingDirectory=$cwd;Isolated=[bool]$isolated};Save-Report
        if($process.ExitCode -ne 0){throw "$name failed"};Write-Host "$name passed"
    }finally{if(!$process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}

function Run-Profile([string]$name,[string]$assembly,[switch]$isolated){
    foreach($provider in @('cobalt','microsoft')){
        $output="$evidence/$name-$provider.json"
        Invoke-Proof "$name-$provider" @($assembly,"$run/work-$name-$provider",$output,$provider,$profile.Framework,$profile.LanguageVersion,$profile.Features) "$run/unrelated working directory" -isolated:$isolated
        $value=Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
        if(!$value.Completed -or $value.Checks -lt 30 -or !$value.OpaquePreserved -or !$value.Reopened -or !$value.Closed -or
            $value.RuntimeAccess -or !$value.FailedBuildRetainedLastGood -or !$value.CancelledBuildReaped -or !$value.FailedSavePreservedFileAndMemory -or
            $null -ne $value.CompilerPid -or $value.ForbiddenAssemblies.Count -ne 0 -or $value.ForbiddenNativeModules.Count -ne 0){throw 'Headless Tooling proof mismatch'}
        $report.Profiles+=@{Name="$name-$provider";Report=$output;Sha256=(Get-FileHash -LiteralPath $output).Hash;Checks=$value.Checks};Save-Report
    }
}
try{
    if($PackageVerification){$report.Sources+=@{Path=[IO.Path]::GetFullPath($PackageVerification);Sha256=(Get-FileHash -LiteralPath $PackageVerification).Hash}}
    foreach($module in @($packages+'ManualTests/ToolingConsumer')){
        foreach($path in (& rg --files "$workspace/$module" -g '*.cs' -g '*.csproj' -g 'PACKAGE.md' -g '*.ps1' -g '*.txt')){
            $report.Sources+=@{Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash}
        }
    }
    Invoke-Proof 'source-build' @('build','ManualTests/ToolingConsumer/ToolingConsumer.csproj','-c','Debug','--verbosity','quiet')
    Run-Profile 'source' "$workspace/ManualTests/ToolingConsumer/bin/Debug/$($profile.Framework)/ToolingConsumer.dll"
    Invoke-Proof 'publish' @('publish','ManualTests/ToolingConsumer/ToolingConsumer.csproj','-c','Release','-o',"$run/published",'--verbosity','quiet')
    Run-Profile 'published' "$run/published/ToolingConsumer.dll"
    foreach($id in $packages){
        if($sharedDelivery){Copy-TrueMoonDeliveryPackage -Delivery $sharedDelivery -Id $id -Destination "$run/feed"}
        else{Invoke-Proof ('pack-'+$id) @('pack',"$id/$id.csproj",'-c','Release','-o',"$run/feed","-p:Version=$version",'--verbosity','quiet')}
        $path="$run/feed/$id.$version.nupkg";$report.Packages+=@{Id=$id;Sha256=(Get-FileHash -LiteralPath $path).Hash;Path=$path}
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
    $references=(@('TrueMoon','TrueMoon.Aether.Tooling','TrueMoon.Extensions.DependencyInjection')|ForEach-Object {'<PackageReference Include="'+$_+'" Version="'+$version+'"/>'}) -join ''
    [IO.File]::WriteAllText("$fresh/Consumer.csproj",'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><AssemblyName>ToolingConsumer</AssemblyName><RootNamespace>TrueMoon.ManualTests.ToolingConsumer</RootNamespace></PropertyGroup><ItemGroup>'+$references+'<EmbeddedResource Include="Game.cs.txt" LogicalName="HeadlessGameSource" WithCulture="false"/></ItemGroup></Project>')
    foreach($name in @('Program.cs','Game.cs.txt')){Copy-Item -LiteralPath "$workspace/ManualTests/ToolingConsumer/$name" -Destination "$fresh/$name"}
    Invoke-Proof 'fresh-restore' @('restore','Consumer.csproj','--configfile',"$fresh/NuGet.Config",'--no-http-cache') $fresh -isolated
    $graph=Get-Content -LiteralPath "$fresh/obj/project.assets.json" -Raw | ConvertFrom-Json -AsHashtable
    if(@($graph.libraries.Values | Where-Object type -ne 'package').Count -or $graph.packageFolders.Count -ne 1 -or
        [IO.Path]::GetFullPath(@($graph.packageFolders.Keys)[0]).TrimEnd('\','/') -ne [IO.Path]::GetFullPath("$run/cache").TrimEnd('\','/')){throw 'Non-isolated package graph'}
    $local=@($graph.libraries.Keys | Where-Object {$_ -match '^TrueMoon(?:\.[^/]+)?/'})
    if(@($local | Where-Object {$_ -match '^TrueMoon\.Aether/|^TrueMoon\.Aether\.Editor|^TrueMoon\.(Alloy|Argentis)'}).Count){throw 'Tooling graph contains legacy monolith or UI'}
    foreach($key in $local){
        if(!$key.EndsWith('/'+$version,[StringComparison]::Ordinal)){throw 'Stale local package'}
        $metadata=Get-Content -LiteralPath (Join-Path "$run/cache" ($graph.libraries[$key].path+'/.nupkg.metadata')) -Raw | ConvertFrom-Json
        if([IO.Path]::GetFullPath($metadata.source) -ne [IO.Path]::GetFullPath("$run/feed")){throw 'Wrong feed'}
    }
    $report.Graphs+=@{Project='ToolingConsumer';ProjectReferences=0;LocalPackages=$local;AssetsSha256=(Get-FileHash -LiteralPath "$fresh/obj/project.assets.json").Hash}
    Invoke-Proof 'fresh-publish' @('publish','Consumer.csproj','--no-restore','-c','Release','-o',"$run/fresh-published",'--verbosity','quiet') $fresh -isolated
    $installed="$run/cache/truemoon.aether.tooling/$version/lib/$($profile.Framework)/TrueMoon.Aether.Tooling.dll"
    if((Get-FileHash -LiteralPath $installed).Hash -ne (Get-FileHash -LiteralPath "$run/fresh-published/TrueMoon.Aether.Tooling.dll").Hash){throw 'Installed Tooling bytes differ'}
    Run-Profile 'fresh' "$run/fresh-published/ToolingConsumer.dll" -isolated
    foreach($source in $report.Sources){if((Get-FileHash -LiteralPath $source.Path).Hash -ne $source.Sha256){throw 'Source changed during proof'}}
    $report.Completed=$true
}catch{$report.Failure=$_.Exception.ToString();throw}finally{Save-Report;Write-Host "Evidence: $evidence"}
