param(
    [string]$Configuration = 'Release',
    [string]$Framework = 'net8.0',
    [string]$DemoDir = 'tmp-purelysharp-demo/DemoApp',
    [string]$LocalFeed = 'artifacts/nuget',
    [switch]$Clean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    if ($Clean -and (Test-Path $DemoDir)) {
        Write-Host "Cleaning demo directory: $DemoDir" -ForegroundColor Yellow
        Remove-Item -Recurse -Force $DemoDir
    }

    # 1) Build local NuGet packages
    Write-Host "Building local NuGet packages..." -ForegroundColor Cyan
    powershell -NoProfile -ExecutionPolicy Bypass -File .\build-nuget.ps1 -Configuration $Configuration -OutputDir (Join-Path $repoRoot $LocalFeed) | Out-Host

    $feedPath = Resolve-Path (Join-Path $repoRoot $LocalFeed)
    Write-Host "Local feed: $feedPath" -ForegroundColor Green

    # 2) Create demo console app
    $demoPath = Join-Path $repoRoot $DemoDir
    New-Item -ItemType Directory -Force -Path $demoPath | Out-Null
    $demoPath = Resolve-Path $demoPath
    Write-Host "Creating demo app in: $demoPath" -ForegroundColor Cyan
    dotnet new console -f $Framework --force --output $demoPath | Out-Host

    $proj = Join-Path $demoPath 'DemoApp.csproj'
    if (!(Test-Path $proj)) {
        # dotnet new may name project after folder
        $proj = Get-ChildItem $demoPath -Filter *.csproj | Select-Object -ExpandProperty FullName -First 1
    }
    if (-not $proj) { throw "Could not locate project file in $demoPath" }
    Write-Host "Project: $proj" -ForegroundColor Green

    # 3) Install packages from local feed
    Write-Host "Installing PurelySharp packages from local feed..." -ForegroundColor Cyan
    dotnet add "$proj" package PurelySharp --source "$feedPath" | Out-Host
    dotnet add "$proj" package PurelySharp.Attributes --source "$feedPath" | Out-Host

    # 4) Write sample source that should produce analyzer diagnostics
    $programPath = Join-Path $demoPath 'Program.cs'
    Write-Host "Writing sample code to: $programPath" -ForegroundColor Cyan
    $code = @'
using System;
using PurelySharp.Attributes;

class Program
{
    // Should trigger PS0004 (pure but missing EnforcePure)
    static int Add(int a, int b) => a + b;

    // Should trigger PS0002 (marked pure but impure)
    [EnforcePure]
    static int Impure()
    {
        Console.WriteLine("side-effect");
        return 0;
    }

    static void Main()
    {
        Console.WriteLine(Add(1, 2));
        Console.WriteLine(Impure());
    }
}
'@
    Set-Content -Path $programPath -Value $code -Encoding UTF8

    # 5) Build and show diagnostics
    Write-Host "Building demo project to surface diagnostics..." -ForegroundColor Cyan
    dotnet build "$proj" -c $Configuration -v m | Out-Host

    Write-Host "Done." -ForegroundColor Green
}
finally {
    Pop-Location
}


