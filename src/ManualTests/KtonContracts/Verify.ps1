[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if($PSVersionTable.PSVersion.Major -lt 7){throw 'PowerShell 7 is required.'}
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath((Join-Path $workspace $EvidenceDirectory))
if(Test-Path -LiteralPath $evidence){throw 'Use a fresh evidence directory.'}
[void][IO.Directory]::CreateDirectory($evidence)
$summary=[ordered]@{Completed=$false;Utc=[DateTimeOffset]::UtcNow;Profile=$null;Sources=@();Commands=@();Reports=@();Failure=$null;Package=$null;Scope='PHYS1 neutral API/validation and fresh package closure; provider/App/ECS unimplemented.'}
$sourceProject=Join-Path $workspace 'TrueMoon.Kton/TrueMoon.Kton.csproj'
$artifacts=Join-Path $evidence 'source-artifacts'
$feed=Join-Path $evidence 'feed';[void][IO.Directory]::CreateDirectory($feed)
$cache=Join-Path $evidence 'packages'
$config=Join-Path $evidence 'NuGet.Config'
[IO.File]::WriteAllText($config,"<configuration><packageSources><clear/><add key='kton' value='$([Security.SecurityElement]::Escape($feed))'/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders></configuration>")
function Invoke-Checked([string]$name,[string[]]$arguments){
    if($arguments[0] -in 'restore','build','pack','publish'){$arguments+="-bl:$evidence/$name-{}.binlog"}
    $info=[Diagnostics.ProcessStartInfo]::new('dotnet');$info.WorkingDirectory=$workspace;$info.UseShellExecute=$false;$info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($arg in $arguments){[void]$info.ArgumentList.Add($arg)}
    $process=[Diagnostics.Process]::new();$process.StartInfo=$info;$started=$false
    Write-Host "Starting $name"
    try{
        $started=$process.Start();if(!$started){throw "Could not start $name"}
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if(!$process.WaitForExit(120000)){$process.Kill($true);$process.WaitForExit();throw "Timeout, reaped: $name"}
        $out=$stdout.GetAwaiter().GetResult();$err=$stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path $evidence "$name.stdout.log"),$out);[IO.File]::WriteAllText((Join-Path $evidence "$name.stderr.log"),$err)
        $summary.Commands+=@{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode}
        if($process.ExitCode -ne 0){Write-Host $out;Write-Host $err;throw "$name failed: $($process.ExitCode)"}
        if($arguments[0] -in 'restore','build','pack','publish'){
            if(!(Get-ChildItem -LiteralPath $evidence -Filter "$name-*.binlog")){throw "Missing binlog: $name"}
        }
        Write-Host "Completed $name"
    }finally{if($started -and !$process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
function New-Consumer([string]$name,[string]$reference){
    $directory=Join-Path $evidence $name
    Write-TrueMoonConsumerBuildProfile -Profile $summary.Profile -Directory $directory
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Program.cs') -Destination $directory
    $project=[xml](Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Probe.csproj') -Raw)
    $group=$project.CreateElement('ItemGroup');$element=$project.CreateElement($reference)
    if($reference -eq 'ProjectReference'){$element.SetAttribute('Include',$sourceProject)}
    else{$element.SetAttribute('Include','TrueMoon.Kton');$element.SetAttribute('Version',"[$packageVersion]")}
    [void]$group.AppendChild($element);[void]$project.DocumentElement.AppendChild($group)
    $path=Join-Path $directory 'Probe.csproj';$project.Save($path);return $path
}
function Run-Probe([string]$name,[string]$dll){
    $path=Join-Path $evidence "$name.json";Invoke-Checked $name @($dll,$path)
    $report=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
    if(!$report.Completed -or $report.ProviderOperationsExecuted){throw "Unexpected report: $name"}
    if(@($report.Assembly.References|Where-Object{$_ -notmatch '^System(\.|$)'}).Count){throw 'Unexpected assembly reference'}
    $summary.Reports+=@{Name=$name;Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash;Checks=$report.CheckCount;ActualDllSha256=$report.Assembly.Sha256}
}
try{
    . (Join-Path $PSScriptRoot '../BuildProfile.ps1')
    $summary.Profile=Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory (Join-Path $evidence 'profile')
    $packageVersion=$summary.Profile.Version+'.phys1.'+[DateTime]::UtcNow.ToString('yyyyMMddHHmmss')
    foreach($directory in @($PSScriptRoot,(Join-Path $workspace 'TrueMoon.Kton'))){
        $summary.Sources+=@(Get-ChildItem -LiteralPath $directory -Recurse -File|Where-Object{$_.FullName -notmatch '[\\/](obj|bin)[\\/]'}|ForEach-Object{
            @{Path=$_.FullName.Substring($workspace.Length+1).Replace('\','/');Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
        })
    }
    $source=New-Consumer 'source-consumer' 'ProjectReference'
    Invoke-Checked 'source-restore' @('restore',$source,'--configfile',$config,'--packages',$cache,'--artifacts-path',$artifacts,'-p:NuGetAudit=false')
    $sourceOutput=Join-Path $evidence 'source-Debug'
    Invoke-Checked 'source-build-Debug' @('build',$source,'--no-restore','-c','Debug','--artifacts-path',$artifacts,'-o',$sourceOutput)
    Run-Probe 'source-Debug' (Join-Path $sourceOutput 'KtonContracts.dll')
    Invoke-Checked 'pack-Release' @('pack',$sourceProject,'--no-restore','-c','Release','--artifacts-path',$artifacts,'-o',$feed,"-p:PackageVersion=$packageVersion")
    $archives=@(Get-ChildItem -LiteralPath $feed -Filter '*.nupkg');if($archives.Count -ne 1){throw 'Expected one package'}
    $zip=[IO.Compression.ZipFile]::OpenRead($archives[0].FullName)
    try{
        $nuspec=@($zip.Entries|Where-Object FullName -like '*.nuspec');$reader=[IO.StreamReader]::new($nuspec[0].Open())
        try{$metadata=[xml]$reader.ReadToEnd()}finally{$reader.Dispose()}
        if($metadata.SelectNodes('//*[local-name()="dependency"]').Count){throw 'Contracts package has dependencies'}
        $dll=@($zip.Entries|Where-Object FullName -like 'lib/*/TrueMoon.Kton.dll');if($dll.Count -ne 1 -or $dll[0].FullName -ne "lib/$($summary.Profile.Framework)/TrueMoon.Kton.dll"){throw 'Unexpected package DLL layout'}
        $stream=$dll[0].Open();try{$dllSha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}
        $summary.Package=@{Id='TrueMoon.Kton';Version=$packageVersion;Sha256=(Get-FileHash -LiteralPath $archives[0].FullName).Hash;DllSha256=$dllSha;Dependencies=@();Path=$archives[0].FullName}
    }finally{$zip.Dispose()}
    $fresh=New-Consumer 'package-consumer' 'PackageReference'
    $freshArtifacts=Join-Path $evidence 'package-artifacts'
    Invoke-Checked 'package-restore' @('restore',$fresh,'--configfile',$config,'--packages',$cache,'--artifacts-path',$freshArtifacts)
    $assets=@(Get-ChildItem -LiteralPath (Join-Path $freshArtifacts 'obj') -Recurse -Filter project.assets.json)
    if($assets.Count -ne 1){throw 'Unexpected consumer graph count'}
    $graph=Get-Content -LiteralPath $assets[0].FullName -Raw|ConvertFrom-Json -AsHashtable
    if(($graph.libraries.Keys -join ',') -ne "TrueMoon.Kton/$packageVersion"){throw 'Fresh consumer graph is not one Kton-only package'}
    foreach($configuration in @('Debug','Release')){
        $output=Join-Path $evidence "package-$configuration"
        if($configuration -eq 'Debug'){Invoke-Checked 'package-build-Debug' @('build',$fresh,'--no-restore','-c',$configuration,'--artifacts-path',$freshArtifacts,'-o',$output)}
        else{Invoke-Checked 'package-publish-Release' @('publish',$fresh,'--no-restore','-c',$configuration,'--self-contained','false','--artifacts-path',$freshArtifacts,'-o',$output)}
        Run-Probe "package-$configuration" (Join-Path $output 'KtonContracts.dll')
        if($summary.Reports[-1].ActualDllSha256 -ne $summary.Package.DllSha256){throw 'Loaded DLL differs from fresh package'}
    }
    foreach($source in $summary.Sources){if((Get-FileHash -LiteralPath (Join-Path $workspace $source.Path)).Hash -ne $source.Sha256){throw "Changed source: $($source.Path)"}}
    $summary.Completed=$true
}catch{$summary.Failure=$_.ToString();throw}
finally{$summary|ConvertTo-Json -Depth 12|Set-Content -LiteralPath (Join-Path $evidence 'verification.json') -Encoding utf8}
