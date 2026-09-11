Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'SharpProof.ReleaseJson.ps1')

function Get-SharpProofThirdPartyComponentGraph {
    param(
        [Parameter()]
        [string]$ContractPath = (Join-Path `
            (Join-Path $PSScriptRoot '..') `
            'eng/release/third-party-components.json')
    )

    $contract = Get-Content -LiteralPath $ContractPath -Raw |
        ConvertFrom-Json
    if ($contract.schemaVersion -ne 1 -or
        $null -eq $contract.PSObject.Properties['packages']) {
        throw 'Unsupported third-party component license authority.'
    }
    return @($contract.packages.PSObject.Properties | ForEach-Object {
        $packageId = $_.Name
        @($_.Value) | ForEach-Object {
            [pscustomobject][ordered]@{
                packageId = $packageId
                id = [string]$_.id
                version = [string]$_.version
                license = [string]$_.license
                entries = @(@($_.entries) |
                    ForEach-Object { [string]$_ } |
                    Sort-Object)
            }
        }
    })
}

function Test-SharpProofThirdPartyComponentProjection {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [object[]]$ActualComponents,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [object[]]$ExpectedComponents
    )

    $propertyNames = @('entries', 'id', 'license', 'packageId', 'version')
    function ConvertTo-ComponentRecord {
        param(
            [Parameter(Mandatory = $true)][object]$Component,
            [Parameter()][switch]$ValidateShape)

        if ($ValidateShape) {
            $actualPropertyNames = @($Component.PSObject.Properties.Name |
                Sort-Object)
            if (($actualPropertyNames -join '|') -cne
                ($propertyNames -join '|')) {
                throw 'Third-party component inventory has an invalid schema.'
            }
        }
        return [pscustomobject][ordered]@{
            packageId = [string]$Component.packageId
            id = [string]$Component.id
            version = [string]$Component.version
            license = [string]$Component.license
            entries = @(@($Component.entries) |
                ForEach-Object { [string]$_ } |
                Sort-Object)
        }
    }
    $actual = @($ActualComponents |
        ForEach-Object { ConvertTo-ComponentRecord $_ -ValidateShape } |
        Sort-Object packageId, id, version)
    $expected = @($ExpectedComponents |
        ForEach-Object { ConvertTo-ComponentRecord $_ } |
        Sort-Object packageId, id, version)
    Assert-SharpProofCanonicalMatch `
        -Actual $actual -Expected $expected -Depth 4 `
        -Message 'Third-party component inventory does not match the authenticated catalog projection.'
}
