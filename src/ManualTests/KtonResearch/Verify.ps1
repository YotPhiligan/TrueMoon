[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [string]$PackageDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 is required by BuildProfile.ps1.' }
if (![OperatingSystem]::IsWindows() -or [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne 'X64') {
    throw 'PHYS0 currently verifies Windows x64 only.'
}
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = [IO.Path]::GetFullPath((Join-Path $workspace $EvidenceDirectory))
if (Test-Path -LiteralPath $evidence) { throw 'Use a fresh evidence directory; previous evidence is retained.' }
[void][IO.Directory]::CreateDirectory($evidence)
$summary = [ordered]@{
    Completed=$false; Utc=[DateTimeOffset]::UtcNow; EvidenceDirectory=$evidence
    Profile=$null; Packages=@(); Commands=@(); Reports=@(); Failure=$null
    ProductionEligible=$false; Scope='PHYS0 package/API/headless research, not PHYS1/provider/App/ECS acceptance.'
    Sources=@(Get-ChildItem -LiteralPath $PSScriptRoot -File | ForEach-Object {
        @{Name=$_.Name;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
    })
}
$packages=@(
    @{Id='BepuPhysics';Version='2.5.0-beta.33';Sha256='9C1DB047CD7B3597CD808D7B94A1EF6626E0041E305BB3D8FE41AA658C3F1D03'},
    @{Id='BepuUtilities';Version='2.5.0-beta.33';Sha256='D1CF1BB261720FEF05D197B4DE9F17E06BAC6D2A6C7574A6DE88BC21122C26EA'}
)

function Invoke-KtonCommand([string]$name,[string[]]$arguments,[int]$timeout=120) {
    $build=$arguments[0] -in @('restore','build','publish')
    if($build){$arguments += "-bl:$evidence/$name-{}.binlog"}
    $info=[Diagnostics.ProcessStartInfo]::new('dotnet')
    $info.WorkingDirectory=$consumer; $info.UseShellExecute=$false; $info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true
    foreach($argument in $arguments){[void]$info.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::new(); $process.StartInfo=$info
    $watch=[Diagnostics.Stopwatch]::StartNew()
    Write-Host "Starting $name"
    try {
        if(!$process.Start()){throw "Could not start $name"}
        $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
        while(!$process.WaitForExit(1000)) {
            if($watch.Elapsed.TotalSeconds -ge $timeout){$process.Kill($true);$process.WaitForExit();throw "Timeout and reaped: $name"}
            if([int]$watch.Elapsed.TotalSeconds % 10 -eq 0){Write-Host "$name running: $([int]$watch.Elapsed.TotalSeconds)s"}
        }
        $out=$stdout.GetAwaiter().GetResult(); $err=$stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path $evidence "$name.stdout.log"),$out)
        [IO.File]::WriteAllText((Join-Path $evidence "$name.stderr.log"),$err)
        $summary.Commands+=@{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode;Seconds=$watch.Elapsed.TotalSeconds}
        if($build -and !(Get-ChildItem -LiteralPath $evidence -Filter "$name-*.binlog")){throw "Missing binlog for $name"}
        if($process.ExitCode -ne 0){Write-Host $out;Write-Host $err;throw "$name failed with exit $($process.ExitCode)"}
        Write-Host "Completed $name"
    } finally {
        if(!$process.HasExited){$process.Kill($true);$process.WaitForExit()}
        $process.Dispose()
    }
}

try {
    . (Join-Path $PSScriptRoot '../BuildProfile.ps1')
    $profile=Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory (Join-Path $evidence 'profile')
    $summary.Profile=$profile
    if($profile.Framework -ne 'net11.0' -or $profile.Features -notmatch 'runtime-async=on'){throw 'Expected the repository .NET11/runtime-async profile.'}
    $consumer=Join-Path $evidence 'consumer'
    Write-TrueMoonConsumerBuildProfile -Profile $profile -Directory $consumer
    Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object {$_.Extension -in '.cs','.csproj' -or $_.Name -eq 'packages.lock.json'} | Copy-Item -Destination $consumer
    $feed=Join-Path $evidence 'feed';[void][IO.Directory]::CreateDirectory($feed)
    foreach($package in $packages){
        $id=$package.Id.ToLowerInvariant();$version=$package.Version
        $filename="$id.$version.nupkg";$archive=Join-Path $feed $filename
        if($PackageDirectory){Copy-Item -LiteralPath (Join-Path $PackageDirectory $filename) -Destination $archive}
        else{Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/$id/$version/$filename" -OutFile $archive -TimeoutSec 60}
        $sha=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
        if($sha -ne $package.Sha256){throw "Package SHA mismatch: $id"}
        $zip=[IO.Compression.ZipFile]::OpenRead($archive)
        try {
            $entry=@($zip.Entries|Where-Object FullName -like '*.nuspec')
            if($entry.Count -ne 1){throw "Expected one nuspec: $id"}
            $reader=[IO.StreamReader]::new($entry[0].Open())
            try{$nuspec=[xml]$reader.ReadToEnd()}finally{$reader.Dispose()}
            $metadata=$nuspec.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]')
            if($metadata.id -ne $package.Id -or $metadata.version -ne $version -or $metadata.license.InnerText -ne 'Apache-2.0'){throw "Unexpected package metadata: $id"}
            if($metadata.repository.commit -ne '29ed466870534c413aba9eeccd813cf15d135d0b'){throw "Unexpected repository commit: $id"}
            $native=@($zip.Entries|Where-Object FullName -match '^runtimes/|\.(so|dylib)$')
            $dllEntries=@($zip.Entries|Where-Object FullName -like '*.dll')
            if($native.Count -ne 0 -or $dllEntries.Count -ne 1 -or $dllEntries[0].FullName -notlike 'lib/net8.0/*.dll'){throw "Unexpected binary layout: $id"}
            $dll=$dllEntries[0];$stream=$dll.Open()
            try{$dllSha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}
            $dependencies=@($nuspec.SelectNodes('//*[local-name()="dependency"]')|ForEach-Object{@{Id=$_.id;Version=$_.version}})
            if($id -eq 'bepuphysics' -and ($dependencies.Count -ne 1 -or $dependencies[0].Id -ne 'BepuUtilities' -or $dependencies[0].Version -ne $version)){throw 'Unexpected BepuPhysics dependency graph.'}
            if($id -eq 'bepuutilities' -and $dependencies.Count -ne 0){throw 'Unexpected BepuUtilities dependencies.'}
            $summary.Packages+=@{Id=$package.Id;Version=$version;Sha256=$sha;Bytes=(Get-Item -LiteralPath $archive).Length;License='Apache-2.0';RepositoryCommit=[string]$metadata.repository.commit;Dependencies=$dependencies;Dll=$dll.FullName;DllSha256=$dllSha;NativeAssets=$native.Count}
        } finally {$zip.Dispose()}
    }
    $config=Join-Path $consumer 'NuGet.Config';$feedXml=[Security.SecurityElement]::Escape($feed)
    [IO.File]::WriteAllText($config,"<configuration><packageSources><clear/><add key='kton-research' value='$feedXml'/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders></configuration>")
    $project=Join-Path $consumer 'Probe.csproj';$cache=Join-Path $evidence 'packages'
    $restore=@('restore',$project,'--configfile',$config,'--packages',$cache)
    if(Test-Path -LiteralPath (Join-Path $consumer 'packages.lock.json')){$restore+='--locked-mode'}
    Invoke-KtonCommand 'restore' $restore
    $assets=Get-Content -LiteralPath (Join-Path $consumer 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
    $libraries=@($assets.libraries.Keys|Sort-Object)
    if(($libraries -join ',') -ne 'BepuPhysics/2.5.0-beta.33,BepuUtilities/2.5.0-beta.33'){throw "Unexpected restored graph: $libraries"}
    $summary.RestoredLibraries=$libraries
    foreach($configuration in @('Debug','Release')){
        $directory=Join-Path $evidence $(if($configuration -eq 'Debug'){'source-Debug'}else{'published-Release'})
        if($configuration -eq 'Debug'){Invoke-KtonCommand 'build-Debug' @('build',$project,'--no-restore','-c','Debug','-o',$directory)}
        else{Invoke-KtonCommand 'publish-Release' @('publish',$project,'--no-restore','-c','Release','--self-contained','false','-o',$directory)}
        $report=Join-Path $evidence "probe-$configuration.json"
        Invoke-KtonCommand "probe-$configuration" @((Join-Path $directory 'KtonResearch.dll'),$report)
        $data=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
        if(!$data.Completed -or $data.LiveOwners -ne 0 -or $data.MaximumBytesAfterCleanup -ne 0){throw "Incomplete/unclean probe: $configuration"}
        foreach($package in $summary.Packages){
            $assembly=@($data.Assemblies|Where-Object Name -eq $package.Id)
            if($assembly.Count -ne 1 -or $assembly[0].Sha256 -ne $package.DllSha256){throw "Actual loaded assembly differs: $($package.Id)"}
        }
        $summary.Reports+=@{Configuration=$configuration;Path=$report;Checks=$data.CheckCount;Sha256=(Get-FileHash -LiteralPath $report).Hash}
    }
    foreach($source in $summary.Sources){
        if((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $source.Name)).Hash -ne $source.Sha256){throw 'Probe sources changed during verification.'}
    }
    $summary.Completed=$true
} catch {$summary.Failure=$_.ToString();throw}
finally {$summary|ConvertTo-Json -Depth 14|Set-Content -LiteralPath (Join-Path $evidence 'verification.json') -Encoding utf8}
