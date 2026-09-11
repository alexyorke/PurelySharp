Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'Assert-SharpProofJsonProperties.ps1')

function Assert-SharpProofStandaloneGateResult {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)]
        [ValidateSet('performance')][string]$ExpectedGate,
        [Parameter(Mandatory = $true)][string]$ExpectedCommit,
        [Parameter(Mandatory = $true)][string]$ExpectedMvid
    )

    $jsonDocument = $null
    try {
        $bytes = [IO.File]::ReadAllBytes($Path)
        if ($bytes.Length -eq 0) {
            throw 'The standalone gate result is empty.'
        }
        if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and
            $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
            throw 'The standalone gate result must be UTF-8 without a BOM.'
        }
        $json = [Text.UTF8Encoding]::new($false, $true).GetString($bytes)
        $jsonDocument = [Text.Json.JsonDocument]::Parse($json)
        if ($jsonDocument.RootElement.ValueKind -ne
                [Text.Json.JsonValueKind]::Object) {
            throw 'The standalone gate result must be an object.'
        }
        Assert-UniqueJsonProperties `
            $jsonDocument.RootElement 'Standalone gate result'
        $document = $json | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        throw "The standalone gate result is not canonical JSON: $($_.Exception.Message)"
    }
    finally {
        if ($null -ne $jsonDocument) {
            $jsonDocument.Dispose()
        }
    }
    Assert-SharpProofExactJsonProperties -Actual $document.PSObject.Properties.Name `
        -Description 'Gate envelope' `
        -Expected @(
            'SchemaVersion', 'Gate', 'Passed', 'SourceCommit',
            'Executable', 'Result')
    if ($document.SchemaVersion -isnot [long] -or
        [int]$document.SchemaVersion -ne 1) {
        throw 'The standalone gate result schema is unsupported.'
    }
    if ($document.Gate -isnot [string] -or
        $document.Gate -cne $ExpectedGate) {
        throw 'The standalone gate result identifies the wrong gate.'
    }
    if ($document.Passed -isnot [bool] -or -not $document.Passed) {
        throw 'The standalone gate result did not pass.'
    }
    if ($document.SourceCommit -isnot [string] -or
        $document.SourceCommit -cne $ExpectedCommit) {
        throw 'The standalone gate result is bound to the wrong source commit.'
    }
    Assert-SharpProofExactJsonProperties -Actual $document.Executable.PSObject.Properties.Name `
        -Description 'Gate executable identity' `
        -Expected @('Mvid')
    if ($document.Executable.Mvid -isnot [string] -or
        $document.Executable.Mvid -cne $ExpectedMvid -or
        [string]::IsNullOrWhiteSpace($document.Executable.Mvid)) {
        throw 'The standalone gate result has the wrong executable identity.'
    }

    $performanceProperties = @(
        'Passed', 'Warmups', 'Samples', 'PackageBuildEstimatorVersion',
        'PackageBuildSdk', 'PackageBuildSamples', 'OrderBalancedRatios',
        'UnannotatedAdvisoryAnalyzerDriverRunCount',
        'UnannotatedAdvisoryAnalysisSessionCreateCount',
        'UnannotatedAdvisoryApiSpecCreateCount',
        'UnannotatedAdvisoryEffectAnalysisCreateCount',
        'OrderBalancedMedianRatio', 'RawMedianRatio',
        'BaselineFirstMedianRatio', 'UnannotatedAdvisoryFirstMedianRatio',
        'RawP95Ratio', 'BaselineRetainedBytes',
        'UnannotatedAdvisoryRetainedBytes', 'RetainedMemoryRatio',
        'RetainedMemoryIncreaseMiB', 'EnabledRetainedCompilationCount',
        'EnabledRetainedMemoryIncreaseMiB', 'IdeEdits',
        'IdeEditP95Milliseconds', 'IdeEditMaximumMilliseconds',
        'IdeDiagnosticReplayFailureCount', 'CancellationP95Milliseconds',
        'ForcedTerminationMilliseconds', 'Failures')
    Assert-SharpProofExactJsonProperties -Actual $document.Result.PSObject.Properties.Name `
        -Description "$ExpectedGate result" `
        -Expected $performanceProperties
    foreach ($property in $document.Result.PSObject.Properties) {
        if ($property.Name -ceq 'Passed') { continue }
        if ($property.Name -in @(
                'Failures', 'PackageBuildSamples', 'OrderBalancedRatios')) {
            if ($property.Value -isnot [Array]) {
                throw "The $ExpectedGate result property '$($property.Name)' must be an array."
            }
            continue
        }
        if ($property.Name -ceq 'PackageBuildEstimatorVersion') {
            if ($property.Value -isnot [string] -or
                [string]::IsNullOrWhiteSpace($property.Value)) {
                throw 'The performance estimator version must be a string.'
            }
            continue
        }
        if ($property.Name -ceq 'PackageBuildSdk') { continue }
        if ($property.Value -isnot [ValueType] -or
            $property.Value -is [bool]) {
            throw "The $ExpectedGate result property '$($property.Name)' must be numeric."
        }
    }
    Assert-SharpProofExactJsonProperties -Actual $document.Result.PackageBuildSdk.PSObject.Properties.Name `
        -Description 'Performance SDK identity' `
        -Expected @('ConfiguredVersion', 'RollForward', 'ResolvedVersion')
    foreach ($sample in @($document.Result.PackageBuildSamples)) {
        Assert-SharpProofExactJsonProperties -Actual $sample.PSObject.Properties.Name `
            -Description 'Performance package-build sample' `
            -Expected @(
                'Index', 'UnannotatedAdvisoryFirst',
                'BaselineMilliseconds',
                'UnannotatedAdvisoryMilliseconds', 'Ratio')
    }
    if ($document.Result.Passed -isnot [bool] -or
        -not $document.Result.Passed -or
        $document.Result.Failures -isnot [Array] -or
        @($document.Result.Failures).Count -ne 0) {
        throw "The $ExpectedGate result does not carry an exact passing status."
    }
    return $document
}
