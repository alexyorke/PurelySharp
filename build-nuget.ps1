param(
	[string]$Configuration = 'Release',
	[string]$OutputDir = 'artifacts/nuget',
	[string]$InstallProject = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSCommandPath
Push-Location $repoRoot
try {
	# Ensure output directory exists
	$outFull = if ([System.IO.Path]::IsPathRooted($OutputDir)) {
		$OutputDir
	}
	else {
		Join-Path $repoRoot $OutputDir
	}
	New-Item -ItemType Directory -Force -Path $outFull | Out-Null
	Get-ChildItem -Path $outFull -Filter *.nupkg -File -ErrorAction SilentlyContinue | Remove-Item -Force

	Write-Host "Building solution ($Configuration)..." -ForegroundColor Cyan
	dotnet build -c $Configuration | Out-Host

	Write-Host "Packing NuGet packages to $outFull" -ForegroundColor Cyan
	dotnet pack .\PurelySharp.Package\PurelySharp.Package.csproj -c $Configuration -o $outFull --no-build | Out-Host
	dotnet pack .\PurelySharp.Attributes\PurelySharp.Attributes.csproj -c $Configuration -o $outFull --no-build | Out-Host

	$packages = Get-ChildItem -Path $outFull -Filter *.nupkg -File | Sort-Object Name
	if (-not $packages -or $packages.Count -eq 0) {
		throw "No NuGet packages were produced in $outFull."
	}

	Write-Host "Built packages:" -ForegroundColor Green
	$packages | ForEach-Object { Write-Host " - $($_.FullName)" -ForegroundColor Green }

	if ($InstallProject) {
		$projPath = Resolve-Path $InstallProject
		Write-Host "Installing PurelySharp from local source into project: $projPath" -ForegroundColor Cyan
		# Install the main analyzer package (includes Attributes for NuGet consumption)
		dotnet add "$projPath" package PurelySharp --source "$outFull" | Out-Host
	}
}
finally {
	Pop-Location
}


