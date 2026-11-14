[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$SourceArchive,
    [string]$ManagedArchive,
    [string]$VisualStudioPath
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if ($PSVersionTable.PSVersion.Major -lt 7 -or ![OperatingSystem]::IsWindows() -or
    [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne 'X64') { throw 'Requires PowerShell 7 / Windows x64.' }
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw 'Use a fresh output directory.'}
[void][IO.Directory]::CreateDirectory($output)
$pins=Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'Source.lock.json')|ConvertFrom-Json
$patch=Join-Path $PSScriptRoot 'libvorbis-lifetime.patch'
if((Get-FileHash -LiteralPath $patch).Hash -ne $pins.patchSha256){throw 'Pinned patch SHA differs.'}
$receipt=[ordered]@{Completed=$false;RuntimeIdentifier=$pins.runtimeIdentifier;PackageId=$pins.packageId;PackageVersion=$pins.packageVersion;Pins=$pins;Commands=@();Sources=@();Failure=$null}
$receipt.Sources=@(Get-ChildItem -LiteralPath $PSScriptRoot -File | ForEach-Object{@{Name=$_.Name;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})

function Invoke-NativeBuildCommand([string]$name,[string]$executable,[string[]]$arguments,[string]$directory,[int]$timeout=180){
    $info=[Diagnostics.ProcessStartInfo]::new($executable)
    $info.WorkingDirectory=$directory;$info.UseShellExecute=$false;$info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($argument in $arguments){[void]$info.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::new();$process.StartInfo=$info;$started=$false
    try{
        Write-Host "Starting $name"
        $started=$process.Start();if(!$started){throw "Failed to start $name"}
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        $timer=[Diagnostics.Stopwatch]::StartNew()
        while(!$process.WaitForExit(1000)){
            if($timer.Elapsed.TotalSeconds -ge $timeout){$process.Kill($true);$process.WaitForExit();throw "Timeout/reaped: $name"}
            if([int]$timer.Elapsed.TotalSeconds % 10 -eq 0){Write-Host "$name running: $([int]$timer.Elapsed.TotalSeconds)s"}
        }
        $out=$stdout.GetAwaiter().GetResult();$err=$stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path $output "$name.stdout.log"),$out)
        [IO.File]::WriteAllText((Join-Path $output "$name.stderr.log"),$err)
        $receipt.Commands+=@{Name=$name;Executable=$executable;Arguments=$arguments;ExitCode=$process.ExitCode}
        if($process.ExitCode -ne 0){Write-Host $out;Write-Host $err;throw "$name exited $($process.ExitCode)"}
        Write-Host "Completed $name"
    }finally{
        if($started -and !$process.HasExited){$process.Kill($true);$process.WaitForExit()}
        $process.Dispose()
    }
}
function Get-PinnedArchive([string]$supplied,[string]$name,[string]$url,[string]$sha){
    $destination=Join-Path $output $name
    if($supplied){Copy-Item -LiteralPath $supplied -Destination $destination}else{Invoke-WebRequest $url -OutFile $destination}
    if((Get-FileHash -LiteralPath $destination).Hash -ne $sha){throw "Archive SHA mismatch: $name"}
    return $destination
}
try{
    if(!$VisualStudioPath){
        $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
        $VisualStudioPath=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
        if($LASTEXITCODE -ne 0){throw 'vswhere failed.'}
    }
    $vcvars=Join-Path $VisualStudioPath 'VC/Auxiliary/Build/vcvarsall.bat'
    if(!(Test-Path -LiteralPath $vcvars)){throw 'MSVC x64 Build Tools required.'}
    $toolset=Get-ChildItem -LiteralPath (Join-Path $VisualStudioPath 'VC/Tools/MSVC') -Directory|Sort-Object{[version]$_.Name} -Descending|Select-Object -First 1
    $dumpbin=Join-Path $toolset.FullName 'bin/Hostx64/x64/dumpbin.exe'
    $receipt.Toolchain=@{VisualStudio=$VisualStudioPath;Msvc=$toolset.Name;CompilerSha256=(Get-FileHash -LiteralPath (Join-Path $toolset.FullName 'bin/Hostx64/x64/cl.exe')).Hash;RuntimeLibrary='/MD';Optimization='/O2';LinkRepro=$true}
    $sourceZip=Get-PinnedArchive $SourceArchive 'upstream-source.zip' $pins.archiveUrl $pins.archiveSha256
    $id=$pins.managedPackageId.ToLowerInvariant();$version=$pins.managedPackageVersion
    $managedZip=Get-PinnedArchive $ManagedArchive "$id.$version.nupkg" "https://api.nuget.org/v3-flatcontainer/$id/$version/$id.$version.nupkg" $pins.managedPackageSha256
    $work=Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-MiniAudioEx-'+[Guid]::NewGuid().ToString('N'))
    [void][IO.Directory]::CreateDirectory($work);$receipt.SourceWorkDirectory=$work
    $trees=@{}
    foreach($kind in @('baseline','patched')){
        $directory=Join-Path $work $kind
        [IO.Compression.ZipFile]::ExtractToDirectory($sourceZip,$directory)
        $source=Join-Path $directory ('miniaudioex-'+$pins.revision)
        $trees[$kind]=$source
        if((Get-FileHash -LiteralPath (Join-Path $source $pins.patchedFile)).Hash -ne $pins.originalFileSha256){throw 'Pinned native source differs.'}
    }
    # Temporary source trees are outside any checkout; git apply changes files without touching repository indexes.
    Invoke-NativeBuildCommand 'patch-check' 'git' @('-c','core.autocrlf=false','apply','--check',$patch) $trees.patched
    Invoke-NativeBuildCommand 'patch-apply' 'git' @('-c','core.autocrlf=false','apply',$patch) $trees.patched
    if((Get-FileHash -LiteralPath (Join-Path $trees.patched $pins.patchedFile)).Hash -ne $pins.patchedFileSha256){throw 'Patched source SHA differs.'}
    foreach($kind in @('baseline','patched')){
        $source=$trees[$kind];$build=Join-Path $output $kind;[void][IO.Directory]::CreateDirectory($build)
        $batch=Join-Path $build 'build-native.cmd'
        $lines=@(
            '@echo off',
            "call `"$vcvars`" x64 > `"$build/vcvars.log`" 2>&1",
            'if errorlevel 1 exit /b %errorlevel%',
            "cl /nologo /O2 /MD /LD /DNDEBUG /D_CRT_SECURE_NO_WARNINGS /I`"$source/include`" /I`"$source/include/miniaudioex`" /I`"$source/include/miniaudioex/extras`" `"$source/src/miniaudioex/miniaudio.c`" `"$source/src/miniaudioex/miniaudioex.c`" `"$source/src/miniaudioex/extras/miniaudio_libvorbis.c`" /link /Brepro /OUT:`"$build/miniaudioex.dll`" /IMPLIB:`"$build/miniaudioex.lib`"",
            'if errorlevel 1 exit /b %errorlevel%',
            "cl /nologo /W4 /WX /O2 /MD /D_CRT_SECURE_NO_WARNINGS /I`"$source/include/miniaudioex`" /I`"$source/include/miniaudioex/extras`" `"$PSScriptRoot/Regression.c`" `"$build/miniaudioex.lib`" /link /OUT:`"$build/Regression.exe`"",
            'exit /b %errorlevel%'
        )
        [IO.File]::WriteAllLines($batch,$lines,[Text.Encoding]::Default)
        Invoke-NativeBuildCommand "native-$kind" $env:ComSpec @('/d','/c',$batch) $build
        Invoke-NativeBuildCommand "exports-$kind" $dumpbin @('/exports',(Join-Path $build 'miniaudioex.dll')) $build
        Invoke-NativeBuildCommand "dependents-$kind" $dumpbin @('/dependents',(Join-Path $build 'miniaudioex.dll')) $build
    }
    $receipt.NativeWarnings=@{}
    foreach($kind in @('baseline','patched')){
        $receipt.NativeWarnings[$kind]=@(Get-Content -LiteralPath (Join-Path $output "native-$kind.stdout.log")|
            ForEach-Object{if($_ -match 'warning (C\d+)'){$matches[1]}}|Group-Object|ForEach-Object{@{Code=$_.Name;Count=$_.Count}})
    }
    $managed=Join-Path $work 'managed';[IO.Compression.ZipFile]::ExtractToDirectory($managedZip,$managed)
    $originalDll=Join-Path $managed 'runtimes/win-x64/native/miniaudioex.dll'
    if((Get-FileHash -LiteralPath $originalDll).Hash -ne $pins.originalNativeSha256){throw 'Original DLL SHA differs.'}
    Invoke-NativeBuildCommand 'exports-original' $dumpbin @('/exports',$originalDll) $work
    function Read-ExportNames([string]$name){
        @(Get-Content -LiteralPath (Join-Path $output "$name.stdout.log") | ForEach-Object{
            if($_ -match '^\s+\d+\s+[0-9A-F]+\s+[0-9A-F]+\s+(\S+)'){$matches[1]}
        }|Sort-Object -Unique)
    }
    $originalExports=Read-ExportNames 'exports-original';$baselineExports=Read-ExportNames 'exports-baseline';$patchedExports=Read-ExportNames 'exports-patched'
    $missing=@($originalExports|Where-Object{$_ -notin $patchedExports})
    if($missing.Count -or @(Compare-Object $baselineExports $patchedExports).Count){throw 'Native export compatibility failed.'}
    $receipt.Exports=@{Original=$originalExports.Count;Baseline=$baselineExports.Count;Patched=$patchedExports.Count;Removed=@($missing)}
    $receipt.Baseline=@{Dll=(Join-Path $output 'baseline/miniaudioex.dll');Sha256=(Get-FileHash -LiteralPath (Join-Path $output 'baseline/miniaudioex.dll')).Hash;Regression=(Join-Path $output 'baseline/Regression.exe')}
    $receipt.Patched=@{Dll=(Join-Path $output 'patched/miniaudioex.dll');Sha256=(Get-FileHash -LiteralPath (Join-Path $output 'patched/miniaudioex.dll')).Hash;Regression=(Join-Path $output 'patched/Regression.exe')}
    $receipt.PatchSha256=(Get-FileHash -LiteralPath $patch).Hash

    . (Join-Path $workspace 'ManualTests/BuildProfile.ps1')
    $profile=Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory (Join-Path $output 'profile')
    $pack=Join-Path $output 'package-project';Write-TrueMoonConsumerBuildProfile -Profile $profile -Directory $pack
    $receipt.BuildProfile=$profile
    Copy-Item -LiteralPath (Join-Path $managed 'lib') -Destination (Join-Path $pack 'lib') -Recurse
    $native=Join-Path $pack 'runtimes/win-x64/native';[void][IO.Directory]::CreateDirectory($native)
    Copy-Item -LiteralPath $receipt.Patched.Dll -Destination (Join-Path $native 'miniaudioex.dll')
    $licenses=Join-Path $pack 'licenses';[void][IO.Directory]::CreateDirectory($licenses)
    Copy-Item -LiteralPath (Join-Path $trees.patched 'LICENSE') -Destination (Join-Path $licenses 'miniaudioex.txt')
    $vorbis=[IO.File]::ReadAllText((Join-Path $trees.patched 'include/miniaudioex/extras/minivorbis.h'))
    $licenseStart=$vorbis.LastIndexOf('Copyright (c) 2002-2020 Xiph.org Foundation')
    $licenseEnd=$vorbis.IndexOf('*/',$licenseStart)
    if($licenseStart -lt 0 -or $licenseEnd -lt 0){throw 'Bundled minivorbis license not found.'}
    [IO.File]::WriteAllText((Join-Path $licenses 'minivorbis.txt'),$vorbis.Substring($licenseStart,$licenseEnd-$licenseStart))
    Copy-Item -LiteralPath (Join-Path $workspace '../LICENSE') -Destination (Join-Path $licenses 'truemoon.txt')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PACKAGE.md') -Destination (Join-Path $pack 'README.md')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.md') -Destination $pack
    Copy-Item -LiteralPath $patch -Destination $pack
    $packageProvenance=@{Pins=$pins;PatchSha256=$receipt.PatchSha256;NativeSha256=$receipt.Patched.Sha256;Toolchain=$receipt.Toolchain;RuntimeIdentifier='win-x64';ManagedDlls=@(Get-ChildItem (Join-Path $pack 'lib') -Recurse -File|ForEach-Object{@{Path=$_.FullName.Substring($pack.Length+1);Sha256=(Get-FileHash $_.FullName).Hash}})}
    $packageProvenance|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $pack 'native-build.json') -Encoding utf8
    $project=@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PackageId>$($pins.packageId)</PackageId><Version>$($pins.packageVersion)</Version>
    <IsPackable>true</IsPackable><IncludeBuildOutput>false</IncludeBuildOutput><EnableDefaultItems>false</EnableDefaultItems>
    <Authors>TrueMoon;W.M.R Jap-A-Joe</Authors><Description>Windows x64 MiniAudioEx 4.0.1 with libvorbis failed-initialization cleanup. Managed binding unchanged.</Description>
    <PackageReadmeFile>README.md</PackageReadmeFile><PackageLicenseExpression>MIT AND MIT-0 AND BSD-3-Clause</PackageLicenseExpression>
    <RepositoryUrl>https://github.com/japajoe/miniaudioex</RepositoryUrl><RepositoryCommit>$($pins.revision)</RepositoryCommit>
    <NoPackageAnalysis>true</NoPackageAnalysis><SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>
  </PropertyGroup>
  <ItemGroup>
    <None Include="lib/netstandard2.0/MiniAudioExNET.dll" Pack="true" PackagePath="lib/netstandard2.0/" />
    <None Include="lib/netstandard2.1/MiniAudioExNET.dll" Pack="true" PackagePath="lib/netstandard2.1/" />
    <None Include="runtimes/win-x64/native/miniaudioex.dll" Pack="true" PackagePath="runtimes/win-x64/native/" />
    <None Include="licenses/*" Pack="true" PackagePath="licenses/" />
    <None Include="README.md;THIRD-PARTY-NOTICES.md;libvorbis-lifetime.patch;native-build.json" Pack="true" PackagePath="" />
  </ItemGroup>
