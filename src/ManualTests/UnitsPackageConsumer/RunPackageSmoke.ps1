[CmdletBinding()]
param([string]$RunDirectory, [string]$PackageVersion)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$allowed = [IO.Path]::GetFullPath((Join-Path $workspace 'TestResults/UnitsPackages')) + [IO.Path]::DirectorySeparatorChar
$run = if($RunDirectory) {[IO.Path]::GetFullPath($RunDirectory)} else {Join-Path $allowed ('run-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff'))}
if(!$run.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) {throw 'RunDirectory must be inside TestResults/UnitsPackages.'}
if(Test-Path -LiteralPath $run) {throw 'Use a fresh RunDirectory; previous evidence is preserved.'}
if(!$PackageVersion) {
    [xml]$props = Get-Content (Join-Path $workspace 'Directory.Build.props') -Raw
    $PackageVersion = $props.SelectSingleNode('//Version').InnerText + '.units.' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff')
}
if($PackageVersion -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?-[0-9A-Za-z.-]+$') {throw 'Use a prerelease version.'}
$feed=Join-Path $run 'feed'; $cache=Join-Path $run 'packages'; $logs=Join-Path $run 'logs'
foreach($directory in @($run,$feed,$cache,$logs)) {[IO.Directory]::CreateDirectory($directory) | Out-Null}
$summary=[ordered]@{Completed=$false;TimestampUtc=[DateTime]::UtcNow.ToString('O');Version=$PackageVersion;RunDirectory=$run;SDK=(& dotnet --version).Trim();OS=[Environment]::OSVersion.VersionString;Architecture=[Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString();Failure=$null;Commands=@();Cases=@();Libraries=@();Generation=@();Packages=@();AnalyzerSha256=@{};SourceFiles=@();NegativeControl=$null}
$versionProperty="-p:TrueMoonPackageVersion=$PackageVersion"

function Invoke-Owned([string]$Name,[string]$FileName,[string[]]$Arguments,[int]$TimeoutSeconds=120,[bool]$ExpectedFailure=$false) {
    $info=[Diagnostics.ProcessStartInfo]::new($FileName)
    $info.WorkingDirectory=$run; $info.UseShellExecute=$false; $info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true
    foreach($argument in $Arguments) {$info.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::new(); $process.StartInfo=$info; $started=$false
    try {
        $started=$process.Start(); if(!$started) {throw "Could not launch $Name"}
        $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
        if(!$process.WaitForExit($TimeoutSeconds*1000)) {throw "$Name timed out."}
        if(!$stdout.Wait(5000) -or !$stderr.Wait(5000)) {throw "$Name output did not close."}
        $output=$stdout.GetAwaiter().GetResult(); $errors=$stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path $logs "$Name.stdout.log"),$output)
        [IO.File]::WriteAllText((Join-Path $logs "$Name.stderr.log"),$errors)
        $summary.Commands+=@{Name=$Name;FileName=$FileName;Arguments=$Arguments;ExitCode=$process.ExitCode}
        if($ExpectedFailure) {
            if($process.ExitCode -eq 0 -or ($output+$errors) -notmatch '(CS0234|CS0246).*Generated') {throw 'Missing Enerit analyzer did not cause the expected generation failure.'}
            $summary.NegativeControl=@{Name=$Name;ExitCode=$process.ExitCode;Diagnostic=$Matches[1]}
        } elseif($process.ExitCode -ne 0) {throw "$Name failed exit$($process.ExitCode). See $logs"}
        return @{Output=$output;Error=$errors;Pid=$process.Id;ExitCode=$process.ExitCode}
    } finally {
        if($started -and !$process.HasExited) {$process.Kill($true); if(!$process.WaitForExit(5000)) {throw 'Owned process could not be reaped.'}}
        $process.Dispose()
    }
}
function DotNet([string]$Name,[string[]]$Arguments,[bool]$ExpectedFailure=$false) {
    Invoke-Owned $Name 'dotnet' $Arguments 120 $ExpectedFailure | Out-Null
    Write-Host "$Name passed"
}
$projects=[Collections.Generic.List[string]]::new(); $visited=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
function Add-Project([string]$Path) {
    $path=[IO.Path]::GetFullPath($Path)
    if(!$path.StartsWith($workspace+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {throw 'Project leaves source workspace.'}
    if(!$visited.Add($path)) {return}
    [xml]$project=Get-Content -LiteralPath $path -Raw
    foreach($reference in $project.SelectNodes('//ProjectReference')) {Add-Project (Join-Path (Split-Path $path) $reference.Include)}
    $projects.Add($path)
}
function Copy-Consumer([string]$Destination) {
    [IO.Directory]::CreateDirectory($Destination) | Out-Null
    foreach($name in @('Directory.Build.props','Directory.Build.targets','Directory.Packages.props','UnitsPackageConsumer.csproj','AnalyzerEvidence.cs')) {Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $Destination}
    foreach($name in @('Program.cs','Contract.cs')) {Copy-Item -LiteralPath (Join-Path $workspace "ManualTests/AppUnitsPipesProbe/$name") -Destination $Destination}
    $fallback=if($env:NUGET_PACKAGES) {$env:NUGET_PACKAGES} else {Join-Path $env:USERPROFILE '.nuget/packages'}
    $feedXml=[Security.SecurityElement]::Escape($feed); $fallbackXml=[Security.SecurityElement]::Escape([IO.Path]::GetFullPath($fallback))
    @"
<configuration>
 <packageSources><clear/><add key="truemoon-local" value="$feedXml"/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources>
 <packageSourceMapping><packageSource key="truemoon-local"><package pattern="TrueMoon"/><package pattern="TrueMoon.*"/></packageSource><packageSource key="nuget.org"><package pattern="*"/></packageSource></packageSourceMapping>
 <fallbackPackageFolders><clear/><add key="existing-third-party-cache" value="$fallbackXml"/></fallbackPackageFolders>
</configuration>
"@ | Set-Content -LiteralPath (Join-Path $Destination 'NuGet.Config') -Encoding utf8
}
function Verify-Assets([string]$Consumer,[bool]$Cobalt) {
    $assets=Get-Content (Join-Path $Consumer 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
    if(@($assets.libraries.Keys | Where-Object {$_ -match '^(Microsoft.CodeAnalysis|TrueMoon.Cobalt.Generator/)'}).Count) {throw 'Build-only Roslyn or standalone Cobalt generator restored.'}
    if(@($assets.libraries.Values | Where-Object type -ne 'package').Count) {throw 'Consumer has a non-package library.'}
    if(@($assets.libraries.Keys | Where-Object {$_ -match '^(Skia|Silk\.|Topten|TrueMoon\.(Alloy|Argentis))'}).Count) {throw 'Unexpected UI/native dependency.'}
    $local=@($assets.libraries.Keys | Where-Object {$_ -match '^TrueMoon(?:\.|/)'}); $expected=8
    if($local.Count -ne $expected) {throw "Unexpected local package count $($local.Count), expected$expected"}
    foreach($library in $local) {
        if(!$library.EndsWith('/'+$PackageVersion)) {throw "Wrong version: $library"}
        $metadata=Get-Content (Join-Path (Join-Path $cache $assets.libraries[$library].path) '.nupkg.metadata') -Raw | ConvertFrom-Json
        if($metadata.source.TrimEnd('\','/') -ne $feed.TrimEnd('\','/')) {throw "Wrong package source: $library"}
    }
    $summary.Libraries+=@{Consumer=[IO.Path]::GetRelativePath($run,$Consumer);Packages=$local}
}
function Verify-Generation([string]$Consumer,[string]$Configuration,[bool]$Cobalt) {
    $analyzers=@(Get-Content (Join-Path $Consumer "obj/$Configuration/net10.0/resolved-analyzers.txt"))
    foreach($id in @('TrueMoon.Enerit.Generator','TrueMoon.Cobalt.Generator')) {
        $analyzerMatches=@($analyzers | Where-Object {$_ -like "*$id.dll"}); $required=$id -eq 'TrueMoon.Enerit.Generator' -or $Cobalt
        if(!$required) {if($analyzerMatches.Count) {throw 'DI-only compiler still has Cobalt analyzer.'};continue}
        $packageId=if($id -eq 'TrueMoon.Cobalt.Generator') {'TrueMoon.Cobalt'} else {$id}
        $expected=Join-Path $cache ("$packageId/$PackageVersion/analyzers/dotnet/cs/$id.dll".ToLowerInvariant())
        if($analyzerMatches.Count -ne 1 -or !$analyzerMatches[0].Equals($expected,[StringComparison]::OrdinalIgnoreCase)) {throw "Wrong compiler analyzer $id"}
        if((Get-FileHash $expected).Hash -ne $summary.AnalyzerSha256[$id]) {throw 'Compiler analyzer SHA mismatch.'}
    }
    $files=@(Get-ChildItem (Join-Path $Consumer "obj/generated/$Configuration") -Recurse -File -Filter '*.g.cs')
    foreach($name in @('ProbeServiceGeneratedImplementation.g.cs','ProbeServiceInvocationServerHandler.g.cs','InvocationServiceInitializer.g.cs','Reply.SerializationExtensions.g.cs')) {if(!@($files | Where-Object Name -eq $name).Count) {throw "Missing generated $name"}}
    if($Cobalt -and !@($files | Where-Object Name -eq 'CobaltResolversRegistration.g.cs').Count) {throw 'Missing Cobalt registration sources.'}
    $summary.Generation+=@{Consumer=[IO.Path]::GetRelativePath($run,$Consumer);Configuration=$Configuration;Analyzers=$analyzers;Sources=@($files | ForEach-Object {@{Path=[IO.Path]::GetRelativePath($run,$_.FullName);Sha256=(Get-FileHash $_.FullName).Hash}})}
}
function Verify-Case($Report,[string]$Scenario,[string]$Provider,[int]$ParentProcessId) {
    if($Report.Scenario -ne $Scenario -or $Report.Provider -ne $Provider -or $Report.ParentPid -ne $ParentProcessId -or !$Report.Cleanup) {throw 'Invalid process identity/result.'}
    $restarted=$Scenario -in @('restart','budget'); $expected=if($restarted){2}else{1}
    if($Report.ChildPids.Count -ne $expected -or @($Report.ChildPids | Sort-Object -Unique).Count -ne $expected -or $Report.ChildPids -contains $ParentProcessId -or $Report.ExitCodes.Count -ne $expected) {throw 'Invalid owned child generations.'}
    if($restarted -and $Report.ExitCodes[0] -eq 0) {throw 'Expected initial crash exit.'}
    if($Scenario -eq 'budget') {if($Report.ExitCodes[-1] -eq 0) {throw 'Expected budget crash exit.'}} elseif($Report.ExitCodes[-1] -ne 0) {throw 'Expected cooperative final child exit0.'}
    if($Report.Restarts -ne [int]$restarted -or $Report.AppCancelled -ne ($Scenario -eq 'budget')) {throw 'Wrong restart budget/App lifetime.'}
    $failures=if($Scenario -in @('restart','budget','stop-pending')){1}else{0}
    $checks=switch($Scenario){'payload'{3};'remote-error'{3};'concurrent'{33};'cancel-request'{2};'restart'{2};'budget'{2};default{1}}
    if($Report.FailedRequests -ne $failures -or $Report.Checks -ne $checks) {throw 'Typed request outcomes mismatch.'}
    $observed=$Scenario -ne 'parent-exit'; $pending=if($observed){0}else{-1}
    if($Report.ClientCleanupObserved -ne $observed -or $Report.WorkersCompleted -ne $observed -or $Report.Pending -ne $pending) {throw 'Cleanup scope mismatch.'}
    if($observed -and (!$Report.ProcessDiagnosticsChecked -or $Report.ProcessDiagnosticCount -le 0)) {throw 'Packaged unit diagnostics were not checked.'}
}
try {
    foreach($id in @('TrueMoon','TrueMoon.Enerit','TrueMoon.Mithril','TrueMoon.Extensions.DependencyInjection')) {Add-Project (Join-Path $workspace "$id/$id.csproj")}
    if($projects.Count -ne 9) {throw 'Unexpected producer project graph.'}
    $inputs=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($project in $projects) {Get-ChildItem (Split-Path $project) -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in @('.cs','.csproj','.md','.props','.targets') } | ForEach-Object {[void]$inputs.Add($_.FullName)}}
    foreach($directory in @($PSScriptRoot,(Join-Path $workspace 'ManualTests/AppUnitsPipesProbe'))) {Get-ChildItem $directory -File | ForEach-Object {[void]$inputs.Add($_.FullName)}}
    foreach($name in @('Directory.Build.props','Directory.Packages.props','NuGet.Config','global.json','../README.md')) {[void]$inputs.Add([IO.Path]::GetFullPath((Join-Path $workspace $name)))}
    $summary.SourceFiles=@($inputs | Sort-Object | ForEach-Object {@{Path=[IO.Path]::GetRelativePath($workspace,$_);Sha256=(Get-FileHash $_).Hash}})
    foreach($project in $projects) {
        [xml]$packProject=Get-Content -LiteralPath $project -Raw
        if($packProject.SelectSingleNode('//IsPackable')?.InnerText -eq 'false') {continue}
        DotNet ('pack-'+[IO.Path]::GetFileNameWithoutExtension($project)) @('pack',$project,'--configuration','Release',"-p:Version=$PackageVersion",'--output',$feed,'--verbosity','quiet')
    }
    $packages=@(Get-ChildItem $feed -Filter '*.nupkg');if($packages.Count -ne 8) {throw 'Package graph incomplete.'}
    foreach($package in $packages) {
        $archive=[IO.Compression.ZipFile]::OpenRead($package.FullName)
        try {
            $entry=@($archive.Entries | Where-Object {$_.FullName.EndsWith('.nuspec')})[0]
            $reader=[IO.StreamReader]::new($entry.Open());try{[xml]$nuspec=$reader.ReadToEnd()}finally{$reader.Dispose()}
            $metadata=$nuspec.SelectSingleNode('//*[local-name()="metadata"]')
            foreach($asset in @('LICENSE','icon.png',$metadata.readme)) {if(!$archive.GetEntry($asset)) {throw "Missing $asset in $($package.Name)"}}
            if($metadata.description -eq 'Package Description') {throw 'Placeholder package description.'}
            foreach($dependency in $nuspec.SelectNodes('//*[local-name()="dependency"]')) {if($dependency.id -match '^TrueMoon(?:\.|$)' -and $dependency.version -notmatch [regex]::Escape($PackageVersion)) {throw 'Producer dependency version mismatch.'}}
            if($metadata.id -like '*.Generator') {
                $analyzer=$archive.GetEntry("analyzers/dotnet/cs/$($metadata.id).dll")
                if(!$analyzer -or @($archive.Entries | Where-Object {$_.FullName.StartsWith('lib/')}).Count) {throw 'Invalid analyzer package layout.'}
                $stream=$analyzer.Open();try{$summary.AnalyzerSha256[$metadata.id]=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}
            }
            if($metadata.id -eq 'TrueMoon.Cobalt') {
                $analyzer=$archive.GetEntry('analyzers/dotnet/cs/TrueMoon.Cobalt.Generator.dll')
                if(!$analyzer -or !$archive.GetEntry('lib/net10.0/TrueMoon.Cobalt.dll') -or !$archive.GetEntry('buildTransitive/TrueMoon.Cobalt.targets')) {throw 'Invalid unified Cobalt package layout.'}
                $stream=$analyzer.Open();try{$summary.AnalyzerSha256['TrueMoon.Cobalt.Generator']=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}
            }
            $summary.Packages+=@{Id=$metadata.id;Version=$metadata.version;Readme=$metadata.readme;Description=$metadata.description;Sha256=(Get-FileHash $package.FullName).Hash}
        } finally {$archive.Dispose()}
    }
    foreach($variant in @('both-generators','microsoft-only')) {
        $cobalt=$variant -eq 'both-generators';$consumer=Join-Path $run $variant;Copy-Consumer $consumer
        $project=Join-Path $consumer 'UnitsPackageConsumer.csproj';$properties=@($versionProperty)
        if(!$cobalt) {$properties+='-p:EnableCobaltGenerator=false'}
        DotNet "restore-$variant" (@('restore',$project,'--configfile',(Join-Path $consumer 'NuGet.Config'),'--packages',$cache,'--verbosity','quiet')+$properties)
        Verify-Assets $consumer $cobalt
        foreach($configuration in @('Debug','Release')) {
            DotNet "build-$variant-$configuration" (@('build',$project,'--configuration',$configuration,'--no-restore','--verbosity','quiet')+$properties)
            Verify-Generation $consumer $configuration $cobalt
            $publish=Join-Path $run "publish/$variant/$configuration"
            DotNet "publish-$variant-$configuration" (@('publish',$project,'--configuration',$configuration,'--no-restore','--self-contained','false','--output',$publish,'--verbosity','quiet')+$properties)
            foreach($delivery in @(@{Name='build';Path=Join-Path $consumer "bin/$configuration/net10.0"},@{Name='publish';Path=$publish})) {
                if(@(Get-ChildItem $delivery.Path -Recurse -File | Where-Object {$_.Name -match '^(TrueMoon\..*\.Generator|Microsoft.CodeAnalysis|Skia|Silk\.).*\.dll$'}).Count) {throw 'Build-only analyzer/graphics DLL in runtime output.'}
                foreach($name in @('UnitsPackageConsumer.dll','UnitsPackageConsumer.deps.json','UnitsPackageConsumer.runtimeconfig.json','TrueMoon.Enerit.dll','TrueMoon.Mithril.dll')) {if(!(Test-Path (Join-Path $delivery.Path $name))) {throw "Missing deployed $name"}}
                $exe=Join-Path $delivery.Path $(if($IsWindows){'UnitsPackageConsumer.exe'}else{'UnitsPackageConsumer'})
                $providers=if($cobalt){@('cobalt','di')}else{@('di')}
                foreach($provider in $providers) {
                    foreach($scenario in @('payload','concurrent','cancel-request','remote-error','restart','budget','stop-pending','startup-failure','parent-exit')) {
                        $name="$variant-$configuration-$($delivery.Name)-$provider-$scenario";$directory=Join-Path $run ("cases/$name-"+[Guid]::NewGuid().ToString('N'))
                        $result=Invoke-Owned $name $exe @($scenario,$provider,$directory) 40
                        if($result.Error) {throw 'Consumer stderr was not empty.'}
                        $lines=@($result.Output -split '\r?\n' | Where-Object {$_.StartsWith('RESULT ')})
                        if($lines.Count -ne 1) {throw 'Expected one consumer result.'}
                        $report=$lines[0].Substring(7) | ConvertFrom-Json
                        Verify-Case $report $scenario $provider $result.Pid
                        $summary.Cases+=@{Name=$name;Consumer=$variant;Configuration=$configuration;Delivery=$delivery.Name;Report=$report}
                        Write-Host "$name passed"
                    }
                }
            }
        }
    }
    $negative=Join-Path $run 'without-enerit-generator';Copy-Consumer $negative;$project=Join-Path $negative 'UnitsPackageConsumer.csproj'
    DotNet 'restore-without-enerit-generator' @('restore',$project,'--configfile',(Join-Path $negative 'NuGet.Config'),'--packages',$cache,$versionProperty,'-p:EnableEneritGenerator=false','--verbosity','quiet')
    DotNet 'build-without-enerit-generator' @('build',$project,'--no-restore',$versionProperty,'-p:EnableEneritGenerator=false','--verbosity','quiet') $true
    if($summary.Cases.Count -ne 108) {throw 'Incomplete108case matrix.'}
    foreach($source in $summary.SourceFiles) {
        if((Get-FileHash -LiteralPath (Join-Path $workspace $source.Path)).Hash -ne $source.Sha256) {throw "Source changed: $($source.Path)"}
    }
    $summary.Completed=$true
    Write-Host "Units package smoke:108cases and omitted Enerit analyzer control passed. $run"
} catch {$summary.Failure=$_.ToString();throw}
finally {$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $run 'summary.json') -Encoding utf8}
