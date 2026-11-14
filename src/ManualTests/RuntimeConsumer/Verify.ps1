[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory,[string]$PackageVerification)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
. (Join-Path $PSScriptRoot '../PackageDelivery.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
$run=Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-ARCH33-delivery-'+[Guid]::NewGuid().ToString('N'))
if(Test-Path -LiteralPath $evidence){throw 'Use a fresh evidence directory.'}
foreach($directory in @($evidence,$run,"$run/feed","$run/cache","$run/unrelated working directory")){[void][IO.Directory]::CreateDirectory($directory)}
$profile=Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory $evidence
$sharedDelivery=Import-TrueMoonPackageDelivery -Manifest $PackageVerification -Profile $profile
$version=if($sharedDelivery){$sharedDelivery.Version}else{$profile.Version+'.arch33.'+(Get-Date -Format 'yyyyMMddHHmmss')}
$packages=@('TrueMoon.Contracts','TrueMoon.Core','TrueMoon.Cobalt','TrueMoon','TrueMoon.Extensions.DependencyInjection','TrueMoon.Aether.Abstractions','TrueMoon.Enerit.Generator','TrueMoon.Enerit','TrueMoon.Mithril','TrueMoon.Aether.Runtime','TrueMoon.Aether.Tooling')
$report=[ordered]@{Completed=$false;Stage='ARCH3.3';Version=$version;RunDirectory=$run;BuildProfile=$profile;Commands=@();Packages=@();Graphs=@();Profiles=@();Sources=@();Failure=$null}
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
function Run-Profile([string]$name,[string]$hostAssembly,[string]$extension,[string]$publisher,[switch]$isolated,[string]$runtimeHost='dotnet'){
    $artifactRoot="$run/artifacts-$name"
    Invoke-Proof "$name-publish-artifact" @($publisher,[IO.Path]::GetDirectoryName($extension),$hostAssembly,$artifactRoot,'ContractExtension.dll','TrueMoon.ManualTests.ContractExtension.ExtensionGame') "$run/unrelated working directory" -isolated:$isolated
    $artifact=(Get-Content -LiteralPath "$evidence/$name-publish-artifact.stdout.log" -Raw | ConvertFrom-Json).DirectoryPath
    $scene="$run/prepared-$name.aetherscene"
    Invoke-Proof "$name-prepare-scene" @($publisher,'--scene',$scene) "$run/unrelated working directory" -isolated:$isolated
    foreach($provider in @('cobalt','microsoft')){
        $output="$evidence/$name-$provider.json";$arguments=@($hostAssembly,$artifact,$scene,$output);if($provider -eq 'microsoft'){$arguments+='microsoft'}
        Invoke-Proof "$name-$provider" $arguments "$run/unrelated working directory" -isolated:$isolated -executable $runtimeHost -runtimeOnly:($runtimeHost -ne 'dotnet')
        $proof=Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
        if(!$proof.Completed -or !$proof.PrivateExtension -or !$proof.SharedContracts -or $proof.ForbiddenAssemblies.Count -ne 0 -or $proof.FactoryCalls -ne 2 -or $proof.Total -ne 88 -or $proof.Count -ne 3 -or $proof.Executions -ne 2 -or $proof.Stops -ne 1 -or $proof.Disposals -ne 1 -or !$proof.RealWorld -or $proof.GameTicks -ne 4 -or $proof.GameRevision -ne 41 -or $proof.GameColor -ne 19){throw 'Runtime proof mismatch'}
        $report.Profiles+=@{Name="$name-$provider";Report=$output;Sha256=(Get-FileHash -LiteralPath $output).Hash};Save-Report
    }
}

try{
    if($PackageVerification){$report.Sources+=@{Path=[IO.Path]::GetFullPath($PackageVerification);Sha256=(Get-FileHash -LiteralPath $PackageVerification).Hash}}
    foreach($module in @($packages+'ManualTests/Abstractions.Extension'+'ManualTests/RuntimeConsumer'+'ManualTests/RuntimeArtifactTool')){
        foreach($path in (& rg --files "$workspace/$module" -g '*.cs' -g '*.csproj')){$report.Sources+=@{Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash}}
    }
    foreach($name in @('RuntimeConsumer','Extension','RuntimeArtifactTool')){Invoke-Proof ('source-build-'+$name) @('build',"ManualTests/$(if($name -eq 'Extension'){'Abstractions.Extension'}else{$name})/$(if($name -eq 'Extension'){'Abstractions.Extension'}else{$name}).csproj",'-c','Debug','--verbosity','quiet')}
    Run-Profile 'source' "$workspace/ManualTests/RuntimeConsumer/bin/Debug/$($profile.Framework)/RuntimeConsumer.dll" "$workspace/ManualTests/Abstractions.Extension/bin/Debug/$($profile.Framework)/ContractExtension.dll" "$workspace/ManualTests/RuntimeArtifactTool/bin/Debug/$($profile.Framework)/RuntimeArtifactTool.dll"
    foreach($name in @('RuntimeConsumer','Extension','RuntimeArtifactTool')){Invoke-Proof ('publish-'+$name) @('publish',"ManualTests/$(if($name -eq 'Extension'){'Abstractions.Extension'}else{$name})/$(if($name -eq 'Extension'){'Abstractions.Extension'}else{$name}).csproj",'-c','Release','-o',"$run/published/$name",'--verbosity','quiet')}
    Run-Profile 'published' "$run/published/RuntimeConsumer/RuntimeConsumer.dll" "$run/published/Extension/ContractExtension.dll" "$run/published/RuntimeArtifactTool/RuntimeArtifactTool.dll"
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
    $specs=@(@{Name='RuntimeConsumer';Assembly='RuntimeConsumer';Namespace='TrueMoon.ManualTests.RuntimeConsumer';References=@('TrueMoon','TrueMoon.Aether.Runtime','TrueMoon.Extensions.DependencyInjection');Exe=$true},@{Name='Extension';Assembly='ContractExtension';Namespace='TrueMoon.ManualTests.ContractExtension';References=@('TrueMoon.Aether.Abstractions');Exe=$false},@{Name='RuntimeArtifactTool';Assembly='RuntimeArtifactTool';Namespace='TrueMoon.ManualTests.RuntimeArtifactTool';References=@('TrueMoon.Aether.Tooling');Exe=$true})
    foreach($spec in $specs){
        [void][IO.Directory]::CreateDirectory("$fresh/$($spec.Name)")
        $refs=($spec.References | ForEach-Object {'<PackageReference Include="'+$_+'" Version="'+$version+'"/>'}) -join '';$output=if($spec.Exe){'<OutputType>Exe</OutputType>'}else{''}
        [IO.File]::WriteAllText("$fresh/$($spec.Name)/Consumer.csproj",'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'+$output+'<AssemblyName>'+$spec.Assembly+'</AssemblyName><RootNamespace>'+$spec.Namespace+'</RootNamespace></PropertyGroup><ItemGroup>'+$refs+'</ItemGroup></Project>')
        foreach($file in (Get-ChildItem -LiteralPath "$workspace/ManualTests/$(if($spec.Name -eq 'Extension'){'Abstractions.Extension'}else{$spec.Name})" -Filter '*.cs')){Copy-Item -LiteralPath $file.FullName -Destination "$fresh/$($spec.Name)/$($file.Name)"}
        Invoke-Proof ('fresh-restore-'+$spec.Name) @('restore',"$($spec.Name)/Consumer.csproj",'--configfile',"$fresh/NuGet.Config",'--no-http-cache') $fresh -isolated
        $graph=Get-Content -LiteralPath "$fresh/$($spec.Name)/obj/project.assets.json" -Raw | ConvertFrom-Json -AsHashtable
        if(@($graph.libraries.Values | Where-Object type -ne 'package').Count -or $graph.packageFolders.Count -ne 1 -or [IO.Path]::GetFullPath(@($graph.packageFolders.Keys)[0]).TrimEnd('\','/') -ne [IO.Path]::GetFullPath("$run/cache").TrimEnd('\','/')){throw 'Non-isolated fresh graph'}
        $local=@($graph.libraries.Keys | Where-Object {$_ -match '^TrueMoon(?:\.[^/]+)?/'})
        if($spec.Name -eq 'Extension' -and @($local | Where-Object {$_ -notmatch '^TrueMoon\.(Aether\.Abstractions|Contracts)/'}).Count){throw 'Extension requires a non-neutral package'}
        if($spec.Name -eq 'RuntimeConsumer' -and @($local | Where-Object {$_ -match '^TrueMoon\.Aether/|^TrueMoon\.Aether\.(Tooling|Editor)/|^TrueMoon\.(Alloy|Argentis)'}).Count){throw 'Runtime graph contains tooling or UI'}
        foreach($key in $local){
            if(!$key.EndsWith('/'+$version,[StringComparison]::Ordinal)){throw 'Stale local package'}
            $metadata=Get-Content -LiteralPath (Join-Path "$run/cache" ($graph.libraries[$key].path+'/.nupkg.metadata')) -Raw | ConvertFrom-Json
            if([IO.Path]::GetFullPath($metadata.source) -ne [IO.Path]::GetFullPath("$run/feed")){throw 'Wrong local feed'}
        }
        $report.Graphs+=@{Project=$spec.Name;ProjectReferences=0;LocalPackages=$local;AssetsSha256=(Get-FileHash -LiteralPath "$fresh/$($spec.Name)/obj/project.assets.json").Hash}
        Invoke-Proof ('fresh-publish-'+$spec.Name) @('publish',"$($spec.Name)/Consumer.csproj",'--no-restore','-c','Release','-o',"$run/fresh-published/$($spec.Name)",'--verbosity','quiet') $fresh -isolated
    }
    $installed="$run/cache/truemoon.aether.abstractions/$version/lib/$($profile.Framework)/TrueMoon.Aether.Abstractions.dll"
    foreach($name in @('RuntimeConsumer','Extension','RuntimeArtifactTool')){if((Get-FileHash -LiteralPath $installed).Hash -ne (Get-FileHash -LiteralPath "$run/fresh-published/$name/TrueMoon.Aether.Abstractions.dll").Hash){throw 'Installed/output Abstractions bytes differ'}}
    Run-Profile 'fresh' "$run/fresh-published/RuntimeConsumer/RuntimeConsumer.dll" "$run/fresh-published/Extension/ContractExtension.dll" "$run/fresh-published/RuntimeArtifactTool/RuntimeArtifactTool.dll" -isolated
    $dotnetRoot=[IO.Path]::GetDirectoryName((Get-Command dotnet).Source)
    $configuration=Get-Content -LiteralPath "$run/fresh-published/RuntimeConsumer/RuntimeConsumer.runtimeconfig.json" -Raw | ConvertFrom-Json
    $runtimeVersion=$configuration.runtimeOptions.framework.version
    $runtimeOnly="$run/runtime-only";[void][IO.Directory]::CreateDirectory($runtimeOnly)
    Copy-Item -LiteralPath "$dotnetRoot/dotnet.exe" -Destination "$runtimeOnly/dotnet.exe"
    foreach($relative in @("host/fxr/$runtimeVersion","shared/Microsoft.NETCore.App/$runtimeVersion")){
        $destination=Join-Path $runtimeOnly $relative
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
        Copy-Item -LiteralPath (Join-Path $dotnetRoot $relative) -Destination $destination -Recurse
    }
    if(Test-Path -LiteralPath "$runtimeOnly/sdk"){throw 'SDK present in the runtime-only root'}
    Invoke-Proof 'runtime-only-sdks' @('--list-sdks') "$run/unrelated working directory" -executable "$runtimeOnly/dotnet.exe" -runtimeOnly
    if(![string]::IsNullOrWhiteSpace([IO.File]::ReadAllText("$evidence/runtime-only-sdks.stdout.log"))){throw 'Runtime-only host discovered an SDK'}
    Invoke-Proof 'runtime-only-runtimes' @('--list-runtimes') "$run/unrelated working directory" -executable "$runtimeOnly/dotnet.exe" -runtimeOnly
    if(@(Get-ChildItem -LiteralPath "$run/fresh-published/RuntimeConsumer" -Filter 'TrueMoon.Aether.Tooling.dll').Count){throw 'Workflow DLL delivered to Runtime consumer'}
    Run-Profile 'sdk-free' "$run/fresh-published/RuntimeConsumer/RuntimeConsumer.dll" "$run/fresh-published/Extension/ContractExtension.dll" "$run/fresh-published/RuntimeArtifactTool/RuntimeArtifactTool.dll" -isolated -runtimeHost "$runtimeOnly/dotnet.exe"
    $report.RuntimeOnly=@{Root=$runtimeOnly;RuntimeVersion=$runtimeVersion;SdkCount=0;WorkflowDelivered=$false;SourceCompilationAtExecution=$false}
    foreach($source in $report.Sources){if((Get-FileHash -LiteralPath $source.Path).Hash -ne $source.Sha256){throw 'Source drift since proof capture'}}
    $report.Completed=$true
}catch{$report.Failure=$_.Exception.ToString();throw}finally{Save-Report;Write-Host "Evidence: $evidence"}
