param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "1.0.0",
    [switch]$SelfContained = $true
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "src/Ladeco.Diag.App/Ladeco.Diag.App.csproj"
$publishDir = Join-Path $root "artifacts/publish/$Runtime"

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

New-Item -Path $publishDir -ItemType Directory -Force | Out-Null

$publishArgs = @(
    "publish",
    $project,
    "-c", $Configuration,
    "-r", $Runtime,
    "-o", $publishDir,
    "/p:PublishSingleFile=true",
    "/p:IncludeNativeLibrariesForSelfExtract=true",
    "/p:DebugType=None",
    "/p:DebugSymbols=false",
    "/p:Version=$Version"
)

if ($SelfContained) {
    $publishArgs += "--self-contained"
    $publishArgs += "true"
} else {
    $publishArgs += "--self-contained"
    $publishArgs += "false"
}

Write-Host "Publishing Ladeco Diag to $publishDir ..."
dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed"
}

Write-Host "Publish completed: $publishDir"
