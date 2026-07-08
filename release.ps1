<#
.SYNOPSIS
    Release gate for the Cauca.ApiClient NuGet package.

.DESCRIPTION
    Builds, tests, packs, and pushes Cauca.ApiClient to the internal CaucaNuget
    feed.

    Steps performed in order:
      1. VERSION     — reads <Version> from Cauca.ApiClient.csproj for display.
      2. BUILD       — dotnet build (Release, GeneratePackageOnBuild disabled).
      3. TEST        — dotnet test (Release, no-build). Skippable via -SkipTests.
      4. PACK        — dotnet pack the package into ./artifacts/nupkg.
      5. PUSH        — nuget push the produced .nupkg to CaucaNuget.

    Supports -WhatIf: the push step is skipped so the full run can be previewed
    without actually publishing the package.

.PARAMETER SkipTests
    Skip the test step. Off by default.
    Use only when you have just run the full test suite locally in this session.

.PARAMETER ArtifactsDir
    Directory where .nupkg files are written. Defaults to ./artifacts/nupkg.
    The directory is cleaned at the start of each pack run.

.EXAMPLE
    .\release.ps1 -WhatIf
    Runs the build and tests; shows what would be pushed — without publishing.

.EXAMPLE
    .\release.ps1
    Full release: build, test, pack, push.

.EXAMPLE
    .\release.ps1 -SkipTests
    Skip the test step (escape hatch — use with care).
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [switch] $SkipTests,

    [string] $ArtifactsDir = (Join-Path $PSScriptRoot 'artifacts' 'nupkg')
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Packable project (relative to repo root)
# ---------------------------------------------------------------------------
$RepoRoot = $PSScriptRoot

$PackableProject = 'Cauca.ApiClient\Cauca.ApiClient.csproj'

$SolutionFile = Join-Path $RepoRoot 'Cauca.ApiClient.sln'

# ---------------------------------------------------------------------------
# STEP 1 — VERSION
# ---------------------------------------------------------------------------
Write-Host "`n=== Step 1: Version ===" -ForegroundColor Cyan

function Get-ProjectVersion {
    [CmdletBinding()]
    param([string] $ProjectPath)

    [xml]$csproj = Get-Content $ProjectPath -Raw
    $versionNode = $csproj.Project.PropertyGroup |
        ForEach-Object { $_.Version } |
        Where-Object { $_ }

    if (-not $versionNode) {
        throw "No <Version> element found in '$ProjectPath'."
    }

    return $versionNode
}

$PackageVersion = Get-ProjectVersion -ProjectPath (Join-Path $RepoRoot $PackableProject)
Write-Host "  $PackableProject  =>  $PackageVersion"
Write-Host "`n  Releasing Cauca.ApiClient $PackageVersion" -ForegroundColor Green

# ---------------------------------------------------------------------------
# STEP 2 — BUILD
# ---------------------------------------------------------------------------
Write-Host "`n=== Step 2: Build ===" -ForegroundColor Cyan

dotnet build $SolutionFile -c Release --nologo -p:GeneratePackageOnBuild=false
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed (exit $LASTEXITCODE). Aborting."
    exit $LASTEXITCODE
}

Write-Host "`n  Build passed." -ForegroundColor Green

# ---------------------------------------------------------------------------
# STEP 3 — TEST
# ---------------------------------------------------------------------------
if ($SkipTests) {
    Write-Warning "Skipping tests (-SkipTests switch is set). Use only when you have just run the full test suite locally in this session."
}
else {
    Write-Host "`n=== Step 3: Test ===" -ForegroundColor Cyan

    dotnet test $SolutionFile -c Release --no-build --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Tests failed (exit $LASTEXITCODE). Aborting."
        exit $LASTEXITCODE
    }

    Write-Host "`n  Tests passed." -ForegroundColor Green
}

# ---------------------------------------------------------------------------
# STEP 4 — PACK
# ---------------------------------------------------------------------------
Write-Host "`n=== Step 4: Pack ===" -ForegroundColor Cyan

# Clean output directory before each run for idempotency.
# -Force keeps -WhatIf runs working: under -WhatIf the Remove-Item above is
# skipped (it honours ShouldProcess), so the directory may still exist here.
if (Test-Path $ArtifactsDir) {
    Remove-Item $ArtifactsDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $ArtifactsDir | Out-Null

$fullPath = Join-Path $RepoRoot $PackableProject
Write-Host "`n  Packing $PackableProject ..."

dotnet pack $fullPath -c Release --no-build --nologo -o $ArtifactsDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet pack failed for '$PackableProject' (exit $LASTEXITCODE). Aborting."
    exit $LASTEXITCODE
}

$ProducedPackages = Get-ChildItem -Path $ArtifactsDir -Filter '*.nupkg'
Write-Host "`n  Packed $($ProducedPackages.Count) package(s):" -ForegroundColor Green
$ProducedPackages | ForEach-Object { Write-Host "    $($_.Name)" }

# ---------------------------------------------------------------------------
# STEP 5 — PUSH
# ---------------------------------------------------------------------------
Write-Host "`n=== Step 5: Push ===" -ForegroundColor Cyan

foreach ($pkg in $ProducedPackages) {
    if ($PSCmdlet.ShouldProcess($pkg.FullName, "nuget push -Source CaucaNuget")) {
        Write-Host "`n  Pushing $($pkg.Name) ..."
        nuget push -Source CaucaNuget $pkg.FullName
        if ($LASTEXITCODE -ne 0) {
            Write-Error "nuget push failed for '$($pkg.Name)' (exit $LASTEXITCODE). Aborting."
            exit $LASTEXITCODE
        }
    }
    else {
        Write-Host "  [WhatIf] Would push: $($pkg.Name)" -ForegroundColor Yellow
    }
}

# ---------------------------------------------------------------------------
# SUMMARY
# ---------------------------------------------------------------------------
Write-Host "`n=== Release Summary ===" -ForegroundColor Cyan
Write-Host "  Version         : $PackageVersion"
Write-Host "  Project packed  : $PackableProject"
Write-Host "  Packages pushed :"
$ProducedPackages | ForEach-Object { Write-Host "    $($_.Name)" }

if ($WhatIfPreference) {
    Write-Host "`n  [WhatIf] No packages were actually pushed." -ForegroundColor Yellow
}
else {
    Write-Host "`n  Release complete." -ForegroundColor Green
}
