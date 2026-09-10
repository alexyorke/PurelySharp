Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-AcceptanceTimingTimeline {
    param(
        [Parameter(Mandatory = $true)][DateTime]$StartedUtc,
        [Parameter(Mandatory = $true)][DateTime]$CompletedUtc,
        [Parameter(Mandatory = $true)][long]$TotalElapsedMilliseconds,
        [Parameter(Mandatory = $true)][object[]]$Phases,
        [Parameter(Mandatory = $true)][string[]]$ExpectedPhaseNames,
        [Parameter(Mandatory = $true)][bool]$RequireComplete
    )

    $outerTicks = ($CompletedUtc - $StartedUtc).Ticks
    if ($StartedUtc.Kind -ne [DateTimeKind]::Utc -or
        $CompletedUtc.Kind -ne [DateTimeKind]::Utc -or
        $outerTicks -lt 0 -or
        $outerTicks % [TimeSpan]::TicksPerMillisecond -ne 0 -or
        $TotalElapsedMilliseconds -ne
            [long]($outerTicks / [TimeSpan]::TicksPerMillisecond) -or
        ($RequireComplete -and $Phases.Count -ne $ExpectedPhaseNames.Count) -or
        (-not $RequireComplete -and
            ($Phases.Count -lt 1 -or
             $Phases.Count -gt $ExpectedPhaseNames.Count))) {
        throw 'Acceptance outer timing interval is invalid.'
    }
    $previousCompleted = $StartedUtc
    for ($index = 0; $index -lt $Phases.Count; $index++) {
        $phase = $Phases[$index]
        $phaseStart = [DateTime]::Parse(
            [string]$phase.startedUtc,
            [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::RoundtripKind)
        $phaseCompleted = [DateTime]::Parse(
            [string]$phase.completedUtc,
            [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::RoundtripKind)
        $phaseTicks = ($phaseCompleted - $phaseStart).Ticks
        if ([string]$phase.name -cne $ExpectedPhaseNames[$index] -or
            [string]$phase.status -cnotin @('passed','failed','skipped') -or
            $phaseStart.Kind -ne [DateTimeKind]::Utc -or
            $phaseCompleted.Kind -ne [DateTimeKind]::Utc -or
            $phaseStart -lt $StartedUtc -or
            $phaseCompleted -gt $CompletedUtc -or
            $phaseStart -lt $previousCompleted -or
            $phaseTicks -lt 0 -or
            $phaseTicks % [TimeSpan]::TicksPerMillisecond -ne 0 -or
            [long]$phase.elapsedMilliseconds -ne
                [long]($phaseTicks / [TimeSpan]::TicksPerMillisecond)) {
            throw "Acceptance timing phase '$index' is invalid."
        }
        $previousCompleted = $phaseCompleted
    }
}

Export-ModuleMember -Function 'Test-AcceptanceTimingTimeline'
