[CmdletBinding()]
param([string]$EvidenceDirectory, [string]$RunDirectory, [string]$PackageVersion)
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
$trueMoonBuildProfile = Get-TrueMoonBuildProfile -Workspace ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))



$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$run = if ($RunDirectory) { [IO.Path]::GetFullPath($RunDirectory) } else {
    Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-ConfigurationDelivery-' + [Guid]::NewGuid().ToString('N'))
}
$evidence = if ($EvidenceDirectory) { [IO.Path]::GetFullPath($EvidenceDirectory) } else {
    Join-Path $workspace ('TestResults/ConfigurationDelivery-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
if ($run -eq $workspace -or $run.StartsWith($workspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'RunDirectory must be outside the checkout.'
}
if ((Test-Path -LiteralPath $run) -or (Test-Path -LiteralPath $evidence)) { throw 'Use fresh run and evidence directories.' }
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$feed = Join-Path $run 'feed'
$cwd = Join-Path $run 'unrelated working directory'
foreach ($directory in @($run, $evidence, $feed, $cwd)) { [void][IO.Directory]::CreateDirectory($directory) }
$version = if ($PackageVersion) { $PackageVersion } else { $trueMoonBuildProfile.Version + '.delivery.' + (Get-Date -Format 'yyyyMMddHHmmss') }
if ($version -notmatch '^[0-9A-Za-z][0-9A-Za-z.+-]*$' -or !$version.StartsWith($trueMoonBuildProfile.Version + '.', [StringComparison]::Ordinal)) {
    throw 'Use a unique local prerelease derived from the current framework version.'
}
$packages = @('TrueMoon.Contracts','TrueMoon.Core','TrueMoon.Cobalt','TrueMoon','TrueMoon.Extensions.DependencyInjection',
    'TrueMoon.Enerit.Generator','TrueMoon.Enerit','TrueMoon.Mithril','TrueMoon.Argentis','TrueMoon.Alloy',
    'TrueMoon.Alloy.Rendering.Skia','TrueMoon.Alloy.Platform.Silk','TrueMoon.Alloy.Hosting','TrueMoon.Aether','TrueMoon.Aether.Editor','TrueMoon.Nom')
$report = [ordered]@{ Completed=$false; Version=$version; BuildProfile=$trueMoonBuildProfile; RunDirectory=$run; Commands=@(); Packages=@();
    ExternalPackages=@(); Sources=@(); Graphs=@(); Profiles=@(); Failure=$null; GpuRun=$false; WindowRun=$false }
function Save-Report { $report | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath (Join-Path $evidence 'verification.json') -Encoding utf8 }
function Invoke-Delivery([string]$name, [string[]]$arguments, [string]$workingDirectory=$workspace, [string]$cache='') {
    $info = [Diagnostics.ProcessStartInfo]::new($dotnet)
    $info.WorkingDirectory=$workingDirectory; $info.UseShellExecute=$false; $info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true
    $info.Environment['APPDATA']=Join-Path $run 'settings'; [void][IO.Directory]::CreateDirectory($info.Environment['APPDATA'])
    $info.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'; $info.Environment['DOTNET_SKIP_FIRST_TIME_EXPERIENCE']='1'
    $info.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli'
    foreach ($key in @('DirectoryPackagesPropsPath','TM_ENGINE_BOOTSTRAP','-u','-p','-ud','TrueMoon.Mithril.ControlPipe',
        'TRUEMOON_GPU_VALIDATION','TRUEMOON_AETHER_RUNTIME__GPUVALIDATION','MYAPP_APP__NAME','MYAPP_PATHS__ROOT')) { [void]$info.Environment.Remove($key) }
    # Explicit test-only environment source used by the unchanged module consumer.
    $info.Environment['TRUEMOON_CORE75_NOM__MAXIMUMOPENSTORES']='4'
    if ($cache) {
        $info.Environment['NUGET_PACKAGES']=$cache
        $info.Environment['NUGET_HTTP_CACHE_PATH']=Join-Path $run 'http-cache'
        $info.Environment['NUGET_PLUGINS_CACHE_PATH']=Join-Path $run 'plugins-cache'
    }
    foreach ($argument in $arguments) { [void]$info.ArgumentList.Add($argument) }
    $process=[Diagnostics.Process]::Start($info)
    $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync(); $timeout=$false
    try {
        if (!$process.WaitForExit(240000)) { $timeout=$true; $process.Kill($true); $process.WaitForExit() }
        [IO.File]::WriteAllText((Join-Path $evidence "$name.stdout.log"), $stdout.GetAwaiter().GetResult())
        [IO.File]::WriteAllText((Join-Path $evidence "$name.stderr.log"), $stderr.GetAwaiter().GetResult())
        $report.Commands+=@{Name=$name;Arguments=$arguments;WorkingDirectory=$workingDirectory;Cache=$cache;ExitCode=$process.ExitCode;TimedOut=$timeout}
        Save-Report
        if ($timeout -or $process.ExitCode -ne 0) { throw "$name failed; logs preserved." }
        if ($arguments[0] -in @('restore','build','publish','pack') -and !(Get-ChildItem -LiteralPath $evidence -Filter "$name-*.binlog")) {
            throw "Missing unique binlog: $name"
        }
        Write-Host "$name passed"
    } finally { if (!$process.HasExited) { $process.Kill($true); $process.WaitForExit() }; $process.Dispose() }
}
function Add-Profile([hashtable]$consumer, [string]$kind, [string]$assembly, [string]$cache) {
    $name=$consumer.Name+'-'+$kind
    $output=Join-Path $evidence $name
    if ($consumer.ContainsKey('Documentation')) {
        Invoke-Delivery $name @($assembly,'--App:Name=configuration-doc-proof') $cwd $cache
        if ([IO.File]::ReadAllText((Join-Path $evidence "$name.stdout.log")) -notmatch 'configuration-doc-proof') {
            throw 'Documentation example did not use the configured App name.'
        }
        [void][IO.Directory]::CreateDirectory($output)
        @{Completed=$true;Checks=1;GpuRun=$false;WindowRun=$false} | ConvertTo-Json |
            Set-Content -LiteralPath (Join-Path $output 'verification.json') -Encoding utf8
    } else { Invoke-Delivery $name @($assembly,$output) $cwd $cache }
    $proof=Get-Content -LiteralPath (Join-Path $output 'verification.json') -Raw | ConvertFrom-Json
    if (!$proof.Completed -or $proof.GpuRun -or $proof.WindowRun -or $proof.Checks -ne $consumer.Checks) {
        throw "Consumer scope mismatch: $name"
    }
    if ($consumer.Children -and !$proof.ChildProcessRun) { throw "Missing owned children: $name" }
    if ($consumer.Name -eq 'modules' -and $proof.ChildProcessRun) { throw 'Unexpected module runtime child.' }
    if ($consumer.Name -eq 'bootstrap' -and ($proof.ReapedChildren -ne 2 -or $proof.NativeRuntimeStarted)) {
        throw 'Bootstrap child scope mismatch.'
    }
    $report.Profiles+=@{Name=$name;Checks=$proof.Checks;Report=(Join-Path $output 'verification.json');OwnedChildren=$consumer.Children}
    Save-Report
}
function Entry-Hash([IO.Compression.ZipArchive]$archive, [string]$name) {
    $entry=$archive.GetEntry($name)
    if (!$entry) { throw "Missing package payload: $name" }
    $stream=$entry.Open()
    try { return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) }
    finally { $stream.Dispose() }
}
function Verify-Payload([string]$id, [string]$path) {
    $archive=[IO.Compression.ZipFile]::OpenRead($path)
    try {
        $files=@(
            @('PACKAGE.md',(Join-Path $workspace "$id/PACKAGE.md")),
            @('LICENSE',(Join-Path $workspace '../LICENSE')),
            @('icon.png',(Join-Path $workspace '../materials/icon.png'))
        )
        # Nom deliberately removes the shared README item in its project; its PACKAGE.md is authoritative.
        if ($id -eq 'TrueMoon.Nom') {
            if ($archive.GetEntry('README.md')) { throw 'Nom unexpectedly includes the removed shared README.' }
        } else { $files+=,@('README.md',(Join-Path $workspace '../README.md')) }
        if ($id -eq 'TrueMoon.Cobalt') {
            $files+=,@('analyzers/dotnet/cs/TrueMoon.Cobalt.Generator.dll',(Join-Path $workspace "TrueMoon.Cobalt.Generator/bin/Release/$($trueMoonBuildProfile.Framework)/TrueMoon.Cobalt.Generator.dll"))
            $files+=,@('buildTransitive/TrueMoon.Cobalt.targets',(Join-Path $workspace 'TrueMoon.Cobalt/buildTransitive/TrueMoon.Cobalt.targets'))
        }
        if ($id -eq 'TrueMoon.Enerit.Generator') {
            $files+=,@('analyzers/dotnet/cs/TrueMoon.Enerit.Generator.dll',(Join-Path $workspace "TrueMoon.Enerit.Generator/bin/Release/$($trueMoonBuildProfile.Framework)/TrueMoon.Enerit.Generator.dll"))
        }
        if ($id -eq 'TrueMoon.Alloy.Rendering.Skia') {
            $files+=,@('nativeAssets/win-x64/libSkiaSharp.dll',(Join-Path $workspace 'TrueMoon.Alloy.Rendering.Skia/NativeAssets/win-x64/libSkiaSharp.dll'))
            foreach ($folder in @('build','buildTransitive')) {
                $files+=,@("$folder/TrueMoon.Alloy.Rendering.Skia.targets",(Join-Path $workspace "TrueMoon.Alloy.Rendering.Skia/$folder/TrueMoon.Alloy.Rendering.Skia.targets"))
            }
        }
        $payloads=@()
        foreach ($file in $files) {
            $hash=Entry-Hash $archive $file[0]
            if ($hash -ne (Get-FileHash -LiteralPath $file[1]).Hash) { throw "Stale package payload: $id/$($file[0])" }
            $payloads+=@{Path=$file[0];Sha256=$hash}
            $report.Sources+=@{Path=$file[1];Sha256=(Get-FileHash -LiteralPath $file[1]).Hash}
        }
        return ,$payloads
    } finally { $archive.Dispose() }
}
try {
    $consumers=@(
        @{Name='profile';Assembly='ConfigurationProfilesConsumer';File='ManualTests/ConfigurationProfileConsumer/Program.cs';Checks=15;Children=0;
            References=@('TrueMoon.Contracts','TrueMoon.Cobalt','TrueMoon');Required=@('TrueMoon.Contracts','TrueMoon.Cobalt','TrueMoon')},
        @{Name='app';Assembly='ConfigurationAppConsumer';File='ManualTests/ConfigurationAppConsumer/Program.cs';Checks=108;Children=2;
            References=@('TrueMoon.Contracts','TrueMoon','TrueMoon.Mithril','TrueMoon.Extensions.DependencyInjection');
            Required=@('TrueMoon.Contracts','TrueMoon.Core','TrueMoon.Cobalt','TrueMoon','TrueMoon.Mithril','TrueMoon.Extensions.DependencyInjection')},
        @{Name='modules';Assembly='ConfigurationModulesConsumer';File='ManualTests/ConfigurationModulesConsumer/Program.cs';Checks=76;Children=0;
            References=@('TrueMoon.Contracts','TrueMoon','TrueMoon.Aether.Editor','TrueMoon.Nom','TrueMoon.Extensions.DependencyInjection');Required=$packages},
        @{Name='bootstrap';Assembly='ConfigurationBootstrapConsumer';File='ManualTests/ConfigurationDelivery/BootstrapConsumer.cs';Checks=30;Children=2;
            References=@('TrueMoon','TrueMoon.Aether','TrueMoon.Mithril','TrueMoon.Extensions.DependencyInjection');
            Required=@('TrueMoon.Contracts','TrueMoon','TrueMoon.Aether','TrueMoon.Mithril','TrueMoon.Extensions.DependencyInjection')},
        @{Name='guide';Assembly='ConfigurationGuideConsumer';File='docs/core/CONFIGURATION_GUIDE.md';Checks=1;Children=0;Documentation=$true;
            References=@('TrueMoon');Required=@('TrueMoon.Contracts','TrueMoon.Cobalt','TrueMoon')}
    )
    foreach ($consumer in $consumers) {
        $path=Join-Path $workspace $consumer.File
        $report.Sources+=@{Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash}
    }
    $script=Join-Path $PSScriptRoot 'Verify.ps1'
    $report.Sources+=@{Path=$script;Sha256=(Get-FileHash -LiteralPath $script).Hash}
    foreach ($file in @('Directory.Build.props','Directory.Packages.props','../README.md','../LICENSE','../materials/icon.png')) {
        $path=[IO.Path]::GetFullPath((Join-Path $workspace $file))
        $report.Sources+=@{Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash}
    }
    foreach ($id in $packages) {
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $workspace $id) -Recurse -File | Where-Object {
            $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in @('.cs','.csproj','.md','.props','.targets')
        }) { $report.Sources+=@{Path=$file.FullName;Sha256=(Get-FileHash -LiteralPath $file.FullName).Hash} }
    }
    $external=@{}
    foreach ($id in @($packages+'TrueMoon.Cobalt.Generator')) {
        $assets=Get-Content -LiteralPath (Join-Path $workspace "$id/obj/project.assets.json") -Raw | ConvertFrom-Json -AsHashtable
        foreach ($library in $assets.libraries.GetEnumerator()) {
            if ($library.Value.type -ne 'package' -or $external.ContainsKey($library.Key)) { continue }
            $relative=$library.Key.ToLowerInvariant(); $parts=$relative.Split('/'); $file="$($parts[0]).$($parts[1]).nupkg"; $cached=$null
            foreach ($folder in $assets.packageFolders.Keys) {
                $candidate=Join-Path $folder "$relative/$file"
                if (Test-Path -LiteralPath $candidate) { $cached=$candidate; break }
            }
            if (!$cached) { throw "Exact cached package missing: $($library.Key)" }
            Copy-Item -LiteralPath $cached -Destination (Join-Path $feed $file)
            $external[$library.Key]=$cached
            $report.ExternalPackages+=@{Identity=$library.Key;Source=$cached;Sha256=(Get-FileHash -LiteralPath $cached).Hash}
        }
    }
    foreach ($id in $packages) {
        $name='pack-'+$id
        Invoke-Delivery $name @('pack',"$id/$id.csproj",'--no-build','--no-restore','-c','Release','-o',$feed,"-p:Version=$version","-bl:$evidence/$name-{}.binlog")
        $path=Join-Path $feed "$id.$version.nupkg"
        if (!(Test-Path -LiteralPath $path)) { throw "Missing package $id" }
        $report.Packages+=@{Id=$id;Path=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash;Payloads=(Verify-Payload $id $path)}
    }
    foreach ($consumer in $consumers) {
        foreach ($kind in @('source','fresh')) {
            $directory=Join-Path $run ($consumer.Name+'-'+$kind)
            [void][IO.Directory]::CreateDirectory($directory)
            [IO.File]::WriteAllText((Join-Path $directory 'NuGet.Config'), '<configuration><packageSources><clear/><add key="local" value="'+[Security.SecurityElement]::Escape($feed)+'"/></packageSources></configuration>')
            Write-TrueMoonConsumerBuildProfile -Profile $trueMoonBuildProfile -Directory $directory
            [IO.File]::WriteAllText((Join-Path $directory 'Directory.Build.targets'), '<Project/>')
            if ($consumer.ContainsKey('Documentation')) {
                $document=[IO.File]::ReadAllText((Join-Path $workspace $consumer.File))
                $block=[regex]::Match($document,'(?s)```csharp\r?\n(.*?)```')
                if (!$block.Success) { throw 'Missing guide C# example.' }
                [IO.File]::WriteAllText((Join-Path $directory 'Program.cs'),$block.Groups[1].Value)
            } else { Copy-Item -LiteralPath (Join-Path $workspace $consumer.File) -Destination (Join-Path $directory 'Program.cs') }
            $references=foreach ($id in $consumer.References) {
                if ($kind -eq 'source') { '<ProjectReference Include="'+[Security.SecurityElement]::Escape((Join-Path $workspace "$id/$id.csproj"))+'"/>' }
                else { '<PackageReference Include="'+$id+'" Version="'+$version+'"/>' }
            }
            if ($kind -eq 'fresh' -and $consumer.Name -in @('modules','bootstrap')) {
                $references+='<PackageReference Include="TrueMoon.Enerit.Generator" Version="'+$version+'" PrivateAssets="all"/>'
            }
            [IO.File]::WriteAllText((Join-Path $directory 'Consumer.csproj'), '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><AssemblyName>'+$consumer.Assembly+'</AssemblyName><RootNamespace>'+$consumer.Assembly+'</RootNamespace></PropertyGroup><ItemGroup>'+($references -join '')+'</ItemGroup></Project>')
            $cache=Join-Path $run ($consumer.Name+'-'+$kind+'-cache')
            $name=$consumer.Name+'-'+$kind+'-restore'
            $restore=@('restore','Consumer.csproj','--configfile',"$directory/NuGet.Config",'--no-http-cache',"-bl:$evidence/$name-{}.binlog")
            if ($kind -eq 'source') { $restore+='--no-dependencies' }
            Invoke-Delivery $name $restore $directory $cache
            if ($kind -eq 'source') {
                $name=$consumer.Name+'-source-build'
                Invoke-Delivery $name @('build','Consumer.csproj','--no-restore','-p:BuildProjectReferences=false','-c','Debug',"-bl:$evidence/$name-{}.binlog") $directory $cache
                Add-Profile $consumer 'source' "$directory/bin/Debug/$($trueMoonBuildProfile.Framework)/$($consumer.Assembly).dll" $cache
                $name=$consumer.Name+'-published-build'; $output=Join-Path $run ($consumer.Name+'-published')
                Invoke-Delivery $name @('publish','Consumer.csproj','--no-restore','-p:BuildProjectReferences=false','-c','Release','-o',$output,'-p:UseAppHost=false',"-bl:$evidence/$name-{}.binlog") $directory $cache
                Add-Profile $consumer 'published' "$output/$($consumer.Assembly).dll" $cache
            } else {
                $graph=Get-Content -LiteralPath "$directory/obj/project.assets.json" -Raw | ConvertFrom-Json -AsHashtable
                if ($graph.libraries.Values.Where({$_.type -eq 'project'}).Count) { throw 'Fresh graph contains ProjectReference.' }
                foreach ($id in $consumer.Required) { if (!$graph.libraries.ContainsKey("$id/$version")) { throw "Missing fresh package $id" } }
                $local=@($graph.libraries.GetEnumerator() | Where-Object {$_.Key.StartsWith('TrueMoon')})
                foreach ($library in $local) {
                    if (!$library.Key.EndsWith('/'+$version)) { throw 'Mixed TrueMoon package versions.' }
                    $metadata=Get-Content -LiteralPath (Join-Path $cache ($library.Value.path+'/.nupkg.metadata')) -Raw | ConvertFrom-Json
                    if ([IO.Path]::GetFullPath($metadata.source) -ne $feed) { throw 'Fresh package source mismatch.' }
                }
                $name=$consumer.Name+'-fresh-publish'; $output=Join-Path $run ($consumer.Name+'-fresh-published')
                Invoke-Delivery $name @('publish','Consumer.csproj','--no-restore','-c','Release','-o',$output,'-p:UseAppHost=false',"-bl:$evidence/$name-{}.binlog") $directory $cache
                $runtimeHashes=@()
                foreach ($target in $graph.targets.Values) { foreach ($library in $target.GetEnumerator() | Where-Object {$_.Key.StartsWith('TrueMoon')}) {
                    if (!$library.Value.ContainsKey('runtime')) { continue }
                    foreach ($file in $library.Value.runtime.Keys | Where-Object { $_.EndsWith('.dll') }) {
                        $installed=Join-Path $cache ($graph.libraries[$library.Key].path+'/'+$file)
                        $deployed=Join-Path $output ([IO.Path]::GetFileName($file))
                        if ((Get-FileHash -LiteralPath $installed).Hash -ne (Get-FileHash -LiteralPath $deployed).Hash) { throw 'Installed/output package hash mismatch.' }
                        $runtimeHashes+=@{Assembly=[IO.Path]::GetFileName($file);Sha256=(Get-FileHash -LiteralPath $deployed).Hash}
                    }
                } }
                $report.Graphs+=@{Consumer=$consumer.Name;ProjectReferences=0;LocalPackages=@($local.Key);Cache=$cache;RuntimeHashes=$runtimeHashes;AssetsSha256=(Get-FileHash -LiteralPath "$directory/obj/project.assets.json").Hash}
                Add-Profile $consumer 'fresh' "$output/$($consumer.Assembly).dll" $cache
            }
        }
    }
    foreach ($source in $report.Sources) { if ((Get-FileHash -LiteralPath $source.Path).Hash -ne $source.Sha256) { throw 'Source drift during delivery.' } }
    $report.Completed=$true
} catch { $report.Failure=$_.Exception.ToString(); throw }
finally { Save-Report; Write-Host "Evidence: $evidence; run: $run" }
