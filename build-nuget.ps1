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
	$outFull = Join-Path $repoRoot $OutputDir
	New-Item -ItemType Directory -Force -Path $outFull | Out-Null

	Write-Host "Building solution ($Configuration) to produce NuGet packages..." -ForegroundColor Cyan
	dotnet build -c $Configuration | Out-Host

	# Collect produced nupkgs
	$pkgs = @()
	$attrsDir = Join-Path $repoRoot "PurelySharp.Attributes/bin/$Configuration"
	$pkgDir = Join-Path $repoRoot "PurelySharp.Package/bin/$Configuration"
	if (Test-Path $attrsDir) { $pkgs += Get-ChildItem $attrsDir -Filter *.nupkg -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1 }
	if (Test-Path $pkgDir) { $pkgs += Get-ChildItem $pkgDir -Filter *.nupkg -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1 }

	if (-not $pkgs -or $pkgs.Count -eq 0) {
		throw "No NuGet packages found."
	}

	Write-Host "Copying packages to $outFull" -ForegroundColor Cyan
	$copied = @()
	foreach ($p in $pkgs) {
		$dest = Join-Path $outFull $p.Name
		Copy-Item $p.FullName $dest -Force
		$copied += $dest
	}

	Write-Host "Built packages:" -ForegroundColor Green
	$copied | ForEach-Object { Write-Host " - $_" -ForegroundColor Green }

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


