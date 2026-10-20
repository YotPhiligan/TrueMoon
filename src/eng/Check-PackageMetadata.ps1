#Requires -Version 7.2
[CmdletBinding()]
param(
    [switch] $Pack,
    [string[]] $DeferredProject = @('TrueMoon.Aether/TrueMoon.Aether.csproj'),
    [string] $RunDirectory,
    [ValidateRange(1,1800)] [int] $TimeoutSeconds = 180
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputRoot = Join-Path $workspace 'TestResults/PackageMetadata'
$run = if ($RunDirectory) { [IO.Path]::GetFullPath($RunDirectory, $workspace) } else { Join-Path $outputRoot ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff')) }
if (!$run.StartsWith($outputRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Use a fresh directory inside TestResults/PackageMetadata.' }
if (Test-Path -LiteralPath $run) { throw 'Existing evidence must not be overwritten.' }
[IO.Directory]::CreateDirectory($run) | Out-Null
$summary = [ordered]@{ Completed = $false; Packed = [bool]$Pack; Projects = @(); Packages = @(); Inputs = @(); Commands = @(); Failure = $null }
function Save-Report { $summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $run 'summary.json') }
function Invoke-Dotnet([string] $Name, [string[]] $Arguments) {
    $info = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $info.WorkingDirectory = $workspace; $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    $info.StandardOutputEncoding = [Text.Encoding]::UTF8; $info.StandardErrorEncoding = [Text.Encoding]::UTF8
    $info.Environment['DOTNET_CLI_UI_LANGUAGE'] = 'en'
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($info)
    $exitCode = $null; $timedOut = $false
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit($TimeoutSeconds * 1000)) { $timedOut = $true; $process.Kill($true); $process.WaitForExit() }
        $output = $stdout.GetAwaiter().GetResult(); $errorText = $stderr.GetAwaiter().GetResult(); $exitCode = $process.ExitCode
        [IO.File]::WriteAllText((Join-Path $run "$Name.stdout.log"), $output)
        [IO.File]::WriteAllText((Join-Path $run "$Name.stderr.log"), $errorText)
        if ($timedOut -or $exitCode) { throw "$Name failed (exit=$exitCode, timeout=$timedOut). See logs in $run" }
        return $output
    } finally {
        if (!$process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $summary.Commands += [ordered]@{ Name = $Name; Arguments = $Arguments; Pid = $process.Id; ExitCode = $exitCode; TimedOut = $timedOut }
        $process.Dispose(); Save-Report
    }
}
function Entry-Hash($Archive, [string] $Path) {
    $entry = $Archive.GetEntry($Path)
    if (!$entry -or $entry.Length -eq 0) { throw "Missing/empty package payload: $Path" }
    $stream = $entry.Open(); $hash = [Security.Cryptography.SHA256]::Create()
    try { return [Convert]::ToHexString($hash.ComputeHash($stream)) } finally { $hash.Dispose(); $stream.Dispose() }
}
try {
    $projectPaths = @(& git -C $workspace ls-files --cached --others --exclude-standard -- '*.csproj' '*.fsproj' '*.vbproj')
    if ($LASTEXITCODE) { throw 'Cannot enumerate repository projects.' }
    $projectPaths = @($projectPaths | Where-Object { Test-Path -LiteralPath (Join-Path $workspace $_) } | Sort-Object -Unique)
    $inputPaths = @($projectPaths | ForEach-Object { Join-Path $workspace $_ })
    $inputPaths += @($projectPaths | ForEach-Object { Get-ChildItem -LiteralPath ([IO.Path]::GetDirectoryName((Join-Path $workspace $_))) -File -Filter '*.md' } | Select-Object -ExpandProperty FullName)
    $inputPaths += @(& git -C $workspace ls-files --cached --others --exclude-standard -- '*.props' '*.targets' 'global.json' | ForEach-Object { Join-Path $workspace $_ })
    $inputPaths += @($PSCommandPath,(Join-Path $workspace '../LICENSE'),(Join-Path $workspace '../materials/icon.png'),(Join-Path $workspace '../README.md'))
    $summary.Inputs = @($inputPaths | Sort-Object -Unique | ForEach-Object { [ordered]@{ Path = [IO.Path]::GetFullPath($_); Sha256 = (Get-FileHash -LiteralPath $_).Hash } })
    $deferred = @($DeferredProject | ForEach-Object { [IO.Path]::GetFullPath($_, $workspace) })
    if (@($deferred | Where-Object { $_ -notin @($projectPaths | ForEach-Object { [IO.Path]::GetFullPath($_, $workspace) }) }).Count) { throw 'Deferred project does not exist in the repository.' }
    $fields = 'IsPackable,IsTestProject,OutputType,PackageId,PackageVersion,Authors,Company,Product,Description,PackageDescription,Copyright,PackageTags,PackageProjectUrl,RepositoryUrl,RepositoryType,PackageLicenseFile,PackageLicenseExpression,PackageIcon,PackageReadmeFile,TargetFramework,AssemblyName,IncludeBuildOutput,GenerateDocumentationFile'
    $issues = @()
    foreach ($path in $projectPaths) {
        $name = [IO.Path]::GetFileNameWithoutExtension($path)
        $properties = (Invoke-Dotnet "evaluate-$name" @('msbuild',$path,'-nologo',"-getProperty:$fields",'-property:Configuration=Release') | ConvertFrom-Json).Properties
        $isDeferred = $properties.IsPackable -ne 'true' -and [IO.Path]::GetFullPath($path, $workspace) -in $deferred
        $selected = $properties.IsPackable -eq 'true' -or $isDeferred
        if ($selected) {
            if ($properties.IsTestProject -eq 'true' -or $properties.OutputType -in 'Exe','WinExe') { $issues += "$name must not be a library package." }
            foreach ($field in @('PackageId','PackageVersion','Authors','Company','Product','Description','Copyright','PackageTags','PackageProjectUrl','RepositoryUrl','RepositoryType','PackageIcon','PackageReadmeFile')) {
                if ([string]::IsNullOrWhiteSpace($properties.$field)) { $issues += "$name missing $field" }
            }
            if ($properties.Description -eq 'Package Description') { $issues += "$name has a placeholder description." }
            if (!!$properties.PackageLicenseFile -eq !!$properties.PackageLicenseExpression) { $issues += "$name must specify exactly one license form." }
            if ($properties.PackageReadmeFile -ne 'PACKAGE.md') { $issues += "$name must select its own PACKAGE.md." }
            if (!(Test-Path -LiteralPath (Join-Path ([IO.Path]::GetDirectoryName((Join-Path $workspace $path))) $properties.PackageReadmeFile))) { $issues += "$name has no local package README." }
        }
        $summary.Projects += [ordered]@{ Project = $path; Selected = $selected; Deferred = $isDeferred; Metadata = $properties }
    }
    if ($issues.Count) { throw ($issues -join [Environment]::NewLine) }
    $selectedProjects = @($summary.Projects | Where-Object Selected)
    if (!$selectedProjects.Count) { throw 'No package projects discovered.' }
    if ($Pack) {
        Add-Type -AssemblyName System.IO.Compression
        foreach ($project in $selectedProjects) {
            $metadata = $project.Metadata; $id = $metadata.PackageId
            $feed = Join-Path $run $(if ($project.Deferred) { 'deferred-preview' } else { 'packages' })
            $arguments = @('pack',$project.Project,'--configuration','Release','--output',$feed,'--verbosity','quiet')
            if ($project.Deferred) { $arguments += '-property:IsPackable=true' }
            $null = Invoke-Dotnet "pack-$id" $arguments
            $archives = @(Get-Item -LiteralPath (Join-Path $feed "$id.$($metadata.PackageVersion).nupkg") -ErrorAction SilentlyContinue)
            if ($archives.Count -ne 1) { throw "Expected one fresh package: $id" }
            $archive = [IO.Compression.ZipFile]::OpenRead($archives[0].FullName)
            try {
                $nuspecEntry = @($archive.Entries | Where-Object { $_.FullName.EndsWith('.nuspec') })
                if ($nuspecEntry.Count -ne 1) { throw "Invalid nuspec count: $id" }
                $reader = [IO.StreamReader]::new($nuspecEntry[0].Open())
                try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
                $actual = $nuspec.package.metadata
                $description = if ($metadata.PackageDescription) { $metadata.PackageDescription } else { $metadata.Description }
                if ($actual.id -ne $id -or $actual.version -ne $metadata.PackageVersion -or $actual.authors -ne $metadata.Authors -or $actual.description -ne $description -or
                    $actual.projectUrl -ne $metadata.PackageProjectUrl -or $actual.repository.url -ne $metadata.RepositoryUrl -or $actual.repository.type -ne $metadata.RepositoryType -or
                    $actual.icon -ne $metadata.PackageIcon -or $actual.readme -ne $metadata.PackageReadmeFile -or !$actual.tags -or $actual.copyright -ne $metadata.Copyright) { throw "Nuspec metadata mismatch: $id" }
                if ($metadata.PackageLicenseFile -and ($actual.license.type -ne 'file' -or $actual.license.InnerText -ne $metadata.PackageLicenseFile)) { throw "License mismatch: $id" }
                if ($metadata.PackageLicenseExpression -and ($actual.license.type -ne 'expression' -or $actual.license.InnerText -ne $metadata.PackageLicenseExpression)) { throw "License expression mismatch: $id" }
                $payloads = @($metadata.PackageReadmeFile,$metadata.PackageIcon)
                if ($metadata.PackageLicenseFile) { $payloads += $metadata.PackageLicenseFile }
                if ($metadata.IncludeBuildOutput -ne 'false') { $payloads += "lib/$($metadata.TargetFramework)/$($metadata.AssemblyName).dll" }
                if ($metadata.IncludeBuildOutput -ne 'false' -and $metadata.GenerateDocumentationFile -eq 'true') { $payloads += "lib/$($metadata.TargetFramework)/$($metadata.AssemblyName).xml" }
                if ($id -eq 'TrueMoon.Cobalt') { $payloads += @('analyzers/dotnet/cs/TrueMoon.Cobalt.Generator.dll','buildTransitive/TrueMoon.Cobalt.targets') }
                if ($id -eq 'TrueMoon.Enerit.Generator') { $payloads += 'analyzers/dotnet/cs/TrueMoon.Enerit.Generator.dll' }
                if ($id -eq 'TrueMoon.Alloy.Rendering.Skia') { $payloads += @('nativeAssets/win-x64/libSkiaSharp.dll','build/TrueMoon.Alloy.Rendering.Skia.targets','buildTransitive/TrueMoon.Alloy.Rendering.Skia.targets') }
                $hashes = @(foreach ($payload in $payloads) { [ordered]@{ Path = $payload; Sha256 = Entry-Hash $archive $payload } })
                $projectReadme = Join-Path ([IO.Path]::GetDirectoryName((Join-Path $workspace $project.Project))) $metadata.PackageReadmeFile
                if ((Entry-Hash $archive $metadata.PackageReadmeFile) -ne (Get-FileHash -LiteralPath $projectReadme).Hash) { throw "README content mismatch: $id" }
                if ((Entry-Hash $archive $metadata.PackageIcon) -ne (Get-FileHash -LiteralPath (Join-Path $workspace '../materials/icon.png')).Hash) { throw "Icon content mismatch: $id" }
                if ($metadata.PackageLicenseFile -and (Entry-Hash $archive $metadata.PackageLicenseFile) -ne (Get-FileHash -LiteralPath (Join-Path $workspace '../LICENSE')).Hash) { throw "License content mismatch: $id" }
                $summary.Packages += [ordered]@{ Id = $id; Version = $actual.version; Deferred = $project.Deferred; Path = $archives[0].FullName; Sha256 = (Get-FileHash -LiteralPath $archives[0].FullName).Hash; Payloads = $hashes }
            } finally { $archive.Dispose() }
            Save-Report
            Write-Host "Package metadata verified: $id"
        }
    }
    foreach ($metadataInput in $summary.Inputs) {
        if ((Get-FileHash -LiteralPath $metadataInput.Path).Hash -ne $metadataInput.Sha256) { throw "Metadata input changed during verification: $($metadataInput.Path)" }
    }
    $summary.Completed = $true
    Write-Host "Verified $($projectPaths.Count) projects, $($selectedProjects.Count) intended packages, $($summary.Packages.Count) archives."
} catch { $summary.Failure = $_.ToString(); throw } finally { Save-Report }
