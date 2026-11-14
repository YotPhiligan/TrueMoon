[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory,[switch]$Reload,[switch]$Migration,[string]$PackageVerification,[ValidateRange(1,1000)][int]$Cycles=1)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if($Reload -and $Migration){throw 'Choose either Reload or Migration.'}
$replacement=$Reload -or $Migration
if($Cycles -ne 1 -and !$replacement){throw 'Multiple cycles require Reload or Migration.'}
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
. (Join-Path $PSScriptRoot '../PackageDelivery.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
$run=Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-ARCH36-'+[Guid]::NewGuid().ToString('N'))
if(Test-Path -LiteralPath $evidence){throw 'Use a fresh evidence directory.'}
foreach($path in @($evidence,$run,"$run/feed","$run/cache","$run/unrelated cwd")){[void][IO.Directory]::CreateDirectory($path)}
$profile=Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory $evidence
$sharedDelivery=Import-TrueMoonPackageDelivery -Manifest $PackageVerification -Profile $profile
$version=if($sharedDelivery){$sharedDelivery.Version}else{$profile.Version+$(if($Migration){'.arch38.'}elseif($Reload){'.arch37.'}else{'.arch36.'})+(Get-Date -Format 'yyyyMMddHHmmss')}
$report=[ordered]@{Completed=$false;Stage=$(if($Migration){'ARCH3.8'}elseif($Reload){'ARCH3.7'}else{'ARCH3.6'});RunDirectory=$run;Version=$version;BuildProfile=$profile;Commands=@();Packages=@();ExternalPackages=@();Profiles=@();Graphs=@();Sources=@();Failure=$null}
function Save-Report{$report | ConvertTo-Json -Depth 18 | Set-Content -LiteralPath "$evidence/verification.json" -Encoding utf8}
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
function Run-Profile([string]$name,[string]$hostDll,[string]$publisherDll,[string]$modDll,[string]$privateDll,[switch]$isolated,[string]$runtimeHost='dotnet'){
    $publishArguments=@($publisherDll,"$run/artifacts-$name",$modDll,$privateDll)
    if($replacement){$publishArguments+=@($(if($Migration){'TrueMoon.ManualTests.ModFixture.MigrationEntry'}else{'TrueMoon.ManualTests.ModFixture.ReloadEntry'}),'1.0.0')}
    Invoke-Proof "$name-publisher" $publishArguments "$run/unrelated cwd" -isolated:$isolated
    $directory=([IO.File]::ReadAllText("$evidence/$name-publisher.stdout.log")).Trim()
    $secondDirectory=$null;$secondMod=$modDll
    if($replacement){
        $secondEntry=if($Migration){'SecondMigrationEntry'}else{'SecondReloadEntry'}
        if($name -in @('fresh','sdk-free')){$secondMod="$run/fresh-published/ModV2/ModFixture.dll";$secondEntry=if($Migration){'MigrationEntry'}else{'ReloadEntry'}}
        Invoke-Proof "$name-publisher-v2" @($publisherDll,"$run/artifacts-$name-v2",$secondMod,$privateDll,"TrueMoon.ManualTests.ModFixture.$secondEntry",'2.0.0') "$run/unrelated cwd" -isolated:$isolated
        $secondDirectory=([IO.File]::ReadAllText("$evidence/$name-publisher-v2.stdout.log")).Trim()
    }
    foreach($di in @('cobalt','msdi')){
        $arguments=if($replacement){@($hostDll,$(if($Migration){'--migration'}else{'--reload'}),$directory,$secondDirectory,$di)}else{@($hostDll,$directory,$di)}
        if($Cycles -gt 1){$arguments=@($hostDll,'--cycles',$directory,$secondDirectory,$di,$(if($Migration){'migration'}else{'reload'}),[string]$Cycles)}
        Invoke-Proof "$name-$di" $arguments "$run/unrelated cwd" -isolated:$isolated -executable $runtimeHost -runtimeOnly:($runtimeHost -ne 'dotnet')
        $proof=[IO.File]::ReadAllText("$evidence/$name-$di.stdout.log") | ConvertFrom-Json
        $ticks=if($replacement){$Cycles+2}else{2};$components=if($Migration){4}else{1}
        if(!$proof.Success -or !$proof.Collected -or !$proof.UnloadRequested -or $proof.Ticks -ne $ticks -or $proof.Components -ne $components -or $proof.ToolingDelivered){throw 'Mod proof evidence incomplete'}
        if($Cycles -eq 1 -and $Reload -and (!$proof.WorldPreserved -or !$proof.StatePreserved -or !$proof.OldCommandsRejected -or !$proof.RetiredCollectedWhileCurrentLive -or $proof.Generation -ne 2 -or $proof.OldCallbacks -ne 0 -or $proof.ValueAfterReload -ne 19)){throw 'Replacement evidence incomplete'}
        if($Cycles -eq 1 -and $Migration -and (!$proof.NewWorld -or !$proof.PersistentIdsPreserved -or !$proof.ReferencesPreserved -or !$proof.StatePreserved -or !$proof.OldCommandsRejected -or !$proof.RetiredCollectedWhileCurrentLive -or $proof.Generation -ne 2 -or $proof.OldCallbacks -ne 0 -or $proof.ValueAfterReload -ne 19)){throw 'World migration evidence incomplete'}
        if($Cycles -gt 1 -and ($proof.Cycles -ne $Cycles -or $proof.RetiredCollected -ne $Cycles -or !$proof.FinalCollected -or $proof.Generation -ne $Cycles+1 -or $proof.Owners -ne 0 -or !$proof.StatePreserved -or !$proof.OldCommandsRejected -or !$proof.RetiredCollectedWhileCurrentLive -or $proof.OldCallbacks -ne 0)){throw 'Cycle evidence incomplete'}
        if($runtimeHost -ne 'dotnet' -and $proof.SdkPresent){throw 'SDK discovered in runtime-only execution'}
        $report.Profiles+=@{Name="$name-$di";Proof=$proof;HostSha256=(Get-FileHash -LiteralPath $hostDll).Hash;ModSha256=(Get-FileHash -LiteralPath $modDll).Hash;ManifestSha256=(Get-FileHash -LiteralPath "$directory/manifest.json").Hash}
        if($replacement){$report.Profiles[-1].CandidateModSha256=(Get-FileHash -LiteralPath $secondMod).Hash;$report.Profiles[-1].CandidateManifestSha256=(Get-FileHash -LiteralPath "$secondDirectory/manifest.json").Hash}
    }
}
try{
    if($PackageVerification){$report.Sources+=@{Path=[IO.Path]::GetFullPath($PackageVerification);Sha256=(Get-FileHash -LiteralPath $PackageVerification).Hash}}
    foreach($folder in @('TrueMoon.Aether.Abstractions/Mods','TrueMoon.Aether.Runtime/Mods','TrueMoon.Aether.Tooling/Mods','ManualTests/ModFixture','ManualTests/ModPrivateDependency','ManualTests/ModConsumer')){
        foreach($file in (Get-ChildItem -LiteralPath "$workspace/$folder" -Recurse -File | Where-Object {$_.FullName -notmatch '[\\/](bin|obj)[\\/]'})){
            $report.Sources+=@{Path=$file.FullName;Sha256=(Get-FileHash -LiteralPath $file.FullName).Hash}
        }
    }
    foreach($configuration in @('Debug','Release')){
        foreach($spec in @(@{Name='mod';Project='ManualTests/ModFixture/ModFixture.csproj'},@{Name='runtime';Project='ManualTests/ModConsumer/Runtime/ModRuntimeConsumer.csproj'},@{Name='publisher';Project='ManualTests/ModConsumer/Publisher/ModPublisher.csproj'})){
            Invoke-Proof "source-$configuration-build-$($spec.Name)" @('build',$spec.Project,'-c',$configuration,'--verbosity','quiet')
            Invoke-Proof "source-$configuration-publish-$($spec.Name)" @('publish',$spec.Project,'--no-build','-c',$configuration,'-o',"$run/published-$configuration/$($spec.Name)",'--verbosity','quiet')
        }
        Run-Profile "source-$configuration" "$workspace/ManualTests/ModConsumer/Runtime/bin/$configuration/$($profile.Framework)/ModRuntimeConsumer.dll" "$workspace/ManualTests/ModConsumer/Publisher/bin/$configuration/$($profile.Framework)/ModPublisher.dll" "$workspace/ManualTests/ModFixture/bin/$configuration/$($profile.Framework)/ModFixture.dll" "$workspace/ManualTests/ModFixture/bin/$configuration/$($profile.Framework)/ModPrivateDependency.dll"
        Run-Profile "published-$configuration" "$run/published-$configuration/runtime/ModRuntimeConsumer.dll" "$run/published-$configuration/publisher/ModPublisher.dll" "$run/published-$configuration/mod/ModFixture.dll" "$run/published-$configuration/mod/ModPrivateDependency.dll"
    }
    foreach($package in @('TrueMoon.Contracts','TrueMoon.Core','TrueMoon.Cobalt','TrueMoon','TrueMoon.Extensions.DependencyInjection','TrueMoon.Aether.Abstractions','TrueMoon.Enerit.Generator','TrueMoon.Enerit','TrueMoon.Mithril','TrueMoon.Aether.Runtime','TrueMoon.Aether.Tooling')){
        if($sharedDelivery){Copy-TrueMoonDeliveryPackage -Delivery $sharedDelivery -Id $package -Destination "$run/feed"}
        else{Invoke-Proof "pack-$package" @('pack',"$package/$package.csproj",'--no-build','-c','Release','-o',"$run/feed","-p:PackageVersion=$version",'--verbosity','quiet')}
        $path=Get-Item -LiteralPath "$run/feed/$package.$version.nupkg";$report.Packages+=@{Id=$package;Path=$path.FullName;Sha256=(Get-FileHash -LiteralPath $path.FullName).Hash}
    }
    $fresh="$run/fresh";Write-TrueMoonConsumerBuildProfile -Profile $profile -Directory $fresh
    [IO.File]::WriteAllText("$fresh/Directory.Build.targets",'<Project/>')
    foreach($assetPath in @('ManualTests/ModConsumer/Runtime/obj/project.assets.json','ManualTests/ModConsumer/Publisher/obj/project.assets.json')){
        $assets=Get-Content -LiteralPath "$workspace/$assetPath" -Raw | ConvertFrom-Json -AsHashtable
        foreach($library in $assets.libraries.Values){
            if($library.type -ne 'package'){continue}
            $copied=$false
            foreach($folder in $assets.packageFolders.Keys){
                $cacheDirectory=Join-Path $folder $library.path
                if(!(Test-Path -LiteralPath $cacheDirectory)){continue}
                foreach($package in (Get-ChildItem -LiteralPath $cacheDirectory -Filter '*.nupkg' -File)){
                    $destination=Join-Path "$run/feed" $package.Name
                    if(!(Test-Path -LiteralPath $destination)){Copy-Item -LiteralPath $package.FullName -Destination $destination;$report.ExternalPackages+=@{Name=$package.Name;Sha256=(Get-FileHash -LiteralPath $destination).Hash}}
                    $copied=$true
                }
            }
            if(!$copied){throw "Required external package archive is absent: $($library.path)"}
        }
    }
    [IO.File]::WriteAllText("$fresh/NuGet.Config",'<configuration><packageSources><clear/><add key="fresh" value="'+[Security.SecurityElement]::Escape("$run/feed")+'"/></packageSources></configuration>')
    foreach($spec in @(@{Name='Runtime';Source='ManualTests/ModConsumer/Runtime';Project='ModRuntimeConsumer'},@{Name='Publisher';Source='ManualTests/ModConsumer/Publisher';Project='ModPublisher'},@{Name='Mod';Source='ManualTests/ModFixture';Project='ModFixture'})){
        [void][IO.Directory]::CreateDirectory("$fresh/$($spec.Name)")
        foreach($file in (Get-ChildItem -LiteralPath "$workspace/$($spec.Source)" -Filter '*.cs')){Copy-Item -LiteralPath $file.FullName -Destination "$fresh/$($spec.Name)/$($file.Name)"}
        $template=[IO.File]::ReadAllText("$workspace/$($spec.Source)/$($spec.Project).csproj.template").Replace('__VERSION__',$version)
        [IO.File]::WriteAllText("$fresh/$($spec.Name)/$($spec.Project).csproj",$template)
    }
    [void][IO.Directory]::CreateDirectory("$fresh/Private")
    Copy-Item -LiteralPath "$workspace/ManualTests/ModPrivateDependency/ModMath.cs" -Destination "$fresh/Private/ModMath.cs"
    [IO.File]::WriteAllText("$fresh/Private/ModPrivateDependency.csproj",'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>$(TF)</TargetFramework><RootNamespace>TrueMoon.ManualTests.ModPrivateDependency</RootNamespace></PropertyGroup></Project>')
    foreach($spec in @(@{Name='Runtime';Project='ModRuntimeConsumer'},@{Name='Publisher';Project='ModPublisher'},@{Name='Mod';Project='ModFixture'})){
        Invoke-Proof "fresh-restore-$($spec.Name)" @('restore',"$($spec.Name)/$($spec.Project).csproj",'--force','--no-cache','--verbosity','quiet') $fresh -isolated
        Invoke-Proof "fresh-publish-$($spec.Name)" @('publish',"$($spec.Name)/$($spec.Project).csproj",'--no-restore','-c','Release','-o',"$run/fresh-published/$($spec.Name)",'--verbosity','quiet') $fresh -isolated
        $assets=Get-Content -LiteralPath "$fresh/$($spec.Name)/obj/project.assets.json" -Raw | ConvertFrom-Json -AsHashtable
        $local=@($assets.libraries.Keys | Where-Object {$_ -like 'TrueMoon*'} | Sort-Object)
        if($spec.Name -eq 'Mod' -and ($local.Count -ne 2 -or $local.Where({$_ -notlike 'TrueMoon.Aether.Abstractions/*' -and $_ -notlike 'TrueMoon.Contracts/*'}).Count -ne 0)){throw 'Mod SDK received implementation packages'}
        if($spec.Name -eq 'Runtime' -and ($local.Where({$_ -match 'Tooling|Editor|Alloy|Argentis'}).Count -ne 0)){throw 'Runtime consumer received tooling/UI'}
        $report.Graphs+=@{Name=$spec.Name;Packages=$local;AssetsSha256=(Get-FileHash -LiteralPath "$fresh/$($spec.Name)/obj/project.assets.json").Hash}
    }
    if($replacement){
        [void][IO.Directory]::CreateDirectory("$fresh/ModV2")
        foreach($file in (Get-ChildItem -LiteralPath "$workspace/ManualTests/ModFixture" -Filter '*.cs')){Copy-Item -LiteralPath $file.FullName -Destination "$fresh/ModV2/$($file.Name)"}
        $template=[IO.File]::ReadAllText("$fresh/Mod/ModFixture.csproj").Replace('<AssemblyName>ModFixture</AssemblyName>','<AssemblyName>ModFixture</AssemblyName><DefineConstants>$(DefineConstants);MOD_V2</DefineConstants>')
        [IO.File]::WriteAllText("$fresh/ModV2/ModFixture.csproj",$template)
        Invoke-Proof 'fresh-restore-ModV2' @('restore','ModV2/ModFixture.csproj','--force','--no-cache','--verbosity','quiet') $fresh -isolated
        Invoke-Proof 'fresh-publish-ModV2' @('publish','ModV2/ModFixture.csproj','--no-restore','-c','Release','-o',"$run/fresh-published/ModV2",'--verbosity','quiet') $fresh -isolated
        if((Get-FileHash -LiteralPath "$run/fresh-published/Mod/ModFixture.dll").Hash -eq (Get-FileHash -LiteralPath "$run/fresh-published/ModV2/ModFixture.dll").Hash){throw 'Fresh candidate DLL was not independently changed'}
    }
    Run-Profile 'fresh' "$run/fresh-published/Runtime/ModRuntimeConsumer.dll" "$run/fresh-published/Publisher/ModPublisher.dll" "$run/fresh-published/Mod/ModFixture.dll" "$run/fresh-published/Mod/ModPrivateDependency.dll" -isolated
    $dotnetRoot=[IO.Path]::GetDirectoryName((Get-Command dotnet).Source)
    $configuration=Get-Content -LiteralPath "$run/fresh-published/Runtime/ModRuntimeConsumer.runtimeconfig.json" -Raw | ConvertFrom-Json
    $runtimeVersion=$configuration.runtimeOptions.framework.version;$runtimeOnly="$run/runtime-only";[void][IO.Directory]::CreateDirectory($runtimeOnly)
    Copy-Item -LiteralPath "$dotnetRoot/dotnet.exe" -Destination "$runtimeOnly/dotnet.exe"
    foreach($relative in @("host/fxr/$runtimeVersion","shared/Microsoft.NETCore.App/$runtimeVersion")){
        $destination=Join-Path $runtimeOnly $relative;[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination));Copy-Item -LiteralPath (Join-Path $dotnetRoot $relative) -Destination $destination -Recurse
    }
    if(Test-Path -LiteralPath "$runtimeOnly/sdk"){throw 'SDK present in runtime-only root'}
    Run-Profile 'sdk-free' "$run/fresh-published/Runtime/ModRuntimeConsumer.dll" "$run/fresh-published/Publisher/ModPublisher.dll" "$run/fresh-published/Mod/ModFixture.dll" "$run/fresh-published/Mod/ModPrivateDependency.dll" -isolated -runtimeHost "$runtimeOnly/dotnet.exe"
    foreach($source in $report.Sources){if((Get-FileHash -LiteralPath $source.Path).Hash -ne $source.Sha256){throw 'Source drift since proof capture'}}
    $report.Completed=$true
}catch{$report.Failure=$_.Exception.ToString();throw}finally{Save-Report;Write-Host "Evidence: $evidence"}