</Project>
"@
    $projectPath=Join-Path $pack 'MiniAudioEx.Package.csproj';[IO.File]::WriteAllText($projectPath,$project)
    [IO.File]::WriteAllText((Join-Path $pack 'NuGet.Config'),'<configuration><packageSources><clear/></packageSources></configuration>')
    Invoke-NativeBuildCommand 'package-restore' 'dotnet' @('restore',$projectPath,'--configfile',(Join-Path $pack 'NuGet.Config'),"-bl:$output/package-restore-{}.binlog") $pack
    $feed=Join-Path $output 'feed'
    Invoke-NativeBuildCommand 'package-pack' 'dotnet' @('pack',$projectPath,'--no-restore','-c','Release','-o',$feed,"-bl:$output/package-pack-{}.binlog") $pack
    $nupkg=Join-Path $feed "$($pins.packageId).$($pins.packageVersion).nupkg"
    if(!(Test-Path -LiteralPath $nupkg)){throw 'Patched package missing.'}
    $archive=[IO.Compression.ZipFile]::OpenRead($nupkg)
    try{
        foreach($asset in @('lib/netstandard2.0/MiniAudioExNET.dll','lib/netstandard2.1/MiniAudioExNET.dll','runtimes/win-x64/native/miniaudioex.dll')){
            $entry=$archive.GetEntry($asset);if(!$entry){throw "Missing package asset: $asset"}
            $stream=$entry.Open()
            try{$sha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}
            if($sha -ne (Get-FileHash -LiteralPath (Join-Path $pack $asset)).Hash){throw "Package asset SHA differs: $asset"}
        }
        if(@($archive.Entries|Where-Object FullName -match 'runtimes/(?!win-x64/)').Count){throw 'Package contains unsupported native RIDs.'}
        $receipt.PackageEntries=@($archive.Entries|ForEach-Object FullName)
    }finally{$archive.Dispose()}
    $receipt.Package=@{Path=$nupkg;Sha256=(Get-FileHash -LiteralPath $nupkg).Hash}
    foreach($source in $receipt.Sources){if((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $source.Name)).Hash -ne $source.Sha256){throw 'Native recipe source changed during build.'}}
    $receipt.Completed=$true
}catch{$receipt.Failure=$_.ToString();throw}
finally{$receipt|ConvertTo-Json -Depth 12|Set-Content -LiteralPath (Join-Path $output 'native-build.json') -Encoding utf8}
