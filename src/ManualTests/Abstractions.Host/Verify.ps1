[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory,[string]$PackageVerification)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
. (Join-Path $PSScriptRoot '../PackageDelivery.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
$run=Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-ARCH31-delivery-'+[Guid]::NewGuid().ToString('N'))
if(Test-Path -LiteralPath $evidence){throw 'Use a fresh evidence directory.'}
foreach($directory in @($evidence,$run,"$run/feed","$run/cache","$run/unrelated working directory")){[void][IO.Directory]::CreateDirectory($directory)}
$profile=Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory $evidence
$sharedDelivery=Import-TrueMoonPackageDelivery -Manifest $PackageVerification -Profile $profile
$version=if($sharedDelivery){$sharedDelivery.Version}else{$profile.Version+'.arch31.'+(Get-Date -Format 'yyyyMMddHHmmss')}
$packages=@('TrueMoon.Contracts','TrueMoon.Core','TrueMoon.Cobalt','TrueMoon','TrueMoon.Extensions.DependencyInjection','TrueMoon.Aether.Abstractions')
$report=[ordered]@{Completed=$false;Stage='ARCH3.1';Version=$version;RunDirectory=$run;BuildProfile=$profile;Commands=@();Packages=@();Graphs=@();Profiles=@();Sources=@();Failure=$null}
function Save-Report{$report | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath "$evidence/verification.json" -Encoding utf8}
function Invoke-Proof([string]$name,[string[]]$arguments,[string]$cwd=$workspace,[switch]$isolated){
    if($arguments[0] -in @('build','publish','pack','restore')){$arguments+="-bl:$evidence/$name-{}.binlog"}
    $info=[Diagnostics.ProcessStartInfo]::new('dotnet');$info.WorkingDirectory=$cwd;$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    if($isolated){$info.Environment['NUGET_PACKAGES']="$run/cache";$info.Environment['NUGET_HTTP_CACHE_PATH']="$run/http-cache"}
    foreach($argument in $arguments){[void]$info.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::Start($info);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    try{
        if(!$process.WaitForExit(180000)){$process.Kill($true);$process.WaitForExit();throw "$name timed out"}
        [IO.File]::WriteAllText("$evidence/$name.stdout.log",$stdout.GetAwaiter().GetResult());[IO.File]::WriteAllText("$evidence/$name.stderr.log",$stderr.GetAwaiter().GetResult())
        $report.Commands+=@{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode;WorkingDirectory=$cwd;Isolated=[bool]$isolated};Save-Report
        if($process.ExitCode -ne 0){throw "$name failed"};Write-Host "$name passed"
    }finally{if(!$process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
function Run-Profile([string]$name,[string]$hostAssembly,[string]$extension,[switch]$isolated){
    foreach($provider in @('cobalt','microsoft')){
        $output="$evidence/$name-$provider.json";$arguments=@($hostAssembly,$extension,$output);if($provider -eq 'microsoft'){$arguments+='microsoft'}
        Invoke-Proof "$name-$provider" $arguments "$run/unrelated working directory" -isolated:$isolated
        $proof=Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
        if(!$proof.Completed -or !$proof.PrivateExtension -or !$proof.SharedContracts -or $proof.ForbiddenAssemblies.Count -ne 0 -or $proof.FactoryCalls -ne 1 -or $proof.QueryCallbacks -ne 1 -or $proof.Commands -ne 2 -or $proof.ValueRecorded -ne 1 -or $proof.Executions -ne 1 -or $proof.Stops -ne 1 -or $proof.Disposals -ne 1 -or $proof.RealWorld){throw 'Contract host proof mismatch'}
        $report.Profiles+=@{Name="$name-$provider";Report=$output;Sha256=(Get-FileHash -LiteralPath $output).Hash};Save-Report
    }
}
try{
    if($PackageVerification){$report.Sources+=@{Path=[IO.Path]::GetFullPath($PackageVerification);Sha256=(Get-FileHash -LiteralPath $PackageVerification).Hash}}
    foreach($module in @($packages+'ManualTests/Abstractions.Extension'+'ManualTests/Abstractions.Host')){
        foreach($path in (& rg --files "$workspace/$module" -g '*.cs' -g '*.csproj')){$report.Sources+=@{Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash}}
    }
    foreach($name in @('Host','Extension')){Invoke-Proof ('source-build-'+$name) @('build',"ManualTests/Abstractions.$name/Abstractions.$name.csproj",'-c','Debug','--verbosity','quiet')}
    Run-Profile 'source' "$workspace/ManualTests/Abstractions.Host/bin/Debug/$($profile.Framework)/Abstractions.Host.dll" "$workspace/ManualTests/Abstractions.Extension/bin/Debug/$($profile.Framework)/ContractExtension.dll"
    foreach($name in @('Host','Extension')){Invoke-Proof ('publish-'+$name) @('publish',"ManualTests/Abstractions.$name/Abstractions.$name.csproj",'-c','Release','-o',"$run/published/$name",'--verbosity','quiet')}
    Run-Profile 'published' "$run/published/Host/Abstractions.Host.dll" "$run/published/Extension/ContractExtension.dll"
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
    $specs=@(@{Name='Host';Assembly='Abstractions.Host';Namespace='TrueMoon.ManualTests.ContractHost';References=@('TrueMoon','TrueMoon.Aether.Abstractions','TrueMoon.Extensions.DependencyInjection');Exe=$true},@{Name='Extension';Assembly='ContractExtension';Namespace='TrueMoon.ManualTests.ContractExtension';References=@('TrueMoon.Aether.Abstractions');Exe=$false})
    foreach($spec in $specs){
        [void][IO.Directory]::CreateDirectory("$fresh/$($spec.Name)")
        $refs=($spec.References | ForEach-Object {'<PackageReference Include="'+$_+'" Version="'+$version+'"/>'}) -join '';$output=if($spec.Exe){'<OutputType>Exe</OutputType>'}else{''}
        [IO.File]::WriteAllText("$fresh/$($spec.Name)/Consumer.csproj",'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'+$output+'<AssemblyName>'+$spec.Assembly+'</AssemblyName><RootNamespace>'+$spec.Namespace+'</RootNamespace></PropertyGroup><ItemGroup>'+$refs+'</ItemGroup></Project>')
        foreach($file in (Get-ChildItem -LiteralPath "$workspace/ManualTests/Abstractions.$($spec.Name)" -Filter '*.cs')){Copy-Item -LiteralPath $file.FullName -Destination "$fresh/$($spec.Name)/$($file.Name)"}
        Invoke-Proof ('fresh-restore-'+$spec.Name) @('restore',"$($spec.Name)/Consumer.csproj",'--configfile',"$fresh/NuGet.Config",'--no-http-cache') $fresh -isolated
        $graph=Get-Content -LiteralPath "$fresh/$($spec.Name)/obj/project.assets.json" -Raw | ConvertFrom-Json -AsHashtable
        if(@($graph.libraries.Values | Where-Object type -ne 'package').Count -or $graph.packageFolders.Count -ne 1 -or [IO.Path]::GetFullPath(@($graph.packageFolders.Keys)[0]).TrimEnd('\','/') -ne [IO.Path]::GetFullPath("$run/cache").TrimEnd('\','/')){throw 'Non-isolated fresh graph'}
        $local=@($graph.libraries.Keys | Where-Object {$_ -match '^TrueMoon(?:\.[^/]+)?/'})
        if($spec.Name -eq 'Extension' -and @($local | Where-Object {$_ -notmatch '^TrueMoon\.(Aether\.Abstractions|Contracts)/'}).Count){throw 'Extension requires a non-neutral package'}
        foreach($key in $local){
            if(!$key.EndsWith('/'+$version,[StringComparison]::Ordinal)){throw 'Stale local package'}
            $metadata=Get-Content -LiteralPath (Join-Path "$run/cache" ($graph.libraries[$key].path+'/.nupkg.metadata')) -Raw | ConvertFrom-Json
            if([IO.Path]::GetFullPath($metadata.source) -ne [IO.Path]::GetFullPath("$run/feed")){throw 'Wrong local feed'}
        }
        $report.Graphs+=@{Project=$spec.Name;ProjectReferences=0;LocalPackages=$local;AssetsSha256=(Get-FileHash -LiteralPath "$fresh/$($spec.Name)/obj/project.assets.json").Hash}
        Invoke-Proof ('fresh-publish-'+$spec.Name) @('publish',"$($spec.Name)/Consumer.csproj",'--no-restore','-c','Release','-o',"$run/fresh-published/$($spec.Name)",'--verbosity','quiet') $fresh -isolated
    }
    $installed="$run/cache/truemoon.aether.abstractions/$version/lib/$($profile.Framework)/TrueMoon.Aether.Abstractions.dll"
    foreach($name in @('Host','Extension')){if((Get-FileHash -LiteralPath $installed).Hash -ne (Get-FileHash -LiteralPath "$run/fresh-published/$name/TrueMoon.Aether.Abstractions.dll").Hash){throw 'Installed/output Abstractions bytes differ'}}
    Run-Profile 'fresh' "$run/fresh-published/Host/Abstractions.Host.dll" "$run/fresh-published/Extension/ContractExtension.dll" -isolated
    foreach($source in $report.Sources){if((Get-FileHash -LiteralPath $source.Path).Hash -ne $source.Sha256){throw 'Source drift since proof capture'}}
    $report.Completed=$true
}catch{$report.Failure=$_.Exception.ToString();throw}finally{Save-Report;Write-Host "Evidence: $evidence"}
