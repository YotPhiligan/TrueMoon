[CmdletBinding()]
param([Parameter(Mandatory)][string]$NativeBuildReceipt)
$ErrorActionPreference='Stop'
if($PSVersionTable.PSVersion.Major -lt 7){throw 'PowerShell 7 required.'}
$receipt=Get-Content -Raw -LiteralPath $NativeBuildReceipt|ConvertFrom-Json
$pins=Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'Source.lock.json')|ConvertFrom-Json
if(!$receipt.Completed -or $receipt.PackageId -ne $pins.packageId -or $receipt.PackageVersion -ne $pins.packageVersion -or
    $receipt.PatchSha256 -ne $pins.patchSha256 -or (Get-FileHash -LiteralPath $receipt.Package.Path).Hash -ne $receipt.Package.Sha256){throw 'Native build receipt/package does not match the pinned fork.'}
$zip=[IO.Compression.ZipFile]::OpenRead($receipt.Package.Path)
try{
    $entry=$zip.GetEntry('runtimes/win-x64/native/miniaudioex.dll')
    if(!$entry){throw 'Windows x64 native asset missing.'}
    $stream=$entry.Open()
    try{$hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}
    if($hash -ne $receipt.Patched.Sha256){throw 'Packaged native asset differs from built DLL.'}
}finally{$zip.Dispose()}
$feed=Join-Path $PSScriptRoot 'feed'
[void][IO.Directory]::CreateDirectory($feed)
$destination=Join-Path $feed ([IO.Path]::GetFileName($receipt.Package.Path))
if(Test-Path -LiteralPath $destination){
    if((Get-FileHash -LiteralPath $destination).Hash -ne $receipt.Package.Sha256){
        throw 'This fork version already exists in the local feed with a different archive SHA. Use its matching receipt or an isolated feed for another build; never replace an existing package version silently.'
    }
}else{Copy-Item -LiteralPath $receipt.Package.Path -Destination $destination}
Write-Host "Verified local package feed: $destination"
