param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "1.0.0",
    [switch]$SelfContained = $true
)

$ErrorActionPreference = "Stop"

function Get-IsccPath {
    if ($env:ISCC_PATH -and (Test-Path $env:ISCC_PATH)) {
        return $env:ISCC_PATH
    }

    $candidates = @(
        "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    return $null
}

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$publishScript = Join-Path $PSScriptRoot "publish-windows.ps1"
$publishDir = Join-Path $root "artifacts/publish/$Runtime"
$outDir = Join-Path $root "artifacts/installer"
$issFile = Join-Path $root "installer/inno/LadecoDiag.iss"

& $publishScript -Configuration $Configuration -Runtime $Runtime -Version $Version -SelfContained:$SelfContained
if ($LASTEXITCODE -ne 0) {
    throw "Publish step failed"
}

if (-not (Test-Path $outDir)) {
    New-Item -Path $outDir -ItemType Directory -Force | Out-Null
}

$iscc = Get-IsccPath
if (-not $iscc) {
    throw "Inno Setup compiler (ISCC.exe) not found. Install Inno Setup 6 or set ISCC_PATH."
}

Write-Host "Building installer with Inno Setup..."
& $iscc "/DMyAppVersion=$Version" "/DPublishDir=$publishDir" "/DOutputDir=$outDir" $issFile

if ($LASTEXITCODE -ne 0) {
    throw "Installer build failed"
}

Write-Host "Installer ready in $outDir"
