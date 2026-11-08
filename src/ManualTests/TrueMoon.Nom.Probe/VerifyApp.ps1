[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory, [ValidateSet('App','Buffer','Raw')][string]$Consumer='App')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath((Join-Path $workspace $EvidenceDirectory))
if(Test-Path -LiteralPath $evidence){throw 'Use a fresh evidence directory.'}
[void][IO.Directory]::CreateDirectory($evidence)
$run=Join-Path ([IO.Path]::GetTempPath()) ('Nom-PublicApp-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($run)
$summary=[ordered]@{Completed=$false;RunDirectory=$run;Commands=@();Graphs=@();Profiles=@();Failure=$null}
$consumerName=switch($Consumer){'Buffer'{'NomBufferConsumer'} 'Raw'{'NomRawConsumer'} default{'NomPublicConsumer'}}
$expectedChecks=switch($Consumer){'Buffer'{28} 'Raw'{31} default{34}}
$templateName=switch($Consumer){'Buffer'{'PublicBufferConsumer.cs.template'} 'Raw'{'PublicRawConsumer.cs.template'} default{'PublicAppConsumer.cs.template'}}
$inputs=@(Get-Item -LiteralPath (Join-Path $PSScriptRoot 'VerifyApp.ps1'),(Join-Path $PSScriptRoot $templateName),(Join-Path $workspace 'Directory.Build.props'),(Join-Path $workspace 'Directory.Packages.props'))
foreach($name in @('TrueMoon.Nom','TrueMoon','TrueMoon.Contracts','TrueMoon.Cobalt','TrueMoon.Cobalt.Generator')){
    $inputs+=Get-ChildItem -LiteralPath (Join-Path $workspace $name) -Recurse -File | Where-Object {$_.Extension -in '.cs','.csproj' -and $_.FullName -notmatch '[\\/](bin|obj)[\\/]'}
}
$summary.Sources=@($inputs|ForEach-Object{@{Path=$_.FullName;Sha256=(Get-FileHash $_.FullName).Hash}})
function Invoke-Dotnet([string]$name,[string[]]$arguments){
    if($arguments[0] -in @('restore','build','publish')){$arguments+="-bl:$evidence/$name-{}.binlog"; $arguments+="-p:DirectoryPackagesPropsPath=$(Join-Path $workspace 'Directory.Packages.props')",'-p:UseArtifactsOutput=true',"-p:ArtifactsPath=$(Join-Path $run 'artifacts')"}
    $info=[Diagnostics.ProcessStartInfo]::new('dotnet');$info.WorkingDirectory=$run;$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($arg in $arguments){$info.ArgumentList.Add($arg)}
    $process=[Diagnostics.Process]::new();$process.StartInfo=$info;$started=$false
    try{
        if(!$process.Start()){throw "Start failed $name"};$started=$true
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if(!$process.WaitForExit(60000)){$process.Kill($true);if(!$process.WaitForExit(10000)){throw 'Failed to reap child'};throw "Timeout $name"}
        [IO.File]::WriteAllText((Join-Path $evidence "$name.stdout.log"),$stdout.GetAwaiter().GetResult());[IO.File]::WriteAllText((Join-Path $evidence "$name.stderr.log"),$stderr.GetAwaiter().GetResult())
        $summary.Commands+=@{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode}
        if($process.ExitCode -ne 0){throw "Failed $name; see $evidence"};Write-Host "$name passed"
    }finally{if($started -and !$process.HasExited){$process.Kill($true);if(!$process.WaitForExit(10000)){throw 'Failed to reap child'}};$process.Dispose()}
}
function Run-Consumer([string]$name,[string]$assembly){
    $directory=[IO.Path]::GetDirectoryName($assembly)
    if(@(Get-ChildItem -LiteralPath $directory -Recurse -Filter 'LiteDB.dll').Count){throw 'Removed backend in output'}
    foreach($file in Get-ChildItem -LiteralPath $directory -Recurse -Filter '*.deps.json'){
        $deps=Get-Content $file.FullName -Raw|ConvertFrom-Json -AsHashtable
        if(@($deps.libraries.Keys|Where-Object{$_ -match '^LiteDB/'}).Count){throw 'Removed backend in dependency graph'}
    }
    $report=Join-Path $evidence "$name.json";Invoke-Dotnet $name @($assembly,$report)
    $proof=Get-Content $report -Raw|ConvertFrom-Json
    if(!$proof.Completed -or $proof.Consumer -ne $consumerName -or @($proof.Checks).Count -ne $expectedChecks){throw 'Incomplete public proof'}
    if((Get-FileHash -LiteralPath $proof.ModulePath).Hash -ne $proof.ModuleSha256 -or [IO.Path]::GetDirectoryName($proof.ModulePath) -ne $directory){throw 'Loaded module identity mismatch'}
    $summary.Profiles+=@{Name=$name;Report=$report;Checks=@($proof.Checks).Count}
}
try{
    $template=Join-Path $PSScriptRoot $templateName
    Copy-Item -LiteralPath $template -Destination (Join-Path $run 'Program.cs')
    $summary.TemplateSha256=(Get-FileHash $template).Hash
    $nom=[Security.SecurityElement]::Escape((Join-Path $workspace 'TrueMoon.Nom/TrueMoon.Nom.csproj'))
    $app=[Security.SecurityElement]::Escape((Join-Path $workspace 'TrueMoon/TrueMoon.csproj'))
    $project=Join-Path $run "$consumerName.csproj"
    "<Project Sdk='Microsoft.NET.Sdk'><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><AssemblyName>$consumerName</AssemblyName></PropertyGroup><ItemGroup><ProjectReference Include='$nom'/><ProjectReference Include='$app'/></ItemGroup></Project>"|Set-Content $project
    Invoke-Dotnet 'restore' @('restore',$project,'-p:NuGetAudit=false')
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $run 'artifacts') -Recurse -Filter 'project.assets.json'){
        $graph=Get-Content $file.FullName -Raw|ConvertFrom-Json -AsHashtable
        if(@($graph.libraries.Keys|Where-Object{$_ -match '^LiteDB/'}).Count){throw 'Removed backend restored'}
        $summary.Graphs+=@{Project=$graph.project.restore.projectPath;Libraries=@($graph.libraries.Keys|Sort-Object)}
    }
    Invoke-Dotnet 'build' @('build',$project,'--no-restore','-c','Debug')
    $configs=@(Get-ChildItem -LiteralPath (Join-Path $run 'artifacts/bin') -Recurse -Filter "$consumerName.runtimeconfig.json")
    if($configs.Count -ne 1){throw 'Missing source output'}
    Run-Consumer 'Debug' (Join-Path $configs[0].DirectoryName "$consumerName.dll")
    $publish=Join-Path $run 'publish';Invoke-Dotnet 'publish' @('publish',$project,'--no-restore','-c','Release','--self-contained','false','-o',$publish)
    Run-Consumer 'Release' (Join-Path $publish "$consumerName.dll")
    if((Get-FileHash $template).Hash -ne $summary.TemplateSha256){throw 'Source changed during proof'}
    foreach($inputFile in $summary.Sources){if((Get-FileHash -LiteralPath $inputFile.Path).Hash -ne $inputFile.Sha256){throw 'Source changed during proof'}}
    $summary.Completed=$true
}catch{$summary.Failure=$_.ToString();throw}
finally{$summary|ConvertTo-Json -Depth 10|Set-Content (Join-Path $evidence 'verification.json') -Encoding utf8}
