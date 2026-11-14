[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory,[switch]$Device)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if($PSVersionTable.PSVersion.Major -lt 7){throw 'PowerShell 7 required.'}
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory,$workspace)
if(Test-Path -LiteralPath $evidence){throw 'Choose a fresh evidence directory.'}
[void][IO.Directory]::CreateDirectory($evidence)
. (Join-Path $workspace 'ManualTests/BuildProfile.ps1')
$profile=Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory (Join-Path $evidence 'profile')
$summary=[ordered]@{Completed=$false;Utc=[DateTimeOffset]::UtcNow;Profile=$profile;DeviceRequested=[bool]$Device;OutputMuted=$true;Commands=@();Reports=@();Packages=@();Failure=$null;Sources=@()}
$roots=@('TrueMoon.Kul','TrueMoon.Kul.Tests','ManualTests/KulModuleProbe')
$summary.Sources=@(foreach($root in $roots){Get-ChildItem -LiteralPath (Join-Path $workspace $root) -File -Recurse|
    Where-Object {$_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in '.cs','.csproj','.md','.ps1'}|
    ForEach-Object{@{Path=$_.FullName.Substring($workspace.Length+1);Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}}})
function Invoke-ProbeCommand([string]$name,[string[]]$arguments,[string]$working=$workspace){
    if($arguments[0] -in 'restore','build','publish','pack'){$arguments+="-bl:$evidence/$name-{}.binlog"}
    $info=[Diagnostics.ProcessStartInfo]::new('dotnet');$info.WorkingDirectory=$working;$info.UseShellExecute=$false;$info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($argument in $arguments){[void]$info.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::new();$process.StartInfo=$info;$started=$false
    try{
        Write-Host "Starting $name";$started=$process.Start();if(!$started){throw 'Process startup failed.'}
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync();$timer=[Diagnostics.Stopwatch]::StartNew()
        while(!$process.WaitForExit(1000)){
            if($timer.Elapsed.TotalSeconds -gt 180){$process.Kill($true);$process.WaitForExit();throw "Timeout/reaped: $name"}
            if([int]$timer.Elapsed.TotalSeconds%10 -eq 0){Write-Host "$name running: $([int]$timer.Elapsed.TotalSeconds)s"}
        }
        $out=$stdout.GetAwaiter().GetResult();$err=$stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path $evidence "$name.stdout.log"),$out);[IO.File]::WriteAllText((Join-Path $evidence "$name.stderr.log"),$err)
        $summary.Commands+=@{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode}
        if($process.ExitCode -ne 0){Write-Host $out;Write-Host $err;throw "$name failed: $($process.ExitCode)"}
        Write-Host "Completed $name"
    }finally{if($started -and !$process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
function Run-Profiles([string]$name,[string]$executable){
    $modes=@('Offline');if($Device){$modes+='Device'}
    foreach($mode in $modes){
        $label="$name-$mode";$report=Join-Path $evidence "$label.json"
        Invoke-ProbeCommand $label @($executable,(Join-Path $evidence 'fixtures'),$report,"--Audio:Mode=$mode",'--Audio:MuteOutput=true')
        $data=Get-Content -Raw -LiteralPath $report|ConvertFrom-Json
        if(!$data.Completed -or $data.Observations.AfterStop.Voices -ne 0 -or $data.Observations.AfterStop.Sessions -ne 0){throw 'Consumer cleanup incomplete.'}
        if($data.Observations.Native.Sha256 -ne $expectedNative){throw 'Consumer did not load the accepted patched native DLL.'}
        $summary.Reports+=@{Name=$label;Path=$report;Sha256=(Get-FileHash -LiteralPath $report).Hash;Checks=$data.Checks.Count;Observations=$data.Observations}
    }
}
try{
    $feed=Join-Path $evidence 'feed';[void][IO.Directory]::CreateDirectory($feed)
    $pins=Get-Content -Raw -LiteralPath (Join-Path $workspace 'eng/native/MiniAudioEx/Source.lock.json')|ConvertFrom-Json
    $nativePackage=Join-Path $workspace "eng/native/MiniAudioEx/feed/$($pins.packageId).$($pins.packageVersion).nupkg"
    if(!(Test-Path -LiteralPath $nativePackage)){throw 'Build/install the patched local feed first; see docs/audio/README.md.'}
    Copy-Item -LiteralPath $nativePackage -Destination $feed
    $zip=[IO.Compression.ZipFile]::OpenRead($nativePackage)
    try{
        $stream=$zip.GetEntry('native-build.json').Open();$reader=[IO.StreamReader]::new($stream)
        try{$nativeProvenance=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()}
        if($nativeProvenance.PatchSha256 -ne $pins.patchSha256){throw 'Native fork provenance mismatch.'}
        $expectedNative=$nativeProvenance.NativeSha256
    }finally{$zip.Dispose()}
    $summary.NativeDependency=@{Sha256=(Get-FileHash -LiteralPath $nativePackage).Hash;NativeSha256=$expectedNative;Pins=$pins}
    $project=Join-Path $PSScriptRoot 'KulModuleProbe.csproj'
    $debug=Join-Path $evidence 'source-Debug'
    Invoke-ProbeCommand 'build-Debug' @('build',$project,'-c','Debug','-o',$debug)
    Run-Profiles 'source-Debug' (Join-Path $debug 'KulModuleProbe.dll')
    $published=Join-Path $evidence 'published-Release'
    Invoke-ProbeCommand 'publish-Release' @('publish',$project,'-c','Release','--self-contained','false','-o',$published)
    Run-Profiles 'published-Release' (Join-Path $published 'KulModuleProbe.dll')

    foreach($id in @('TrueMoon.Contracts','TrueMoon.Cobalt','TrueMoon','TrueMoon.Kul')){
        Invoke-ProbeCommand "pack-$id" @('pack',(Join-Path $workspace "$id/$id.csproj"),'-c','Release','-o',$feed)
    }
    $summary.Packages=@(Get-ChildItem -LiteralPath $feed -Filter '*.nupkg'|ForEach-Object{@{Name=$_.Name;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash;Bytes=$_.Length}})
    $consumer=Join-Path $evidence 'package-consumer'
    Write-TrueMoonConsumerBuildProfile -Profile $profile -Directory $consumer
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Program.cs'),(Join-Path $PSScriptRoot 'ProbeFixtures.cs') -Destination $consumer
    $assets=Join-Path $consumer 'Assets';[void][IO.Directory]::CreateDirectory($assets)
    Copy-Item -LiteralPath (Join-Path $workspace 'ManualTests/AudioFixtures/music.ogg') -Destination $assets
    $packageProject=@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><AssemblyName>KulModuleProbe</AssemblyName><GenerateDocumentationFile>false</GenerateDocumentationFile></PropertyGroup>
  <ItemGroup><PackageReference Include="TrueMoon" Version="$($profile.Version)" /><PackageReference Include="TrueMoon.Kul" Version="$($profile.Version)" />
  <None Update="Assets/music.ogg" CopyToOutputDirectory="PreserveNewest" /></ItemGroup>
</Project>
"@
    $consumerProject=Join-Path $consumer 'Consumer.csproj';[IO.File]::WriteAllText($consumerProject,$packageProject)
    $feedXml=[Security.SecurityElement]::Escape($feed)
    $config=Join-Path $consumer 'NuGet.Config';[IO.File]::WriteAllText($config,"<configuration><packageSources><clear/><add key='private-audio' value='$feedXml'/></packageSources><packageSourceMapping><clear/><packageSource key='private-audio'><package pattern='*'/></packageSource></packageSourceMapping><fallbackPackageFolders><clear/></fallbackPackageFolders></configuration>")
    Invoke-ProbeCommand 'restore-package-consumer' @('restore',$consumerProject,'--configfile',$config,'--packages',(Join-Path $evidence 'private-cache')) $consumer
    $output=Join-Path $evidence 'package-published-Release'
    Invoke-ProbeCommand 'publish-package-consumer' @('publish',$consumerProject,'--no-restore','-c','Release','--self-contained','false','-o',$output) $consumer
    Run-Profiles 'package-Release' (Join-Path $output 'KulModuleProbe.dll')
    $modulePackage=Join-Path $feed "TrueMoon.Kul.$($profile.Version).nupkg"
    $zip=[IO.Compression.ZipFile]::OpenRead($modulePackage)
    try{
        $stream=$zip.GetEntry("lib/$($profile.Framework)/TrueMoon.Kul.dll").Open()
        try{$moduleHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}
    }finally{$zip.Dispose()}
    if($moduleHash -ne (Get-FileHash -LiteralPath (Join-Path $output 'TrueMoon.Kul.dll')).Hash){throw 'Package consumer module DLL differs from nupkg asset.'}
    $summary.PackageModuleSha256=$moduleHash
    foreach($source in $summary.Sources){if((Get-FileHash -LiteralPath (Join-Path $workspace $source.Path)).Hash -ne $source.Sha256){throw 'Source changed during verification.'}}
    $summary.Fixtures=@(Get-ChildItem -LiteralPath (Join-Path $evidence 'fixtures') -File|ForEach-Object{@{Name=$_.Name;Bytes=$_.Length;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
    $summary.Completed=$true
}catch{$summary.Failure=$_.ToString();throw}
finally{$summary|ConvertTo-Json -Depth 16|Set-Content -LiteralPath (Join-Path $evidence 'verification.json') -Encoding utf8}
