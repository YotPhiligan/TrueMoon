[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory, [string]$RunDirectory)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if($PSVersionTable.PSVersion.Major -lt 7){throw 'PowerShell 7 is required.'}
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath((Join-Path $workspace $EvidenceDirectory))
$run=if($RunDirectory){[IO.Path]::GetFullPath($RunDirectory)}else{Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-KtonPHYS4-'+[Guid]::NewGuid().ToString('N'))}
if($run -eq $workspace -or $run.StartsWith($workspace+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Consumer directory must be outside the checkout.'}
if((Test-Path -LiteralPath $evidence) -or (Test-Path -LiteralPath $run)){throw 'Use fresh evidence and consumer directories.'}
foreach($directory in @($evidence,$run)){[void][IO.Directory]::CreateDirectory($directory)}
$family=@('TrueMoon.Contracts','TrueMoon.Cobalt','TrueMoon','TrueMoon.Extensions.DependencyInjection','TrueMoon.Kton','TrueMoon.Kton.Bepu')
$feed=Join-Path $run 'feed';[void][IO.Directory]::CreateDirectory($feed)
$cache=Join-Path $run 'packages';$artifacts=Join-Path $run 'source-artifacts';$config=Join-Path $run 'NuGet.Config'
[IO.File]::WriteAllText($config,"<configuration><packageSources><clear/><add key='local' value='$([Security.SecurityElement]::Escape($feed))'/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders></configuration>")
$summary=[ordered]@{Completed=$false;Utc=[DateTimeOffset]::UtcNow;RunDirectory=$run;Profile=$null;Sources=@();Commands=@();Reports=@();Packages=@();ExternalPackages=@();Graphs=@();GeneratedResolvers=@();Failure=$null;Scope='PHYS4 real BEPU App/configuration/factory/lifetime; Cobalt and Microsoft DI. No ECS/GPU/window/platform matrix.'}
function Invoke-Checked([string]$name,[string[]]$arguments){
    $build=$arguments[0] -in 'restore','build','pack','publish'
    if($build){$arguments+="-bl:$evidence/$name-{}.binlog"}
    $info=[Diagnostics.ProcessStartInfo]::new('dotnet');$info.WorkingDirectory=$run;$info.UseShellExecute=$false;$info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    $info.Environment['NUGET_PACKAGES']=$cache;$info.Environment['NUGET_HTTP_CACHE_PATH']=Join-Path $run 'http-cache'
    foreach($arg in $arguments){[void]$info.ArgumentList.Add($arg)}
    $process=[Diagnostics.Process]::new();$process.StartInfo=$info;$started=$false
    Write-Host "Starting $name"
    try{
        $started=$process.Start();if(!$started){throw "Could not start $name"}
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync();$timer=[Diagnostics.Stopwatch]::StartNew()
        while(!$process.WaitForExit(1000)){
            if($timer.Elapsed.TotalSeconds -ge 180){$process.Kill($true);$process.WaitForExit();throw "Timeout, reaped: $name"}
            if([int]$timer.Elapsed.TotalSeconds % 10 -eq 0){Write-Host "$name running $([int]$timer.Elapsed.TotalSeconds)s"}
        }
        $out=$stdout.GetAwaiter().GetResult();$err=$stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path $evidence "$name.stdout.log"),$out);[IO.File]::WriteAllText((Join-Path $evidence "$name.stderr.log"),$err)
        $summary.Commands+=@{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode;Seconds=$timer.Elapsed.TotalSeconds}
        if($process.ExitCode -ne 0){Write-Host ($out -split '\r?\n'|Select-Object -Last 25|Out-String);Write-Host $err;throw "$name failed: $($process.ExitCode)"}
        if($build -and !(Get-ChildItem -LiteralPath $evidence -Filter "$name-*.binlog")){throw "Missing binlog: $name"}
        Write-Host "Completed $name"
    }finally{if($started -and !$process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
function New-Consumer([string]$name,[bool]$fresh){
    $directory=Join-Path $run $name
    Write-TrueMoonConsumerBuildProfile -Profile $summary.Profile -Directory $directory
    Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs'|Copy-Item -Destination $directory
    $project=[xml](Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Probe.csproj') -Raw)
    $group=$project.CreateElement('ItemGroup')
    foreach($id in @('TrueMoon.Kton.Bepu','TrueMoon','TrueMoon.Extensions.DependencyInjection')){
        $element=$project.CreateElement($(if($fresh){'PackageReference'}else{'ProjectReference'}))
        if($fresh){$element.SetAttribute('Include',$id);$element.SetAttribute('Version',"[$packageVersion]")}
        else{$element.SetAttribute('Include',(Join-Path $workspace "$id/$id.csproj"))}
        [void]$group.AppendChild($element)
    }
    if(!$fresh){
        $element=$project.CreateElement('ProjectReference');$element.SetAttribute('Include',(Join-Path $workspace 'TrueMoon.Cobalt.Generator/TrueMoon.Cobalt.Generator.csproj'))
        $element.SetAttribute('ReferenceOutputAssembly','false');$element.SetAttribute('OutputItemType','Analyzer');[void]$group.AppendChild($element)
    }
    [void]$project.DocumentElement.AppendChild($group)
    $path=Join-Path $directory 'Probe.csproj';$project.Save($path);return $path
}
function Run-Probe([string]$name,[string]$dll,[bool]$fresh){
    $path=Join-Path $evidence "$name.json";Invoke-Checked $name @($dll,$path)
    $report=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
    if(!$report.Completed -or @($report.Checks.Provider|Select-Object -Unique).Count -ne 2 -or !$report.Owners.Count){throw "Unexpected report: $name"}
    foreach($owner in $report.Owners){if($owner.Resources.ActiveResourceOwners -ne 0 -or $owner.Resources.PublishedWorlds -ne 0 -or $owner.Resources.MaximumPoolBytesAfterCleanup -ne 0 -or $owner.Resources.PendingFailedCreationCleanup){throw 'Resource ownership proof failed'}}
    foreach($assembly in $report.Assemblies){
        if(@($assembly.References|Where-Object{$_ -match 'Aether|Alloy|Argentis|Silk|Skia'}).Count){throw 'Unexpected engine/UI reference'}
        if($fresh -and !$assembly.SharedFramework){$package=@($summary.Packages|Where-Object Id -eq $assembly.Name);if($package.Count -ne 1 -or $assembly.Sha256 -ne $package[0].DllSha256){throw "Loaded DLL mismatch: $($assembly.Name)"}}
    }
    $summary.Reports+=@{Name=$name;Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash;Checks=$report.CheckCount}
}
try{
    . (Join-Path $PSScriptRoot '../BuildProfile.ps1')
    $summary.Profile=Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory (Join-Path $evidence 'profile')
    $packageVersion=$summary.Profile.Version+'.phys4.'+[DateTime]::UtcNow.ToString('yyyyMMddHHmmss')
    $sourceDirectories=@($family+'TrueMoon.Cobalt.Generator')
    foreach($directory in @($PSScriptRoot)+@($sourceDirectories|ForEach-Object{Join-Path $workspace $_})){
        $summary.Sources+=@(Get-ChildItem -LiteralPath $directory -Recurse -File|Where-Object{$_.FullName -notmatch '[\\/](obj|bin)[\\/]'}|ForEach-Object{
            @{Path=$_.FullName.Substring($workspace.Length+1).Replace('\','/');Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
        })
    }
    # Freeze exact external packages already selected by the restored workspace graphs, including generator build tools.
    $external=@{}
    foreach($id in $sourceDirectories){
        $assets=Get-Content -LiteralPath (Join-Path $workspace "$id/obj/project.assets.json") -Raw|ConvertFrom-Json -AsHashtable
        foreach($library in $assets.libraries.GetEnumerator()){
            if($library.Value.type -ne 'package' -or $external.ContainsKey($library.Key)){continue}
            $relative=$library.Key.ToLowerInvariant();$parts=$relative.Split('/');$file="$($parts[0]).$($parts[1]).nupkg";$cached=$null
            foreach($folder in $assets.packageFolders.Keys){$candidate=Join-Path $folder "$relative/$file";if(Test-Path -LiteralPath $candidate){$cached=$candidate;break}}
            $target=Join-Path $feed $file
            if($cached){Copy-Item -LiteralPath $cached -Destination $target}
            else{Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/$relative/$file" -OutFile $target -TimeoutSec 60}
            $external[$library.Key]=$target
            $summary.ExternalPackages+=@{Identity=$library.Key;Source=$cached;Sha256=(Get-FileHash -LiteralPath $target).Hash}
        }
    }
    $bepu=@{BepuPhysics='9C1DB047CD7B3597CD808D7B94A1EF6626E0041E305BB3D8FE41AA658C3F1D03';BepuUtilities='D1CF1BB261720FEF05D197B4DE9F17E06BAC6D2A6C7574A6DE88BC21122C26EA'}
    foreach($id in $bepu.Keys){if((Get-FileHash -LiteralPath (Join-Path $feed ($id.ToLowerInvariant()+'.2.5.0-beta.33.nupkg'))).Hash -ne $bepu[$id]){throw "BEPU archive drift: $id"}}
    $source=New-Consumer 'source-consumer' $false
    Invoke-Checked 'source-restore' @('restore',$source,'--configfile',$config,'--packages',$cache,'--artifacts-path',$artifacts,'-p:NuGetAudit=false')
    $sourceOutput=Join-Path $run 'source-Debug';$generated=Join-Path $run 'source-generated'
    Invoke-Checked 'source-build-Debug' @('build',$source,'--no-restore','-c','Debug','--artifacts-path',$artifacts,'-o',$sourceOutput,'-p:EmitCompilerGeneratedFiles=true',"-p:CompilerGeneratedFilesOutputPath=$generated")
    $resolvers=@(Get-ChildItem -LiteralPath $generated -Recurse -Filter '*.cs'|Where-Object{(Get-Content -LiteralPath $_.FullName -Raw) -match 'new global::KtonAppProbe.ProbeSession'})
    if(!$resolvers.Count){throw 'Missing real Cobalt generated ProbeSession constructor'}
    $summary.GeneratedResolvers+=@($resolvers|ForEach-Object{@{Path=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
    Run-Probe 'source-Debug' (Join-Path $sourceOutput 'KtonAppProbe.dll') $false
    foreach($id in $family){Invoke-Checked "pack-$id" @('pack',(Join-Path $workspace "$id/$id.csproj"),'--no-restore','-c','Release','--artifacts-path',$artifacts,'-o',$feed,"-p:Version=$packageVersion")}
    foreach($archive in Get-ChildItem -LiteralPath $feed -Filter '*.nupkg'){
        $zip=[IO.Compression.ZipFile]::OpenRead($archive.FullName)
        try{
            $nuspec=@($zip.Entries|Where-Object FullName -like '*.nuspec');$reader=[IO.StreamReader]::new($nuspec[0].Open())
            try{$xml=[xml]$reader.ReadToEnd()}finally{$reader.Dispose()}
            $metadata=$xml.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]');$id=[string]$metadata.id
            $dll=@($zip.Entries|Where-Object FullName -eq "lib/$($summary.Profile.Framework)/$id.dll")
            if($dll.Count -eq 0){$dll=@($zip.Entries|Where-Object FullName -like "lib/net8.0/$id.dll")}
            if($dll.Count -eq 1){$stream=$dll[0].Open();try{$sha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}}
            else{$sha=$null}
            $summary.Packages+=@{Id=$id;Version=[string]$metadata.version;Sha256=(Get-FileHash -LiteralPath $archive.FullName).Hash;DllSha256=$sha;
                Dependencies=@($xml.SelectNodes('//*[local-name()="dependency"]')|ForEach-Object{@{Id=$_.id;Version=$_.version}})}
        }finally{$zip.Dispose()}
    }
    $fresh=New-Consumer 'package-consumer' $true;$freshArtifacts=Join-Path $run 'package-artifacts'
    Invoke-Checked 'package-restore' @('restore',$fresh,'--configfile',$config,'--packages',$cache,'--artifacts-path',$freshArtifacts)
    $assets=@(Get-ChildItem -LiteralPath (Join-Path $freshArtifacts 'obj') -Recurse -Filter project.assets.json)
    $graph=Get-Content -LiteralPath $assets[0].FullName -Raw|ConvertFrom-Json -AsHashtable
    if($assets.Count -ne 1 -or $graph.libraries.Count -ne 9 -or @($graph.libraries.Values|Where-Object type -eq 'project').Count){throw 'Expected exact 9-package graph without ProjectReferences'}
    foreach($id in @('BepuPhysics','BepuUtilities')){if(!$graph.libraries.ContainsKey("$id/2.5.0-beta.33")){throw 'BEPU pin drift'}}
    foreach($id in $family){if(!$graph.libraries.ContainsKey("$id/$packageVersion")){throw 'TrueMoon package-family drift'}}
    foreach($library in $graph.libraries.GetEnumerator()){
        $metadata=Get-Content -LiteralPath (Join-Path $cache ($library.Value.path+'/.nupkg.metadata')) -Raw|ConvertFrom-Json
        if([IO.Path]::GetFullPath($metadata.source) -ne $feed){throw 'Package source mismatch'}
    }
    $summary.Graphs+=@{Libraries=@($graph.libraries.Keys);ProjectReferences=0;AssetsSha256=(Get-FileHash -LiteralPath $assets[0].FullName).Hash}
    foreach($configuration in @('Debug','Release')){
        $output=Join-Path $run "package-$configuration";$generated=Join-Path $run "package-generated-$configuration"
        $arguments=@($(if($configuration -eq 'Debug'){'build'}else{'publish'}),$fresh,'--no-restore','-c',$configuration,'--artifacts-path',$freshArtifacts,'-o',$output,'-p:EmitCompilerGeneratedFiles=true',"-p:CompilerGeneratedFilesOutputPath=$generated")
        if($configuration -eq 'Release'){$arguments+=@('--self-contained','false')}
        Invoke-Checked "package-$configuration-build" $arguments
        $resolvers=@(Get-ChildItem -LiteralPath $generated -Recurse -Filter '*.cs'|Where-Object{(Get-Content -LiteralPath $_.FullName -Raw) -match 'new global::KtonAppProbe.ProbeSession'})
        if(!$resolvers.Count){throw 'Fresh Cobalt analyzer did not generate the consumer constructor'}
        $summary.GeneratedResolvers+=@($resolvers|ForEach-Object{@{Path=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
        Run-Probe "package-$configuration" (Join-Path $output 'KtonAppProbe.dll') $true
    }
    foreach($source in $summary.Sources){if((Get-FileHash -LiteralPath (Join-Path $workspace $source.Path)).Hash -ne $source.Sha256){throw "Changed source: $($source.Path)"}}
    foreach($source in $summary.Profile.Sources){if((Get-FileHash -LiteralPath $source.Path).Hash -ne $source.Sha256){throw "Build profile changed: $($source.Path)"}}
    $summary.Completed=$true
}catch{$summary.Failure=$_.ToString();throw}
finally{$summary|ConvertTo-Json -Depth 14|Set-Content -LiteralPath (Join-Path $evidence 'verification.json') -Encoding utf8}
