[CmdletBinding()]
param(
    [switch]$SkipCanonical
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Resolve-SharpProofContainedPath.ps1')
$schemaPath = Join-Path $repositoryRoot `
    'SharpProof.CompilerArtifact\CompilerArtifactModel.schema.json'
$generatorPath = Join-Path $PSScriptRoot `
    'Generate-CompilerArtifactModel.ps1'
$pwsh = (Get-Command pwsh -ErrorAction Stop).Source
$temporaryBase = Join-Path `
    ([IO.Path]::GetTempPath()) `
    'SharpProof-compiler-artifact-generator-tests'
$temporaryRoot = Join-Path `
    $temporaryBase `
    ('run-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($temporaryRoot)

function Invoke-GeneratorCase {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$Schema,

        [Parameter(Mandatory = $true)]
        [bool]$ShouldPass,

        [string]$ExpectedMessage = ''
    )

    $caseRoot = Join-Path $temporaryRoot $Name
    [void][IO.Directory]::CreateDirectory($caseRoot)
    $caseSchema = Join-Path $caseRoot 'schema.json'
    [IO.File]::WriteAllText(
        $caseSchema,
        $Schema,
        [Text.UTF8Encoding]::new($false))
    $arguments = @(
        '-NoLogo',
        '-NoProfile',
        '-File',
        $generatorPath,
        '-SchemaPath',
        $caseSchema,
        '-ModelOutputPath',
        (Join-Path $caseRoot 'model.generated.cs'),
        '-PortableOutputPath',
        (Join-Path $caseRoot 'portable.generated.cs'),
        '-CompilationOutputPath',
        (Join-Path $caseRoot 'compilation.generated.cs'),
        '-CollectorOutputPath',
        (Join-Path $caseRoot 'collector.generated.cs'))
    & $pwsh @arguments *> (Join-Path $caseRoot 'generator.log')
    $exitCode = $LASTEXITCODE
    if ($ShouldPass -and $exitCode -ne 0) {
        throw "Canonical generator case '$Name' failed with exit code $exitCode."
    }
    if (-not $ShouldPass -and $exitCode -eq 0) {
        throw "Malformed generator case '$Name' was accepted."
    }
    $generatorLog = [IO.File]::ReadAllText(
        (Join-Path $caseRoot 'generator.log'))
    $normalizedGeneratorLog = [Text.RegularExpressions.Regex]::Replace(
        $generatorLog,
        '[\s|]+',
        ' ')
    if (-not $ShouldPass -and
        -not $normalizedGeneratorLog.Contains(
            $ExpectedMessage,
            [StringComparison]::Ordinal)) {
        throw (
            "Malformed generator case '$Name' did not fail for the " +
            "expected reason '$ExpectedMessage'. Output: $generatorLog")
    }
}

function Copy-JsonObject {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Object
    )

    $json = $Object | ConvertTo-Json -Depth 100
    return ($json | ConvertFrom-Json -Depth 100)
}

function Get-PropertyGroup {
    param(
        [Parameter(Mandatory = $true)]
        [object]$SchemaObject,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $groupsMember = $SchemaObject.PSObject.Properties['propertyGroups']
    if ($null -eq $groupsMember -or $null -eq $groupsMember.Value) {
        throw "Canonical schema must define property group '$Name'."
    }
    $group = @($groupsMember.Value) |
        Where-Object { [string]$_.name -eq $Name } |
        Select-Object -First 1
    if ($null -eq $group) {
        throw "Canonical schema must define property group '$Name'."
    }
    return $group
}

function New-PropertyGroupTestClass {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [object[]]$Properties
    )

    return [pscustomobject]@{
        kind = 'class'
        name = $Name
        properties = @($Properties)
    }
}

function Add-SchemaDeclaration {
    param(
        [Parameter(Mandatory = $true)]
        [object]$SchemaObject,

        [Parameter(Mandatory = $true)]
        [object]$Declaration
    )

    $SchemaObject.declarations = @($SchemaObject.declarations) + $Declaration
}

