[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageSource,
    [Parameter(Mandatory = $true)]
    [ValidateSet('linux', 'windows', 'macos')]
    [string]$OsFamily
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SharpProof.ReleaseBundle.ps1')
$actualOsFamily = if ($IsLinux) {
    'linux'
}
elseif ($IsWindows) {
    'windows'
}
elseif ($IsMacOS) {
    'macos'
}
else {
    throw 'Unable to determine the executing host OS family from PowerShell runtime facts.'
}
if ($OsFamily -cne $actualOsFamily) {
    throw (
        "Portable consumer OS-family mismatch: expected '$OsFamily', " +
        "but the executing host is '$actualOsFamily'.")
}
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
& (Join-Path $PSScriptRoot 'Test-SharpProofPackageConsumers.ps1') `
    -PackageSource $PackageSource -FrameworkConsumersOnly
if ($LASTEXITCODE -ne 0) { throw 'Portable package consumer failed.' }
$resolvedSource = (Resolve-Path -LiteralPath $PackageSource).Path
$evidencePath = Join-Path $repositoryRoot `
    "artifacts/release-qualification/portable-$OsFamily.json"
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($evidencePath)) |
    Out-Null
$packages = @(Get-ChildItem -LiteralPath $resolvedSource -File |
    Where-Object Extension -In @('.nupkg', '.snupkg') |
    Sort-Object Name |
    ForEach-Object {
        [ordered]@{
            fileName = $_.Name
            bytes = [int64]$_.Length
            sha256 = Get-SharpProofFileSha256 -Path $_.FullName
        }
    })
[IO.File]::WriteAllText(
    $evidencePath,
    (([ordered]@{
        schemaVersion = 2
        status = 'passed'
        commit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
        osFamily = $OsFamily
        packageArtifacts = $packages
    } | ConvertTo-Json -Depth 4) + "`n"),
    [Text.UTF8Encoding]::new($false))
& (Join-Path $PSScriptRoot 'Write-SharpProofQualificationReceipt.ps1') `
    -Gate "portable-$OsFamily" -EvidencePath $evidencePath
