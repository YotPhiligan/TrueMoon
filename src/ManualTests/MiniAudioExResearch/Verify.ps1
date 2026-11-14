[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [switch]$Device,
    [switch]$MutedDevice,
    [string]$PatchedPackage,
    [string]$NativeBuildReceipt
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 is required by the common BuildProfile helper.' }
if (![OperatingSystem]::IsWindows() -or [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne 'X64') {
    throw 'This prototype profile is Windows x64 only.'
}
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fixedDependency = ![string]::IsNullOrWhiteSpace($PatchedPackage)
if($fixedDependency -ne (![string]::IsNullOrWhiteSpace($NativeBuildReceipt))){throw 'Supply both -PatchedPackage and -NativeBuildReceipt.'}
$nativeBuild=$null
if($fixedDependency){
    $PatchedPackage=[IO.Path]::GetFullPath($PatchedPackage)
    $NativeBuildReceipt=[IO.Path]::GetFullPath($NativeBuildReceipt)
    $nativeBuild=Get-Content -Raw -LiteralPath $NativeBuildReceipt|ConvertFrom-Json
    if(!$nativeBuild.Completed -or (Get-FileHash -LiteralPath $PatchedPackage).Hash -ne $nativeBuild.Package.Sha256){throw 'Patched package/receipt mismatch.'}
}
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory, $workspace)
if (Test-Path -LiteralPath $evidence) { throw 'Choose a fresh evidence directory; previous reports are preserved.' }
[void][IO.Directory]::CreateDirectory($evidence)
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
$profile = Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory (Join-Path $evidence 'profile')
$consumer = Join-Path $evidence 'consumer'
Write-TrueMoonConsumerBuildProfile -Profile $profile -Directory $consumer
Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object Extension -in '.cs', '.csproj' |
    Copy-Item -Destination $consumer
$feed = Join-Path $evidence 'feed'; [void][IO.Directory]::CreateDirectory($feed)
$packages = @(
    @{Id='JAJ.Packages.MiniAudioEx';Version='4.0.1';ExpectedSha256='E57733E2FFBB45703EE03C1945B396CEA322F3DE06A6C1D7D4F6F834834809E7'},
    @{Id='OggVorbisEncoder';Version='1.2.2';ExpectedSha256='6D3D4E4C3928B54D5E37F02F345CEC3FB8D19B8CB718D1A5EC58D70A1561341F'}
)
if($fixedDependency){
    $packages[0]=@{Id=$nativeBuild.PackageId;Version=$nativeBuild.PackageVersion;ExpectedSha256=$nativeBuild.Package.Sha256;LocalPath=$PatchedPackage}
    $projectPath=Join-Path $consumer 'Probe.csproj'
    $text=[IO.File]::ReadAllText($projectPath)
    $original='<PackageReference Include="JAJ.Packages.MiniAudioEx" Version="4.0.1" />'
    if(!$text.Contains($original)){throw 'Probe package reference anchor changed.'}
    [IO.File]::WriteAllText($projectPath,$text.Replace($original,"<PackageReference Include=`"$($nativeBuild.PackageId)`" Version=`"$($nativeBuild.PackageVersion)`" />"))
}
$summary = [ordered]@{
    Completed=$false; Utc=[DateTimeOffset]::UtcNow; DeviceRequested=[bool]$Device
    MutedDevice=[bool]$MutedDevice
    Profile=$profile; EvidenceDirectory=$evidence; Packages=@(); Commands=@(); Reports=@(); Failure=$null
    KnownDefects=@(); ProductionEligible=$false; Scope='Routed valid-fixture binding prototype; not a production audio module.'
    PatchedDependency=$fixedDependency;DependencyProfileVerified=$false;AudioModuleImplemented=$false;NativeRegression=$null
    Sources=@(Get-ChildItem -LiteralPath $PSScriptRoot -File | ForEach-Object {
        @{Name=$_.Name;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
    })
}
if($fixedDependency){
    $summary.Scope='Patched Windows x64 dependency: original decoder vtable without routing guard; independent NuGet source/publish proof.'
    $summary.NativeBuildReceipt=@{Path=$NativeBuildReceipt;Sha256=(Get-FileHash -LiteralPath $NativeBuildReceipt).Hash}
}

function Invoke-AudioProbe([string]$name, [string[]]$arguments, [int]$timeout=120, [int]$expectedExitCode=0, [string]$executable='dotnet') {
    $build = $arguments[0] -in @('restore','build','publish')
    if ($build) { $arguments += "-bl:$evidence/$name-{}.binlog" }
    $info = [Diagnostics.ProcessStartInfo]::new($executable)
    $info.WorkingDirectory=$consumer; $info.UseShellExecute=$false; $info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true
    foreach ($argument in $arguments) { [void]$info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo=$info
    $started = [DateTime]::UtcNow
    Write-Host "Starting $name"
    try {
        if (!$process.Start()) { throw "Failed to start $name" }
        $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
        while (!$process.WaitForExit(1000)) {
            $elapsed=([DateTime]::UtcNow-$started).TotalSeconds
            if ($elapsed -ge $timeout) {
                $process.Kill($true); $process.WaitForExit()
                throw "Timed out and reaped $name after $timeout seconds"
            }
            if ([int]$elapsed % 10 -eq 0) { Write-Host "$name running: $([int]$elapsed)s" }
        }
        $out=$stdout.GetAwaiter().GetResult(); $err=$stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path $evidence "$name.stdout.log"),$out)
        [IO.File]::WriteAllText((Join-Path $evidence "$name.stderr.log"),$err)
        $summary.Commands += @{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode;ExpectedExitCode=$expectedExitCode;Seconds=([DateTime]::UtcNow-$started).TotalSeconds}
        if ($build -and !(Get-ChildItem -LiteralPath $evidence -Filter "$name-*.binlog")) { throw "Missing binlog: $name" }
        if ($process.ExitCode -ne $expectedExitCode) { Write-Host $out; Write-Host $err; throw "$name exited $($process.ExitCode), expected $expectedExitCode" }
        Write-Host "Completed $name"
    } finally {
        if (!$process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
}

try {
    foreach ($package in $packages) {
        $id=$package.Id.ToLowerInvariant(); $version=$package.Version
        $archive=Join-Path $feed "$id.$version.nupkg"
        if($package.ContainsKey('LocalPath')){Copy-Item -LiteralPath $package.LocalPath -Destination $archive}
        else{Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/$id/$version/$id.$version.nupkg" -OutFile $archive}
        $sha=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
        if ($package.ExpectedSha256 -and $sha -ne $package.ExpectedSha256) { throw "Archive SHA differs: $id" }
        $summary.Packages += @{Id=$package.Id;Version=$version;Sha256=$sha;Bytes=(Get-Item -LiteralPath $archive).Length}
    }
    $feedXml=[Security.SecurityElement]::Escape($feed)
    $config=Join-Path $consumer 'NuGet.Config'
    [IO.File]::WriteAllText($config,"<configuration><packageSources><clear/><add key='audio-research' value='$feedXml'/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders></configuration>")
    $project=Join-Path $consumer 'Probe.csproj'; $cache=Join-Path $evidence 'packages'
    Invoke-AudioProbe 'restore' @('restore',$project,'--configfile',$config,'--packages',$cache)
    $debug=Join-Path $evidence 'source-Debug'
    Invoke-AudioProbe 'build-Debug' @('build',$project,'--no-restore','-c','Debug','-o',$debug)
    $fixtures=Join-Path $evidence 'fixtures'
    Invoke-AudioProbe 'fixtures' @((Join-Path $debug 'MiniAudioExResearch.dll'),'fixtures',$fixtures,(Join-Path $evidence 'fixtures.json'))
    $originalAbi=$null
    if($fixedDependency){
        $baselineReport=Join-Path $evidence 'native-baseline.json';$patchedReport=Join-Path $evidence 'native-patched.json'
        Invoke-AudioProbe 'native-baseline' @($fixtures,$baselineReport) 120 1 -executable $nativeBuild.Baseline.Regression
        Invoke-AudioProbe 'native-patched' @($fixtures,$patchedReport,'--fixed') 120 0 -executable $nativeBuild.Patched.Regression
        $baselineNative=Get-Content -Raw -LiteralPath $baselineReport|ConvertFrom-Json
        $patchedNative=Get-Content -Raw -LiteralPath $patchedReport|ConvertFrom-Json
        if($baselineNative.Completed -or $baselineNative.ObservedLeakedBytes -ne 110040 -or $baselineNative.ObservedLeakedBlocks -ne 131){throw 'Native baseline did not reproduce the pinned defects.'}
        if(!$patchedNative.Completed -or $patchedNative.Failures -ne 0 -or $patchedNative.ObservedLeakedBytes -ne 0 -or $patchedNative.ObservedLeakedBlocks -ne 0){throw 'Native patched regression failed.'}
        $summary.NativeRegression=@{Baseline=$baselineNative;Patched=$patchedNative;BaselineReport=$baselineReport;PatchedReport=$patchedReport}

        # Inspect the original shipped DLL in a separate control process; it is never used by the patched consumer profiles.
        $control=Join-Path $evidence 'original-abi-control';[void][IO.Directory]::CreateDirectory($control)
        Get-ChildItem -LiteralPath $debug | Copy-Item -Destination $control -Recurse
        $originalArchive=Join-Path (Split-Path $NativeBuildReceipt) 'jaj.packages.miniaudioex.4.0.1.nupkg'
        if((Get-FileHash -LiteralPath $originalArchive).Hash -ne $nativeBuild.Pins.managedPackageSha256){throw 'Original ABI archive SHA differs.'}
        $zip=[IO.Compression.ZipFile]::OpenRead($originalArchive)
        try{
            $originalEntryStream=$zip.GetEntry('runtimes/win-x64/native/miniaudioex.dll').Open()
            try{$destination=[IO.File]::Create((Join-Path $control 'miniaudioex.dll'));try{$originalEntryStream.CopyTo($destination)}finally{$destination.Dispose()}}finally{$originalEntryStream.Dispose()}
        }finally{$zip.Dispose()}
        $abiReport=Join-Path $evidence 'original-abi-control.json'
        Invoke-AudioProbe 'original-abi-control' @((Join-Path $control 'MiniAudioExResearch.dll'),'inspect',$fixtures,$abiReport,'raw-vorbis')
        $originalAbi=Get-Content -Raw -LiteralPath $abiReport|ConvertFrom-Json
        if($originalAbi.Observations.NativeSha256 -ne $nativeBuild.Pins.originalNativeSha256){throw 'Original ABI DLL differs.'}
        $summary.OriginalAbiControl=@{Path=$abiReport;Sha256=(Get-FileHash -LiteralPath $abiReport).Hash}
    }
    foreach ($configuration in @('Debug','Release')) {
        $directory=$debug
        if ($configuration -eq 'Release') {
            $directory=Join-Path $evidence 'published-Release'
            Invoke-AudioProbe 'publish-Release' @('publish',$project,'--no-restore','-c','Release','--self-contained','false','-o',$directory)
        }
        $modes=@('inspect','cleanup','offline'); if ($Device) { $modes+=$(if($MutedDevice){'device-muted'}else{'device'}) }
        foreach ($mode in $modes) {
            $name="$mode-$configuration"; $report=Join-Path $evidence "$name.json"
            $probeArguments=@((Join-Path $directory 'MiniAudioExResearch.dll'),$mode,$fixtures,$report)
            if($fixedDependency){$probeArguments+='raw-vorbis'}
            Invoke-AudioProbe $name $probeArguments
            $data=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
            if (!$data.Completed -or $data.LiveObjects -ne 0 -or $data.LiveObjectBytes -ne 0) { throw "Incomplete report: $name" }
            if($fixedDependency -and !$data.Observations.RawVorbisBackend){throw 'Patched consumer used the routing guard.'}
            if($fixedDependency -and $mode -eq 'inspect'){
                if($data.Observations.NativeSha256 -ne $nativeBuild.Patched.Sha256){throw 'Consumer loaded an unexpected native DLL.'}
                foreach($property in $originalAbi.Observations.AllNativeObjectSizes.PSObject.Properties){
                    if($data.Observations.AllNativeObjectSizes.($property.Name) -ne $property.Value){throw "Native object ABI size changed: $($property.Name)"}
                }
                if($data.Observations.ManagedSha256 -ne $originalAbi.Observations.ManagedSha256){throw 'Managed binding changed.'}
            }
            $summary.Reports+=@{Name=$name;Path=$report;Checks=$data.CheckCount;Sha256=(Get-FileHash -LiteralPath $report).Hash}
        }
        if($fixedDependency){
            foreach($mode in @('raw-vorbis-defect','malformed-vorbis-defect','failure-stress')){
                $name="$mode-$configuration";$report=Join-Path $evidence "$name.json"
                Invoke-AudioProbe $name @((Join-Path $directory 'MiniAudioExResearch.dll'),$mode,$fixtures,$report,'raw-vorbis')
                $data=Get-Content -Raw -LiteralPath $report|ConvertFrom-Json
                if(!$data.Completed -or $data.CleanupFailures.Count -ne 0 -or $data.LiveObjects -ne 0 -or !$data.Observations.RawVorbisBackend){throw "Patched failure-path regression: $name"}
                $summary.Reports+=@{Name=$name;Path=$report;Checks=$data.CheckCount;Sha256=(Get-FileHash -LiteralPath $report).Hash}
            }
            continue
        }
        # These child processes intentionally fail. They preserve the vendor defects rather than hiding them in a passing profile.
        foreach ($mode in @('raw-vorbis-defect','malformed-vorbis-defect')) {
            $name="$mode-$configuration"; $report=Join-Path $evidence "$name.json"
            Invoke-AudioProbe $name @((Join-Path $directory 'MiniAudioExResearch.dll'),$mode,$fixtures,$report) 120 1
            $data=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
            $expectedBlocks=$(if($mode -eq 'raw-vorbis-defect'){1}else{2})
            $expectedBytes=840*$expectedBlocks
            if ($data.Completed -or $data.LiveObjects -ne 0 -or $data.CleanupFailures.Count -ne 1 -or
                $data.CleanupFailures[0].Bytes -ne $expectedBytes -or $data.CleanupFailures[0].Blocks -ne $expectedBlocks) {
                throw "Known defect did not reproduce with its recorded allocation signature: $name"
            }
            $summary.KnownDefects+=@{Name=$name;Path=$report;Bytes=$expectedBytes;Blocks=$expectedBlocks;Sha256=(Get-FileHash -LiteralPath $report).Hash}
            Write-Host "KNOWN VENDOR DEFECT: $name left $expectedBytes tracked native bytes. Production eligibility remains false."
        }
    }
    $summary.Fixtures=@(Get-ChildItem -LiteralPath $fixtures -File | ForEach-Object {
        @{Name=$_.Name;Bytes=$_.Length;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
    })
    foreach ($source in $summary.Sources) {
        if ((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $source.Name)).Hash -ne $source.Sha256) { throw 'Probe source changed during verification.' }
    }
    $summary.Completed=$true
    $summary.DependencyProfileVerified=$fixedDependency
} catch { $summary.Failure=$_.ToString(); throw }
finally { $summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $evidence 'verification.json') -Encoding utf8 }