try {
    $canonical = [IO.File]::ReadAllText($schemaPath)
    if (-not $SkipCanonical) {
        Invoke-GeneratorCase `
            -Name 'canonical' `
            -Schema $canonical `
            -ShouldPass $true
    }

    $unknownProperty = $canonical.Replace(
        '      "method": "TypeRow",',
        "      `"unexpected`": true,`n      `"method`": `"TypeRow`",")
    Invoke-GeneratorCase `
        -Name 'unknown-property' `
        -Schema $unknownProperty `
        -ShouldPass $false `
        -ExpectedMessage "unsupported property 'unexpected'"

    $unknownRole = $canonical.Replace(
        '{ "role": "direct", "member": "Kind" }',
        '{ "role": "unsupported", "member": "Kind" }')
    Invoke-GeneratorCase `
        -Name 'unknown-role' `
        -Schema $unknownRole `
        -ShouldPass $false `
        -ExpectedMessage "Unsupported metadata-row projection role 'unsupported'"

    $unknownSlotRole = $canonical.Replace(
        '{ "kind": "Boolean", "slots": ["booleanValue",',
        '{ "kind": "Boolean", "slots": ["unsupported",')
    Invoke-GeneratorCase `
        -Name 'unknown-slot-role' `
        -Schema $unknownSlotRole `
        -ShouldPass $false `
        -ExpectedMessage "has unsupported role 'unsupported'"

    $duplicateMethod = $canonical.Replace(
        '      "method": "VariableRow",',
        '      "method": "TypeRow",')
    Invoke-GeneratorCase `
        -Name 'duplicate-method' `
        -Schema $duplicateMethod `
        -ShouldPass $false `
        -ExpectedMessage "Duplicate portable IR metadata-row method 'TypeRow'"

    $missingArgument = $canonical.Replace(
        '        { "role": "optionalStringValue", "member": "Description" }',
        '')
    Invoke-GeneratorCase `
        -Name 'missing-argument' `
        -Schema $missingArgument `
        -ShouldPass $false `
        -ExpectedMessage 'at least one argument'

    $canonicalObject = $canonical | ConvertFrom-Json -Depth 100
    $sourceLocationGroup = Get-PropertyGroup `
        -SchemaObject $canonicalObject -Name 'SourceLocation'

    $explicitJsonNameSchema = Copy-JsonObject $canonicalObject
    $explicitJsonNameGroup = Get-PropertyGroup `
        -SchemaObject $explicitJsonNameSchema -Name 'SourceLocation'
    $explicitJsonNameProperty = @($explicitJsonNameGroup.properties)[0]
    $explicitJsonNameProperty | Add-Member `
        -MemberType NoteProperty `
        -Name 'jsonName' `
        -Value 'explicitName' `
        -Force
    Invoke-GeneratorCase `
        -Name 'explicit-json-name' `
        -Schema ($explicitJsonNameSchema | ConvertTo-Json -Depth 100) `
        -ShouldPass $false `
        -ExpectedMessage 'cannot define an explicit JSON name'

    $jsonNameCollisionSchema = Copy-JsonObject $canonicalObject
    Add-SchemaDeclaration `
        -SchemaObject $jsonNameCollisionSchema `
        -Declaration (New-PropertyGroupTestClass `
            -Name 'JsonNameCollisionCase' `
            -Properties @(
                [pscustomobject]@{
                    name = 'Foo'
                    type = 'string'
                    accessibility = 'public'
                    set = 'set'
                    default = [pscustomobject]@{ kind = 'stringEmpty' }
                }
                [pscustomobject]@{
                    name = 'foo'
                    type = 'string'
                    accessibility = 'public'
                    set = 'set'
                    default = [pscustomobject]@{ kind = 'stringEmpty' }
                }))
    Invoke-GeneratorCase `
        -Name 'json-name-collision' `
        -Schema ($jsonNameCollisionSchema | ConvertTo-Json -Depth 100) `
        -ShouldPass $false `
        -ExpectedMessage 'invalid JSON name'

    $unknownGroupSchema = Copy-JsonObject $canonicalObject
    Add-SchemaDeclaration `
        -SchemaObject $unknownGroupSchema `
        -Declaration (New-PropertyGroupTestClass `
            -Name 'PropertyGroupUnknownReferenceCase' `
            -Properties @(
                [pscustomobject]@{ group = 'MissingGroup' }))
    Invoke-GeneratorCase `
        -Name 'unknown-property-group' `
        -Schema ($unknownGroupSchema | ConvertTo-Json -Depth 100) `
        -ShouldPass $false `
        -ExpectedMessage "Unknown property group 'MissingGroup'."

    $groupReferenceWithMembersSchema = Copy-JsonObject $canonicalObject
    Add-SchemaDeclaration `
        -SchemaObject $groupReferenceWithMembersSchema `
        -Declaration (New-PropertyGroupTestClass `
            -Name 'PropertyGroupReferenceWithMembersCase' `
            -Properties @(
                [pscustomobject]@{
                    group = 'SourceLocation'
                    name = 'Unexpected'
                }))
    Invoke-GeneratorCase `
        -Name 'property-group-reference-with-members' `
        -Schema ($groupReferenceWithMembersSchema | ConvertTo-Json -Depth 100) `
        -ShouldPass $false `
        -ExpectedMessage "Property group reference 'SourceLocation' cannot define other members."

    $duplicateGroupSchema = Copy-JsonObject $canonicalObject
    $duplicateGroup = Copy-JsonObject $sourceLocationGroup
    $duplicateGroupSchema.propertyGroups = @(
        $duplicateGroupSchema.propertyGroups) + $duplicateGroup
    Invoke-GeneratorCase `
        -Name 'duplicate-property-group' `
        -Schema ($duplicateGroupSchema | ConvertTo-Json -Depth 100) `
        -ShouldPass $false `
        -ExpectedMessage "Duplicate property group 'SourceLocation'."

    $nestedGroupSchema = Copy-JsonObject $canonicalObject
    $nestedGroup = Get-PropertyGroup `
        -SchemaObject $nestedGroupSchema -Name 'SourceLocation'
    $nestedProperty = @($nestedGroup.properties)[0]
    $nestedProperty | Add-Member `
        -MemberType NoteProperty `
        -Name 'group' `
        -Value 'SourceLocation' `
        -Force
    Invoke-GeneratorCase `
        -Name 'nested-property-group' `
        -Schema ($nestedGroupSchema | ConvertTo-Json -Depth 100) `
        -ShouldPass $false `
        -ExpectedMessage "Property group 'SourceLocation' cannot contain group references."

    $duplicateExpandedSchema = Copy-JsonObject $canonicalObject
    $duplicateExpandedGroup = Get-PropertyGroup `
        -SchemaObject $duplicateExpandedSchema -Name 'SourceLocation'
    $duplicateExpandedProperty = Copy-JsonObject `
        (@($duplicateExpandedGroup.properties)[0])
    Add-SchemaDeclaration `
        -SchemaObject $duplicateExpandedSchema `
        -Declaration (New-PropertyGroupTestClass `
            -Name 'PropertyGroupDuplicateExpandedPropertyCase' `
            -Properties @(
                [pscustomobject]@{ group = 'SourceLocation' }
                $duplicateExpandedProperty))
    Invoke-GeneratorCase `
        -Name 'duplicate-expanded-property' `
        -Schema ($duplicateExpandedSchema | ConvertTo-Json -Depth 100) `
        -ShouldPass $false `
        -ExpectedMessage 'repeats property'

    $malformedGroupSchema = Copy-JsonObject $canonicalObject
    $malformedSourceGroup = Get-PropertyGroup `
        -SchemaObject $malformedGroupSchema -Name 'SourceLocation'
    $malformedGroup = Copy-JsonObject $malformedSourceGroup
    $malformedGroup.name = 'MalformedOrdinaryProperty'
    $malformedProperty = @($malformedGroup.properties)[0]
    $malformedProperty.PSObject.Properties.Remove('type')
    $malformedGroupSchema.propertyGroups = @(
        $malformedGroupSchema.propertyGroups) + $malformedGroup
    Add-SchemaDeclaration `
        -SchemaObject $malformedGroupSchema `
        -Declaration (New-PropertyGroupTestClass `
            -Name 'PropertyGroupMalformedOrdinaryPropertyCase' `
            -Properties @(
                [pscustomobject]@{ group = 'MalformedOrdinaryProperty' }))
    Invoke-GeneratorCase `
        -Name 'malformed-property-group-member' `
        -Schema ($malformedGroupSchema | ConvertTo-Json -Depth 100) `
        -ShouldPass $false `
        -ExpectedMessage "must define 'type'"

    Write-Host 'Compiler-artifact generator validation passed.'
}
finally {
    $resolvedBase = [IO.Path]::GetFullPath($temporaryBase)
    $resolvedRoot = Resolve-SharpProofContainedPath `
        -Root $resolvedBase -Path $temporaryRoot `
        -ParameterName 'Generator test directory'
    if (Test-Path -LiteralPath $resolvedRoot) {
        Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
    }
}
