[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory,[string]$ArchiveDirectory,[switch]$Captured)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if(![OperatingSystem]::IsWindows() -or [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne 'X64'){throw 'This research profile is Windows x64 only.'}
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath((Join-Path $workspace $EvidenceDirectory))
if(Test-Path -LiteralPath $evidence){throw 'Use a fresh evidence directory.'}
[void][IO.Directory]::CreateDirectory($evidence)
if(!$ArchiveDirectory){$ArchiveDirectory=Join-Path $workspace 'TestResults/AssimpResearch-20261107'}
$archives=@(
    @{Id='Silk.NET.Assimp';Version='2.23.0';Sha256='3542E71B820DC7F57BF8B19CD240B19CF093E319123DA9E8F578D42108B18758'},
    @{Id='Ultz.Native.Assimp';Version='6.0.2';Sha256='B7A8DA0D48EA87BC6E362E3C707ABE09B143718787676FD6007EBF6FEDC1AF2A'},
    @{Id='AssimpNetter';Version='6.0.5';Sha256='805526348F7D868BC2BA1B56538C5AB1409E5E99E9BB653AF895DC3E5BD2919E'},
    @{Id='SharpGLTF.Core';Version='1.0.7';Sha256='2C1EA4AA2A4298A5847714B91E94CCC47FB3600CA5B27DD04FFE664E72B3A819'}
)
$run=Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-ModelImport-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($run)
$feed=Join-Path $run 'feed';[void][IO.Directory]::CreateDirectory($feed)
foreach($p in $archives){
    $id=$p.Id.ToLowerInvariant();$name="$id.$($p.Version).nupkg";$file=Join-Path $ArchiveDirectory $name
    if(!(Test-Path -LiteralPath $file)){$file=Join-Path $feed $name;Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/$id/$($p.Version)/$name" -OutFile $file}
    if((Get-FileHash $file).Hash -ne $p.Sha256){throw 'Archive hash differs'}
    if([IO.Path]::GetFullPath($file) -ne [IO.Path]::GetFullPath((Join-Path $feed $name))){Copy-Item -LiteralPath $file -Destination $feed}
}
$fallback=Join-Path $env:USERPROFILE '.nuget/packages'
$config=Join-Path $run 'NuGet.Config'
$feedXml=[Security.SecurityElement]::Escape($feed);$fallbackXml=[Security.SecurityElement]::Escape($fallback)
"<configuration><packageSources><clear/><add key='research-feed' value='$feedXml'/></packageSources><fallbackPackageFolders><clear/><add key='cached-third-party' value='$fallbackXml'/></fallbackPackageFolders></configuration>"|Set-Content $config -Encoding utf8
$summary=[ordered]@{Completed=$false;RunDirectory=$run;ArchiveInputs=@($archives|ForEach-Object{[pscustomobject]$_});Sources=@(Get-ChildItem $PSScriptRoot -File|Where-Object Extension -in '.cs','.ps1'|ForEach-Object{[pscustomobject]@{Path=$_.FullName;Sha256=(Get-FileHash $_.FullName).Hash}});Commands=@();Profiles=@();Failure=$null}
$nativeHashes=@{}
foreach($entry in @(@{Package='assimpnetter.6.0.5.nupkg';File='runtimes/win-x64/native/assimp.dll';Names=@('Netter','NetterLow')},@{Package='ultz.native.assimp.6.0.2.nupkg';File='runtimes/win-x64/native/Assimp64.dll';Names=@('Silk')})){
    $zip=[IO.Compression.ZipFile]::OpenRead((Join-Path $feed $entry.Package))
    try{$stream=$zip.GetEntry($entry.File).Open();try{$hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()};foreach($name in $entry.Names){$nativeHashes[$name]=$hash}}finally{$zip.Dispose()}
}
$summary.NativeHashes=$nativeHashes
function Invoke-Probe([string]$name,[string[]]$arguments,[int]$timeout=60){
    if($arguments[0] -in @('restore','build','publish')){$arguments+="-bl:$evidence/$name-{}.binlog"}
    $info=[Diagnostics.ProcessStartInfo]::new('dotnet');$info.WorkingDirectory=$run;$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($a in $arguments){$info.ArgumentList.Add($a)}
    $process=[Diagnostics.Process]::new();$process.StartInfo=$info;$started=$false
    try{
        if(!$process.Start()){throw "Start failed $name"};$started=$true;$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if(!$process.WaitForExit($timeout*1000)){$process.Kill($true);if(!$process.WaitForExit(10000)){throw "Failed to reap $name"};throw "Timed out $name"}
        [IO.File]::WriteAllText((Join-Path $evidence "$name.stdout.log"),$stdout.GetAwaiter().GetResult());[IO.File]::WriteAllText((Join-Path $evidence "$name.stderr.log"),$stderr.GetAwaiter().GetResult())
        $summary.Commands+=@{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode}
        if($process.ExitCode -ne 0){throw "Failed $name exit $($process.ExitCode); see $evidence"}
        Write-Host "$name passed"
    }finally{if($started -and !$process.HasExited){$process.Kill($true);if(!$process.WaitForExit(10000)){throw "Cleanup did not reap $name"}};$process.Dispose()}
}
try{
    $fixtures=Join-Path $run 'fixtures'
    foreach($candidate in @(@{Name='Netter';Define='NETTER';Id='AssimpNetter';Version='6.0.5'},@{Name='NetterLow';Define='NETTERLOW';Id='AssimpNetter';Version='6.0.5'},@{Name='Silk';Define='SILK';Id='Silk.NET.Assimp';Version='2.23.0'},@{Name='Gltf';Define='GLTF';Id='SharpGLTF.Core';Version='1.0.7'}) | Where-Object { !$Captured -or $_.Name -eq 'NetterLow' }){
        $dir=Join-Path $run $candidate.Name;[void][IO.Directory]::CreateDirectory($dir)
        Copy-Item "$PSScriptRoot/*.cs" -Destination $dir
        '<Project/>'|Set-Content (Join-Path $dir 'Directory.Build.props');'<Project/>'|Set-Content (Join-Path $dir 'Directory.Build.targets')
        $reference=[Security.SecurityElement]::Escape((Join-Path $workspace 'TrueMoon.Aether/TrueMoon.Aether.csproj'))
        $project=Join-Path $dir 'ModelImportResearch.csproj'
        @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><AllowUnsafeBlocks>true</AllowUnsafeBlocks><DefineConstants>$($candidate.Define)</DefineConstants></PropertyGroup><ItemGroup><PackageReference Include="$($candidate.Id)" Version="$($candidate.Version)"/><ProjectReference Include="$reference"/></ItemGroup></Project>
"@|Set-Content $project -Encoding utf8
        Invoke-Probe "$($candidate.Name)-restore" @('restore',$project,'--configfile',$config,'--packages',(Join-Path $dir 'packages'))
        Invoke-Probe "$($candidate.Name)-Debug" @('build',$project,'--no-restore','-c','Debug')
        $assembly=Join-Path $dir 'bin/Debug/net10.0/ModelImportResearch.dll'
        if(!(Test-Path $fixtures)){Invoke-Probe 'fixtures' @($assembly,'fixtures',$fixtures)}
        $report=Join-Path $evidence "$($candidate.Name)-Debug.json"
        $probeArguments=@($fixtures,$report);if($Captured){$probeArguments=@('captured')+$probeArguments}
        Invoke-Probe "$($candidate.Name)-source" (@($assembly)+$probeArguments) 60
        $result=Get-Content $report -Raw|ConvertFrom-Json
        if(!$result.Completed -or $result.LiveImports -ne 0){throw 'Source proof incomplete'}
        if($candidate.Name -ne 'Gltf' -and $result.Native.Sha256 -ne $nativeHashes[$candidate.Name]){throw 'Source native differs from archive'}
        $summary.Profiles+=@{Name="$($candidate.Name)-Debug";Report=$report}
        $publish=Join-Path $dir 'publish'
        Invoke-Probe "$($candidate.Name)-publish" @('publish',$project,'--no-restore','-c','Release','--self-contained','false','-o',$publish)
        $report=Join-Path $evidence "$($candidate.Name)-Release.json"
        $probeArguments=@($fixtures,$report);if($Captured){$probeArguments=@('captured')+$probeArguments}
        Invoke-Probe "$($candidate.Name)-published" (@((Join-Path $publish 'ModelImportResearch.dll'))+$probeArguments) 60
        $result=Get-Content $report -Raw|ConvertFrom-Json
        if(!$result.Completed -or $result.LiveImports -ne 0){throw 'Published proof incomplete'}
        if($candidate.Name -ne 'Gltf' -and $result.Native.Sha256 -ne $nativeHashes[$candidate.Name]){throw 'Published native differs from archive'}
        $summary.Profiles+=@{Name="$($candidate.Name)-Release";Report=$report}
    }
    $summary.Fixtures=@(Get-ChildItem $fixtures -File|ForEach-Object{[pscustomobject]@{Name=$_.Name;Bytes=$_.Length;Sha256=(Get-FileHash $_.FullName).Hash}})
    foreach($source in $summary.Sources){if((Get-FileHash $source.Path).Hash -ne $source.Sha256){throw 'Source changed during probe'}}
    $summary.Completed=$true
}catch{$summary.Failure=$_.ToString();throw}
finally{$summary|ConvertTo-Json -Depth 8|Set-Content (Join-Path $evidence 'verification.json') -Encoding utf8}
