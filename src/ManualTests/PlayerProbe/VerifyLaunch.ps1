[CmdletBinding()]
param([Parameter(Mandatory)][string]$PlayerAssembly,[Parameter(Mandatory)][string]$CombinedAssembly,[Parameter(Mandatory)][string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
if(Test-Path -LiteralPath $evidence){throw 'Use fresh evidence.'}
[void][IO.Directory]::CreateDirectory($evidence)
$player=[IO.Path]::GetFullPath($PlayerAssembly);$combined=[IO.Path]::GetFullPath($CombinedAssembly)
$cases=@(
    @{Name='player-help';Host=$player;Arguments=@('--help');Exit=0},
    @{Name='combined-help';Host=$combined;Arguments=@('--help');Exit=0},
    @{Name='player-requires-inputs';Host=$player;Arguments=@();Exit=1},
    @{Name='player-rejects-editor';Host=$player;Arguments=@('--editor');Exit=1},
    @{Name='conflicting-roles';Host=$combined;Arguments=@('--player','--editor');Exit=1},
    @{Name='duplicate-role';Host=$combined;Arguments=@('--editor','--editor');Exit=1},
    @{Name='child-missing-bootstrap';Host=$combined;Arguments=@('--runtime');Exit=1},
    @{Name='child-malformed-bootstrap';Host=$combined;Arguments=@('--runtime');Bootstrap='{bad';Exit=1},
    @{Name='protected-editor-fallback';Host=$combined;Arguments=@('--editor');Bootstrap='{bad';Exit=1},
    @{Name='protected-player-fallback';Host=$combined;Arguments=@('--player','--artifact','missing','--scene','missing');Bootstrap='{bad';Exit=1},
    @{Name='unknown-option';Host=$combined;Arguments=@('--unknown');Exit=1},
    @{Name='false-role-selector';Host=$combined;Arguments=@('--player=false');Exit=1},
    @{Name='editor-rejects-player-input';Host=$combined;Arguments=@('--editor','--artifact','missing');Exit=1},
    @{Name='player-rejects-project';Host=$combined;Arguments=@('--player','--artifact','missing','--scene','missing','--project','missing.aetherproj');Exit=1},
    @{Name='invalid-scene-format';Host=$player;Arguments=@('--artifact','missing','--scene','missing','--scene-format','unknown');Exit=1},
    @{Name='invalid-width-below';Host=$player;Arguments=@('--artifact','missing','--scene','missing','--width','15');Exit=1},
    @{Name='invalid-width-above';Host=$player;Arguments=@('--artifact','missing','--scene','missing','--width','1921');Exit=1},
    @{Name='duplicate-artifact';Host=$player;Arguments=@('--artifact','missing','--artifact','other','--scene','missing');Exit=1},
    @{Name='child-invalid-parent';Host=$combined;Arguments=@('--runtime');Bootstrap='{bad';Parent='not-a-pid';Exit=1},
    @{Name='player-invalid-artifact-before-app';Host=$player;Arguments=@('--artifact','missing','--scene','missing','--report',"$evidence/forbidden-report");Exit=1})
$results=@()
foreach($case in $cases){
    $info=[Diagnostics.ProcessStartInfo]::new('dotnet');$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.WorkingDirectory=$evidence;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($key in @('TM_ENGINE_BOOTSTRAP','-p','TrueMoon.Mithril.ControlPipe')){[void]$info.Environment.Remove($key)}
    if($case.ContainsKey('Bootstrap')){$info.Environment['TM_ENGINE_BOOTSTRAP']=$case.Bootstrap}
    if($case.ContainsKey('Bootstrap')){$info.Environment['-p']=if($case.ContainsKey('Parent')){$case.Parent}else{$PID.ToString([Globalization.CultureInfo]::InvariantCulture)}}
    [void]$info.ArgumentList.Add($case.Host);foreach($argument in $case.Arguments){[void]$info.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::Start($info);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    try{
        if(!$process.WaitForExit(15000)){$process.Kill($true);$process.WaitForExit();throw "Role probe timed out: $($case.Name)"}
        $outText=$stdout.GetAwaiter().GetResult();$errorText=$stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText("$evidence/$($case.Name).stdout.log",$outText);[IO.File]::WriteAllText("$evidence/$($case.Name).stderr.log",$errorText)
        if($process.ExitCode -ne $case.Exit){throw "Unexpected exit for $($case.Name): $($process.ExitCode)"}
        $expected=switch($case.Name){
            'player-help'{'AetherPlayer'} 'combined-help'{'AetherHost'}
            'player-requires-inputs'{'Player requires --artifact and --scene'}
            'player-rejects-editor'{'only the Player role'} 'conflicting-roles'{'Choose one launch role'}
            'duplicate-role'{'ConfigurationException'} 'duplicate-artifact'{'ConfigurationException'}
            'child-missing-bootstrap'{'requires a protected bootstrap'} 'child-malformed-bootstrap'{'Malformed runtime bootstrap JSON'}
            'child-invalid-parent'{'Runtime parent must be'}
            'protected-editor-fallback'{'Protected runtime identity'} 'protected-player-fallback'{'Protected runtime identity'}
            'unknown-option'{'ConfigurationException'} 'false-role-selector'{'Role selectors cannot be false'}
            'editor-rejects-player-input'{'Player inputs cannot configure the Editor'} 'player-rejects-project'{'Player does not accept Editor'}
            'invalid-scene-format'{'Scene format must be'} 'invalid-width-below'{'Invalid Player window dimensions'} 'invalid-width-above'{'Invalid Player window dimensions'}
            'player-invalid-artifact-before-app'{'FileNotFoundException'}
        }
        if(!$expected -or !($outText+$errorText).Contains($expected)){throw "Wrong rejection/help outcome for $($case.Name)"}
        $results+=@{Name=$case.Name;ExitCode=$process.ExitCode;Arguments=$case.Arguments}
    }finally{$process.Dispose()}
}
if(Test-Path -LiteralPath "$evidence/forbidden-report"){throw 'Invalid Player launch constructed App diagnostics.'}
@{Completed=$true;Cases=$results;PlayerSha256=(Get-FileHash -LiteralPath $player).Hash;CombinedSha256=(Get-FileHash -LiteralPath $combined).Hash} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath "$evidence/verification.json" -Encoding utf8
Write-Host "$($results.Count) executable launch checks passed"
