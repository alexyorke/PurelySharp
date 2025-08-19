param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Write-Section([string]$Message) {
    Write-Host "==> $Message"
}

$root = $PSScriptRoot
if (-not $root -or $root -eq "") {
    $root = Split-Path -Parent $MyInvocation.MyCommand.Path
}

Write-Section "Changing directory to repo root: $root"
Set-Location -Path $root -ErrorAction Stop

Write-Section "Restoring packages"
dotnet restore
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE" }

Write-Section "Building non-VSIX projects ($Configuration)"
dotnet build .\PurelySharp.Attributes\PurelySharp.Attributes.csproj -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "dotnet build Attributes failed with exit code $LASTEXITCODE" }

dotnet build .\PurelySharp.Analyzer\PurelySharp.Analyzer.csproj -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "dotnet build Analyzer failed with exit code $LASTEXITCODE" }

dotnet build .\PurelySharp.CodeFixes\PurelySharp.CodeFixes.csproj -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "dotnet build CodeFixes failed with exit code $LASTEXITCODE" }

$vsixDir = Join-Path $root "PurelySharp.Vsix\bin\$Configuration"
$nupkgDir = Join-Path $root "PurelySharp.Attributes\bin\$Configuration"

$vsix = Get-ChildItem -Path $vsixDir -Filter *.vsix -Recurse -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$nupkg = Get-ChildItem -Path $nupkgDir -Filter *.nupkg -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (-not $vsix) {
    Write-Section "Building VSIX using MSBuild.exe"

    $candidateMsBuildPaths = @()

    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\\Installer\\vswhere.exe"
    if (Test-Path $vswhere) {
        $path = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\\**\\Bin\\MSBuild.exe" 2>$null | Select-Object -First 1
        if ($path) { $candidateMsBuildPaths += $path }
    }

    $candidateMsBuildPaths += @(
        "C:\\Program Files\\Microsoft Visual Studio\\2022\\Community\\MSBuild\\Current\\Bin\\MSBuild.exe",
        "C:\\Program Files\\Microsoft Visual Studio\\2022\\Professional\\MSBuild\\Current\\Bin\\MSBuild.exe",
        "C:\\Program Files\\Microsoft Visual Studio\\2022\\Enterprise\\MSBuild\\Current\\Bin\\MSBuild.exe",
        "C:\\Program Files (x86)\\Microsoft Visual Studio\\2022\\BuildTools\\MSBuild\\Current\\Bin\\MSBuild.exe",
        "C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\BuildTools\\MSBuild\\Current\\Bin\\MSBuild.exe"
    )

    $msbuildPath = $candidateMsBuildPaths | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (-not $msbuildPath) {
        $searchRoots = @("C:\\Program Files\\Microsoft Visual Studio", "C:\\Program Files (x86)\\Microsoft Visual Studio")
        foreach ($rootPath in $searchRoots) {
            $found = Get-ChildItem -Path $rootPath -Filter MSBuild.exe -Recurse -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -like "*\\MSBuild\\Current\\Bin\\MSBuild.exe" } |
                Select-Object -First 1
            if ($found) { $msbuildPath = $found.FullName; break }
        }
    }

    if (-not $msbuildPath) { throw "Could not locate MSBuild.exe. Please install Visual Studio 2022 (any edition) or MSBuild Build Tools with the VS extension workload." }

    & $msbuildPath ".\PurelySharp.Vsix\PurelySharp.Vsix.csproj" /t:Build /p:Configuration=$Configuration /p:EnableVsixPackaging=true
    if ($LASTEXITCODE -ne 0) { throw "MSBuild VSIX build failed with exit code $LASTEXITCODE" }

    $vsix = Get-ChildItem -Path $vsixDir -Filter *.vsix -ErrorAction Stop | Sort-Object LastWriteTime -Descending | Select-Object -First 1
}

if (-not $nupkg) {
    Write-Section "NuGet package not found, packing Attributes project"
    dotnet pack .\PurelySharp.Attributes\PurelySharp.Attributes.csproj -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed with exit code $LASTEXITCODE" }
    $nupkg = Get-ChildItem -Path $nupkgDir -Filter *.nupkg -ErrorAction Stop | Sort-Object LastWriteTime -Descending | Select-Object -First 1
}

Write-Host ""
Write-Section "Artifacts"
if ($vsix) { Write-Host ("VSIX: " + $vsix.FullName) } else { Write-Host "VSIX: not found" }
if ($nupkg) { Write-Host ("NuGet: " + $nupkg.FullName) } else { Write-Host "NuGet: not found" }

exit 0


