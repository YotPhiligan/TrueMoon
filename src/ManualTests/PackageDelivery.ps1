# A completed local delivery is immutable; consumers copy the same archives into isolated feeds.
function Import-TrueMoonPackageDelivery {
    param([string]$Manifest,[Parameter(Mandatory)]$Profile)
    if (!$Manifest) { return $null }
    $path=[IO.Path]::GetFullPath($Manifest)
    $delivery=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    if (!$delivery.Completed -or $delivery.Failure -or !$delivery.Version.StartsWith($Profile.Version+'.',[StringComparison]::Ordinal)) { throw 'A completed coherent delivery for the current base version is required.' }
    foreach($name in @('Framework','TestFramework','Sdk','LanguageVersion','Features','Version')) {
        if($delivery.BuildProfile.$name -cne $Profile.$name){throw "Shared delivery profile differs: $name"}
    }
    foreach($source in $delivery.BuildProfile.Sources){if((Get-FileHash -LiteralPath $source.Path).Hash -ne $source.Sha256){throw 'Shared delivery build inputs changed.'}}
    if(@($delivery.Packages.Id | Sort-Object -Unique).Count -ne $delivery.Packages.Count){throw 'Duplicate shared package identities.'}
    foreach($package in $delivery.Packages){if((Get-FileHash -LiteralPath $package.Path).Hash -ne $package.Sha256){throw "Changed shared archive: $($package.Id)"}}
    return $delivery
}
function Copy-TrueMoonDeliveryPackage {
    param([Parameter(Mandatory)]$Delivery,[Parameter(Mandatory)][string]$Id,[Parameter(Mandatory)][string]$Destination)
    $entry=@($Delivery.Packages | Where-Object Id -eq $Id)
    if($entry.Count -ne 1){throw "Missing coherent package: $Id"}
    if((Get-FileHash -LiteralPath $entry[0].Path).Hash -ne $entry[0].Sha256){throw "Changed coherent package: $Id"}
    $target=Join-Path $Destination "$Id.$($Delivery.Version).nupkg"
    Copy-Item -LiteralPath $entry[0].Path -Destination $target
    if((Get-FileHash -LiteralPath $target).Hash -ne $entry[0].Sha256){throw 'Copied archive hash differs.'}
}
